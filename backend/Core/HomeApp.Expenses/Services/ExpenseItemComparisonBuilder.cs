using HomeApp.Expenses.Data;

namespace HomeApp.Expenses.Services;

public static class ExpenseItemComparisonBuilder
{
    public static IReadOnlyList<ExpenseItemComparisonRecord> Build(IEnumerable<ExpenseLineItem> items)
    {
        return items
            .GroupBy(item => item.NormalizedName)
            .Select(group =>
            {
                var purchases = group
                    .Select(MapPurchase)
                    .OrderBy(item => item.ComparablePrice ?? decimal.MaxValue)
                    .ThenByDescending(item => item.ExpenseDate)
                    .ToList();
                return new ExpenseItemComparisonRecord(
                    group.OrderByDescending(item => item.ExpenseEntry.ExpenseDate).First().CanonicalName,
                    group.Key,
                    purchases.Count,
                    purchases.Min(item => item.ComparablePrice),
                    purchases.FirstOrDefault(item => item.ComparablePrice.HasValue)?.MerchantName,
                    purchases);
            })
            .OrderBy(item => item.Name)
            .Take(100)
            .ToList();
    }

    private static ExpenseItemPurchaseRecord MapPurchase(ExpenseLineItem item)
    {
        var comparablePrice = ExpenseLineItem.ResolveUnitPrice(item.UnitPrice, item.Quantity, item.TotalPrice);
        return new ExpenseItemPurchaseRecord(
            item.Id,
            item.ExpenseEntryId,
            item.Name,
            item.CanonicalName,
            item.Quantity,
            item.Unit,
            item.UnitPrice,
            item.TotalPrice,
            comparablePrice,
            item.ExpenseEntry.CurrencyCode,
            item.ExpenseEntry.MerchantName,
            item.ExpenseEntry.Location,
            item.ExpenseEntry.ExpenseDate);
    }
}
