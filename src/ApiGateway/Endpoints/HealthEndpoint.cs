using FastEndpoints;
using System.Text.Json;

namespace ApiGateway.Endpoints;

public class HealthEndpoint : Endpoint<EmptyRequest>
{
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly string[] ServiceNames = new[]
    {
        "gameservice",
        "recyclerservice",
        "truckservice",
        "headquartersservice",
        "recyclingplantservice"
    };

    public HealthEndpoint(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public override void Configure()
    {
        Get("/health");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        var results = new Dictionary<string, string>();
        var anyUnhealthy = false;

        foreach (var svc in ServiceNames)
        {
            try
            {
                var url = $"http://{svc}/health/live";
                using var resp = await client.GetAsync(url, ct);
                if (resp.IsSuccessStatusCode)
                {
                    results[svc] = "Healthy";
                }
                else
                {
                    results[svc] = $"Unhealthy ({(int)resp.StatusCode})";
                    anyUnhealthy = true;
                }
            }
            catch (Exception)
            {
                results[svc] = "Unreachable";
                anyUnhealthy = true;
            }
        }

        var overallStatus = anyUnhealthy ? "Unhealthy" : "Healthy";
        HttpContext.Response.StatusCode = anyUnhealthy ? 503 : 200;
        HttpContext.Response.ContentType = "application/json";

        var payload = new
        {
            status = overallStatus,
            services = results
        };

        await JsonSerializer.SerializeAsync(HttpContext.Response.Body, payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }, ct);
    }
}