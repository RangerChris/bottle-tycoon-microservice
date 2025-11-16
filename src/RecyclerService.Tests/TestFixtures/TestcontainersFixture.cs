using System.Diagnostics;
using System.Net.Sockets;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace RecyclerService.Tests.TestFixtures;

public class TestcontainersFixture : IAsyncDisposable
{
    public TestcontainersFixture()
    {
        // Configure Testcontainers to avoid trying to attach to streams in constrained Docker environments
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

        RabbitMq = new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("rabbitmq:3-management-alpine")
            .WithName("test-rabbitmq")
            .WithPortBinding(5672, true)
            .WithPortBinding(15672, true)
            .WithEnvironment("RABBITMQ_DEFAULT_USER", "guest")
            .WithEnvironment("RABBITMQ_DEFAULT_PASS", "guest")
            .Build();
    }

    public PostgreSqlTestcontainer Postgres { get; }
    public TestcontainersContainer RabbitMq { get; }
    public bool IsAvailable { get; private set; }

    public async ValueTask DisposeAsync()
    {
        if (IsAvailable)
        {
            await RabbitMq.StopAsync();
            await Postgres.StopAsync();
        }
    }

    public async ValueTask InitializeAsync()
    {
        try
        {
            await Postgres.StartAsync();
            await RabbitMq.StartAsync();
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