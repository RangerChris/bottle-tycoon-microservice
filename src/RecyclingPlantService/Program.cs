using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RecyclingPlantService.Consumers;
using RecyclingPlantService.Data;
using RecyclingPlantService.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddFastEndpoints();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => { o.CustomSchemaIds(t => t.FullName?.Replace('+', '.') ?? t.Name); });

// Database: prefer Postgres connection if configured, otherwise use InMemory for local/dev/test reliability
var rpConn = builder.Configuration.GetConnectionString("RecyclingPlantConnection");
if (!string.IsNullOrEmpty(rpConn))
{
    builder.Services.AddDbContext<RecyclingPlantDbContext>(options =>
        options.UseNpgsql(rpConn));
}
else
{
    builder.Services.AddDbContext<RecyclingPlantDbContext>(options =>
        options.UseInMemoryDatabase("RecyclingPlantService_Db"));
}

// MassTransit
var enableMessaging = builder.Configuration.GetValue<bool?>("ENABLE_MESSAGING") ?? true;
if (enableMessaging)
{
    builder.Services.AddMassTransit(x =>
    {
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
        .AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? "RecyclingPlantService")
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
    var conn = builder.Configuration.GetConnectionString("RecyclingPlantConnection");
    if (!string.IsNullOrEmpty(conn))
    {
        healthChecks.AddNpgSql(conn);
    }
}

// Business Services
builder.Services.AddScoped<IRecyclingPlantService, RecyclingPlantService.Services.RecyclingPlantService>();

try
{
    var app = builder.Build();

    // Apply migrations if using Postgres, otherwise EnsureCreated for in-memory
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
        if (dbContext.Database.ProviderName?.IndexOf("Npgsql", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            dbContext.Database.EnsureCreated();
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

    var swaggerEnabled = true;

    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseHttpsRedirection();

    app.UseOpenTelemetryPrometheusScrapingEndpoint();

    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready");

    app.UseFastEndpoints();

    app.MapGet("/", () => swaggerEnabled ? Results.Redirect("/swagger") : Results.Text("RecyclingPlantService OK"));

    Log.Information("Starting RecyclingPlantService host");
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