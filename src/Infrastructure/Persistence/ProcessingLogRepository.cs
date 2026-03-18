using Application;
using Domain;

namespace Infrastructure.Persistence;

public class ProcessingLogRepository : IProcessingLogRepository
{
    private readonly OrderDbContext _db;

    public ProcessingLogRepository(OrderDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(ProcessingLog log, CancellationToken cancellationToken = default)
    {
        _db.ProcessingLogs.Add(log);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
