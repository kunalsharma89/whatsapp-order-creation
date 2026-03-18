using Shared.Contracts;

namespace Application;

public interface IOrderFailurePublisher
{
    Task PublishToRetryAsync(OrderReceivedEvent evt, int retryCount, CancellationToken cancellationToken = default);
    Task PublishToDlqAsync(OrderReceivedEvent evt, string error, CancellationToken cancellationToken = default);
}
