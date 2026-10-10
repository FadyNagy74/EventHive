using Consul;
using ApiGateway.Infrastructure.Yarp;
using YarpRouteConfig = Yarp.ReverseProxy.Configuration.RouteConfig;
using YarpClusterConfig = Yarp.ReverseProxy.Configuration.ClusterConfig;
using Yarp.ReverseProxy.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Consul Client ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConsulClient>(_ =>
    new ConsulClient(cfg =>
        cfg.Address = new Uri(builder.Configuration["Consul:Host"] ?? "http://localhost:8500")));
// ──────────────────────────────────────────────────────────────────────────────

// ── Authentication & Authorization removed (handled by downstream microservices) ──

// ── YARP & Consul Sync ─────────────────────────────────────────────────────────
var configProvider = new InMemoryConfigProvider(new List<YarpRouteConfig>(), new List<YarpClusterConfig>());
builder.Services.AddSingleton(configProvider);
builder.Services.AddSingleton<IProxyConfigProvider>(configProvider);

builder.Services.AddReverseProxy();
builder.Services.AddHostedService<ConsulYarpSyncWorker>();
// ──────────────────────────────────────────────────────────────────────────────

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();
app.MapReverseProxy();

app.Run();
