using System.Text.RegularExpressions;
using Application.Dtos;

namespace Application;

public class OrderParser : IOrderParser
{
    // Matches "ItemName xN" or "ItemName x N" (e.g. "Pizza x2", "Coke x 1")
    private static readonly Regex ItemPattern = new(
        @"([^,]+?)\s*x\s*(\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ParseOrderResult Parse(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return ParseOrderResult.Failed("Message is empty.");

        var text = rawText.Trim();
        const string orderPrefix = "order:";
        if (!text.StartsWith(orderPrefix, StringComparison.OrdinalIgnoreCase))
            return ParseOrderResult.Failed("Message must start with 'Order:'.");

        var content = text[orderPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(content))
            return ParseOrderResult.Failed("No order items after 'Order:'.");

        var items = new List<OrderItemDto>();
        var matches = ItemPattern.Matches(content);

        if (matches.Count == 0)
            return ParseOrderResult.Failed("Could not parse any items. Expected format: ItemName xQuantity (e.g. Pizza x2, Coke x1).");

        foreach (Match match in matches)
        {
            if (match.Success && match.Groups.Count >= 3)
            {
                var name = match.Groups[1].Value.Trim();
                if (int.TryParse(match.Groups[2].Value, out var qty) && qty > 0 && !string.IsNullOrWhiteSpace(name))
                    items.Add(new OrderItemDto { Name = name, Quantity = qty });
            }
        }

        if (items.Count == 0)
            return ParseOrderResult.Failed("No valid items parsed.");

        return ParseOrderResult.Succeeded(items);
    }
}
