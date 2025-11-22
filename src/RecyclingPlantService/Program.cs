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
builder.Services.AddSwaggerGen(o =>
{
    // Use full type name (including nested type marker '+') as schema id to prevent duplicate short names like "Request"
    o.CustomSchemaIds(t => t.FullName?.Replace('+', '.') ?? t.Name);
});

// Database
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<RecyclingPlantDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("RecyclingPlantConnection")));
}
else
{
    // When running tests, allow choosing an in-memory provider via configuration for reliability in tests
    // Default to using the in-memory provider for the Testing environment unless explicitly disabled by configuration.
    var useInMemory = builder.Configuration.GetValue<bool?>("USE_INMEMORY") ?? true;
    if (useInMemory)
    {
        builder.Services.AddDbContext<RecyclingPlantDbContext>(options =>
            options.UseInMemoryDatabase("RecyclingPlantService_TestDb"));
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

    if (app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
            dbContext.Database.Migrate();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while applying database migrations. The application will continue to start, but database functionality may be degraded");
        }
    }

    // In testing environment ensure the database schema exists (useful for in-memory sqlite or in-memory provider)
    if (app.Environment.IsEnvironment("Testing"))
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RecyclingPlantDbContext>();
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