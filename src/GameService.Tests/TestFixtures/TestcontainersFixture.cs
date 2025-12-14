using System.Text.Json.Serialization;
using DotNet.Testcontainers.Builders;
using FastEndpoints;
using GameService.Data;
using GameService.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Serilog;
using Testcontainers.PostgreSql;
using Xunit;

[assembly: CollectionBehavior(MaxParallelThreads = 0)]

namespace GameService.Tests.TestFixtures;

public class TestcontainersFixture : IAsyncLifetime
{
    private readonly string _databaseName;
    private IHost? _host;

    public TestcontainersFixture()
    {
        _databaseName = $"gamestate_{Guid.NewGuid().ToString("N")}";
        // Configure a wait strategy so StartAsync doesn't return until the container port is available
        Postgres = new PostgreSqlBuilder()
            .WithDatabase(_databaseName)
            .WithUsername("postgres")
            .WithPassword("password")
            .WithImage("postgres:16-alpine")
            .WithPortBinding(5432, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5432))
            .WithAutoRemove(true)
            .WithCleanUp(true)
            .Build();
    }

    public PostgreSqlContainer Postgres { get; }

    public HttpClient Client { get; private set; } = null!;

    public bool Started { get; private set; }

    public string ConnectionString { get; private set; } = "";

    public async ValueTask InitializeAsync()
    {
        var started = await TryStartPostgresAsync();

        if (!started)
        {
            throw new InvalidOperationException("Testcontainer failed to start. Ensure Docker is running and testcontainers can create PostgreSQL containers.");
        }

        ConnectionString = Postgres.GetConnectionString();

        // set public flag so callers/tests can safely decide whether the container is available
        Started = started;

        // Build minimal WebApplication for tests (fallback to in-memory sqlite when containers not available)
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });

        var inMemory = new Dictionary<string, string?>
        {
            ["ENABLE_MESSAGING"] = "false",
            ["DatabaseProvider"] = "Npgsql",
            ["ConnectionStrings:GameStateConnection"] = ConnectionString
        };

        builder.Configuration.AddInMemoryCollection(inMemory);

        // Register services needed by the app endpoints
        builder.Services.AddFastEndpoints();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddAuthorization();
        builder.Services.AddHealthChecks();

        builder.Services.AddDbContext<GameDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("GameStateConnection")));

        // Business services
        builder.Services.AddScoped<IPlayerService, PlayerService>();

        // JSON options (same shape as app)
        builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });

        // Use TestServer
        builder.WebHost.UseTestServer();

        var app = builder.Build();

        // Configure minimal request pipeline so FastEndpoints routes are registered
        app.UseFastEndpoints();

        _host = app;

        // Apply migrations / ensure created
        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            // await db.Database.MigrateAsync();
            await db.Database.EnsureCreatedAsync();
        }

        // Start the host
        await _host.StartAsync();

        // Create HttpClient from TestServer
        Client = _host.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_host != null)
            {
                try
                {
                    Client.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error disposing HttpClient");
                }

                await _host.StopAsync();
                _host.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error disposing Host");
        }

        try
        {
            await Postgres.DisposeAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error disposing Postgres container");
        }
    }

    private async Task<bool> TryStartPostgresAsync(int maxAttempts = 3)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (await AttemptStartAsync(attempt))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
        }

        return false;
    }

    private async Task<bool> AttemptStartAsync(int attempt)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));

            await StartContainerInternalAsync(cts.Token);

            var cs = Postgres.GetConnectionString();

            if (await ProbeDatabaseAsync(cs, cts.Token))
            {
                ConnectionString = cs;
                return true;
            }

            await StopContainerSafeAsync(cts.Token);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Attempt {Attempt} to start Postgres container failed", attempt);
        }

        return false;
    }

    private async Task StartContainerInternalAsync(CancellationToken ct)
    {
        try
        {
            await Postgres.StartAsync(ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("cannot hijack chunked or content length stream"))
        {
            Log.Warning("Ignored hijacking error: {Message}", ex.Message);
        }
    }

    private async Task<bool> ProbeDatabaseAsync(string connectionString, CancellationToken ct)
    {
        for (var i = 0; i < 3; i++)
        {
            try
            {
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync(ct);
                await conn.CloseAsync();
                return true;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Probe attempt {Attempt} failed for {ConnectionString}", i + 1, connectionString);
                await Task.Delay(1000, ct);
            }
        }

        return false;
    }

    private async Task StopContainerSafeAsync(CancellationToken ct)
    {
        try
        {
            await Postgres.StopAsync(ct);
        }
        catch (Exception e)
        {
            Log.Error(e, "An exception occurred while stopping the postgres container during retry");
        }
    }
}