namespace TruckService.Services;

public class RandomLoadProvider : ILoadProvider
{
    private readonly Random _rng = new();

    public (int glass, int metal, int plastic) GetLoadForRecycler(Guid recyclerId)
    {
        return (_rng.Next(0, 30), _rng.Next(0, 20), _rng.Next(0, 25));
    }
}