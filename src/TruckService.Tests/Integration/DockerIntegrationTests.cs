using System.Diagnostics;
using System.Net.Http.Json;
using Shouldly;
using Xunit;

namespace TruckService.Tests.Integration;

public class DockerIntegrationTests
{
    // This test is skipped by default because it requires Docker Compose and the dev stack.
    [Fact(Skip = "Requires Docker Compose local environment (docker-compose.dev.yml)")]
    public async Task FullStack_DispatchAndProcessDelivery_EndToEnd()
    {
        var composeFile = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "..", "docker-compose.dev.yml");

        // Start compose
        var up = Process.Start(new ProcessStartInfo
        {
            FileName = "docker-compose",
            Arguments = $"-f \"{composeFile}\" up --build -d",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        Debug.Assert(up != null, nameof(up) + " != null");
        await up.WaitForExitAsync(TestContext.Current.CancellationToken);
        if (up.ExitCode != 0)
        {
            throw new Exception("docker-compose up failed: " + await up.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken));
        }

        try
        {
            // Wait for truck service to be available
            var client = new HttpClient { BaseAddress = new Uri("http://localhost:5003") };
            var healthy = false;
            for (var i = 0; i < 60; i++)
            {
                try
                {
                    var r = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
                    if (r.IsSuccessStatusCode)
                    {
                        healthy = true;
                        break;
                    }
                }
                catch
                {
                    // ignored
                }

                await Task.Delay(2000, TestContext.Current.CancellationToken);
            }

            if (!healthy)
            {
                throw new Exception("TruckService did not become healthy in time");
            }

            // Create a truck
            var create = new { Id = Guid.NewGuid(), LicensePlate = "INT-1", Model = "M", IsActive = true };
            var createRes = await client.PostAsJsonAsync("/trucks", create, TestContext.Current.CancellationToken);
            createRes.EnsureSuccessStatusCode();
            await createRes.Content.ReadFromJsonAsync<object>(TestContext.Current.CancellationToken);

            // Dispatch
            var dispatch = new { TruckId = create.Id, RecyclerId = Guid.NewGuid(), DistanceKm = 10.0 };
            var dispatchRes = await client.PostAsJsonAsync($"/api/v1/trucks/{create.Id}/dispatch", dispatch, TestContext.Current.CancellationToken);
            dispatchRes.EnsureSuccessStatusCode();

            // Trigger worker to process queued delivery
            var procRes = await client.PostAsync("/admin/routeworker/process-next", null, TestContext.Current.CancellationToken);
            procRes.EnsureSuccessStatusCode();

            // Fetch history
            var historyRes = await client.GetAsync($"/api/v1/trucks/{create.Id}/history", TestContext.Current.CancellationToken);
            historyRes.EnsureSuccessStatusCode();
            var history = await historyRes.Content.ReadFromJsonAsync<object[]>(TestContext.Current.CancellationToken);
            history.ShouldNotBeNull();
            history.Length.ShouldBeGreaterThan(0);

            // Fetch earnings
            var earningsRes = await client.GetAsync($"/api/v1/trucks/{create.Id}/earnings", TestContext.Current.CancellationToken);
            earningsRes.EnsureSuccessStatusCode();
            var earnings = await earningsRes.Content.ReadFromJsonAsync<decimal>(TestContext.Current.CancellationToken);
            earnings.ShouldBeGreaterThanOrEqualTo(0);
        }
        finally
        {
            // Tear down compose
            var down = Process.Start(new ProcessStartInfo
            {
                FileName = "docker-compose",
                Arguments = $"-f \"{composeFile}\" down",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            Debug.Assert(down != null, nameof(down) + " != null");
            await down.WaitForExitAsync(TestContext.Current.CancellationToken);
        }
    }
}