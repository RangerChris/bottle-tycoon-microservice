using FastEndpoints;

namespace GameService.Endpoints;

public class InitializeEndpoint : EndpointWithoutRequest
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<InitializeEndpoint> _logger;
    private readonly Services.IPlayerService _playerService;

    public InitializeEndpoint(IHttpClientFactory httpClientFactory, ILogger<InitializeEndpoint> logger, Services.IPlayerService playerService)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _playerService = playerService;
    }

    public override void Configure()
    {
        Post("/initialize");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Initialize player service
        await _playerService.ResetAsync();
        await _playerService.CreatePlayerAsync();

        // Initialize recycler service
        try
        {
            var client = _httpClientFactory.CreateClient("RecyclerService");
            var response = await client.PostAsync("/initialize", null, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            // Log the error but don't fail the initialization
            _logger.LogError(ex, "Failed to initialize RecyclerService");
        }
    }
}