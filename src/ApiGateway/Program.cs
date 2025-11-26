using AspNetCoreRateLimit;
using FastEndpoints;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddFastEndpoints(options => { options.Assemblies = [typeof(Program).Assembly]; });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthorization();

// Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

// YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? "ApiGateway")
        .AddEnvironmentVariableDetector())
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddJaegerExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddPrometheusExporter());

// Health Checks
builder.Services.AddHealthChecks()
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!)
    .AddRabbitMQ(async sp =>
    {
        var factory = new ConnectionFactory
        {
            HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost",
            UserName = builder.Configuration["RabbitMQ:Username"] ?? "guest",
            Password = builder.Configuration["RabbitMQ:Password"] ?? "guest"
        };
        return await factory.CreateConnectionAsync();
    });

try
{
    var app = builder.Build();

    // Configure the HTTP request pipeline.
    var swaggerEnabled = app.Environment.IsDevelopment();

    if (swaggerEnabled)
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    // Rate Limiting
    app.UseIpRateLimiting();


    // OpenTelemetry Prometheus
    app.MapPrometheusScrapingEndpoint();

    // Health Checks
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready");

    // YARP
    app.MapReverseProxy();

    app.UseFastEndpoints();

    app.MapGet("/", () => "ApiGateway OK");

    Log.Information("Starting ApiGateway host");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program
{
}