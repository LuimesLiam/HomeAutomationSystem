using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HomeApp.Expenses.Services;

internal static partial class ExpenseFetchImportParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ParsedExpenseFetchRequest Parse(string requestText)
    {
        if (string.IsNullOrWhiteSpace(requestText))
        {
            throw new InvalidOperationException("Paste a fetch request before executing the import.");
        }

        var trimmed = requestText.Trim();
        var match = FetchCallRegex().Match(trimmed);
        if (!match.Success)
        {
            throw new InvalidOperationException("The pasted text was not recognized as a supported fetch(...) request.");
        }

        var urlLiteral = match.Groups["url"].Value;
        var optionsJson = match.Groups["options"].Value;

        var url = JsonSerializer.Deserialize<string>(urlLiteral, JsonOptions);
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException("The fetch request did not include a valid URL.");
        }

        using var document = JsonDocument.Parse(optionsJson);
        var root = document.RootElement;

        var method = root.TryGetProperty("method", out var methodElement) && methodElement.ValueKind == JsonValueKind.String
            ? methodElement.GetString()
            : "GET";

        var body = root.TryGetProperty("body", out var bodyElement) && bodyElement.ValueKind == JsonValueKind.String
            ? bodyElement.GetString()
            : null;

        var referrer = root.TryGetProperty("referrer", out var referrerElement) && referrerElement.ValueKind == JsonValueKind.String
            ? referrerElement.GetString()
            : null;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("headers", out var headersElement) && headersElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in headersElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    headers[property.Name] = property.Value.GetString() ?? string.Empty;
                }
            }
        }

        return new ParsedExpenseFetchRequest(url.Trim(), (method ?? "GET").Trim().ToUpperInvariant(), headers, body, referrer);
    }

    [GeneratedRegex("""fetch\(\s*(?<url>"(?:\\.|[^"])*")\s*,\s*(?<options>\{[\s\S]*\})\s*\)\s*;?""", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex FetchCallRegex();
}

internal static class WealthsimpleActivityImportParser
{
    public const string SchemaKey = "wealthsimple-activity-v1";
    public const string SchemaDisplayName = "Wealthsimple Activity Feed";

    public static IReadOnlyList<ExpenseCsvNormalizedRow> ParseRows(string responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            throw new InvalidOperationException("The fetch request returned an empty response.");
        }

        using var document = JsonDocument.Parse(responseJson);
        if (document.RootElement.TryGetProperty("errors", out var errorsElement) && errorsElement.ValueKind == JsonValueKind.Array && errorsElement.GetArrayLength() > 0)
        {
            throw new InvalidOperationException($"The remote API returned an error: {errorsElement[0]}");
        }

        if (!document.RootElement.TryGetProperty("data", out var dataElement) ||
            !dataElement.TryGetProperty("activityFeedItems", out var activityFeedItemsElement) ||
            !activityFeedItemsElement.TryGetProperty("edges", out var edgesElement) ||
            edgesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("The response did not contain a Wealthsimple activity feed.");
        }

        var rows = new List<ExpenseCsvNormalizedRow>();

        foreach (var edgeElement in edgesElement.EnumerateArray())
        {
            if (!edgeElement.TryGetProperty("node", out var nodeElement) || nodeElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var amountSign = GetString(nodeElement, "amountSign");
            var amount = ParseDecimal(GetString(nodeElement, "amount"), "amount");

            if (!string.Equals(amountSign, "negative", StringComparison.OrdinalIgnoreCase) || amount <= 0m)
            {
                continue;
            }

            var occurredAtRaw = GetString(nodeElement, "occurredAt")
                ?? throw new InvalidOperationException("A Wealthsimple activity row was missing occurredAt.");
            var occurredAt = DateTimeOffset.Parse(occurredAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var expenseDate = DateOnly.FromDateTime(occurredAt.UtcDateTime);
            var currencyCode = NormalizeCurrency(GetString(nodeElement, "currency"));
            var merchantName =
                FirstNonEmpty(
                    GetString(nodeElement, "spendMerchant"),
                    GetString(nodeElement, "billPayCompanyName"),
                    GetString(nodeElement, "billPayPayeeNickname"),
                    GetString(nodeElement, "aftOriginatorName"),
                    GetString(nodeElement, "eTransferName"),
                    GetString(nodeElement, "counterPartyName"),
                    GetString(nodeElement, "institutionName"),
                    GetString(nodeElement, "p2pHandle"))
                ?? "Unknown";
            var type = GetString(nodeElement, "type");
            var subType = GetString(nodeElement, "subType");
            var transactionType = FirstNonEmpty(subType, type);
            var canonicalId = FirstNonEmpty(GetString(nodeElement, "canonicalId"), GetString(nodeElement, "externalCanonicalId"));
            var accountId = GetString(nodeElement, "accountId");
            var status = GetString(nodeElement, "status");
            var description = string.Join(
                " - ",
                new[] { merchantName, type, subType }
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Select(static value => value!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase));
            var notes = $"Imported from Wealthsimple activity feed. Account: {accountId ?? "unknown"}. Status: {status ?? "unknown"}. Occurred at: {occurredAt:O}. Canonical id: {canonicalId ?? "n/a"}.";
            var sourceReference = canonicalId
                ?? string.Join('|', new[]
                {
                    accountId ?? string.Empty,
                    occurredAt.ToString("O", CultureInfo.InvariantCulture),
                    amount.ToString("0.00", CultureInfo.InvariantCulture),
                    currencyCode
                });

            rows.Add(new ExpenseCsvNormalizedRow(
                rows.Count + 1,
                expenseDate,
                null,
                amount,
                currencyCode,
                merchantName,
                description,
                notes,
                type,
                null,
                transactionType,
                sourceReference,
                FlattenNode(nodeElement)));
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException("The fetch response did not contain any negative spend rows to import.");
        }

        return rows;
    }

    private static Dictionary<string, string> FlattenNode(JsonElement nodeElement)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in nodeElement.EnumerateObject())
        {
            values[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.Null => string.Empty,
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                _ => property.Value.GetRawText()
            };
        }

        return values;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static decimal ParseDecimal(string? value, string fieldName)
    {
        if (!decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidOperationException($"A Wealthsimple activity row contained an invalid {fieldName} value.");
        }

        return Math.Abs(result);
    }

    private static string NormalizeCurrency(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "USD" : value.Trim().ToUpperInvariant();
        return normalized.Length > 3 ? normalized[..3] : normalized;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim();
}

public sealed record ImportExpenseFetchRequest(
    string? ModelKey,
    string RequestText);

internal sealed record ParsedExpenseFetchRequest(
    string Url,
    string Method,
    IReadOnlyDictionary<string, string> Headers,
    string? Body,
    string? Referrer);
