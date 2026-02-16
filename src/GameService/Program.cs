﻿﻿using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using FastEndpoints;
using GameService.Data;
using GameService.Services;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var environmentName = builder.Environment.EnvironmentName;
builder.Configuration.SetBasePath(builder.Environment.ContentRootPath);
builder.Configuration.AddJsonFile("appsettings.json", false, true);
builder.Configuration.AddJsonFile($"appsettings.{environmentName}.json", true, true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

try
{
    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .CreateLogger();
}
catch
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateLogger();
}

builder.Host.UseSerilog();

builder.Services.AddFastEndpoints();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("GameStateConnection")));

// OpenTelemetry SDK configuration
Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([
    new TraceContextPropagator()
]));

var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? builder.Configuration["OTEL_SERVICE_NAME"] ?? "GameService";
Log.Information("Configuring OpenTelemetry with service name: {ServiceName}", serviceName);

var meterName = "GameService";

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName, serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://jaeger:4318/v1/traces");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
            Log.Information("OTLP exporter configured with endpoint: {Endpoint}", options.Endpoint);
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter(meterName)
        .AddPrometheusExporter());

// Health Checks
var healthChecks = builder.Services.AddHealthChecks();
var dbCs = builder.Configuration.GetConnectionString("GameStateConnection");
if (!string.IsNullOrEmpty(dbCs))
{
    healthChecks.AddNpgSql(dbCs);
}

// Business Services
builder.Services.AddScoped<IPlayerService, PlayerService>();

// Telemetry Services
builder.Services.AddSingleton<IGameTelemetryStore, GameTelemetryStore>();
builder.Services.AddSingleton(sp =>
{
    var telemetryStore = sp.GetRequiredService<IGameTelemetryStore>();
    return new GameMetrics(telemetryStore);
});

// Add HttpClient for inter-service communication
builder.Services.AddHttpClient("GameService", client =>
{
    client.BaseAddress = new Uri("http://localhost"); // Local GameService port
});
builder.Services.AddHttpClient("RecyclerService", client => { client.BaseAddress = new Uri(builder.Configuration["Services:RecyclerService"] ?? "http://recyclerservice:80"); });
builder.Services.AddHttpClient("TruckService", client => { client.BaseAddress = new Uri(builder.Configuration["Services:TruckService"] ?? "http://truckservice:80"); });
builder.Services.AddHttpClient("HeadquartersService", client => { client.BaseAddress = new Uri(builder.Configuration["Services:HeadquartersService"] ?? "http://headquartersservice:80"); });
builder.Services.AddHttpClient("RecyclingPlantService", client => { client.BaseAddress = new Uri(builder.Configuration["Services:RecyclingPlantService"] ?? "http://recyclingplantservice:80"); });

// Add JSON options to avoid serialization cycles
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var enableHttpsRedirection = builder.Configuration.GetValue("EnableHttpsRedirection", true);
var aspnetcoreUrls = builder.Configuration["ASPNETCORE_URLS"] ?? "http://+:80";
var httpsUrlConfigured = aspnetcoreUrls.IndexOf("https", StringComparison.OrdinalIgnoreCase) >= 0;
var useHttpsRedirection = enableHttpsRedirection && httpsUrlConfigured;

try
{
    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        dbContext.Database.EnsureCreated();

        // Initialize GameMetrics to ensure ObservableGauge is created
        var gameMetrics = app.Services.GetRequiredService<GameMetrics>();
        Log.Information("GameMetrics initialized for telemetry tracking");
    }

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        c.RoutePrefix = string.Empty;
    });

    if (useHttpsRedirection)
    {
        app.UseHttpsRedirection();
    }
    else
    {
        Log.Information("HTTPS redirection skipped because only HTTP endpoints are configured");
    }

    app.UseCors("AllowAll");

    // OpenTelemetry Prometheus
    app.UseOpenTelemetryPrometheusScrapingEndpoint();

    app.UseFastEndpoints();

    // Provide compatibility for UI bundles that request /v1/swagger.json when UI is served at root
    app.MapGet("/v1/swagger.json", () => Results.Redirect("/swagger/v1/swagger.json"));

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var announcedUrls = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : "http://+:80";
        Log.Information("GameService ready at {Urls}", announcedUrls);
    });

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