using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RecyclerService.Consumers;
using RecyclerService.Data;
using RecyclerService.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddFastEndpoints();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    // Use full type name (including nested type marker '+') as schema id to prevent duplicate short names like "Request"
    o.CustomSchemaIds(t => t.FullName?.Replace('+', '.') ?? t.Name);
});

// Database
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<RecyclerDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("RecyclerConnection")));
}
else
{
    // When running tests, allow choosing an in-memory provider via configuration for reliability in tests
    // Default to using the in-memory provider for the Testing environment unless explicitly disabled by configuration.
    var useInMemory = builder.Configuration.GetValue<bool?>("USE_INMEMORY") ?? true;
    if (useInMemory)
    {
        builder.Services.AddDbContext<RecyclerDbContext>(options =>
            options.UseInMemoryDatabase("RecyclerService_TestDb"));
    }
    // If tests intentionally set USE_INMEMORY=false, they are expected to register the DbContext themselves.
    // Test projects may still override the DbContext registration using ConfigureTestServices or ConfigureServices in the test host.
}

// MassTransit
var enableMessaging = builder.Configuration.GetValue<bool?>("ENABLE_MESSAGING") ?? true;
if (enableMessaging)
{
    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<TruckArrivedConsumer>();
        x.AddConsumer<TruckLoadedConsumer>();

        x.SetKebabCaseEndpointNameFormatter();

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

            cfg.ConfigureEndpoints(context);
        });
    });
}
else
{
    Log.Information("Messaging disabled via ENABLE_MESSAGING=false; MassTransit will not be started");
}

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? "RecyclerService")
        .AddEnvironmentVariableDetector())
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddJaegerExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

// Health Checks
var healthChecks = builder.Services.AddHealthChecks();
if (!builder.Environment.IsEnvironment("Testing"))
{
    var conn = builder.Configuration.GetConnectionString("RecyclerConnection");
    if (!string.IsNullOrEmpty(conn))
    {
        healthChecks.AddNpgSql(conn);
    }
}

// Business Services
builder.Services.AddScoped<IRecyclerService, RecyclerService.Services.RecyclerService>();

try
{
    var app = builder.Build();

        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();

            // Log DB connection string for visibility
            try
            {
                var conn = dbContext.Database.GetDbConnection().ConnectionString;
                Log.Information("RecyclerService will attempt migrations using connection: {Conn}", conn);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to read DB connection string for logging");
            }

            // Ensure schema exists first (safe in dev/non-testing) to avoid "relation does not exist" during early requests
            try
            {
                dbContext.Database.EnsureCreated();
                Log.Information("Database.EnsureCreated() executed at startup");
            }
            catch (Exception ensureEx)
            {
                Log.Warning(ensureEx, "EnsureCreated() failed during startup; will continue with migration attempts");
            }

            // Retry logic for applying migrations (helps when DB may be starting concurrently)
            var maxAttempts = builder.Configuration.GetValue<int?>("DB_MIGRATION_MAX_ATTEMPTS") ?? 6;
            var retryDelaySeconds = builder.Configuration.GetValue<int?>("DB_MIGRATION_RETRY_DELAY_SECONDS") ?? 5;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    // Try to open a connection to ensure database is reachable before migrating
                    try
                    {
                        var connObj = dbContext.Database.GetDbConnection();
                        connObj.Open();
                        connObj.Close();
                    }
                    catch (Exception connEx)
                    {
                        Log.Warning(connEx, "Database connection check failed on attempt {Attempt}/{Max}", attempt, maxAttempts);
                    }

                    List<string> pendingList;
                    try
                    {
                        pendingList = dbContext.Database.GetPendingMigrations().ToList();
                    }
                    catch (Exception pendEx)
                    {
                        Log.Warning(pendEx, "Failed to enumerate pending migrations on attempt {Attempt}/{Max}", attempt, maxAttempts);
                        pendingList = new List<string>();
                    }

                    if (pendingList.Count > 0)
                    {
                        Log.Information("Applying {Count} pending migrations: {Names}", pendingList.Count, string.Join(',', pendingList));
                    }

                    dbContext.Database.Migrate();

                    Log.Information("Database migrations applied successfully");
                    break;
                }
                catch (Exception migrateEx)
                {
                    Log.Warning(migrateEx, "Migration attempt {Attempt}/{Max} failed", attempt, maxAttempts);

                    if (attempt == maxAttempts)
                    {
                        Log.Error(migrateEx, "All migration attempts failed");

                        // Fallback: attempt to create database schema if migrations couldn't be applied
                        try
                        {
                            Log.Information("Attempting fallback Database.EnsureCreated() to create schema");
                            dbContext.Database.EnsureCreated();
                            Log.Information("Database.EnsureCreated() succeeded");
                        }
                        catch (Exception ensureEx)
                        {
                            Log.Error(ensureEx, "Database.EnsureCreated() fallback also failed");
                        }

                        break;
                    }

                    try
                    {
                        Thread.Sleep(retryDelaySeconds * 1000);
                    }
                    catch (Exception sleepEx)
                    {
                        Log.Warning(sleepEx, "Sleep interrupted during migration retry wait");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while attempting database migrations during startup. The application will continue to start, but database functionality may be degraded");
        }

    // In testing environment ensure the database schema exists (useful for in-memory sqlite or in-memory provider)
    if (app.Environment.IsEnvironment("Testing"))
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
            dbContext.Database.EnsureCreated();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while ensuring database creation in Testing environment");
        }
    }

    var swaggerEnabled = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing");

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    app.UseOpenTelemetryPrometheusScrapingEndpoint();

    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready");

    app.UseFastEndpoints();

    app.MapGet("/", () => swaggerEnabled ? Results.Redirect("/swagger") : Results.Text("RecyclerService OK"));

    Log.Information("Starting RecyclerService host");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly during startup");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public abstract partial class Program
{
}

[ExcludeFromCodeCoverage]
public abstract partial class Program
{
}