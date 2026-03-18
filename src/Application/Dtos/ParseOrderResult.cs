namespace Application.Dtos;

public class ParseOrderResult
{
    public bool Success { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public static ParseOrderResult Succeeded(List<OrderItemDto> items) =>
        new() { Success = true, Items = items };

    public static ParseOrderResult Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}
