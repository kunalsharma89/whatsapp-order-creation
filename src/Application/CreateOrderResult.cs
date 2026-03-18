namespace Application;

public class CreateOrderResult
{
    public bool Success { get; set; }
    public Guid? OrderId { get; set; }
    public bool IsDuplicate { get; set; }
    public string? ErrorMessage { get; set; }

    public static CreateOrderResult Succeeded(Guid orderId, bool isDuplicate = false) =>
        new() { Success = true, OrderId = orderId, IsDuplicate = isDuplicate };

    public static CreateOrderResult Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}
