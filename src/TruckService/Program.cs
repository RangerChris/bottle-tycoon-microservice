using System.Diagnostics.CodeAnalysis;
using FastEndpoints;
using FastEndpoints.Swagger;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using TruckService.Data;
using TruckService.Services;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

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
    var pg = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(pg))
    {
        builder.Services.AddDbContext<TruckDbContext>(o => o.UseNpgsql(pg));
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
if (isTesting)
{
    hc.AddCheck("self", () => HealthCheckResult.Healthy());
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString))
    {
        hc.AddNpgSql(connectionString);
        hc.AddRabbitMQ();
    }
}

// MassTransit
var enableMessaging = builder.Configuration.GetValue<bool?>("ENABLE_MESSAGING") ?? true;
if (enableMessaging)
{
    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

            cfg.ConfigureEndpoints(context);
        });
    });
}
else
{
    Log.Information("Messaging disabled via ENABLE_MESSAGING=false; MassTransit will not be started");
}

var app = builder.Build();

// ensure DB created/migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TruckDbContext>();

    // If using the Npgsql provider (Postgres) apply migrations so schema is created/updated.
    // Otherwise (sqlite/in-memory) use EnsureCreated for quick local/test setup.
    if (db.Database.ProviderName?.IndexOf("Npgsql", StringComparison.OrdinalIgnoreCase) >= 0)
    {
        db.Database.Migrate();
    }
    else
    {
        db.Database.EnsureCreated();
    }

    // seed known truck used by tests
    if (isTesting && !db.Trucks.Any(t => t.Id == Guid.Parse("11111111-1111-1111-1111-111111111111")))
    {
        db.Trucks.Add(new TruckEntity
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            LicensePlate = "TRK-001",
            Model = "Model-A",
            IsActive = true
        });
        db.SaveChanges();
    }
}

var swaggerEnabled = app.Environment.IsDevelopment() || isTesting;

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

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