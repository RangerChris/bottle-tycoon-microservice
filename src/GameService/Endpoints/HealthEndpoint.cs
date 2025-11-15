using FastEndpoints;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GameService.Endpoints;

public class HealthEndpoint : EndpointWithoutRequest<HealthEndpoint.HealthResponse>
{
    private readonly HealthCheckService _healthService;

    public HealthEndpoint(HealthCheckService healthService)
    {
        _healthService = healthService;
    }

    public override void Configure()
    {
        Get("/health");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var report = await _healthService.CheckHealthAsync(ct);
        var response = new HealthResponse(
            report.Status.ToString(),
            report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())
        );

        var statusCode = report.Status == HealthStatus.Healthy ? 200 : 503;
        await SendAsync(response, statusCode, ct);
    }

    public sealed record HealthResponse(string Status, Dictionary<string, string> Checks);
}