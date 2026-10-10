using System.Security.Cryptography;
using System.Text.Json;
using HomeApp.Expenses.Controllers;
using HomeApp.Expenses.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace HomeApp.Expenses.Tests;

public sealed class ReceiptReviewTests
{
    private readonly IDataProtectionProvider _provider = new EphemeralDataProtectionProvider();

    private string Ticket(DateTimeOffset createdAt, string? imageHash = null) =>
        _provider.CreateProtector("HomeApp.Receipts.Review.v1").Protect(JsonSerializer.Serialize(
            new ReceiptReviewTicket(new ReceiptExpenseExtraction(), "gpt-6-luna",
                imageHash ?? Convert.ToHexString(SHA256.HashData(Array.Empty<byte>())), createdAt)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tampered-review")]
    public async Task SaveRejectsMissingOrTamperedReview(string? token)
    {
        var controller = new ExpensesController(null!, _provider);
        var result = await controller.SaveReviewed(new AnalyzeExpenseFormDto { ReviewToken = token }, default);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SaveRejectsExpiredReview()
    {
        var controller = new ExpensesController(null!, _provider);
        var result = await controller.SaveReviewed(new AnalyzeExpenseFormDto
        {
            ReviewToken = Ticket(DateTimeOffset.UtcNow.AddHours(-25)), TotalAmount = 10m
        }, default);
        Assert.Contains("expired", Assert.IsType<BadRequestObjectResult>(result.Result).Value!.ToString());
    }

    [Fact]
    public async Task SaveRejectsReplacedImage()
    {
        var controller = new ExpensesController(null!, _provider);
        var result = await controller.SaveReviewed(new AnalyzeExpenseFormDto
        {
            ReviewToken = Ticket(DateTimeOffset.UtcNow, "another-image"), TotalAmount = 10m
        }, default);
        Assert.Contains("image changed", Assert.IsType<BadRequestObjectResult>(result.Result).Value!.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SaveRequiresPositiveReviewedTotal(int? total)
    {
        var controller = new ExpensesController(null!, _provider);
        var result = await controller.SaveReviewed(new AnalyzeExpenseFormDto
        {
            ReviewToken = Ticket(DateTimeOffset.UtcNow), TotalAmount = total
        }, default);
        Assert.Contains("greater than zero", Assert.IsType<BadRequestObjectResult>(result.Result).Value!.ToString());
    }
}
