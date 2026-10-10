namespace PaymentService.Infrastructure.Consul;

public sealed class ConsulConfig
{
    public string Host { get; init; } = "http://localhost:8500";

    public string ServiceName { get; init; } = string.Empty;

    public string ServiceHost { get; init; } = "localhost";

    public int ServicePort { get; init; }

    public string HealthCheckPath { get; init; } = "/health";

    public string HealthCheckInterval { get; init; } = "10s";

    public string HealthCheckTimeout { get; init; } = "5s";

    public string DeregisterCriticalServiceAfter { get; init; } = "1m";
}
