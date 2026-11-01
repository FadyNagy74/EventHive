using Consul;
using EventHive.Shared.Exceptions;
using EventHive.Shared.MessageBroker;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using PaymentService.Consumers;
using PaymentService.Infrastructure.Consul;
using PaymentService.Infrastructure.Persistence;
using PaymentService.Infrastructure.Repositories;
using PaymentService.Services;
using Polly;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Consul ────────────────────────────────────────────────────────────────────
builder.Services.Configure<ConsulConfig>(builder.Configuration.GetSection("Consul"));

builder.Services.AddSingleton<IConsulClient>(_ =>
    new ConsulClient(cfg =>
        cfg.Address = new Uri(builder.Configuration["Consul:Host"] ?? "http://localhost:8500")));

builder.Services.AddHostedService<ConsulRegistrationService>();
// ──────────────────────────────────────────────────────────────────────────────

//DB
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

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

//RabbitMq | MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<BookingCreatedConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(
            builder.Configuration["RabbitMQ:Host"] ?? "localhost",
            builder.Configuration["RabbitMQ:VirtualHost"] ?? "/",
            h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

        cfg.ReceiveEndpoint("payment-booking-created-queue", e =>
        {
            e.ConfigureConsumeTopology = false;
            e.Bind("booking-events", b =>
            {
                b.ExchangeType = "topic";
                b.RoutingKey = "booking.created";
            });
            e.ConfigureConsumer<BookingCreatedConsumer>(ctx);
        });

        cfg.Message<PaymentSucceeded>(m => m.SetEntityName("payment-succeeded"));
        cfg.Publish<PaymentSucceeded>(p =>
        {
            p.ExchangeType = "topic";
        });
        cfg.Send<PaymentSucceeded>(s =>
        {
            s.UseRoutingKeyFormatter(context => "payment.succeeded");
        });

        cfg.Message<PaymentFailed>(m => m.SetEntityName("payment-failed"));
        cfg.Publish<PaymentFailed>(p =>
        {
            p.ExchangeType = "topic";
        });
        cfg.Send<PaymentFailed>(s =>
        {
            s.UseRoutingKeyFormatter(context => "payment.failed");
        });

        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentService, PaymentService.Services.PaymentService>();

builder.Services.AddHttpClient("Paymob", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Paymob:BaseUrl"] ?? "https://accept.paymob.com");
    client.DefaultRequestHeaders.Add("Authorization", $"Token {builder.Configuration["Paymob:SecretKey"]}");
})
.AddResilienceHandler("paymob-pipeline", pipelineBuilder =>
{
    pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(6));
    pipelineBuilder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(15),
        MinimumThroughput = 5,
        BreakDuration = TimeSpan.FromSeconds(30)
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health check endpoint consumed by Consul (returns 200 OK by default)
builder.Services.AddHealthChecks();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
});

var app = builder.Build();

app.UseExceptionHandler();

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

