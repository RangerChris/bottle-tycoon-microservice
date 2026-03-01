using System.Text.Json;

namespace TruckService.Services;

public class RecyclerServiceLoadProvider(IHttpClientFactory httpClientFactory, ILogger<RecyclerServiceLoadProvider> logger, IConfiguration configuration) : ILoadProvider
{
    private readonly string _recyclerServiceUrl = configuration["RecyclerService:Url"] ?? "http://localhost:5002";

    public async Task<(int glass, int metal, int plastic)> GetLoadForRecyclerAsync(Guid recyclerId, int maxCapacity, CancellationToken ct = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            var response = await client.PostAsJsonAsync(
                $"{_recyclerServiceUrl}/recyclers/{recyclerId}/pickup",
                new { RecyclerId = recyclerId, MaxCapacity = maxCapacity },
                ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to pickup bottles from recycler {RecyclerId}: {StatusCode}", recyclerId, response.StatusCode);
                return (0, 0, 0);
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<PickupResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result?.BottlesPickedUp == null)
            {
                return (0, 0, 0);
            }

            var glass = result.BottlesPickedUp.GetValueOrDefault("glass", 0);
            var metal = result.BottlesPickedUp.GetValueOrDefault("metal", 0);
            var plastic = result.BottlesPickedUp.GetValueOrDefault("plastic", 0);

            logger.LogInformation("Picked up from recycler {RecyclerId}: Glass={Glass}, Metal={Metal}, Plastic={Plastic}", recyclerId, glass, metal, plastic);

            return (glass, metal, plastic);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error picking up bottles from recycler {RecyclerId}", recyclerId);
            return (0, 0, 0);
        }
    }

    private class PickupResponse
    {
        public Dictionary<string, int>? BottlesPickedUp { get; set; }
        public int TotalPickedUp { get; set; }
        public Dictionary<string, int>? RemainingBottles { get; set; }
    }
}