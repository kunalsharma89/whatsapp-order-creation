using Application;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class OrderRepository : IOrderRepository
{
    private readonly OrderDbContext _db;

    public OrderRepository(OrderDbContext db)
    {
        _db = db;
    }

    public async Task<Order?> GetByMessageIdAsync(string messageId, CancellationToken cancellationToken = default)
    {
        return await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.ExternalMessageId == messageId, cancellationToken);
    }

    public async Task<Order> AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);
        return order;
    }

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        _db.Orders.Update(order);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
