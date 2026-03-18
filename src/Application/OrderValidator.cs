using Application.Dtos;

namespace Application;

public class OrderValidator : IOrderValidator
{
    public (bool IsValid, string? ErrorMessage) Validate(List<OrderItemDto> items)
    {
        if (items == null || items.Count == 0)
            return (false, "Order must contain at least one item.");

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Name))
                return (false, "Item name cannot be empty.");
            if (item.Quantity <= 0)
                return (false, $"Quantity for '{item.Name}' must be greater than zero.");
        }

        return (true, null);
    }
}
