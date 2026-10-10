using System.Collections.Concurrent;
using Consul;
using Yarp.ReverseProxy.Configuration;
using YarpRouteConfig = Yarp.ReverseProxy.Configuration.RouteConfig;
using YarpClusterConfig = Yarp.ReverseProxy.Configuration.ClusterConfig;
using YarpDestinationConfig = Yarp.ReverseProxy.Configuration.DestinationConfig;

namespace ApiGateway.Infrastructure.Yarp;

public class ConsulYarpSyncWorker : BackgroundService
{
    private readonly IConsulClient _consulClient;
    private readonly InMemoryConfigProvider _configProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConsulYarpSyncWorker> _logger;

    private readonly ConcurrentDictionary<string, (Task Task, CancellationTokenSource Cts)> _serviceWatchers =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, ServiceEntry[]> _serviceHealthCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly object _rebuildLock = new();

    private ulong _catalogWaitIndex;

    public ConsulYarpSyncWorker(
        IConsulClient consulClient,
        InMemoryConfigProvider configProvider,
        IConfiguration configuration,
        ILogger<ConsulYarpSyncWorker> logger)
    {
        _consulClient = consulClient;
        _configProvider = configProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncServiceNamesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error watching Consul catalog service names. Retrying shortly.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }


        try
        {
            await Task.WhenAll(_serviceWatchers.Values.Select(w => w.Task));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SyncServiceNamesAsync(CancellationToken stoppingToken)
    {
        var queryOptions = new QueryOptions
        {
            WaitIndex = _catalogWaitIndex,
            WaitTime = TimeSpan.FromMinutes(5)
        };

        var response = await _consulClient.Catalog.Services(queryOptions, stoppingToken);
        _catalogWaitIndex = NextIndex(response.LastIndex, _catalogWaitIndex);

        var currentNames = response.Response.Keys
            .Where(n => !n.Equals("consul", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var name in currentNames)
        {
            if (_serviceWatchers.ContainsKey(name)) continue;

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var task = Task.Run(() => WatchServiceHealthAsync(name, linkedCts.Token), linkedCts.Token);
            _serviceWatchers[name] = (task, linkedCts);

            _logger.LogInformation("Started health watcher for service {ServiceName}.", name);
        }

        foreach (var existingName in _serviceWatchers.Keys.ToList())
        {
            if (currentNames.Contains(existingName)) continue;

            if (_serviceWatchers.TryRemove(existingName, out var entry))
            {
                entry.Cts.Cancel();
                entry.Cts.Dispose();
            }

            _serviceHealthCache.TryRemove(existingName, out _);
            _logger.LogInformation("Stopped health watcher for removed service {ServiceName}.", existingName);
        }

        RebuildAndPushConfig();
    }

    private async Task WatchServiceHealthAsync(string serviceName, CancellationToken ct)
    {
        var waitIndex = 0UL;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var queryOptions = new QueryOptions
                {
                    WaitIndex = waitIndex,
                    WaitTime = TimeSpan.FromMinutes(5)
                };

                var response = await _consulClient.Health.Service(
                    serviceName, tag: "", passingOnly: true, queryOptions, ct);

                waitIndex = NextIndex(response.LastIndex, waitIndex);

                _serviceHealthCache[serviceName] = response.Response;
                RebuildAndPushConfig();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error watching health for service {ServiceName}. Retrying shortly.", serviceName);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }
    private static ulong NextIndex(ulong returnedIndex, ulong previousIndex)
        => returnedIndex < previousIndex ? 0UL : returnedIndex;

    private void RebuildAndPushConfig()
    {
        lock (_rebuildLock)
        {
            var routes = LoadStaticRoutes();
            var clusters = new List<YarpClusterConfig>();

            foreach (var (serviceName, instances) in _serviceHealthCache)
            {
                if (instances.Length == 0) continue;

                var destinations = instances.ToDictionary(
                    instance => instance.Service.ID,
                    instance =>
                    {
                        var host = instance.Service.Address;
                        // If running gateway locally on host, translate Docker's special hostname back to loopback
                        if (host.Equals("host.docker.internal", StringComparison.OrdinalIgnoreCase))
                        {
                            host = "localhost";
                        }

                        return new YarpDestinationConfig
                        {
                            Address = $"http://{host}:{instance.Service.Port}"
                        };
                    });

                clusters.Add(new YarpClusterConfig
                {
                    ClusterId = serviceName,
                    Destinations = destinations
                });
            }

            _configProvider.Update(routes, clusters);

            _logger.LogInformation(
                "YARP routing updated: {RouteCount} routes, {ClusterCount} clusters.",
                routes.Count, clusters.Count);
        }
    }
    private List<YarpRouteConfig> LoadStaticRoutes()
    {
        var routes = new List<YarpRouteConfig>();

        var routeDict = _configuration.GetSection("ReverseProxy:Routes").Get<Dictionary<string, YarpRouteConfig>>();
        if (routeDict is null) return routes;

        foreach (var (key, route) in routeDict)
        {
            routes.Add(route with { RouteId = route.RouteId ?? key });
        }

        return routes;
    }
}