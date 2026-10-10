using Consul;
using Microsoft.Extensions.Options;

namespace PaymentService.Infrastructure.Consul;

public sealed class ConsulRegistrationService : IHostedService
{
    private readonly IConsulClient _consul;
    private readonly ConsulConfig _config;
    private readonly ILogger<ConsulRegistrationService> _logger;

    private readonly string _serviceInstanceId;

    public ConsulRegistrationService(
        IConsulClient consul,
        IOptions<ConsulConfig> config,
        ILogger<ConsulRegistrationService> logger)
    {
        _consul = consul;
        _config = config.Value;
        _logger = logger;
        _serviceInstanceId = $"{_config.ServiceName}-{Guid.NewGuid()}";
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var healthCheckUrl =
            $"http://{_config.ServiceHost}:{_config.ServicePort}{_config.HealthCheckPath}";

        var registration = new AgentServiceRegistration
        {
            ID = _serviceInstanceId,
            Name = _config.ServiceName,
            Address = _config.ServiceHost,
            Port = _config.ServicePort,
            Tags = ["v1"],
            Check = new AgentServiceCheck
            {
                HTTP = healthCheckUrl,
                Interval = ParseConsulDuration(_config.HealthCheckInterval),
                Timeout = ParseConsulDuration(_config.HealthCheckTimeout),
                DeregisterCriticalServiceAfter = ParseConsulDuration(_config.DeregisterCriticalServiceAfter)
            }
        };

        await _consul.Agent.ServiceDeregister(_serviceInstanceId, cancellationToken);
        await _consul.Agent.ServiceRegister(registration, cancellationToken);

        _logger.LogInformation(
            "Registered [{ServiceName}] with Consul as [{InstanceId}] — health check: {HealthCheckUrl}",
            _config.ServiceName, _serviceInstanceId, healthCheckUrl);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Deregistering [{ServiceName}/{InstanceId}] from Consul…",
            _config.ServiceName, _serviceInstanceId);

        await _consul.Agent.ServiceDeregister(_serviceInstanceId, cancellationToken);
    }

    private static TimeSpan ParseConsulDuration(string duration)
    {
        if (duration.EndsWith('m'))
            return TimeSpan.FromMinutes(double.Parse(duration.TrimEnd('m')));
        if (duration.EndsWith('s'))
            return TimeSpan.FromSeconds(double.Parse(duration.TrimEnd('s')));
        return TimeSpan.Parse(duration);
    }
}
