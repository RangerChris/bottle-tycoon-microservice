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

// Database
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<GameDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
}
else
{
    // In Testing environment, use in-memory Sqlite for standalone container startup
    builder.Services.AddDbContext<GameDbContext>(options =>
        options.UseSqlite("DataSource=:memory:"));
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
if (!builder.Environment.IsEnvironment("Testing"))
{
    healthChecks
        .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!)
        .AddRedis(builder.Configuration.GetConnectionString("Redis")!);
}

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
    // By default apply migrations on startup for all environments except "Testing".
    // This can be disabled by setting configuration key APPLY_MIGRATIONS=false.
    if (!app.Environment.IsEnvironment("Testing") && builder.Configuration.GetValue<bool?>("APPLY_MIGRATIONS") != false)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            // Wait for the database to accept connections (helps when starting with Docker Compose)
            var dbConnection = dbContext.Database.GetDbConnection();
            var connectionString = dbConnection.ConnectionString;
            var maxAttempts = 30;
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

                // If running in Development (used by Docker Compose here), ensure the DB is created
                // This avoids relying on compiled migrations being discovered inside the container image
                if (app.Environment.IsDevelopment())
                {
                    var created = dbContext.Database.EnsureCreated();
                    Log.Information(created ? "Database created via EnsureCreated" : "Database already existed (EnsureCreated)");
                }
                else
                {
                    var compiledMigrations = dbContext.Database.GetMigrations();
                    if (compiledMigrations != null && compiledMigrations.Any())
                    {
                        dbContext.Database.Migrate();
                        Log.Information("Database migrations applied successfully");
                    }
                    else
                    {
                        var created = dbContext.Database.EnsureCreated();
                        Log.Information(created ? "Database created via EnsureCreated" : "Database already existed (EnsureCreated)");
                    }
                }

                // Double-check that the Players table exists; if not, apply EF generated create script as a fallback
                try
                {
                    using var checkConn = new NpgsqlConnection(connectionString);
                    checkConn.Open();
                    // Check for either lowercase or quoted-cased table name in pg_tables
                    using var cmd = new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname='public' AND tablename IN ('players','Players') LIMIT 1", checkConn);
                    var existsName = cmd.ExecuteScalar() as string;
                    if (string.IsNullOrEmpty(existsName))
                    {
                        Log.Warning("Players table not found; creating schema via generated SQL script");
                        var script = dbContext.Database.GenerateCreateScript();
                        dbContext.Database.ExecuteSqlRaw(script);
                        Log.Information("Database schema created via generated SQL script");

                        // As a final fallback (robust for Docker startup ordering), ensure key tables exist using explicit SQL
                        var createPlayersSql = @"CREATE TABLE IF NOT EXISTS public.players (
    ""Id"" uuid PRIMARY KEY,
    ""Credits"" numeric(18,2) NOT NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NOT NULL
);";

                        var createPurchasesSql = @"CREATE TABLE IF NOT EXISTS public.purchases (
    ""Id"" uuid PRIMARY KEY,
    ""PlayerId"" uuid NOT NULL,
    ""ItemType"" text NOT NULL,
    ""Amount"" numeric(18,2) NOT NULL,
    ""PurchasedAt"" timestamp with time zone NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_Purchases_PlayerId ON public.purchases (""PlayerId"");
ALTER TABLE IF EXISTS public.purchases ADD CONSTRAINT IF NOT EXISTS FK_Purchases_Players_PlayerId FOREIGN KEY (""PlayerId"") REFERENCES public.players(""Id"") ON DELETE CASCADE;";

                        var createUpgradesSql = @"CREATE TABLE IF NOT EXISTS public.upgrades (
    ""Id"" uuid PRIMARY KEY,
    ""PlayerId"" uuid NOT NULL,
    ""ItemType"" text NOT NULL,
    ""ItemId"" integer NOT NULL,
    ""NewLevel"" integer NOT NULL,
    ""Cost"" numeric(18,2) NOT NULL,
    ""UpgradedAt"" timestamp with time zone NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_Upgrades_PlayerId ON public.upgrades (""PlayerId"");
ALTER TABLE IF EXISTS public.upgrades ADD CONSTRAINT IF NOT EXISTS FK_Upgrades_Players_PlayerId FOREIGN KEY (""PlayerId"") REFERENCES public.players(""Id"") ON DELETE CASCADE;";

                        using var createCmd = new NpgsqlCommand(createPlayersSql, checkConn);
                        createCmd.ExecuteNonQuery();
                        using var createCmd2 = new NpgsqlCommand(createPurchasesSql, checkConn);
                        createCmd2.ExecuteNonQuery();
                        using var createCmd3 = new NpgsqlCommand(createUpgradesSql, checkConn);
                        createCmd3.ExecuteNonQuery();

                        Log.Information("Database schema ensured via explicit CREATE TABLE statements");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to verify/create Players table via fallback script");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while applying database migrations. The application will continue to start, but database functionality may be degraded");
        }
    }

    // Configure the HTTP request pipeline.
    var swaggerEnabled = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing");

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