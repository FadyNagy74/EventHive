using BookingService.Consumers;
using BookingService.Infrastructure.Api;
using BookingService.Infrastructure.Consul;
using BookingService.Infrastructure.Persistence;
using BookingService.Infrastructure.Repositories;
using BookingService.Services;
using Consul;
using EventHive.Shared.Exceptions;
using EventHive.Shared.MessageBroker;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
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
builder.Services.AddDbContext<BookingDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

var secretKey = builder.Configuration["JWT:SecretKey"]
    ?? throw new InvalidOperationException("JWT:SecretKey is not configured.");

//Auth Validation
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
    x.AddDelayedMessageScheduler();
    x.AddConsumer<PaymentSucceededConsumer>();
    x.AddConsumer<PaymentFailedConsumer>();
    x.AddConsumer<ExpireBookingConsumer>();
    x.AddConsumer<EventCancelledConsumer>();
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

        cfg.ReceiveEndpoint("payment-succeeded-queue", e =>
        {
            e.ConfigureConsumeTopology = false;
            e.Bind("payment-succeeded", b =>
            {
                b.ExchangeType = "topic";
                b.RoutingKey = "payment.succeeded";
            });
            e.ConfigureConsumer<PaymentSucceededConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("payment-failed-queue", e =>
        {
            e.ConfigureConsumeTopology = false;
            e.Bind("payment-failed", b =>
            {
                b.ExchangeType = "topic";
                b.RoutingKey = "payment.failed";
            });
            e.ConfigureConsumer<PaymentFailedConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("event-cancelled-queue", e =>
        {
            e.ConfigureConsumeTopology = false;
            e.Bind("event-cancelled", b =>
            {
                b.ExchangeType = "topic";
                b.RoutingKey = "event.cancelled";
            });
            e.ConfigureConsumer<EventCancelledConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("expire-booking-queue", e =>
        {
            e.ConfigureConsumer<ExpireBookingConsumer>(ctx);
        });

        cfg.UseDelayedMessageScheduler();

        EndpointConvention.Map<ExpireBooking>(new Uri("queue:expire-booking-queue"));

        cfg.Message<BookingCreated>(m => m.SetEntityName("booking-events"));
        cfg.Publish<BookingCreated>(p =>
        {
            p.ExchangeType = "topic";
        });
        cfg.Send<BookingCreated>(s =>
        {
            s.UseRoutingKeyFormatter(context => "booking.created");
        });

        cfg.Message<BookingExpired>(m => m.SetEntityName("booking-events"));
        cfg.Publish<BookingExpired>(p =>
        {
            p.ExchangeType = "topic";
        });
        cfg.Send<BookingExpired>(s =>
        {
            s.UseRoutingKeyFormatter(context => "booking.expired");
        });

        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IBookingService, BookingService.Services.BookingService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<ICatalogApi, CatalogApi>()
    .AddResilienceHandler("catalog-pipeline", pipelineBuilder =>
    {
        pipelineBuilder.AddTimeout(TimeSpan.FromSeconds(5));
        pipelineBuilder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(10),
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(25)
        });
    });


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Health check endpoint consumed by Consul (returns 200 OK by default)
builder.Services.AddHealthChecks();

builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
});

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
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

