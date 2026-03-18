using Domain;

namespace Application;

public interface IProcessingLogRepository
{
    Task AddAsync(ProcessingLog log, CancellationToken cancellationToken = default);
}
