﻿using FastEndpoints;
using GameService.Services;

namespace GameService.Endpoints;

public class InitializeEndpoint : EndpointWithoutRequest
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<InitializeEndpoint> _logger;
    private readonly IPlayerService _playerService;

    public InitializeEndpoint(IHttpClientFactory httpClientFactory, ILogger<InitializeEndpoint> logger, IPlayerService playerService)
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
        _logger.LogInformation("Resetting and creating default player state");
        await _playerService.ResetAsync();
        await _playerService.CreatePlayerAsync();

        await InitializeServiceAsync("RecyclerService", "/initialize", ct);
        await InitializeServiceAsync("TruckService", "/initialize", ct);
        await InitializeServiceAsync("HeadquartersService", "/initialize", ct);
        await InitializeServiceAsync("RecyclingPlantService", "/initialize", ct);
    }

    private async Task InitializeServiceAsync(string clientName, string path, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Initializing {Service}", clientName);
            var client = _httpClientFactory.CreateClient(clientName);
            var response = await client.PostAsync(path, null, ct);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("{Service} initialization succeeded", clientName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize {Service}", clientName);
        }
    }
}