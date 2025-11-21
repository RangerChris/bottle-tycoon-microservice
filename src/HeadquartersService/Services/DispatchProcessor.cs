using HeadquartersService.Models;

namespace HeadquartersService.Services;

public class DispatchProcessor : BackgroundService
{
    private readonly IFleetService _fleet;
    private readonly ILogger<DispatchProcessor> _logger;
    private readonly IDispatchQueue _queue;

    public DispatchProcessor(IDispatchQueue queue, IFleetService fleet, ILogger<DispatchProcessor> logger)
    {
        _queue = queue;
        _fleet = fleet;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_queue.TryDequeue(out var req) && req != null)
                {
                    if (_fleet.TryAssignTruck(req, out var truck) && truck != null)
                    {
                        _logger.LogInformation("Assigned truck {TruckId} to dispatch {DispatchId}", truck.Id, req.Id);
                        // simulate progress
                        await Task.Delay(10, stoppingToken);
                        req.Status = DispatchStatus.InProgress;
                        await Task.Delay(10, stoppingToken);
                        req.Status = DispatchStatus.Completed;
                        truck.Status = TruckStatus.Idle;
                        _logger.LogInformation("Completed dispatch {DispatchId}", req.Id);
                    }
                    else
                    {
                        // requeue
                        req.Status = DispatchStatus.Pending;
                        _queue.Enqueue(req);
                        _logger.LogWarning("No truck available for dispatch {DispatchId}; requeued", req.Id);
                        await Task.Delay(50, stoppingToken);
                    }
                }
                else
                {
                    await Task.Delay(100, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing dispatch queue");
            }
        }
    }
}