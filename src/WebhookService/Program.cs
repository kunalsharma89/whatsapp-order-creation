using Application;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using WebhookService.Middleware;
using WebhookService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) =>
{
    lc.ReadFrom.Configuration(ctx.Configuration)
      .Enrich.FromLogContext()
      .Enrich.WithProperty("Application", "WebhookService")
      .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {CorrelationId} {Message:lj}{NewLine}{Exception}")
      .WriteTo.File("logs/webhook-.txt", rollingInterval: RollingInterval.Day);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<IWebhookMessageHandler, WebhookMessageHandler>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5433;Database=WhatsAppOrders;Username=postgres;Password=postgres";
var rabbitHost = builder.Configuration["RabbitMQ:HostName"] ?? "localhost";
var rabbitPort = int.TryParse(builder.Configuration["RabbitMQ:Port"], out var p) ? p : 5672;
var rabbitUser = builder.Configuration["RabbitMQ:UserName"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
var rabbitVhost = (builder.Configuration["RabbitMQ:VirtualHost"] ?? "/").TrimStart('/');
var rabbitConnectionString = $"amqp://{Uri.EscapeDataString(rabbitUser)}:{Uri.EscapeDataString(rabbitPass)}@{rabbitHost}:{rabbitPort}/{rabbitVhost}";

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres", failureStatus: HealthStatus.Unhealthy, tags: new[] { "ready", "db" })
    .AddRabbitMQ(rabbitConnectionString, name: "rabbitmq", failureStatus: HealthStatus.Unhealthy, tags: new[] { "ready", "messaging" });

var app = builder.Build();

// Apply pending EF Core migrations at startup (no need to run dotnet ef database update)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Database migration failed. Ensure PostgreSQL is running and connection string is correct.");
    }
}

app.UseCorrelationId();
app.UseSerilogRequestLogging();
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            })
        });
        await context.Response.WriteAsync(result);
    }
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapGet("/health/live", () => Results.Json(new { status = "Healthy" }));
app.MapControllers();
app.Run();
