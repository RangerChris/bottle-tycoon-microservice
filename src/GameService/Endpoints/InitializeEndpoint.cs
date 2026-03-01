using FastEndpoints;
using GameService.Services;

namespace GameService.Endpoints;

public class InitializeEndpoint(IHttpClientFactory httpClientFactory, ILogger<InitializeEndpoint> logger, IPlayerService playerService) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/initialize");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        logger.LogInformation("Resetting and creating default player state");
        await playerService.ResetAsync();
        await playerService.CreatePlayerAsync();

        await InitializeServiceAsync("RecyclerService", "/initialize", ct);
        await InitializeServiceAsync("TruckService", "/initialize", ct);
        await InitializeServiceAsync("HeadquartersService", "/initialize", ct);
        await InitializeServiceAsync("RecyclingPlantService", "/initialize", ct);
    }

    private async Task InitializeServiceAsync(string clientName, string path, CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Initializing {Service}", clientName);
            var client = httpClientFactory.CreateClient(clientName);
            var response = await client.PostAsync(path, null, ct);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("{Service} initialization succeeded", clientName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize {Service}", clientName);
        }
    }
}