using Application;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderProcessingService;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<CircuitBreakerOptions>(
    builder.Configuration.GetSection(CircuitBreakerOptions.SectionName));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<OrderConsumerHostedService>();

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "OrderProcessingService")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {MessageId} {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/order-processing-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Services.AddSerilog();

var host = builder.Build();
Log.Information("Order Processing Service starting");
await host.RunAsync();
