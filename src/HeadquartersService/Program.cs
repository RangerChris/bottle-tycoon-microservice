using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using FastEndpoints.Swagger;
using HeadquartersService.Services;
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

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddFastEndpoints()
    .SwaggerDocument();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator([
    new TraceContextPropagator()
]));

var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? builder.Configuration["OTEL_SERVICE_NAME"] ?? "HeadquartersService";
Log.Information("Configuring OpenTelemetry with service name: {ServiceName}", serviceName);


builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName, serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options =>
        {
            options.Endpoint = new Uri("http://jaeger:4318/v1/traces");
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
            Log.Information("OTLP exporter configured with endpoint: {Endpoint}", options.Endpoint);
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddHealthChecks();
builder.Services.AddSingleton<IDispatchQueue, DispatchQueue>();
builder.Services.AddSingleton<IFleetService, FleetService>();
builder.Services.AddSingleton<IHeadquartersService, HeadquartersService.Services.HeadquartersService>();
builder.Services.AddHostedService<DispatchProcessor>();
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


    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        // When serving the UI at the app root (RoutePrefix = empty), the UI's
        // default relative doc path becomes /v1/swagger.json which doesn't
        // match the generated endpoint (/swagger/v1/swagger.json). Explicitly
        // set the Swagger endpoint to the generated JSON path.
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        c.RoutePrefix = string.Empty;
    });

    app.UseHttpsRedirection();

    app.UseOpenTelemetryPrometheusScrapingEndpoint();


    app.UseCors("AllowAll");

    app.UseFastEndpoints();

    // Some Swagger UI bundles request /v1/swagger.json when served at root.
    // Provide a small redirect so both /v1/swagger.json and /swagger/v1/swagger.json work.
    app.MapGet("/v1/swagger.json", () => Results.Redirect("/swagger/v1/swagger.json"));

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var announcedUrls = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : builder.Configuration["ASPNETCORE_URLS"] ?? "http://+:80";
        Log.Information("HeadquartersService ready at {Urls}", announcedUrls);
    });

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

[ExcludeFromCodeCoverage]
public abstract partial class Program
{
}