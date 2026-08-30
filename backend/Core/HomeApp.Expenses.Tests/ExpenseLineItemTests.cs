using HomeApp.Expenses.Data;
using HomeApp.Expenses.Services;
using Xunit;

namespace HomeApp.Expenses.Tests;

public sealed class ExpenseLineItemTests
{
    [Theory]
    [InlineData("  Organic CUCUMBERS (3 pk) ", "organic cucumber 3 pk")]
    [InlineData("Milk--2%", "milk 2")]
    [InlineData("Dish Soap / Lemon", "dish soap lemon")]
    [InlineData("APPLES", "apple")]
    [InlineData("berries", "berry")]
    public void NormalizeName_MakesReceiptLabelsSearchable(string input, string expected)
    {
        Assert.Equal(expected, ExpenseLineItem.NormalizeName(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeName_ReturnsEmpty_ForMissingNames(string? input)
    {
        Assert.Equal(string.Empty, ExpenseLineItem.NormalizeName(input));
    }

    [Fact]
    public void ResolveUnitPrice_PrefersReceiptUnitPrice()
    {
        Assert.Equal(1.25m, ExpenseLineItem.ResolveUnitPrice(1.25m, 3m, 3.75m));
    }

    [Fact]
    public void ResolveUnitPrice_DerivesPriceFromQuantityAndLineTotal()
    {
        Assert.Equal(1.3333m, ExpenseLineItem.ResolveUnitPrice(null, 3m, 4m));
    }

    [Fact]
    public void ResolveUnitPrice_UsesLineTotalForSingleUnquantifiedItem()
    {
        Assert.Equal(2.49m, ExpenseLineItem.ResolveUnitPrice(null, null, 2.49m));
    }

    [Fact]
    public void ResolveTotalPrice_DerivesMissingLineTotalFromQuantity()
    {
        Assert.Equal(7.50m, ExpenseLineItem.ResolveTotalPrice(null, 2.50m, 3m));
    }

    [Fact]
    public void ComparisonBuilder_SortsPurchasesByUnitPriceAndFindsCheapestStore()
    {
        var firstExpense = new ExpenseEntry
        {
            Id = 10,
            MerchantName = "Corner Market",
            CurrencyCode = "USD",
            ExpenseDate = new DateOnly(2026, 8, 20)
        };
        var secondExpense = new ExpenseEntry
        {
            Id = 11,
            MerchantName = "Fresh Foods",
            CurrencyCode = "USD",
            ExpenseDate = new DateOnly(2026, 8, 21)
        };
        var items = new[]
        {
            new ExpenseLineItem { Id = 1, ExpenseEntryId = 10, ExpenseEntry = firstExpense, Name = "CUCUMBERS", CanonicalName = "Cucumber", NormalizedName = "cucumber", Quantity = 2, TotalPrice = 3.00m },
            new ExpenseLineItem { Id = 2, ExpenseEntryId = 11, ExpenseEntry = secondExpense, Name = "Cucumber", CanonicalName = "Cucumber", NormalizedName = "cucumber", Quantity = 3, TotalPrice = 3.60m }
        };

        var result = Assert.Single(ExpenseItemComparisonBuilder.Build(items));

        Assert.Equal(1.20m, result.LowestPrice);
        Assert.Equal("Fresh Foods", result.LowestPriceMerchant);
        Assert.Equal("Fresh Foods", result.Purchases[0].MerchantName);
        Assert.Equal(1.50m, result.Purchases[1].ComparablePrice);
    }
}
