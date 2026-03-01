using FastEndpoints;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GameService.Endpoints;

public class HealthEndpoint(HealthCheckService healthService) : EndpointWithoutRequest<HealthEndpoint.HealthResponse>
{
    public override void Configure()
    {
        Get("/health");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var report = await healthService.CheckHealthAsync(ct);
        var response = new HealthResponse(
            report.Status.ToString(),
            report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())
        );

        var statusCode = report.Status == HealthStatus.Healthy ? 200 : 503;
        HttpContext.Response.StatusCode = statusCode;
        HttpContext.Response.ContentType = "application/json";
        await HttpContext.Response.WriteAsJsonAsync(response, ct);
    }

    public sealed record HealthResponse(string Status, Dictionary<string, string> Checks);
}