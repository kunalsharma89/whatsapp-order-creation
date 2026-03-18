using Application.Dtos;

namespace Application;

public interface IOrderValidator
{
    (bool IsValid, string? ErrorMessage) Validate(List<OrderItemDto> items);
}
