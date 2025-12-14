using RecyclerService.Models;

namespace RecyclerService.Services;

public interface IRecyclerService
{
    Task<Recycler?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Recycler> VisitorArrivedAsync(Guid recyclerId, Visitor visitor, CancellationToken ct = default);
    Task<List<Recycler>> GetAllAsync(CancellationToken ct = default);
    Task ResetAsync();
    Task<Recycler> CreateRecyclerAsync(Recycler? recycler = null);
}