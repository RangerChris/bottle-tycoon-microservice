using RecyclerService.Models;

namespace RecyclerService.Services;

public interface IRecyclerService
{
    Task<Recycler?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Recycler>> GetAllAsync(CancellationToken ct = default);
    Task<Recycler> CustomerArrivedAsync(Guid recyclerId, Customer customer, CancellationToken ct = default);
    Task<Customer?> GetNextCustomerAsync(Guid recyclerId, CancellationToken ct = default);
    Task MarkCustomerDoneAsync(Guid customerId, CancellationToken ct = default);
    Task ResetAsync();
    Task<Recycler> CreateRecyclerAsync(Recycler? recycler = null);
    Task RecordBottlesProcessedAsync(Dictionary<string, int> bottlesByType);
}