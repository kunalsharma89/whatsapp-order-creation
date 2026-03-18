using Application.Dtos;

namespace Application;

public interface IOrderParser
{
    /// <summary>
    /// Parses raw message text (e.g. "Order: Pizza x2, Coke x1") into structured order items.
    /// </summary>
    ParseOrderResult Parse(string rawText);
}
