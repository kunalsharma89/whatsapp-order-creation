using Domain;

namespace Application;

public interface IOrderRepository
{
    Task<Order?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default);
    Task<Order> AddAsync(Order order, CancellationToken cancellationToken = default);
    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);
}
