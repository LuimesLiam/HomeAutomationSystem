namespace HomeApp.Expenses.Data;

public sealed class ExpenseEntry
{
    public int Id { get; set; }
    public int? ExpenseGroupId { get; set; }
    public ExpenseGroup? ExpenseGroup { get; set; }
    public string SourceType { get; set; } = ExpenseSourceTypes.Manual;
    public DateOnly ExpenseDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public decimal TotalAmount { get; set; }
    public decimal? SubtotalAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? TipAmount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public string? MerchantName { get; set; }
    public string? Location { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string? ReceiptFileName { get; set; }
    public byte[]? ReceiptImageBytes { get; set; }
    public string? ReceiptImageContentType { get; set; }
    public string? ModelKeyUsed { get; set; }
    public decimal? AnalysisConfidence { get; set; }
    public string? ReceiptText { get; set; }
    public string? RawExtractionJson { get; set; }
    public string? DuplicateFingerprint { get; set; }
    public bool IsWorkExpense { get; set; }
    public bool IsReimbursable { get; set; }
    public string? ImportSchemaKey { get; set; }
    public string? ImportSourceReference { get; set; }
    public string? ImportFileName { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<ExpenseLineItem> LineItems { get; set; } = new List<ExpenseLineItem>();
}

public static class ExpenseSourceTypes
{
    public const string Manual = "manual";
    public const string Receipt = "receipt";
    public const string CsvImport = "csv-import";
    public const string FetchImport = "fetch-import";
}
