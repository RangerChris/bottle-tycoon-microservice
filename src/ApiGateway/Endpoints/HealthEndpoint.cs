using FastEndpoints;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ApiGateway.Endpoints;

public class HealthEndpoint : Endpoint<EmptyRequest, HealthResponse>
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

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var report = await _healthService.CheckHealthAsync(ct);
        var response = new HealthResponse(report.Status.ToString(), new Dictionary<string, string>());

        var statusCode = report.Status == HealthStatus.Healthy ? 200 : 503;
        HttpContext.Response.StatusCode = statusCode;
        HttpContext.Response.ContentType = "application/json";
        await HttpContext.Response.WriteAsJsonAsync(response, ct);
    }
}

public sealed record HealthResponse(string Status, Dictionary<string, string> Checks);