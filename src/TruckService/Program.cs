using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using TruckService.Data;
using TruckService.Services;
using Swashbuckle.AspNetCore.SwaggerGen;

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

var isTesting = builder.Environment.IsEnvironment("Testing");

if (isTesting)
{
    // in-memory sqlite for tests
    var conn = new SqliteConnection("DataSource=:memory:");
    conn.Open();
    builder.Services.AddDbContext<TruckDbContext>(o => o.UseSqlite(conn));
}
else
{
    // Choose DB provider by configuration: prefer Postgres when DefaultConnection present, otherwise fall back to SQLite file.
    var pgConn = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(pgConn))
    {
        builder.Services.AddDbContext<TruckDbContext>(o => o.UseNpgsql(pgConn));
    }
    else
    {
        builder.Services.AddDbContext<TruckDbContext>(o => o.UseSqlite("Data Source=truckservice.db"));
    }
}

builder.Services.AddScoped<ITruckRepository, EfTruckRepository>();
builder.Services.AddScoped<ILoadProvider, RandomLoadProvider>();
builder.Services.AddScoped<ITruckManager, TruckManager>(sp =>
{
    var repo = sp.GetRequiredService<ITruckRepository>();
    var db = sp.GetRequiredService<TruckDbContext>();
    var load = sp.GetRequiredService<ILoadProvider>();
    var logger = sp.GetRequiredService<ILogger<TruckManager>>();
    return new TruckManager(repo, db, load, logger);
});
builder.Services.AddScoped<IRouteWorker, RouteWorker>();
builder.Services.AddFastEndpoints()
    .SwaggerDocument();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var hc = builder.Services.AddHealthChecks();
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString))
    {
        hc.AddNpgSql(connectionString);
    }
}

// Messaging removed: MassTransit and RabbitMQ are no longer used

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? "TruckService")
        .AddEnvironmentVariableDetector())
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddJaegerExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

var app = builder.Build();

try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TruckDbContext>();
    db.Database.EnsureCreated();
    Log.Information("TruckService: ensured database exists");
}
catch (Exception ex)
{
    Log.Error(ex, "An error occurred while ensuring the TruckService database exists");
}

var swaggerEnabled = true; // same for all environments

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

app.UseFastEndpoints()
    .UseSwaggerGen();

app.MapGet("/", () => swaggerEnabled ? Results.Redirect("/swagger") : Results.Text("TruckService OK"));

Log.Information("Starting TruckService host");
app.Run();

public abstract partial class Program
{
}

[ExcludeFromCodeCoverage]
public abstract partial class Program
{
}