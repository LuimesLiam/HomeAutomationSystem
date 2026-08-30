using System.Text;

namespace HomeApp.Expenses.Data;

public sealed class ExpenseLineItem
{
    public int Id { get; set; }
    public int ExpenseEntryId { get; set; }
    public ExpenseEntry ExpenseEntry { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string CanonicalName { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }

    public static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(name.Length);
        var previousWasSpace = false;
        foreach (var character in name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
            }
            else if (!previousWasSpace && builder.Length > 0)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        var words = builder.ToString().Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(SingularizeWord));
    }

    private static string SingularizeWord(string word)
    {
        if (word.Length > 4 && word.EndsWith("ies", StringComparison.Ordinal))
        {
            return $"{word[..^3]}y";
        }

        if (word.Length > 3 && word.EndsWith('s') &&
            !word.EndsWith("ss", StringComparison.Ordinal) &&
            !word.EndsWith("us", StringComparison.Ordinal) &&
            !word.EndsWith("is", StringComparison.Ordinal))
        {
            return word[..^1];
        }

        return word;
    }

    public static decimal? ResolveUnitPrice(decimal? unitPrice, decimal? quantity, decimal totalPrice)
    {
        if (unitPrice is > 0)
        {
            return decimal.Round(unitPrice.Value, 4);
        }

        return quantity is > 0
            ? decimal.Round(totalPrice / quantity.Value, 4)
            : totalPrice > 0 ? totalPrice : null;
    }

    public static decimal ResolveTotalPrice(decimal? totalPrice, decimal? unitPrice, decimal? quantity)
    {
        if (totalPrice is > 0)
        {
            return decimal.Round(totalPrice.Value, 2);
        }

        return unitPrice is > 0
            ? decimal.Round(unitPrice.Value * (quantity is > 0 ? quantity.Value : 1m), 2)
            : 0m;
    }
}
