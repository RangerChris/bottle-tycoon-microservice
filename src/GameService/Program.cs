using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using FastEndpoints;
using GameService.Consumers;
using GameService.Data;
using GameService.Services;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddFastEndpoints();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthorization();

// Database provider selection (default to Npgsql). Tests can override by setting DatabaseProvider=Sqlite and ConnectionStrings:DefaultConnection appropriately.
var providerAlreadyRegistered = builder.Services.Any(sd =>
    sd.ServiceType == typeof(DbContextOptions<GameDbContext>)
    || sd.ServiceType == typeof(GameDbContext)
    || sd.ImplementationType == typeof(GameDbContext));
var providerRegistered = false;

// If running under the WebApplicationFactory test host with environment "Testing", do not register a provider here; tests will register Sqlite.
if (!builder.Environment.IsEnvironment("Testing") && !providerAlreadyRegistered)
{
    var dbProvider = (builder.Configuration["DatabaseProvider"] ?? "Npgsql").Trim();
    if (string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        // Use SQLite (tests can provide connection string or use in-memory)
        var sqliteConn = builder.Configuration.GetConnectionString("DefaultConnection") ?? "DataSource=:memory:";
        builder.Services.AddDbContext<GameDbContext>(options => options.UseSqlite(sqliteConn));
    }
    else
    {
        // Default to Postgres
        builder.Services.AddDbContext<GameDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("GameStateConnection") ?? builder.Configuration.GetConnectionString("DefaultConnection")));
    }

    providerRegistered = true;
}

// Redis
builder.Services.AddStackExchangeRedisCache(options => { options.Configuration = builder.Configuration.GetConnectionString("Redis"); });

// MassTransit with RabbitMQ (optional via ENABLE_MESSAGING=false)
var enableMessaging = builder.Configuration.GetValue<bool?>("ENABLE_MESSAGING") ?? true;
if (enableMessaging)
{
    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<CreditsCreditedConsumer>();
        x.AddConsumer<DeliveryCompletedConsumer>();

        x.SetKebabCaseEndpointNameFormatter();

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"], h =>
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
        .AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? "GameService")
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
// Only add NpgSql health check when the Postgres provider was actually registered
if (providerRegistered && string.Equals((builder.Configuration["DatabaseProvider"] ?? "Npgsql").Trim(), "Npgsql", StringComparison.OrdinalIgnoreCase))
{
    healthChecks.AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);
}

// Redis health check always added (if connection string missing it may throw during runtime)
healthChecks.AddRedis(builder.Configuration.GetConnectionString("Redis")!);

// Business Services
builder.Services.AddScoped<IPlayerService, PlayerService>();

// Add JSON options to avoid serialization cycles
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

try
{
    var app = builder.Build();

    // Apply EF Core migrations using the built app's service provider to avoid duplicating singletons
    // Apply migrations when configuration key APPLY_MIGRATIONS is not explicitly false.
    if (builder.Configuration.GetValue<bool?>("APPLY_MIGRATIONS") != false)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            // Rely on EF Core migrations to create schema; do not use EnsureCreated or custom SQL fallbacks.
            // Wait for the database to accept connections (helps when starting with Docker Compose)
            var dbConnection = dbContext.Database.GetDbConnection();
            var connectionString = dbConnection.ConnectionString;
            var maxAttempts = builder.Configuration.GetValue<int?>("DB_MIGRATION_MAX_ATTEMPTS") ?? 30;
            var attempt = 0;
            var connected = false;
            while (attempt < maxAttempts && !connected)
            {
                try
                {
                    attempt++;
                    using var conn = new NpgsqlConnection(connectionString);
                    conn.Open();
                    connected = true;
                }
                catch (Exception)
                {
                    Log.Information("Waiting for database to become available (attempt {Attempt}/{Max})", attempt, maxAttempts);
                    Thread.Sleep(2000);
                }
            }

            if (!connected)
            {
                Log.Error("Could not connect to the database after {MaxAttempts} attempts; skipping migrations", maxAttempts);
            }
            else
            {
                // Log target DB host/database (don't log full connection string)
                try
                {
                    var csb = new NpgsqlConnectionStringBuilder(connectionString);
                    Log.Information("Applying DB initialization to {Host}/{Database} in {Env}", csb.Host, csb.Database, app.Environment.EnvironmentName);
                }
                catch
                {
                    Log.Information("Applying DB initialization in {Env}", app.Environment.EnvironmentName);
                }

                // Retry migration/apply loop
                var migrationAttempts = 0;
                var migrationMax = builder.Configuration.GetValue<int?>("DB_MIGRATION_MAX_ATTEMPTS") ?? 6;
                var migrationDelay = builder.Configuration.GetValue<int?>("DB_MIGRATION_RETRY_DELAY_SECONDS") ?? 5;
                for (migrationAttempts = 1; migrationAttempts <= migrationMax; migrationAttempts++)
                {
                    try
                    {

                        // Attempt to apply EF Core migrations; do not fall back to custom SQL
                        dbContext.Database.Migrate();
                        Log.Information("GameService: Database migrations applied successfully");

                        break; // success
                    }
                    catch (Exception migrateEx)
                    {
                        Log.Warning(migrateEx, "GameService: Migration attempt {Attempt}/{Max} failed", migrationAttempts, migrationMax);
                        if (migrationAttempts == migrationMax)
                        {
                            Log.Error(migrateEx, "GameService: All migration attempts failed; migrations must be applied manually");
                            break;
                        }

                        Thread.Sleep(migrationDelay * 1000);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while applying database migrations. The application will continue to start, but database functionality may be degraded");
        }
    }

    // Configure the HTTP request pipeline.
    var swaggerEnabled = true;

    if (swaggerEnabled)
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    // OpenTelemetry Prometheus
    app.UseOpenTelemetryPrometheusScrapingEndpoint();

    // Health Checks
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready");

    app.UseFastEndpoints();

    app.MapGet("/", () => swaggerEnabled ? Results.Redirect("/swagger") : Results.Text("GameService OK"));

    Log.Information("Starting GameService host");
    app.Run();
}
catch (Exception ex)
{
    // Log fatal startup exceptions
    Log.Fatal(ex, "Host terminated unexpectedly during startup");
    throw; // rethrow to let the host fail if necessary
}
finally
{
    // Ensure any buffered logs are written out
    Log.CloseAndFlush();
}

public abstract partial class Program
{
}

[ExcludeFromCodeCoverage]
public abstract partial class Program
{
}