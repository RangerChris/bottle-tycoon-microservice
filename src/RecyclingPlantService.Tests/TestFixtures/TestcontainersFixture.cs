using System.Diagnostics;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace RecyclingPlantService.Tests.TestFixtures;

public class TestcontainersFixture : IAsyncDisposable
{
    public TestcontainersFixture()
    {
        TestcontainersSettings.HubImageNamePrefix = "ryuk:0.3.0";

        Postgres = new TestcontainersBuilder<PostgreSqlTestcontainer>()
            .WithDatabase(new PostgreSqlTestcontainerConfiguration
            {
                Database = "recyclerstate",
                Username = "postgres",
                Password = "password"
            })
            .WithImage("postgres:16-alpine")
            .Build();

        Redis = new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("redis:7-alpine")
            .WithName("test-redis-rplant")
            .WithPortBinding(6379, true)
            .Build();
    }

    public PostgreSqlTestcontainer Postgres { get; }
    public TestcontainersContainer Redis { get; }
    public bool IsAvailable { get; private set; }

    public async ValueTask DisposeAsync()
    {
        if (IsAvailable)
        {
            await Redis.StopAsync();
            await Postgres.StopAsync();
        }
    }

    public async ValueTask InitializeAsync()
    {
        try
        {
            await Postgres.StartAsync();
            await Redis.StartAsync();
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