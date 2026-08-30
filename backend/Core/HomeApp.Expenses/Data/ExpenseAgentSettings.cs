namespace HomeApp.Expenses.Data;

public sealed class ExpenseAgentSettings
{
    public int Id { get; set; }
    public string? ModelKey { get; set; }
    public string DefaultCurrencyCode { get; set; } = "USD";
    public string? ExtractionPrompt { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
