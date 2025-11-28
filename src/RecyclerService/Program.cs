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
builder.Services.AddSwaggerGen(o => { o.CustomSchemaIds(t => t.FullName?.Replace('+', '.') ?? t.Name); });

// Database: prefer Postgres when configured, otherwise fallback to in-memory
var recyclerConn = builder.Configuration.GetConnectionString("RecyclerConnection");
if (!string.IsNullOrEmpty(recyclerConn))
{
    builder.Services.AddDbContext<RecyclerDbContext>(options => options.UseNpgsql(recyclerConn));
}
else
{
    builder.Services.AddDbContext<RecyclerDbContext>(options => options.UseInMemoryDatabase("RecyclerService_Db"));
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
        // Apply migrations if using Postgres, otherwise EnsureCreated for in-memory
        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
            if (dbContext.Database.ProviderName?.IndexOf("Npgsql", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                dbContext.Database.EnsureCreated();
                Log.Information("Database.EnsureCreated() executed at startup");
            }
            else
            {
                dbContext.Database.EnsureCreated();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while initializing the database");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while attempting database migrations during startup. The application will continue to start, but database functionality may be degraded");
    }

    // Ensure schema exists
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        dbContext.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred while ensuring database creation");
    }

    var swaggerEnabled = true;

    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseHttpsRedirection();

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