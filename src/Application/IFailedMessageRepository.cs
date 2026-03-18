using Domain;

namespace Application;

public interface IFailedMessageRepository
{
    Task AddAsync(FailedMessage failedMessage, CancellationToken cancellationToken = default);
}
