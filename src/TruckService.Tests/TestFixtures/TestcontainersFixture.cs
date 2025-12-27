using System.Text.Json.Serialization;
using DotNet.Testcontainers.Builders;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Testcontainers.PostgreSql;
using TruckService.Data;
using TruckService.Services;
using Xunit;

[assembly: CollectionBehavior(MaxParallelThreads = 0)]

namespace TruckService.Tests.TestFixtures;

public class TestcontainersFixture : IAsyncLifetime
{
    private readonly string _databaseName;

    public TestcontainersFixture()
    {
        _databaseName = $"truckstate_{Guid.NewGuid().ToString("N")}";
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

    public List<HttpRequestMessage> HttpRequests { get; } = [];

    public IHost? Host { get; private set; }

    public async ValueTask InitializeAsync()
    {
        await Postgres.StartAsync();

        ConnectionString = Postgres.GetConnectionString();
        // Ensure the host is resolvable
        ConnectionString = ConnectionString.Replace("Host=localhost", "Host=127.0.0.1");

        // set public flag so callers/tests can safely decide whether the container is available
        Started = true;

        // Build minimal WebApplication for tests (fallback to in-memory sqlite when containers not available)
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });

        var inMemory = new Dictionary<string, string?>
        {
            ["ENABLE_MESSAGING"] = "false",
            ["DatabaseProvider"] = "Npgsql",
            ["ConnectionStrings:DefaultConnection"] = ConnectionString
        };

        builder.Configuration.AddInMemoryCollection(inMemory);

        // Register services needed by the app endpoints
        builder.Services.AddFastEndpoints();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddAuthorization();
        builder.Services.AddHealthChecks();
        builder.Services.AddHttpClient();

        // Add HttpClient for inter-service communication (mocked for tests)
        builder.Services.AddHttpClient("GameService", client =>
            {
                client.BaseAddress = new Uri("http://localhost:5001"); // Test port, but since no service, it will be mocked
            })
            .AddHttpMessageHandler(() => new CapturingHandler(HttpRequests));

        builder.Services.AddDbContext<TruckDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddScoped<ITruckRepository, EfTruckRepository>();
        builder.Services.AddScoped<ILoadProvider, RandomLoadProvider>();
        builder.Services.AddScoped<ITruckManager, TruckManager>();
        builder.Services.AddScoped<IRouteWorker, RouteWorker>();
        builder.Services.AddScoped<ITruckService, Services.TruckService>();

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

        app.MapGet("/", () => Results.Text("TruckService OK"));

        Host = app;

        // Apply migrations / ensure created
        using (var scope = Host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TruckDbContext>();
            // await db.Database.MigrateAsync();
            await db.Database.EnsureCreatedAsync();
        }

        // Start the host
        await Host.StartAsync();

        // Create HttpClient from TestServer
        Client = Host.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Host != null)
            {
                try
                {
                    Client.Dispose();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error disposing HttpClient");
                }

                await Host.StopAsync();
                Host.Dispose();
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

    private class CapturingHandler : DelegatingHandler
    {
        private readonly List<HttpRequestMessage> _requests;

        public CapturingHandler(List<HttpRequestMessage> requests)
        {
            _requests = requests;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _requests.Add(request);
            // Return a successful response since the service isn't running
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}