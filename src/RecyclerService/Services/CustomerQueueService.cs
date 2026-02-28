using Microsoft.EntityFrameworkCore;
using RecyclerService.Data;
using RecyclerService.Models;

namespace RecyclerService.Services;

public interface ICustomerQueueService
{
    Task<Customer?> GetNextWaitingCustomerAsync(Guid recyclerId, CancellationToken ct = default);
    Task MarkAsProcessingAsync(Guid customerId, CancellationToken ct = default);
    Task MarkAsDoneAsync(Guid customerId, CancellationToken ct = default);
    Task<int> GetQueueDepthAsync(Guid recyclerId, CancellationToken ct = default);
}

public class CustomerQueueService : ICustomerQueueService
{
    private readonly RecyclerDbContext _db;
    private readonly ILogger<CustomerQueueService> _logger;

    public CustomerQueueService(RecyclerDbContext db, ILogger<CustomerQueueService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Customer?> GetNextWaitingCustomerAsync(Guid recyclerId, CancellationToken ct = default)
    {
        var customer = await _db.Customers
            .Where(c => c.RecyclerId == recyclerId && c.Status == CustomerStatus.Waiting)
            .OrderBy(c => c.ArrivedAt)
            .FirstOrDefaultAsync(ct);

        if (customer != null)
        {
            customer.Status = CustomerStatus.Processing;
            customer.ServiceStartedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Customer {CustomerId} marked as Processing at Recycler {RecyclerId}", customer.Id, recyclerId);
        }

        return customer;
    }

    public async Task MarkAsProcessingAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer != null)
        {
            customer.Status = CustomerStatus.Processing;
            customer.ServiceStartedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Customer {CustomerId} marked as Processing", customerId);
        }
    }

    public async Task MarkAsDoneAsync(Guid customerId, CancellationToken ct = default)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer != null)
        {
            customer.Status = CustomerStatus.Done;
            customer.ProcessedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Customer {CustomerId} marked as Done at {ProcessedAt}", customerId, customer.ProcessedAt);
        }
    }

    public async Task<int> GetQueueDepthAsync(Guid recyclerId, CancellationToken ct = default)
    {
        return await _db.Customers
            .Where(c => c.RecyclerId == recyclerId && c.Status == CustomerStatus.Waiting)
            .CountAsync(ct);
    }
}