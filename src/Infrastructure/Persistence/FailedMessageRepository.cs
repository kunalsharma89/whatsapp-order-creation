using Application;
using Domain;

namespace Infrastructure.Persistence;

public class FailedMessageRepository : IFailedMessageRepository
{
    private readonly OrderDbContext _db;

    public FailedMessageRepository(OrderDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(FailedMessage failedMessage, CancellationToken cancellationToken = default)
    {
        _db.FailedMessages.Add(failedMessage);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
