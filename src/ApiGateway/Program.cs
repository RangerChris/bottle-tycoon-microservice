using AspNetCoreRateLimit;
using FastEndpoints;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var environmentName = builder.Environment.EnvironmentName;
builder.Configuration.SetBasePath(builder.Environment.ContentRootPath);
builder.Configuration.AddJsonFile("appsettings.json", false, true);
builder.Configuration.AddJsonFile($"appsettings.{environmentName}.json", true, true);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

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

// Register IHttpClientFactory for outgoing calls
builder.Services.AddHttpClient();

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
var healthChecks = builder.Services.AddHealthChecks();

var enableExternalHealthChecks = builder.Configuration.GetValue("HealthChecks:ExternalDependencies", true);
if (enableExternalHealthChecks)
{
    healthChecks
        .AddRedis(builder.Configuration.GetConnectionString("Redis")!)
        .AddRabbitMQ(async _ =>
        {
            var factory = new ConnectionFactory
            {
                HostName = builder.Configuration["RabbitMQ:Host"] ?? "localhost",
                UserName = builder.Configuration["RabbitMQ:Username"] ?? "guest",
                Password = builder.Configuration["RabbitMQ:Password"] ?? "guest"
            };
            return await factory.CreateConnectionAsync();
        });
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy
            .WithOrigins(
                "http://localhost:3000",
                "http://frontend:3000",
                "http://apigateway:80",
                "http://gameservice:80",
                "http://recyclerservice:80",
                "http://truckservice:80",
                "http://headquartersservice:80",
                "http://recyclingplantservice:80")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

try
{
    var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseHttpsRedirection();
    app.UseIpRateLimiting();
    app.UseCors("Frontend");

    // OpenTelemetry Prometheus
    app.MapPrometheusScrapingEndpoint();

    // Health Checks (public endpoints)
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready");

    // YARP
    app.MapReverseProxy();

    app.UseFastEndpoints();

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