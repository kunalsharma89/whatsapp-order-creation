using Application.Dtos;
using Domain;
using Shared.Contracts;

namespace Application;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProcessingLogRepository _processingLogRepository;
    private readonly IOrderParser _orderParser;
    private readonly IOrderValidator _orderValidator;

    public OrderService(
        IOrderRepository orderRepository,
        IProcessingLogRepository processingLogRepository,
        IOrderParser orderParser,
        IOrderValidator orderValidator)
    {
        _orderRepository = orderRepository;
        _processingLogRepository = processingLogRepository;
        _orderParser = orderParser;
        _orderValidator = orderValidator;
    }

    public async Task<CreateOrderResult> CreateOrderAsync(OrderReceivedEvent receivedEvent, CancellationToken cancellationToken = default)
    {
        // Idempotency: if we already processed this message, return existing order
        var existing = await _orderRepository.GetByMessageIdAsync(receivedEvent.MessageId, cancellationToken);
        if (existing != null)
            return CreateOrderResult.Succeeded(existing.Id, isDuplicate: true);

        var parseResult = _orderParser.Parse(receivedEvent.RawText);
        if (!parseResult.Success)
            return CreateOrderResult.Failed(parseResult.ErrorMessage ?? "Parse failed.");

        var (isValid, validationError) = _orderValidator.Validate(parseResult.Items);
        if (!isValid)
            return CreateOrderResult.Failed(validationError ?? "Validation failed.");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            ExternalMessageId = receivedEvent.MessageId,
            UserId = receivedEvent.UserId,
            PhoneNumber = receivedEvent.PhoneNumber,
            Status = OrderStatus.Processed,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow,
            Items = parseResult.Items.Select(dto => new OrderItem
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Quantity = dto.Quantity
            }).ToList()
        };

        foreach (var item in order.Items)
            item.OrderId = order.Id;

        await _orderRepository.AddAsync(order, cancellationToken);

        await _processingLogRepository.AddAsync(new ProcessingLog
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            MessageId = receivedEvent.MessageId,
            Action = "OrderCreated",
            Payload = $"Order {order.Id} created from message {receivedEvent.MessageId}",
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        return CreateOrderResult.Succeeded(order.Id);
    }
}
