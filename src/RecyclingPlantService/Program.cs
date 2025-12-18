﻿using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RecyclingPlantService.Data;
using RecyclingPlantService.Services;
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
        var announcedUrls = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : configuredUrls ?? "http://+:80";
        Log.Information("RecyclingPlantService ready at {Urls}", announcedUrls);
    });

    app.MapGet("/", () => swaggerEnabled ? Results.Redirect("/") : Results.Text("RecyclingPlantService OK"));

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