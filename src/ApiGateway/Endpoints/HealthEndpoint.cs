using FastEndpoints;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using HealthChecks.UI.Client;

namespace ApiGateway.Endpoints;

public class HealthEndpoint : Endpoint<EmptyRequest>
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

        var statusCode = report.Status == HealthStatus.Unhealthy ? 503 : 200;
        HttpContext.Response.StatusCode = statusCode;
        HttpContext.Response.ContentType = "application/json";
        await UIResponseWriter.WriteHealthCheckUIResponse(HttpContext, report);
    }
}