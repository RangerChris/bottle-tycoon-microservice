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

var environmentName = builder.Environment.EnvironmentName;
builder.Configuration.SetBasePath(builder.Environment.ContentRootPath);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddFastEndpoints();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthorization();

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("GameStateConnection")));

builder.Services.AddStackExchangeRedisCache(options => { options.Configuration = builder.Configuration.GetConnectionString("Redis"); });

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
            Log.Information("Using RabbitMQHost {RabbitMQHost}", builder.Configuration["RabbitMQ:Host"]);
            cfg.Host(builder.Configuration["RabbitMQ:Host"], h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? throw new ConfigurationException("RabbitMQ:Username is not configured"));
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? throw new ConfigurationException("RabbitMQ:Password is not configured"));
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
var dbCs = builder.Configuration.GetConnectionString("GameStateConnection");
healthChecks.AddNpgSql(dbCs!);

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
    // Log which connection string key was used
    Log.Information("Using database connection string key GameStateConnection");

    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        var dbConnection = dbContext.Database.GetDbConnection();
        var connectionString = dbConnection.ConnectionString;
        try
        {
            var csb = new NpgsqlConnectionStringBuilder(connectionString);
            Log.Information("Applying DB initialization to {Host}/{Database} in {Env}", csb.Host, csb.Database, app.Environment.EnvironmentName);
        }
        catch
        {
            Log.Information("Applying DB initialization in {Env}", app.Environment.EnvironmentName);
        }

        dbContext.Database.EnsureCreated();
        Log.Information("GameService: Database ensured to exist");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while applying database migrations. The application will continue to start, but database functionality may be degraded");
    }

    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseHttpsRedirection();

    // OpenTelemetry Prometheus
    app.UseOpenTelemetryPrometheusScrapingEndpoint();

    // Health Checks
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready");

    app.UseFastEndpoints();

    Log.Information("Starting GameService host");
    app.Run();
}
catch (Exception ex)
{
    // Log fatal startup exceptions
    Log.Fatal(ex, "Host terminated unexpectedly during startup");
    throw; // rethrow to let the host fail if necessary
}

[ExcludeFromCodeCoverage]
public abstract partial class Program
{
}