using Application;
using Infrastructure.Messaging;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMQOptions>(configuration.GetSection(RabbitMQOptions.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5433;Database=WhatsAppOrders;Username=postgres;Password=postgres";
        services.AddDbContext<OrderDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IFailedMessageRepository, FailedMessageRepository>();
        services.AddScoped<IProcessingLogRepository, ProcessingLogRepository>();
        services.AddSingleton<RabbitMQPublisher>();
        services.AddSingleton<IMessagePublisher>(sp => sp.GetRequiredService<RabbitMQPublisher>());
        services.AddSingleton<IOrderFailurePublisher>(sp => sp.GetRequiredService<RabbitMQPublisher>());

        return services;
    }
}
