using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using FastEndpoints;
using GameService.Data;
using GameService.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace GameService.Tests.TestFixtures;

public class TestcontainersFixture : IAsyncLifetime
{
    private IHost? _host;
    private SqliteConnection? _sqliteConnection;

    public TestcontainersFixture()
    {
        // Configure Testcontainers to avoid trying to attach to streams in constrained Docker environments
        TestcontainersSettings.HubImageNamePrefix = "ryuk:0.3.0";

        Postgres = new TestcontainersBuilder<PostgreSqlTestcontainer>()
            .WithDatabase(new PostgreSqlTestcontainerConfiguration
            {
                Database = "gamestate",
                Username = "postgres",
                Password = "password"
            })
            .WithImage("postgres:16-alpine")
            .Build();
    }

    public PostgreSqlTestcontainer Postgres { get; }
    public bool IsAvailable { get; private set; }

    public HttpClient Client { get; private set; } = null!;

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

        // Build minimal WebApplication for tests (fallback to in-memory sqlite when containers not available)
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });

        var usePostgres = IsAvailable;

        var inMemory = new Dictionary<string, string?>
        {
            ["ENABLE_MESSAGING"] = "false",
            ["DatabaseProvider"] = usePostgres ? "Npgsql" : "Sqlite",
            ["ConnectionStrings:GameStateConnection"] = usePostgres ? Postgres.ConnectionString : "DataSource=:memory:"
        };

        builder.Configuration.AddInMemoryCollection(inMemory);

        // Register services needed by the app endpoints
        builder.Services.AddFastEndpoints();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddAuthorization();
        builder.Services.AddHealthChecks();

        if (usePostgres)
        {
            builder.Services.AddDbContext<GameDbContext>(options => options.UseNpgsql(Postgres.ConnectionString));
        }
        else
        {
            // Use the shared Sqlite connection so the in-memory DB lives for the host lifetime
            _sqliteConnection = new SqliteConnection("DataSource=:memory:");
            _sqliteConnection.Open();
            builder.Services.AddDbContext<GameDbContext>(options => options.UseSqlite(_sqliteConnection));
        }

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
            if (usePostgres)
            {
                db.Database.Migrate();
            }
            else
            {
                db.Database.EnsureCreated();
            }
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
                    Client?.Dispose();
                }
                catch
                {
                }

                await _host.StopAsync();
                _host.Dispose();
            }
        }
        catch
        {
        }

        if (IsAvailable)
        {

            try
            {
                await Postgres.StopAsync();
            }
            catch
            {
            }
        }
        else
        {
            try
            {
                _sqliteConnection?.Close();
                _sqliteConnection?.Dispose();
            }
            catch
            {
            }
        }
    }
}