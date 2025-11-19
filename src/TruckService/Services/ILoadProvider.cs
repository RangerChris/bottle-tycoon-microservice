namespace TruckService.Services;

public interface ILoadProvider
{
    (int glass, int metal, int plastic) GetLoadForRecycler(Guid recyclerId);
}