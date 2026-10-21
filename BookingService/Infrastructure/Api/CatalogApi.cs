using BookingService.Services.DTOs.Responses;
using Consul;
using EventHive.Shared.Common;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace BookingService.Infrastructure.Api
{
    public class CatalogApi : ICatalogApi
    {
        private const string CatalogServiceName = "catalog-service";

        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<CatalogApi> _logger;
        private readonly IConsulClient _consulClient;
        private readonly IConfiguration _configuration;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public CatalogApi(
            HttpClient httpClient,
            IHttpContextAccessor httpContextAccessor,
            ILogger<CatalogApi> logger,
            IConsulClient consulClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _consulClient = consulClient;
            _configuration = configuration;
        }

        public async Task<Result<ReserveSeatsResponseDto>> ReserveSeatsAsync(Guid? eventId, int? quantity, CancellationToken cancellationToken = default)
        {
            try
            {
                var baseUrl = await GetServiceBaseUrlAsync(CatalogServiceName);
                var requestUri = $"{baseUrl.TrimEnd('/')}/internal/catalog/event/{eventId}/reserve?quantity={quantity}";
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
                AddAuthorizationHeader(request);

                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    var reservationDto = await JsonSerializer.DeserializeAsync<ReserveSeatsResponseDto>(stream, _jsonOptions, cancellationToken);
                    if (reservationDto != null)
                    {
                        return Result<ReserveSeatsResponseDto>.Success(reservationDto);
                    }
                }

                var error = await ParseErrorAsync(response, cancellationToken);
                return Result<ReserveSeatsResponseDto>.Failure(error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling CatalogService ReserveSeatsAsync for EventId: {EventId}", eventId);
                return Result<ReserveSeatsResponseDto>.Failure(Error.Failure($"CatalogService communication failure"));
            }
        }

        public async Task<Result<string>> ReleaseSeatsAsync(Guid? eventId, int? quantity, CancellationToken cancellationToken = default)
        {
            try
            {
                var baseUrl = await GetServiceBaseUrlAsync(CatalogServiceName);
                var requestUri = $"{baseUrl.TrimEnd('/')}/internal/catalog/event/{eventId}/release?quantity={quantity}";
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
                AddAuthorizationHeader(request);

                using var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    return Result<string>.Success(string.IsNullOrWhiteSpace(content) ? "Seats released successfully" : content.Trim('"'));
                }

                var error = await ParseErrorAsync(response, cancellationToken);
                return Result<string>.Failure(error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling CatalogService ReleaseSeatsAsync for EventId: {EventId}", eventId);
                return Result<string>.Failure(Error.Failure($"CatalogService communication failure: {ex.Message}"));
            }
        }

        private static string? _cachedServiceToken;
        private static DateTime _tokenExpiresAt = DateTime.MinValue;
        private static readonly object _tokenLock = new();

        private void AddAuthorizationHeader(HttpRequestMessage request)
        {
            var authHeader = _httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authHeader))
            {
                request.Headers.TryAddWithoutValidation("Authorization", authHeader);
                return;
            }

            var serviceToken = GetOrCreateServiceToken();
            if (!string.IsNullOrWhiteSpace(serviceToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
            }
        }

        private string? GetOrCreateServiceToken()
        {
            if (_cachedServiceToken != null && DateTime.UtcNow < _tokenExpiresAt)
            {
                return _cachedServiceToken;
            }

            lock (_tokenLock)
            {
                if (_cachedServiceToken != null && DateTime.UtcNow < _tokenExpiresAt)
                {
                    return _cachedServiceToken;
                }

                var secretKey = _configuration["JWT:SecretKey"];
                var issuer = _configuration["JWT:Issuer"];
                var audience = _configuration["JWT:Audience"];

                if (string.IsNullOrWhiteSpace(secretKey))
                {
                    _logger.LogWarning("Cannot generate internal service token: JWT:SecretKey is not configured.");
                    return null;
                }

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                var claims = new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, "BookingService"),
                    new Claim(ClaimTypes.Name, "BookingService-Internal"),
                    new Claim(ClaimTypes.Role, "InternalService"),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                };

                var expires = DateTime.UtcNow.AddMinutes(10);
                var token = new JwtSecurityToken(
                    issuer: issuer,
                    audience: audience,
                    claims: claims,
                    expires: expires,
                    signingCredentials: creds);

                _cachedServiceToken = new JwtSecurityTokenHandler().WriteToken(token);
                _tokenExpiresAt = expires.AddMinutes(-1);

                return _cachedServiceToken;
            }
        }

        private static async Task<Error> ParseErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            string description = string.Empty;

            if (!string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    var errorList = JsonSerializer.Deserialize<List<string>>(content, _jsonOptions);
                    if (errorList != null && errorList.Count > 0)
                    {
                        description = string.Join("; ", errorList);
                    }
                }
                catch
                {
                    description = content.Trim('"', '\r', '\n');
                }
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                description = response.ReasonPhrase ?? $"Catalog service returned status {(int)response.StatusCode}";
            }

            return response.StatusCode switch
            {
                HttpStatusCode.NotFound => Error.NotFound(description),
                HttpStatusCode.Conflict => Error.Conflict(description),
                HttpStatusCode.BadRequest => Error.Validation(description),
                HttpStatusCode.Unauthorized => Error.Unauthorized(description),
                _ => Error.Failure(description)
            };
        }

        private async Task<string> GetServiceBaseUrlAsync(string serviceName)
        {
            try
            {
                var query = await _consulClient.Health.Service(serviceName, tag: null, passingOnly: true);
                var instances = query.Response;

                if (instances != null && instances.Length > 0)
                {
                    var chosen = instances[Random.Shared.Next(instances.Length)].Service;
                    var host = string.IsNullOrWhiteSpace(chosen.Address) ? "localhost" : chosen.Address;

                    if (host.Equals("host.docker.internal", StringComparison.OrdinalIgnoreCase))
                    {
                        host = "localhost";
                    }

                    return $"http://{host}:{chosen.Port}";
                }

                _logger.LogWarning("Consul found no passing instances for service [{ServiceName}]. Falling back to configuration.", serviceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query Consul for [{ServiceName}]. Falling back to configuration.", serviceName);
            }

            return _configuration["CatalogService:BaseUrl"] ?? "http://localhost:5041";
        }
    }
}
