// ...existing code...

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

namespace GameService.Tests.TestFixtures;

public class SharedTestHostFixture : IDisposable
{
    private readonly IHost _host;
    private readonly PostgreSqlTestcontainer? _pgContainer;
    private readonly SqliteConnection? _sqliteConnection;
    private readonly bool _usePostgres;

    public SharedTestHostFixture()
    {
        // Try to start Postgres testcontainer, otherwise fallback to shared in-memory sqlite
        try
        {
            _pgContainer = new TestcontainersBuilder<PostgreSqlTestcontainer>()
                .WithDatabase(new PostgreSqlTestcontainerConfiguration
                {
                    Database = "testdb",
                    Username = "postgres",
                    Password = "postgres"
                })
                .WithImage("postgres:15-alpine")
                .WithCleanUp(true)
                .Build();

            _pgContainer.StartAsync().GetAwaiter().GetResult();
            _usePostgres = true;
        }
        catch (Exception ex)
        {
            _usePostgres = false;
            _sqliteConnection = new SqliteConnection("DataSource=:memory:");
            _sqliteConnection.Open();
            Console.WriteLine("[TestFixture] Postgres testcontainer start failed; falling back to in-memory SQLite. Reason: " + ex.Message);
        }

        // Build minimal WebApplication for tests
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });

        // Minimal configuration for test host: use in-memory configuration to disable external integrations
        var inMemory = new Dictionary<string, string?>
        {
            ["ENABLE_MESSAGING"] = "false",
            ["DatabaseProvider"] = _usePostgres ? "Npgsql" : "Sqlite",
            ["ConnectionStrings:DefaultConnection"] = _usePostgres ? _pgContainer!.ConnectionString : "DataSource=:memory:"
        };
        builder.Configuration.AddInMemoryCollection(inMemory);

        // Register services needed by the app endpoints
        builder.Services.AddFastEndpoints();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddAuthorization();
        builder.Services.AddHealthChecks();

        if (_usePostgres && _pgContainer != null)
        {
            builder.Services.AddDbContext<GameDbContext>(options => options.UseNpgsql(_pgContainer.ConnectionString));
        }
        else
        {
            // Use the shared Sqlite connection so the in-memory DB lives for the host lifetime
            builder.Services.AddDbContext<GameDbContext>(options => options.UseSqlite(_sqliteConnection ?? throw new InvalidOperationException("Sqlite connection missing")));
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
            if (_usePostgres)
            {
                db.Database.Migrate();
            }
            else
            {
                db.Database.EnsureCreated();
            }
        }

        // Start the host
        _host.StartAsync().GetAwaiter().GetResult();

        // Create HttpClient from TestServer
        Client = _host.GetTestClient();
    }

    public HttpClient Client { get; }

    public void Dispose()
    {
        try
        {
            Client?.Dispose();
        }
        catch
        {
        }

        try
        {
            _host?.StopAsync().GetAwaiter().GetResult();
            _host?.Dispose();
        }
        catch
        {
        }

        if (_usePostgres)
        {
            try
            {
                _pgContainer?.StopAsync().GetAwaiter().GetResult();
                _pgContainer?.DisposeAsync().AsTask().GetAwaiter().GetResult();
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
// ...existing code...