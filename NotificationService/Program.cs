using Consul;
using EventHive.Shared.Exceptions;
using EventHive.Shared.MessageBroker;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using NotificationService.Consumers;
using NotificationService.Infrastructure.Consul;
using NotificationService.Infrastructure.Repositories;
using NotificationService.Infrastructure.Senders;
using NotificationService.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

try
{
    BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
}
catch (BsonSerializationException)
{
}

// Consul
builder.Services.Configure<ConsulConfig>(builder.Configuration.GetSection("Consul"));

builder.Services.AddSingleton<IConsulClient>(_ =>
    new ConsulClient(cfg =>
        cfg.Address = new Uri(builder.Configuration["Consul:Host"] ?? "http://localhost:8500")));

builder.Services.AddHostedService<ConsulRegistrationService>();

// MongoDB
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(builder.Configuration["MongoDb:ConnectionString"] ?? "mongodb://localhost:27017/?uuidRepresentation=standard"));

builder.Services.AddScoped<IMongoDatabase>(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    var dbName = builder.Configuration["MongoDb:DatabaseName"] ?? "EventHiveNotification";
    return client.GetDatabase(dbName);
});

// Repositories & Services
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationSender, LoggingNotificationSender>();
builder.Services.AddScoped<INotificationService, NotificationService.Services.NotificationService>();

// MassTransit / RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<BookingCreatedConsumer>();
    x.AddConsumer<PaymentSucceededConsumer>();
    x.AddConsumer<PaymentFailedConsumer>();
    x.AddConsumer<BookingExpiredConsumer>();
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

        cfg.ReceiveEndpoint("notification-booking-created-queue", e =>
        {
            e.ConfigureConsumeTopology = false;
            e.Bind("booking-events", b =>
            {
                b.ExchangeType = "topic";
                b.RoutingKey = "booking.created";
            });
            e.ConfigureConsumer<BookingCreatedConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("notification-payment-succeeded-queue", e =>
        {
            e.ConfigureConsumeTopology = false;
            e.Bind("payment-succeeded", b =>
            {
                b.ExchangeType = "topic";
                b.RoutingKey = "payment.succeeded";
            });
            e.ConfigureConsumer<PaymentSucceededConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("notification-payment-failed-queue", e =>
        {
            e.ConfigureConsumeTopology = false;
            e.Bind("payment-failed", b =>
            {
                b.ExchangeType = "topic";
                b.RoutingKey = "payment.failed";
            });
            e.ConfigureConsumer<PaymentFailedConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("notification-booking-expired-queue", e =>
        {
            e.ConfigureConsumeTopology = false;
            e.Bind("booking-events", b =>
            {
                b.ExchangeType = "topic";
                b.RoutingKey = "booking.expired";
            });
            e.ConfigureConsumer<BookingExpiredConsumer>(ctx);
        });

        cfg.ReceiveEndpoint("notification-event-cancelled-queue", e =>
        {
            e.ConfigureConsumeTopology = false;
            e.Bind("event-cancelled", b =>
            {
                b.ExchangeType = "topic";
                b.RoutingKey = "event.cancelled";
            });
            e.ConfigureConsumer<EventCancelledConsumer>(ctx);
        });
    });
});

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

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

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
