using System.Diagnostics;
using System.Net.Sockets;
using Testcontainers.PostgreSql;

namespace RecyclerService.Tests.TestFixtures;

public class TestcontainersFixture : IAsyncDisposable
{
    public TestcontainersFixture()
    {

        Postgres = new PostgreSqlBuilder()
            .WithDatabase("recyclerstate")
            .WithUsername("postgres")
            .WithPassword("password")
            .WithImage("postgres:16-alpine")
            .Build();
    }

    public PostgreSqlContainer Postgres { get; }
    public bool IsAvailable { get; private set; }

    public async ValueTask DisposeAsync()
    {
        if (IsAvailable)
        {
            await Postgres.StopAsync();
        }
    }

    public async ValueTask InitializeAsync()
    {
        try
        {
            await Postgres.StartAsync();
            IsAvailable = true;
        }
        catch (SocketException)
        {
            IsAvailable = false;
            Debug.WriteLine("Docker not available for testcontainers; integration tests will be skipped.");
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            Debug.WriteLine($"Testcontainers failed to start: {ex.Message}");
        }
    }
}