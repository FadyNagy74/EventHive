using AuthenticationService.DataSeeding;
using AuthenticationService.Domain.Entities;
using AuthenticationService.Infrastructure.Consul;
using AuthenticationService.Infrastructure.Persistence;
using AuthenticationService.Infrastructure.Repositories;
using AuthenticationService.Services;
using Consul;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Database & Identity ────────────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// ── Google OAuth2 & JWT ──────────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddGoogle(googleOptions =>
    {
        googleOptions.ClientId     = builder.Configuration["Authentication:Google:ClientId"]!;
        googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        googleOptions.SignInScheme = IdentityConstants.ExternalScheme;
        googleOptions.CallbackPath = "/api/authentication/signin-google";
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:SecretKey"])),
        };
    });


// ── Application services & repositories ───────────────────────────────────────
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
// ──────────────────────────────────────────────────────────────────────────────

// ── Consul ─────────────────────────────────────────────────────────────────────
builder.Services.Configure<ConsulConfig>(builder.Configuration.GetSection("Consul"));

builder.Services.AddSingleton<IConsulClient>(_ =>
    new ConsulClient(cfg =>
        cfg.Address = new Uri(builder.Configuration["Consul:Host"] ?? "http://localhost:8500")));

builder.Services.AddHostedService<ConsulRegistrationService>();
// ──────────────────────────────────────────────────────────────────────────────

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Basic health check — DB check requires an extra NuGet package, add later if needed
builder.Services.AddHealthChecks();

builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
});

builder.Services.AddExceptionHandler<EventHive.Shared.Exceptions.GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

await RoleSeeder.SeedRolesAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost,
    KnownNetworks = { },
    KnownProxies = { }
});

// Internal service — runs on HTTP only (traffic arrives from ApiGateway over private network).
// HTTPS is terminated at the ApiGateway; no redirection needed here.

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
