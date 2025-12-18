﻿using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using FastEndpoints;
using GameService.Data;
using GameService.Services;
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

// Messaging and Redis removed: services should call each other directly via HTTP.

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


// Business Services
builder.Services.AddScoped<IPlayerService, PlayerService>();

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

var configuredUrls = builder.Configuration["ASPNETCORE_URLS"];
var enableHttpsRedirection = builder.Configuration.GetValue("EnableHttpsRedirection", true);
var httpsUrlConfigured = configuredUrls?.IndexOf("https", StringComparison.OrdinalIgnoreCase) >= 0;
var useHttpsRedirection = enableHttpsRedirection && httpsUrlConfigured;

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
        var announcedUrls = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : configuredUrls ?? "http://+:80";
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