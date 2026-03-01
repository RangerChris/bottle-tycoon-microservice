using HeadquartersService.Models;

namespace HeadquartersService.Services;

public class DispatchProcessor(IDispatchQueue queue, IFleetService fleet, ILogger<DispatchProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (queue.TryDequeue(out var req) && req != null)
                {
                    if (fleet.TryAssignTruck(req, out var truck) && truck != null)
                    {
                        logger.LogInformation("Assigned truck {TruckId} to dispatch {DispatchId}", truck.Id, req.Id);
                        // simulate progress
                        await Task.Delay(10, stoppingToken);
                        req.Status = DispatchStatus.InProgress;
                        await Task.Delay(10, stoppingToken);
                        req.Status = DispatchStatus.Completed;
                        truck.Status = TruckStatus.Idle;
                        logger.LogInformation("Completed dispatch {DispatchId}", req.Id);
                    }
                    else
                    {
                        // requeue
                        req.Status = DispatchStatus.Pending;
                        queue.Enqueue(req);
                        logger.LogWarning("No truck available for dispatch {DispatchId}; requeued", req.Id);
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
                logger.LogError(ex, "Error processing dispatch queue");
            }
        }
    }
}