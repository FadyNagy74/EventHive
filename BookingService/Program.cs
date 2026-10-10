using Consul;
using BookingService.Infrastructure.Consul;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Consul ────────────────────────────────────────────────────────────────────
builder.Services.Configure<ConsulConfig>(builder.Configuration.GetSection("Consul"));

builder.Services.AddSingleton<IConsulClient>(_ =>
    new ConsulClient(cfg =>
        cfg.Address = new Uri(builder.Configuration["Consul:Host"] ?? "http://localhost:8500")));

builder.Services.AddHostedService<ConsulRegistrationService>();
// ──────────────────────────────────────────────────────────────────────────────

var secretKey = builder.Configuration["JWT:SecretKey"]
    ?? throw new InvalidOperationException("JWT:SecretKey is not configured.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["JWT:Issuer"],

            ValidateAudience = true,
            ValidAudience = builder.Configuration["JWT:Audience"],

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        };
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health check endpoint consumed by Consul (returns 200 OK by default)
builder.Services.AddHealthChecks();

builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Internal service — runs on HTTP only (traffic arrives from ApiGateway over private network).
// HTTPS is terminated at the ApiGateway; no redirection needed here.
app.UseAuthentication();

app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllers();

app.Run();

