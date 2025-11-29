using FastEndpoints;

namespace HeadquartersService.Endpoints;

public class GetRootEndpoint // Removed EndpointWithoutRequest inheritance
{
    public void Configure()
    {
        // Get /ping endpoint removed intentionally.
    }

    public Task HandleAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}

// Previously contained the /ping endpoint; removed to avoid FastEndpoints scanning an endpoint with no HTTP verbs.