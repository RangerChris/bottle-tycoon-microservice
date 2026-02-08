using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using TruckService.Data;
using TruckService.Services;

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

builder.Services.AddScoped<ITruckRepository, EfTruckRepository>();

if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("UseRandomLoadProvider"))
{
    builder.Services.AddScoped<ILoadProvider, RandomLoadProvider>();
}
else
{
    builder.Services.AddScoped<ILoadProvider, RecyclerServiceLoadProvider>();
}

builder.Services.AddScoped<ITruckManager, TruckManager>(sp =>
{
    var repo = sp.GetRequiredService<ITruckRepository>();
    var db = sp.GetRequiredService<TruckDbContext>();
    var load = sp.GetRequiredService<ILoadProvider>();
    var logger = sp.GetRequiredService<ILogger<TruckManager>>();
    return new TruckManager(repo, db, load, logger);
});
builder.Services.AddScoped<IRouteWorker, RouteWorker>();
builder.Services.AddScoped<ITruckService, TruckService.Services.TruckService>();
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

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TruckDbContext>();
    dbContext.Database.EnsureCreated();
}

var swaggerEnabled = true; // same for all environments

var configuredUrls = builder.Configuration["ASPNETCORE_URLS"];
var enableHttpsRedirection = builder.Configuration.GetValue("EnableHttpsRedirection", true);
var httpsUrlConfigured = configuredUrls?.IndexOf("https", StringComparison.OrdinalIgnoreCase) >= 0;
var useHttpsRedirection = enableHttpsRedirection && httpsUrlConfigured;

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        c.RoutePrefix = string.Empty;
    });
}

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

app.UseFastEndpoints()
    .UseSwaggerGen();

app.MapGet("/v1/swagger.json", () => Results.Redirect("/swagger/v1/swagger.json"));

app.Lifetime.ApplicationStarted.Register(() =>
{
    var announcedUrls = app.Urls.Count > 0 ? string.Join(", ", app.Urls) : builder.Configuration["ASPNETCORE_URLS"] ?? "http://+:80";
    Log.Information("TruckService ready at {Urls}", announcedUrls);
});

app.MapGet("/", () => swaggerEnabled ? Results.Content("", "text/html") : Results.Text("TruckService OK"));

Log.Information("Starting TruckService host");
app.Run();

public abstract partial class Program
{
}

[ExcludeFromCodeCoverage]
public abstract partial class Program
{
}