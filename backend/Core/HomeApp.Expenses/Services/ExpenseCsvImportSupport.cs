using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;

namespace HomeApp.Expenses.Services;

internal static class ExpenseCsvImportSchemas
{
    private static readonly IReadOnlyList<ExpenseCsvImportSchema> Schemas =
    [
        new(
            "credit-card-basic-v1",
            "Credit Card Export",
            ["transaction_date", "post_date", "type", "details", "amount", "currency"],
            record =>
            {
                var transactionDate = ParseDate(record, "transaction_date");
                var postDate = ParseOptionalDate(record, "post_date");
                var details = GetValue(record, "details");
                var type = GetValue(record, "type");
                var currency = GetValue(record, "currency");
                var amount = ParseDecimal(record, "amount");
                var normalizedCurrency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
                var descriptionParts = new[] { type, details }
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Select(static value => value!.Trim());
                var sourceReference = string.Join('|', new[]
                {
                    transactionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    postDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                    type?.Trim() ?? string.Empty,
                    details?.Trim() ?? string.Empty,
                    amount.ToString("0.00", CultureInfo.InvariantCulture),
                    normalizedCurrency
                });

                return new ExpenseCsvNormalizedRow(
                    record.RowNumber,
                    transactionDate,
                    postDate,
                    amount,
                    normalizedCurrency,
                    details,
                    string.Join(" - ", descriptionParts),
                    $"Imported from credit card CSV. Type: {type?.Trim() ?? "Unknown"}. Post date: {(postDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "N/A")}.",
                    "Card",
                    null,
                    type,
                    sourceReference,
                    record.Values);
            })
    ];

    public static ExpenseCsvImportParseResult Parse(string csvContent)
    {
        using var reader = new StringReader(csvContent);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim,
            IgnoreBlankLines = true
        });

        if (!csv.Read() || !csv.ReadHeader())
        {
            throw new InvalidOperationException("The CSV file is empty or missing a header row.");
        }

        var header = csv.HeaderRecord ?? [];
        var schema = DetectSchema(header)
            ?? throw new InvalidOperationException(
                $"Unsupported CSV schema. Received columns: {string.Join(", ", header)}");

        var rows = new List<ExpenseCsvNormalizedRow>();
        while (csv.Read())
        {
            var values = header.ToDictionary(
                column => column,
                column => csv.GetField(column) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

            if (values.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(schema.Map(new ExpenseCsvRawRecord((int)csv.Parser.Row, values)));
        }

        return new ExpenseCsvImportParseResult(schema.Key, schema.DisplayName, rows);
    }

    private static ExpenseCsvImportSchema? DetectSchema(IEnumerable<string> header)
    {
        var set = new HashSet<string>(header.Select(NormalizeHeader), StringComparer.OrdinalIgnoreCase);
        return Schemas.FirstOrDefault(schema => schema.RequiredHeaders.All(set.Contains));
    }

    private static string NormalizeHeader(string value) => value.Trim().ToLowerInvariant();

    private static string? GetValue(ExpenseCsvRawRecord record, string key) =>
        record.Values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static DateOnly ParseDate(ExpenseCsvRawRecord record, string key)
    {
        var value = GetValue(record, key)
            ?? throw new InvalidOperationException($"Row {record.RowNumber} is missing required column '{key}'.");

        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            return result;
        }

        throw new InvalidOperationException($"Row {record.RowNumber} contains an invalid date for '{key}': {value}");
    }

    private static DateOnly? ParseOptionalDate(ExpenseCsvRawRecord record, string key)
    {
        var value = GetValue(record, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            return result;
        }

        throw new InvalidOperationException($"Row {record.RowNumber} contains an invalid date for '{key}': {value}");
    }

    private static decimal ParseDecimal(ExpenseCsvRawRecord record, string key)
    {
        var value = GetValue(record, key)
            ?? throw new InvalidOperationException($"Row {record.RowNumber} is missing required column '{key}'.");

        if (decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        throw new InvalidOperationException($"Row {record.RowNumber} contains an invalid amount for '{key}': {value}");
    }
}

internal sealed record ExpenseCsvImportSchema(
    string Key,
    string DisplayName,
    IReadOnlyCollection<string> RequiredHeaders,
    Func<ExpenseCsvRawRecord, ExpenseCsvNormalizedRow> Map);

internal sealed record ExpenseCsvRawRecord(
    int RowNumber,
    IReadOnlyDictionary<string, string> Values);

public sealed record ExpenseCsvImportParseResult(
    string SchemaKey,
    string SchemaDisplayName,
    IReadOnlyList<ExpenseCsvNormalizedRow> Rows);

public sealed record ExpenseCsvNormalizedRow(
    int RowNumber,
    DateOnly ExpenseDate,
    DateOnly? PostDate,
    decimal TotalAmount,
    string CurrencyCode,
    string? MerchantName,
    string? Description,
    string? Notes,
    string? PaymentMethod,
    string? Location,
    string? TransactionType,
    string SourceReference,
    IReadOnlyDictionary<string, string> RawColumns);

internal sealed class ExpenseCsvClassificationBatchResponse
{
    public List<ExpenseCsvClassificationItem> Items { get; set; } = [];
}

internal sealed class ExpenseCsvClassificationItem
{
    public int RowNumber { get; set; }
    public string? ClassificationGroupKey { get; set; }
    public decimal? Confidence { get; set; }
    public string? Reason { get; set; }
}

public sealed record ImportExpenseCsvRequest(
    string? ModelKey,
    string FileName,
    string CsvContent);

public sealed record ExpenseCsvImportResult(
    string SchemaKey,
    string SchemaDisplayName,
    int TotalRows,
    int AddedCount,
    int DuplicateCount,
    int ReviewCount,
    int FailedCount,
    IReadOnlyList<ExpenseCsvImportReportItem> Items);

public sealed record ExpenseCsvImportReportItem(
    int RowNumber,
    string Status,
    string? Message,
    string? MerchantName,
    decimal? Amount,
    string? CurrencyCode,
    DateOnly? ExpenseDate,
    string? ExpenseGroupName,
    string? SchemaKey,
    ExpenseRecord? Expense,
    IReadOnlyDictionary<string, string>? RawColumns,
    ExpenseImportApprovalPayload? ApprovalPayload = null);

internal static class ExpenseCsvImportReportStatuses
{
    public const string Added = "added";
    public const string Duplicate = "duplicate";
    public const string Review = "review";
    public const string Failed = "failed";
}

internal static class ExpenseCsvImportJson
{
    public static string SerializeRawColumns(IReadOnlyDictionary<string, string> rawColumns) =>
        JsonSerializer.Serialize(rawColumns);
}

public sealed record ExpenseImportApprovalPayload(
    int RowNumber,
    string SourceType,
    string ImportFileName,
    string SchemaKey,
    DateOnly ExpenseDate,
    decimal TotalAmount,
    string CurrencyCode,
    string? MerchantName,
    string? Description,
    string? Notes,
    string? PaymentMethod,
    string? Location,
    string? SourceReference,
    int? ExpenseGroupId,
    string? ExpenseGroupName,
    string? ModelKeyUsed,
    decimal? AnalysisConfidence,
    IReadOnlyDictionary<string, string>? RawColumns);

public sealed record ApproveExpenseImportRowsRequest(
    string SchemaKey,
    string SchemaDisplayName,
    IReadOnlyList<ExpenseImportApprovalPayload> Rows);
