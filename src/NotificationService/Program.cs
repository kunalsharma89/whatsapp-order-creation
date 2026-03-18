using Infrastructure;
using Microsoft.Extensions.Hosting;
using NotificationService;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<OrderProcessedConsumerService>();

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/notification-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Services.AddSerilog();

var host = builder.Build();
Log.Information("Notification Service starting");
await host.RunAsync();
