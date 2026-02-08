﻿using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RecyclerService.Data;
using RecyclerService.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var environmentName = builder.Environment.EnvironmentName;
builder.Configuration.SetBasePath(builder.Environment.ContentRootPath);
builder.Configuration.AddJsonFile("appsettings.json", false, true);
builder.Configuration.AddJsonFile($"appsettings.{environmentName}.json", true, true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

if (builder.Environment.IsEnvironment("Testing"))
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateLogger();
}
else
{
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
}

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

// Messaging removed: services call each other directly via HTTP

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
        .AddMeter("RecyclerService")
        .AddMeter("Microsoft.AspNetCore.Hosting")
        .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
        .AddPrometheusExporter());

// Metrics initialization - create meter and register it
var meterName = "RecyclerService";
var meter = new Meter(meterName, "1.0");
builder.Services.AddSingleton(meter);

// create and register the bottles_processed counter on the shared meter so DI can inject it
var bottlesProcessedCounter = meter.CreateCounter<long>("bottles_processed", unit: "bottles", description: "Number of bottles processed by type");
builder.Services.AddSingleton<Counter<long>>(bottlesProcessedCounter);

builder.Services.AddSingleton<IRecyclerTelemetryStore, RecyclerTelemetryStore>();
builder.Services.AddSingleton<RecyclerMetrics>();

// Health Checks
var healthChecks = builder.Services.AddHealthChecks();
if (!string.IsNullOrEmpty(recyclerConn))
{
    healthChecks.AddNpgSql(recyclerConn);
}

builder.Services.AddScoped<IRecyclerService, RecyclerService.Services.RecyclerService>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});
builder.Services.AddHttpClient();

// Add HttpClient for inter-service communication
builder.Services.AddHttpClient("GameService", client => { client.BaseAddress = new Uri(builder.Configuration["Services:GameService"] ?? "http://gameservice:80"); });

try
{
    var app = builder.Build();

    app.Services.GetRequiredService<RecyclerMetrics>();

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<RecyclerDbContext>();
        dbContext.Database.EnsureCreated();
    }

    var swaggerEnabled = true;

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        c.RoutePrefix = string.Empty;
    });

    var configuredUrls = builder.Configuration["ASPNETCORE_URLS"];
    var enableHttpsRedirection = builder.Configuration.GetValue("EnableHttpsRedirection", true);
    var httpsUrlConfigured = configuredUrls?.IndexOf("https", StringComparison.OrdinalIgnoreCase) >= 0;
    var useHttpsRedirection = enableHttpsRedirection && httpsUrlConfigured;

    if (useHttpsRedirection)
    {
        app.UseHttpsRedirection();
    }
    else
    {
        Log.Information("HTTPS redirection skipped because only HTTP endpoints are configured");
    }

    app.UseOpenTelemetryPrometheusScrapingEndpoint();


    app.UseCors("AllowAll");

    app.UseFastEndpoints();

    app.MapGet("/v1/swagger.json", () => Results.Redirect("/swagger/v1/swagger.json"));

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var announcedUrls = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : builder.Configuration["ASPNETCORE_URLS"] ?? "http://+:80";
        Log.Information("RecyclerService ready at {Urls}", announcedUrls);
    });

    if (!swaggerEnabled)
    {
        app.MapGet("/", () => Results.Text("RecyclerService OK"));
    }

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