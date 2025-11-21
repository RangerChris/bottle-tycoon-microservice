using FastEndpoints;
using FastEndpoints.Swagger;
using HeadquartersService.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddFastEndpoints()
    .SwaggerDocument();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? "HeadquartersService")
        .AddEnvironmentVariableDetector())
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddJaegerExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddHealthChecks();
builder.Services.AddSingleton<IDispatchQueue, DispatchQueue>();
builder.Services.AddSingleton<IFleetService, FleetService>();
builder.Services.AddHostedService<DispatchProcessor>();

try
{
    var app = builder.Build();

    var swaggerEnabled = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing");

    if (swaggerEnabled)
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

    app.MapGet("/", () => swaggerEnabled ? Results.Redirect("/swagger") : Results.Text("HeadquartersService OK"));

    Log.Information("Starting HeadquartersService host");
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