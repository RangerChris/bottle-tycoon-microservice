using AspNetCoreRateLimit;
using FastEndpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using Serilog;

namespace ApiGateway.Tests;

public class TestStartup
{
    private readonly IConfiguration _configuration;

    public TestStartup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // Configure Serilog for testing
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        services.AddFastEndpoints();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddAuthorization();

        // JWT Authentication (simplified for testing)
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = false
                };
            });

        // Rate Limiting
        services.AddMemoryCache();
        services.Configure<IpRateLimitOptions>(_configuration.GetSection("IpRateLimiting"));
        services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
        services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
        services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

        // YARP Reverse Proxy
        services.AddReverseProxy()
            .LoadFromConfig(_configuration.GetSection("ReverseProxy"));

        // OpenTelemetry (minimal for testing)
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService("ApiGateway.Test"))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation());

        // Health Checks
        services.AddHealthChecks()
            .AddRedis(_configuration.GetConnectionString("Redis")!)
            .AddRabbitMQ(async sp =>
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri($"amqp://{_configuration["RabbitMQ:Username"]}:{_configuration["RabbitMQ:Password"]}@{_configuration["RabbitMQ:Host"]}:5672/")
                };
                return await factory.CreateConnectionAsync();
            });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment() || env.IsEnvironment("Testing"))
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        // Rate Limiting
        app.UseIpRateLimiting();

        // Authentication & Authorization
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseRouting();

        // YARP
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapReverseProxy();
            endpoints.MapGet("/", () => "ApiGateway OK");
        });

        app.UseFastEndpoints();
    }
}