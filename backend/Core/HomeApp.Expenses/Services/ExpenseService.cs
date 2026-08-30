using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using HomeApp.AI;
using HomeApp.AI.Data;
using HomeApp.AI.Services;
using HomeApp.Expenses.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAIChatClient = OpenAI.Chat.ChatClient;

namespace HomeApp.Expenses.Services;

public sealed class ExpenseService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> MerchantNoiseTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "THE",
        "AND",
        "STORE",
        "STORES",
        "SHOP",
        "SHOPS",
        "MARKET",
        "MARKETS",
        "SUPERMARKET",
        "SUPERMARKETS",
        "SUPERCENTER",
        "CENTER",
        "CENTRE",
        "LOCATION",
        "CANADA",
        "CA",
        "INC",
        "LTD",
        "LIMITED",
        "CORP",
        "CORPORATION",
        "COMPANY",
        "CO"
    };

    private readonly HomeAppExpensesDbContext _dbContext;
    private readonly IHomeAppAgentModelCatalog _modelCatalog;
    private readonly AiSettingsService _aiSettingsService;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;

    public ExpenseService(
        HomeAppExpensesDbContext dbContext,
        IHomeAppAgentModelCatalog modelCatalog,
        AiSettingsService aiSettingsService,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _modelCatalog = modelCatalog;
        _aiSettingsService = aiSettingsService;
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
    }

    public async Task EnsureSeededAsync(CancellationToken ct = default)
    {
        if (!await _dbContext.ExpenseAgentSettings.AnyAsync(ct))
        {
            HomeAppAgentModelConfiguration? defaultModel = null;
            try
            {
                defaultModel = await _modelCatalog.GetResolvedModelAsync(null, ct);
            }
            catch (InvalidOperationException)
            {
            }

            _dbContext.ExpenseAgentSettings.Add(new ExpenseAgentSettings
            {
                ModelKey = defaultModel?.ModelKey,
                DefaultCurrencyCode = "USD",
                ExtractionPrompt = string.Empty
            });
        }

        if (!await _dbContext.ExpenseGroups.AnyAsync(ct))
        {
            _dbContext.ExpenseGroups.AddRange(
            [
                new ExpenseGroup { Key = "food", Name = "Food", Color = "#16a34a", DisplayOrder = 0 },
                new ExpenseGroup { Key = "phones", Name = "Phones", Color = "#2563eb", DisplayOrder = 1 },
                new ExpenseGroup { Key = "internet", Name = "Internet", Color = "#0f766e", DisplayOrder = 2 },
                new ExpenseGroup { Key = "home", Name = "Home", Color = "#475569", DisplayOrder = 3 },
                new ExpenseGroup { Key = "gas", Name = "gas", Color = "#f59e0b", DisplayOrder = 4 },
                new ExpenseGroup { Key = "other", Name = "other", Color = "#64748b", DisplayOrder = 5 },
                new ExpenseGroup { Key = "utilities", Name = "Utilities", Color = "#7c3aed", DisplayOrder = 6 },
                new ExpenseGroup { Key = "subs", Name = "Subs", Color = "#db2777", DisplayOrder = 7 },
                new ExpenseGroup { Key = "car-insurance", Name = "Car insurance", Color = "#dc2626", DisplayOrder = 8 },
                new ExpenseGroup { Key = "personal-liam", Name = "Personal - Liam", Color = "#0891b2", DisplayOrder = 9 },
                new ExpenseGroup { Key = "personal-sophia", Name = "Personal - Sophia", Color = "#9333ea", DisplayOrder = 10 },
                new ExpenseGroup { Key = "fun", Name = "fun", Color = "#ea580c", DisplayOrder = 11 },
                new ExpenseGroup { Key = "dept", Name = "Dept", Color = "#1f2937", DisplayOrder = 12 }
            ]);
        }

        var existingLineItems = await _dbContext.ExpenseLineItems.ToListAsync(ct);
        foreach (var item in existingLineItems)
        {
            if (string.IsNullOrWhiteSpace(item.CanonicalName))
            {
                item.CanonicalName = item.Name;
            }

            item.NormalizedName = ExpenseLineItem.NormalizeName(item.CanonicalName);
        }

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<ExpenseDashboardSnapshot> GetDashboardAsync(ExpenseQuery query, CancellationToken ct = default)
    {
        var groups = await _dbContext.ExpenseGroups
            .AsNoTracking()
            .OrderBy(group => group.DisplayOrder)
            .ThenBy(group => group.Name)
            .ToListAsync(ct);

        var settings = await GetSettingsEntityAsync(ct);
        var aiSettings = await _aiSettingsService.GetSnapshotAsync(ct);
        var enabledProvidersById = aiSettings.Providers
            .Where(provider => provider.IsEnabled)
            .ToDictionary(provider => provider.Id);
        var models = aiSettings.Models
            .Where(model => model.IsEnabled && enabledProvidersById.ContainsKey(model.ProviderId))
            .OrderByDescending(model => model.IsDefault)
            .ThenBy(model => model.Name)
            .Select(model =>
            {
                var provider = enabledProvidersById[model.ProviderId];
                return new ExpenseModelOption(
                    model.Key,
                    model.Name,
                    model.ModelId,
                    provider.Name,
                    model.IsDefault);
            })
            .ToList();

        var expenseQuery = _dbContext.ExpenseEntries
            .AsNoTracking()
            .Include(expense => expense.ExpenseGroup)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            expenseQuery = expenseQuery.Where(expense =>
                (expense.MerchantName ?? string.Empty).ToLower().Contains(search) ||
                (expense.Location ?? string.Empty).ToLower().Contains(search) ||
                (expense.Description ?? string.Empty).ToLower().Contains(search) ||
                (expense.Notes ?? string.Empty).ToLower().Contains(search) ||
                (expense.ReceiptText ?? string.Empty).ToLower().Contains(search));
        }

        if (query.ExpenseGroupId.HasValue)
        {
            expenseQuery = expenseQuery.Where(expense => expense.ExpenseGroupId == query.ExpenseGroupId.Value);
        }

        if (query.DateFrom.HasValue)
        {
            expenseQuery = expenseQuery.Where(expense => expense.ExpenseDate >= query.DateFrom.Value);
        }

        if (query.DateTo.HasValue)
        {
            expenseQuery = expenseQuery.Where(expense => expense.ExpenseDate <= query.DateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.SourceType))
        {
            var sourceType = query.SourceType.Trim().ToLowerInvariant();
            expenseQuery = expenseQuery.Where(expense => expense.SourceType.ToLower() == sourceType);
        }

        var expenses = await expenseQuery
            .OrderByDescending(expense => expense.ExpenseDate)
            .ThenByDescending(expense => expense.CreatedAtUtc)
            .ToListAsync(ct);

        var summary = BuildSummary(expenses, groups);

        return new ExpenseDashboardSnapshot(
            expenses.Select(MapExpense).ToList(),
            groups.Select(MapGroup).ToList(),
            new ExpenseSettingsSnapshot(settings.ModelKey, settings.DefaultCurrencyCode, settings.ExtractionPrompt),
            models,
            summary);
    }

    public async Task<ExpenseDashboardSnapshot> SaveSettingsAsync(ExpenseSettingsUpdate update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var settings = await GetSettingsEntityAsync(ct);
        settings.ModelKey = string.IsNullOrWhiteSpace(update.ModelKey) ? null : update.ModelKey.Trim();
        settings.DefaultCurrencyCode = NormalizeCurrency(update.DefaultCurrencyCode);
        settings.ExtractionPrompt = string.IsNullOrWhiteSpace(update.ExtractionPrompt) ? null : update.ExtractionPrompt.Trim();
        settings.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var existingGroups = await _dbContext.ExpenseGroups
            .OrderBy(group => group.Id)
            .ToListAsync(ct);
        var existingById = existingGroups.ToDictionary(group => group.Id);
        var retainedIds = new HashSet<int>();

        for (var index = 0; index < update.Groups.Count; index++)
        {
            var group = update.Groups[index];
            if (group.Id > 0 && existingById.TryGetValue(group.Id, out var existing))
            {
                existing.Key = NormalizeKey(group.Key, group.Name);
                existing.Name = group.Name.Trim();
                existing.Color = NormalizeColor(group.Color);
                existing.IsEnabled = group.IsEnabled;
                existing.DisplayOrder = index;
                retainedIds.Add(existing.Id);
            }
            else
            {
                var created = new ExpenseGroup
                {
                    Key = NormalizeKey(group.Key, group.Name),
                    Name = group.Name.Trim(),
                    Color = NormalizeColor(group.Color),
                    IsEnabled = group.IsEnabled,
                    DisplayOrder = index
                };

                _dbContext.ExpenseGroups.Add(created);
            }
        }

        foreach (var obsolete in existingGroups.Where(group => !retainedIds.Contains(group.Id)))
        {
            obsolete.IsEnabled = false;
        }

        await _dbContext.SaveChangesAsync(ct);
        return await GetDashboardAsync(new ExpenseQuery(), ct);
    }

    public async Task<ExpenseRecord> CreateManualExpenseAsync(CreateManualExpenseRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = await GetSettingsEntityAsync(ct);
        var expense = await CreateExpenseEntryAsync(
            new PersistExpenseRequest(
                request.ExpenseGroupId,
                ExpenseSourceTypes.Manual,
                request.ExpenseDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                request.TotalAmount,
                request.SubtotalAmount,
                request.TaxAmount,
                request.TipAmount,
                NormalizeCurrency(request.CurrencyCode ?? settings.DefaultCurrencyCode),
                request.MerchantName,
                request.Location,
                request.PaymentMethod,
                request.Description,
                request.Notes,
                null,
                null,
                null,
                null,
                null,
                null,
                null),
            ct);

        return MapExpense(expense);
    }

    public async Task<ExpenseDetailRecord?> GetExpenseAsync(int expenseId, CancellationToken ct = default)
    {
        var expense = await _dbContext.ExpenseEntries
            .AsNoTracking()
            .Include(item => item.ExpenseGroup)
            .Include(item => item.LineItems)
            .FirstOrDefaultAsync(item => item.Id == expenseId, ct);

        return expense == null ? null : MapExpenseDetail(expense);
    }

    public async Task<ExpenseItemReclassificationRecord?> ReclassifyItemAsync(
        int itemId,
        string canonicalName,
        bool applyToMatching,
        CancellationToken ct = default)
    {
        var cleanedName = NullIfWhiteSpace(canonicalName);
        if (cleanedName == null)
        {
            throw new InvalidOperationException("A canonical item name is required.");
        }

        var item = await _dbContext.ExpenseLineItems.FirstOrDefaultAsync(entry => entry.Id == itemId, ct);
        if (item == null)
        {
            return null;
        }

        var previousKey = item.NormalizedName;
        var newKey = ExpenseLineItem.NormalizeName(cleanedName);
        var matchingItems = applyToMatching
            ? await _dbContext.ExpenseLineItems.Where(entry => entry.NormalizedName == previousKey).ToListAsync(ct)
            : [item];

        foreach (var matchingItem in matchingItems)
        {
            matchingItem.CanonicalName = cleanedName;
            matchingItem.NormalizedName = newKey;
        }

        await _dbContext.SaveChangesAsync(ct);
        return new ExpenseItemReclassificationRecord(cleanedName, newKey, matchingItems.Count);
    }

    public async Task<IReadOnlyList<ExpenseItemComparisonRecord>> SearchItemsAsync(
        string? search,
        CancellationToken ct = default)
    {
        var normalizedSearch = ExpenseLineItem.NormalizeName(search);
        var query = _dbContext.ExpenseLineItems
            .AsNoTracking()
            .Include(item => item.ExpenseEntry)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var singularSearch = normalizedSearch.Length > 3 && normalizedSearch.EndsWith('s')
                ? normalizedSearch[..^1]
                : normalizedSearch;
            query = singularSearch == normalizedSearch
                ? query.Where(item => item.NormalizedName.Contains(normalizedSearch))
                : query.Where(item => item.NormalizedName.Contains(normalizedSearch) || item.NormalizedName.Contains(singularSearch));
        }

        var items = await query
            .OrderByDescending(item => item.ExpenseEntry.ExpenseDate)
            .ThenBy(item => item.Name)
            .Take(1000)
            .ToListAsync(ct);

        return ExpenseItemComparisonBuilder.Build(items);
    }

    public async Task<ExpenseRecord?> UpdateExpenseAsync(int expenseId, UpdateExpenseRequest request, CancellationToken ct = default)
    {
        var expense = await _dbContext.ExpenseEntries
            .Include(item => item.ExpenseGroup)
            .FirstOrDefaultAsync(item => item.Id == expenseId, ct);

        if (expense == null)
        {
            return null;
        }

        expense.ExpenseGroupId = request.ExpenseGroupId;
        expense.ExpenseDate = request.ExpenseDate;
        expense.TotalAmount = request.TotalAmount;
        expense.SubtotalAmount = request.SubtotalAmount;
        expense.TaxAmount = request.TaxAmount;
        expense.TipAmount = request.TipAmount;
        expense.CurrencyCode = NormalizeCurrency(request.CurrencyCode);
        expense.MerchantName = NullIfWhiteSpace(request.MerchantName);
        expense.Location = NullIfWhiteSpace(request.Location);
        expense.PaymentMethod = NullIfWhiteSpace(request.PaymentMethod);
        expense.Description = NullIfWhiteSpace(request.Description);
        expense.Notes = NullIfWhiteSpace(request.Notes);
        expense.ReceiptText = NullIfWhiteSpace(request.ReceiptText);
        expense.IsWorkExpense = request.IsWorkExpense;
        expense.IsReimbursable = request.IsReimbursable;
        expense.DuplicateFingerprint = BuildDuplicateFingerprint(
            expense.ExpenseDate,
            expense.TotalAmount,
            expense.CurrencyCode,
            expense.MerchantName,
            expense.Description,
            expense.ReceiptText);
        expense.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return MapExpense(expense);
    }

    public async Task<ExpensePreviewRecord> AnalyzeExpensePreviewAsync(AnalyzeExpenseRequest request, CancellationToken ct = default)
    {
        var (extraction, groups, model) = await AnalyzeExpenseCore(request, ct);
        var matchedGroupId = ResolveGroupId(groups, request.ExpenseGroupId, extraction.ClassificationGroupKey);
        var settings = await GetSettingsEntityAsync(ct);

        return new ExpensePreviewRecord(
            matchedGroupId,
            request.ExpenseDate ?? extraction.ExpenseDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            request.TotalAmount ?? extraction.TotalAmount,
            request.SubtotalAmount ?? extraction.SubtotalAmount,
            request.TaxAmount ?? extraction.TaxAmount,
            request.TipAmount ?? extraction.TipAmount,
            NormalizeCurrency(request.CurrencyCode ?? extraction.CurrencyCode ?? settings.DefaultCurrencyCode),
            Coalesce(request.MerchantName, extraction.MerchantName),
            Coalesce(request.Location, extraction.Location),
            Coalesce(request.PaymentMethod, extraction.PaymentMethod),
            Coalesce(request.Description, extraction.Description),
            Coalesce(request.Notes, extraction.Notes),
            string.IsNullOrWhiteSpace(request.ReceiptText) ? extraction.ReceiptText : request.ReceiptText.Trim(),
            model.ModelKey,
            extraction);
    }

    public async Task<AnalyzedExpenseRecord> AnalyzeAndCreateExpenseAsync(AnalyzeExpenseRequest request, CancellationToken ct = default)
    {
        var settings = await GetSettingsEntityAsync(ct);
        var (extraction, groups, model) = await AnalyzeExpenseCore(request, ct);
        var matchedGroupId = ResolveGroupId(groups, request.ExpenseGroupId, extraction.ClassificationGroupKey);
        var resolvedTotal = request.TotalAmount ?? extraction.TotalAmount ?? 0m;

        if (resolvedTotal <= 0)
        {
            throw new InvalidOperationException("The model could not determine a total amount. Enter it manually and try again.");
        }

        var expense = await CreateExpenseEntryAsync(
            new PersistExpenseRequest(
                matchedGroupId,
                ExpenseSourceTypes.Receipt,
                request.ExpenseDate ?? extraction.ExpenseDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                resolvedTotal,
                request.SubtotalAmount ?? extraction.SubtotalAmount,
                request.TaxAmount ?? extraction.TaxAmount,
                request.TipAmount ?? extraction.TipAmount,
                NormalizeCurrency(request.CurrencyCode ?? extraction.CurrencyCode ?? settings.DefaultCurrencyCode),
                Coalesce(request.MerchantName, extraction.MerchantName),
                Coalesce(request.Location, extraction.Location),
                Coalesce(request.PaymentMethod, extraction.PaymentMethod),
                Coalesce(request.Description, extraction.Description),
                Coalesce(request.Notes, extraction.Notes),
                request.FileName,
                request.ImageBytes.IsEmpty ? null : request.ImageBytes.ToArray(),
                request.ImageContentType,
                model.ModelKey,
                extraction.Confidence,
                string.IsNullOrWhiteSpace(request.ReceiptText) ? extraction.ReceiptText : request.ReceiptText.Trim(),
                JsonSerializer.Serialize(extraction, JsonOptions),
                LineItems: extraction.Items),
            ct);

        return new AnalyzedExpenseRecord(MapExpense(expense), extraction);
    }

    public async Task<ExpenseCsvImportResult> ImportCsvExpensesAsync(ImportExpenseCsvRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.CsvContent))
        {
            throw new InvalidOperationException("The uploaded CSV file was empty.");
        }

        var parseResult = ExpenseCsvImportSchemas.Parse(request.CsvContent);
        if (parseResult.Rows.Count == 0)
        {
            throw new InvalidOperationException("The CSV file did not contain any importable rows.");
        }

        return await ImportNormalizedExpensesAsync(
            new ImportNormalizedExpenseRowsRequest(
                request.ModelKey,
                request.FileName,
                ExpenseSourceTypes.CsvImport,
                parseResult.SchemaKey,
                parseResult.SchemaDisplayName,
                parseResult.Rows),
            ct);
    }

    public async Task<ExpenseCsvImportResult> ImportFetchExpensesAsync(ImportExpenseFetchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parsedRequest = ExpenseFetchImportParser.Parse(request.RequestText);
        var responseJson = await ExecuteFetchImportRequestAsync(parsedRequest, ct);
        var rows = WealthsimpleActivityImportParser.ParseRows(responseJson);

        return await ImportNormalizedExpensesAsync(
            new ImportNormalizedExpenseRowsRequest(
                request.ModelKey,
                $"fetch:{new Uri(parsedRequest.Url, UriKind.Absolute).Host}",
                ExpenseSourceTypes.FetchImport,
                WealthsimpleActivityImportParser.SchemaKey,
                WealthsimpleActivityImportParser.SchemaDisplayName,
                rows),
            ct);
    }

    public async Task<ExpenseCsvImportResult> ApproveImportedExpenseRowsAsync(ApproveExpenseImportRowsRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Rows.Count == 0)
        {
            throw new InvalidOperationException("Choose at least one review row to approve.");
        }

        var reportItems = new List<ExpenseCsvImportReportItem>(request.Rows.Count);

        foreach (var row in request.Rows)
        {
            try
            {
                var duplicate = await FindDuplicateExpenseAsync(
                    new ExpenseDuplicateCheckRequest(
                        row.ExpenseDate,
                        row.TotalAmount,
                        row.CurrencyCode,
                        row.MerchantName,
                        row.Description,
                        row.SourceReference),
                    ct);

                if (duplicate != null)
                {
                    reportItems.Add(new ExpenseCsvImportReportItem(
                        row.RowNumber,
                        ExpenseCsvImportReportStatuses.Duplicate,
                        $"Skipped during approval because it now matches existing expense #{duplicate.Id}.",
                        row.MerchantName,
                        row.TotalAmount,
                        row.CurrencyCode,
                        row.ExpenseDate,
                        duplicate.ExpenseGroupName,
                        row.SchemaKey,
                        duplicate,
                        row.RawColumns));
                    continue;
                }

                var expense = await CreateExpenseEntryAsync(
                    new PersistExpenseRequest(
                        row.ExpenseGroupId,
                        row.SourceType,
                        row.ExpenseDate,
                        row.TotalAmount,
                        null,
                        null,
                        null,
                        NormalizeCurrency(row.CurrencyCode),
                        row.MerchantName,
                        row.Location,
                        row.PaymentMethod,
                        row.Description,
                        row.Notes,
                        row.ImportFileName,
                        null,
                        row.SourceType == ExpenseSourceTypes.CsvImport ? "text/csv" : "application/json",
                        row.ModelKeyUsed,
                        row.AnalysisConfidence,
                        null,
                        ExpenseCsvImportJson.SerializeRawColumns(row.RawColumns ?? new Dictionary<string, string>()),
                        false,
                        false,
                        row.SchemaKey,
                        row.SourceReference,
                        row.ImportFileName),
                    ct);

                reportItems.Add(new ExpenseCsvImportReportItem(
                    row.RowNumber,
                    ExpenseCsvImportReportStatuses.Added,
                    "Approved and imported.",
                    row.MerchantName,
                    row.TotalAmount,
                    row.CurrencyCode,
                    row.ExpenseDate,
                    expense.ExpenseGroup?.Name,
                    row.SchemaKey,
                    MapExpense(expense),
                    row.RawColumns));
            }
            catch (Exception ex)
            {
                reportItems.Add(new ExpenseCsvImportReportItem(
                    row.RowNumber,
                    ExpenseCsvImportReportStatuses.Failed,
                    ex.Message,
                    row.MerchantName,
                    row.TotalAmount,
                    row.CurrencyCode,
                    row.ExpenseDate,
                    row.ExpenseGroupName,
                    row.SchemaKey,
                    null,
                    row.RawColumns,
                    row));
            }
        }

        return new ExpenseCsvImportResult(
            request.SchemaKey,
            request.SchemaDisplayName,
            reportItems.Count,
            reportItems.Count(item => item.Status == ExpenseCsvImportReportStatuses.Added),
            reportItems.Count(item => item.Status == ExpenseCsvImportReportStatuses.Duplicate),
            reportItems.Count(item => item.Status == ExpenseCsvImportReportStatuses.Review),
            reportItems.Count(item => item.Status == ExpenseCsvImportReportStatuses.Failed),
            reportItems);
    }

    private async Task<(ReceiptExpenseExtraction Extraction, IReadOnlyList<ExpenseGroup> Groups, HomeAppAgentModelConfiguration Model)> AnalyzeExpenseCore(
        AnalyzeExpenseRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ImageBytes.IsEmpty && string.IsNullOrWhiteSpace(request.ReceiptText))
        {
            throw new InvalidOperationException("A receipt image or receipt text is required for analysis.");
        }

        var settings = await GetSettingsEntityAsync(ct);
        var groups = await _dbContext.ExpenseGroups
            .AsNoTracking()
            .Where(group => group.IsEnabled)
            .OrderBy(group => group.DisplayOrder)
            .ThenBy(group => group.Name)
            .ToListAsync(ct);
        var recentItemNames = await _dbContext.ExpenseLineItems
            .AsNoTracking()
            .OrderByDescending(item => item.Id)
            .Select(item => new { item.CanonicalName, item.NormalizedName })
            .Take(2000)
            .ToListAsync(ct);
        var existingItemNames = recentItemNames
            .GroupBy(item => item.NormalizedName)
            .Select(group => group.First().CanonicalName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Take(150)
            .ToList();

        var modelKey = string.IsNullOrWhiteSpace(request.ModelKey)
            ? settings.ModelKey
            : request.ModelKey;

        var model = await _modelCatalog.GetResolvedModelAsync(modelKey, ct);
        var chatClient = ExpenseAnalysisChatClientFactory.Create(model, _loggerFactory);

        var prompt = BuildReceiptPrompt(groups, existingItemNames, settings, request);
        var userContent = new List<AIContent> { new TextContent(prompt) };

        if (request.ImageBytes.Length > 0 && !string.IsNullOrWhiteSpace(request.ImageContentType))
        {
            userContent.Add(new DataContent(request.ImageBytes, request.ImageContentType));
        }

        if (!string.IsNullOrWhiteSpace(request.ReceiptText))
        {
            userContent.Add(new TextContent($"Receipt text provided by user:\n{request.ReceiptText.Trim()}"));
        }

        var response = await chatClient.GetResponseAsync(
            [new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, userContent)],
            new ChatOptions
            {
                Instructions = "Extract structured expense information from receipts and return JSON matching the requested schema.",
                Temperature = model.Temperature is null ? null : (float)model.Temperature.Value,
                MaxOutputTokens = model.MaxOutputTokens,
                ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema<ReceiptExpenseExtraction>()
            },
            ct);

        var extraction = JsonSerializer.Deserialize<ReceiptExpenseExtraction>(response.Text ?? string.Empty, JsonOptions)
            ?? throw new InvalidOperationException("The expense extraction model did not return a valid payload.");

        return (extraction, groups, model);
    }

    private async Task<ExpenseAgentSettings> GetSettingsEntityAsync(CancellationToken ct)
    {
        var settings = await _dbContext.ExpenseAgentSettings.FirstOrDefaultAsync(ct);
        if (settings != null)
        {
            return settings;
        }

        settings = new ExpenseAgentSettings();
        _dbContext.ExpenseAgentSettings.Add(settings);
        await _dbContext.SaveChangesAsync(ct);
        return settings;
    }

    private static ExpenseRecord MapExpense(ExpenseEntry expense)
    {
        return new ExpenseRecord(
            expense.Id,
            expense.ExpenseGroupId,
            expense.ExpenseGroup?.Name,
            expense.ExpenseGroup?.Color,
            expense.SourceType,
            expense.ExpenseDate,
            expense.TotalAmount,
            expense.SubtotalAmount,
            expense.TaxAmount,
            expense.TipAmount,
            expense.CurrencyCode,
            expense.MerchantName,
            expense.Location,
            expense.PaymentMethod,
            expense.Description,
            expense.Notes,
            expense.ReceiptFileName,
            expense.ModelKeyUsed,
            expense.AnalysisConfidence,
            expense.ReceiptText,
            expense.IsWorkExpense,
            expense.IsReimbursable,
            expense.CreatedAtUtc,
            expense.UpdatedAtUtc);
    }

    private static ExpenseDetailRecord MapExpenseDetail(ExpenseEntry expense)
    {
        return new ExpenseDetailRecord(
            MapExpense(expense),
            expense.RawExtractionJson,
            expense.ReceiptImageBytes is { Length: > 0 } && !string.IsNullOrWhiteSpace(expense.ReceiptImageContentType)
                ? $"data:{expense.ReceiptImageContentType};base64,{Convert.ToBase64String(expense.ReceiptImageBytes)}"
                : null,
            expense.ReceiptImageContentType,
            expense.LineItems.OrderBy(item => item.Id).Select(MapLineItem).ToList());
    }

    private static ExpenseLineItemRecord MapLineItem(ExpenseLineItem item) =>
        new(item.Id, item.Name, item.CanonicalName, item.Quantity, item.Unit, item.UnitPrice, item.TotalPrice);

    private static ExpenseGroupRecord MapGroup(ExpenseGroup group) =>
        new(group.Id, group.Key, group.Name, group.Color, group.IsEnabled, group.DisplayOrder);

    private static ExpenseSummary BuildSummary(IReadOnlyList<ExpenseEntry> expenses, IReadOnlyList<ExpenseGroup> groups)
    {
        var totalAmount = expenses.Sum(expense => expense.TotalAmount);
        var average = expenses.Count == 0 ? 0m : totalAmount / expenses.Count;
        var byGroup = expenses
            .GroupBy(expense => expense.ExpenseGroupId)
            .Select(grouping =>
            {
                var match = groups.FirstOrDefault(group => group.Id == grouping.Key);
                return new ExpenseGroupTotal(
                    grouping.Key,
                    match?.Name ?? "Unclassified",
                    match?.Color ?? "#64748b",
                    grouping.Sum(item => item.TotalAmount),
                    grouping.Count());
            })
            .OrderByDescending(item => item.TotalAmount)
            .ThenBy(item => item.Name)
            .ToList();

        DateOnly? latestExpenseDate = expenses.Count == 0 ? null : expenses.Max(expense => expense.ExpenseDate);

        return new ExpenseSummary(totalAmount, expenses.Count, average, latestExpenseDate, byGroup);
    }

    private static string BuildReceiptPrompt(
        IReadOnlyList<ExpenseGroup> groups,
        IReadOnlyList<string> existingItemNames,
        ExpenseAgentSettings settings,
        AnalyzeExpenseRequest request)
    {
        var groupList = string.Join(", ", groups.Select(group => $"{group.Key} ({group.Name})"));
        var userHints = new List<string>();

        if (request.ExpenseGroupId.HasValue)
        {
            userHints.Add($"Preferred expense group id: {request.ExpenseGroupId.Value}");
        }

        if (request.ExpenseDate.HasValue)
        {
            userHints.Add($"Manual date override: {request.ExpenseDate.Value:yyyy-MM-dd}");
        }

        if (request.TotalAmount.HasValue)
        {
            userHints.Add($"Manual total override: {request.TotalAmount.Value}");
        }

        if (!string.IsNullOrWhiteSpace(request.MerchantName))
        {
            userHints.Add($"Manual merchant hint: {request.MerchantName.Trim()}");
        }

        var prompt = $"""
Extract expense details from the receipt.

Allowed expense groups:
{groupList}

Return the best matching classificationGroupKey using one of the allowed group keys when possible.
Use ISO date format for expenseDate.
Use null when a field cannot be determined confidently.
Confidence should be a number from 0 to 1.
Currency codes should be ISO-4217 such as USD.
Extract every purchased line item into items. Use a concise, standardized singular product name without SKU codes;
retain brand, variant, and size only when they materially affect a fair price comparison. Include quantity and unit when shown,
and distinguish unitPrice (price for one item/unit) from totalPrice (the extended line total after quantity).
Do not include subtotal, tax, tip, discounts, payments, or the receipt total as purchased items.
Reuse an exact name from the existing product vocabulary when it is the same product. Create a new name only when none fit.

Existing product vocabulary:
{(existingItemNames.Count == 0 ? "None yet." : string.Join(", ", existingItemNames))}

User hints:
{(userHints.Count == 0 ? "None." : string.Join('\n', userHints))}
""";

        if (!string.IsNullOrWhiteSpace(settings.ExtractionPrompt))
        {
            prompt += $"\n\nAdditional extraction guidance:\n{settings.ExtractionPrompt.Trim()}";
        }

        return prompt;
    }

    private async Task<IReadOnlyList<ExpenseCsvClassificationItem>> ClassifyCsvRowsAsync(
        IReadOnlyList<ExpenseCsvNormalizedRow> rows,
        IReadOnlyList<ExpenseGroup> groups,
        HomeAppAgentModelConfiguration model,
        CancellationToken ct)
    {
        var chatClient = ExpenseAnalysisChatClientFactory.Create(model, _loggerFactory);
        var responses = new List<ExpenseCsvClassificationItem>();

        foreach (var batch in rows.Chunk(20))
        {
            var payload = BuildCsvClassificationPrompt(batch, groups);
            var response = await chatClient.GetResponseAsync(
                [new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, [new TextContent(payload)])],
                new ChatOptions
                {
                    Instructions = "Classify bank and credit card transactions into the closest matching expense group and return JSON matching the requested schema.",
                    Temperature = model.Temperature is null ? null : (float)model.Temperature.Value,
                    MaxOutputTokens = model.MaxOutputTokens,
                    ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.ForJsonSchema<ExpenseCsvClassificationBatchResponse>()
                },
                ct);

            var parsed = JsonSerializer.Deserialize<ExpenseCsvClassificationBatchResponse>(response.Text ?? string.Empty, JsonOptions)
                ?? throw new InvalidOperationException("The expense classification model did not return a valid payload.");

            responses.AddRange(parsed.Items);
        }

        return responses;
    }

    private static string BuildCsvClassificationPrompt(
        IReadOnlyList<ExpenseCsvNormalizedRow> rows,
        IReadOnlyList<ExpenseGroup> groups)
    {
        var groupList = string.Join(", ", groups.Select(group => $"{group.Key} ({group.Name})"));
        var builder = new StringBuilder();
        builder.AppendLine("Classify each transaction into one of the allowed expense groups.");
        builder.AppendLine($"Allowed groups: {groupList}");
        builder.AppendLine("Return one item for every rowNumber provided.");
        builder.AppendLine("If unsure, choose the closest group and lower the confidence.");
        builder.AppendLine();

        foreach (var row in rows)
        {
            builder.AppendLine(
                $"rowNumber={row.RowNumber}; date={row.ExpenseDate:yyyy-MM-dd}; amount={row.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture)}; currency={row.CurrencyCode}; merchant={row.MerchantName ?? "Unknown"}; description={row.Description ?? "None"}; transactionType={row.TransactionType ?? "Unknown"}");
        }

        return builder.ToString();
    }

    private static int? ResolveGroupId(IReadOnlyList<ExpenseGroup> groups, int? requestedGroupId, string? extractedGroupKey)
    {
        if (requestedGroupId.HasValue && groups.Any(group => group.Id == requestedGroupId.Value))
        {
            return requestedGroupId;
        }

        if (string.IsNullOrWhiteSpace(extractedGroupKey))
        {
            return null;
        }

        return groups.FirstOrDefault(group => string.Equals(group.Key, extractedGroupKey.Trim(), StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private static string NormalizeCurrency(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "USD" : value.Trim().ToUpperInvariant();
        return normalized.Length > 3 ? normalized[..3] : normalized;
    }

    private static string NormalizeKey(string? key, string? name)
    {
        var raw = string.IsNullOrWhiteSpace(key) ? name ?? "group" : key;
        var chars = raw.Trim().ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var compact = new string(chars);
        while (compact.Contains("--", StringComparison.Ordinal))
        {
            compact = compact.Replace("--", "-", StringComparison.Ordinal);
        }

        return compact.Trim('-');
    }

    private static string NormalizeColor(string? color) =>
        string.IsNullOrWhiteSpace(color) ? "#2563eb" : color.Trim();

    private async Task<ExpenseCsvImportResult> ImportNormalizedExpensesAsync(
        ImportNormalizedExpenseRowsRequest request,
        CancellationToken ct)
    {
        var settings = await GetSettingsEntityAsync(ct);
        var groups = await _dbContext.ExpenseGroups
            .AsNoTracking()
            .Where(group => group.IsEnabled)
            .OrderBy(group => group.DisplayOrder)
            .ThenBy(group => group.Name)
            .ToListAsync(ct);

        var modelKey = string.IsNullOrWhiteSpace(request.ModelKey)
            ? settings.ModelKey
            : request.ModelKey.Trim();

        var model = await _modelCatalog.GetResolvedModelAsync(modelKey, ct);
        var classifications = await ClassifyCsvRowsAsync(request.Rows, groups, model, ct);
        var classificationLookup = classifications.ToDictionary(item => item.RowNumber);
        var reportItems = new List<ExpenseCsvImportReportItem>(request.Rows.Count);

        foreach (var row in request.Rows)
        {
            try
            {
                classificationLookup.TryGetValue(row.RowNumber, out var classification);
                var matchedGroupId = ResolveGroupId(groups, null, classification?.ClassificationGroupKey);
                var duplicate = await FindDuplicateExpenseAsync(
                    new ExpenseDuplicateCheckRequest(
                        row.ExpenseDate,
                        row.TotalAmount,
                        row.CurrencyCode,
                        row.MerchantName,
                        row.Description,
                        row.SourceReference),
                    ct);

                if (duplicate != null)
                {
                    reportItems.Add(new ExpenseCsvImportReportItem(
                        row.RowNumber,
                        ExpenseCsvImportReportStatuses.Duplicate,
                        $"Skipped because it matches existing expense #{duplicate.Id}.",
                        row.MerchantName,
                        row.TotalAmount,
                        row.CurrencyCode,
                        row.ExpenseDate,
                        duplicate.ExpenseGroupName,
                        request.SchemaKey,
                        duplicate,
                        row.RawColumns));
                    continue;
                }

                var potentialDuplicate = await FindPotentialDuplicateExpenseAsync(
                    new ExpenseDuplicateCheckRequest(
                        row.ExpenseDate,
                        row.TotalAmount,
                        row.CurrencyCode,
                        row.MerchantName,
                        row.Description,
                        row.SourceReference),
                    ct);

                if (potentialDuplicate != null)
                {
                    reportItems.Add(new ExpenseCsvImportReportItem(
                        row.RowNumber,
                        ExpenseCsvImportReportStatuses.Review,
                        $"Possible duplicate of expense #{potentialDuplicate.Id} from {potentialDuplicate.ExpenseDate:yyyy-MM-dd}. Approve to import this row.",
                        row.MerchantName,
                        row.TotalAmount,
                        row.CurrencyCode,
                        row.ExpenseDate,
                        potentialDuplicate.ExpenseGroupName,
                        request.SchemaKey,
                        potentialDuplicate,
                        row.RawColumns,
                        BuildApprovalPayload(request, row, matchedGroupId, groups, model.ModelKey, classification?.Confidence)));
                    continue;
                }

                var expense = await CreateExpenseEntryAsync(
                    new PersistExpenseRequest(
                        matchedGroupId,
                        request.SourceType,
                        row.ExpenseDate,
                        row.TotalAmount,
                        null,
                        null,
                        null,
                        NormalizeCurrency(row.CurrencyCode),
                        row.MerchantName,
                        row.Location,
                        row.PaymentMethod,
                        row.Description,
                        row.Notes,
                        request.ImportFileName,
                        null,
                        request.SourceType == ExpenseSourceTypes.CsvImport ? "text/csv" : "application/json",
                        model.ModelKey,
                        classification?.Confidence,
                        null,
                        ExpenseCsvImportJson.SerializeRawColumns(row.RawColumns),
                        false,
                        false,
                        request.SchemaKey,
                        row.SourceReference,
                        request.ImportFileName),
                    ct);

                reportItems.Add(new ExpenseCsvImportReportItem(
                    row.RowNumber,
                    ExpenseCsvImportReportStatuses.Added,
                    classification?.Reason,
                    row.MerchantName,
                    row.TotalAmount,
                    row.CurrencyCode,
                    row.ExpenseDate,
                    expense.ExpenseGroup?.Name,
                    request.SchemaKey,
                    MapExpense(expense),
                    row.RawColumns));
            }
            catch (Exception ex)
            {
                reportItems.Add(new ExpenseCsvImportReportItem(
                    row.RowNumber,
                    ExpenseCsvImportReportStatuses.Failed,
                    ex.Message,
                    row.MerchantName,
                    row.TotalAmount,
                    row.CurrencyCode,
                    row.ExpenseDate,
                    null,
                    request.SchemaKey,
                    null,
                    row.RawColumns));
            }
        }

        return new ExpenseCsvImportResult(
            request.SchemaKey,
            request.SchemaDisplayName,
            request.Rows.Count,
            reportItems.Count(item => item.Status == ExpenseCsvImportReportStatuses.Added),
            reportItems.Count(item => item.Status == ExpenseCsvImportReportStatuses.Duplicate),
            reportItems.Count(item => item.Status == ExpenseCsvImportReportStatuses.Review),
            reportItems.Count(item => item.Status == ExpenseCsvImportReportStatuses.Failed),
            reportItems);
    }

    private async Task<string> ExecuteFetchImportRequestAsync(ParsedExpenseFetchRequest request, CancellationToken ct)
    {
        using var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), request.Url);
        var contentType = request.Headers.TryGetValue("content-type", out var contentTypeHeader) ? contentTypeHeader : "application/json";

        if (!string.IsNullOrWhiteSpace(request.Body))
        {
            httpRequest.Content = new StringContent(request.Body, Encoding.UTF8);
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                httpRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }
        }

        foreach (var header in request.Headers)
        {
            if (string.Equals(header.Key, "content-type", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(header.Key, "content-length", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(header.Key, "host", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(header.Key, "cookie", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                httpRequest.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (Uri.TryCreate(request.Referrer, UriKind.Absolute, out var referrerUri))
        {
            httpRequest.Headers.Referrer = referrerUri;
        }

        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var reason = string.IsNullOrWhiteSpace(payload) ? response.ReasonPhrase : payload;
            throw new InvalidOperationException($"The pasted fetch request failed with {(int)response.StatusCode}: {reason}");
        }

        return payload;
    }

    private async Task<ExpenseEntry> CreateExpenseEntryAsync(PersistExpenseRequest request, CancellationToken ct)
    {
        if (request.TotalAmount <= 0)
        {
            throw new InvalidOperationException("Expense total amount must be greater than zero.");
        }

        var now = DateTimeOffset.UtcNow;
        var expense = new ExpenseEntry
        {
            ExpenseGroupId = request.ExpenseGroupId,
            SourceType = request.SourceType,
            ExpenseDate = request.ExpenseDate,
            TotalAmount = request.TotalAmount,
            SubtotalAmount = request.SubtotalAmount,
            TaxAmount = request.TaxAmount,
            TipAmount = request.TipAmount,
            CurrencyCode = NormalizeCurrency(request.CurrencyCode),
            MerchantName = NullIfWhiteSpace(request.MerchantName),
            Location = NullIfWhiteSpace(request.Location),
            PaymentMethod = NullIfWhiteSpace(request.PaymentMethod),
            Description = NullIfWhiteSpace(request.Description),
            Notes = NullIfWhiteSpace(request.Notes),
            ReceiptFileName = NullIfWhiteSpace(request.ReceiptFileName),
            ReceiptImageBytes = request.ReceiptImageBytes,
            ReceiptImageContentType = NullIfWhiteSpace(request.ReceiptImageContentType),
            ModelKeyUsed = NullIfWhiteSpace(request.ModelKeyUsed),
            AnalysisConfidence = request.AnalysisConfidence,
            ReceiptText = NullIfWhiteSpace(request.ReceiptText),
            RawExtractionJson = NullIfWhiteSpace(request.RawExtractionJson),
            IsWorkExpense = request.IsWorkExpense,
            IsReimbursable = request.IsReimbursable,
            ImportSchemaKey = NullIfWhiteSpace(request.ImportSchemaKey),
            ImportSourceReference = NullIfWhiteSpace(request.ImportSourceReference),
            ImportFileName = NullIfWhiteSpace(request.ImportFileName),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        foreach (var extractedItem in request.LineItems ?? [])
        {
            var name = NullIfWhiteSpace(extractedItem.Name);
            var totalPrice = ExpenseLineItem.ResolveTotalPrice(
                extractedItem.TotalPrice,
                extractedItem.UnitPrice,
                extractedItem.Quantity);
            if (name == null || totalPrice <= 0)
            {
                continue;
            }

            expense.LineItems.Add(new ExpenseLineItem
            {
                Name = name,
                CanonicalName = name,
                NormalizedName = ExpenseLineItem.NormalizeName(name),
                Quantity = extractedItem.Quantity is > 0 ? extractedItem.Quantity : null,
                Unit = NullIfWhiteSpace(extractedItem.Unit),
                UnitPrice = ExpenseLineItem.ResolveUnitPrice(extractedItem.UnitPrice, extractedItem.Quantity, totalPrice),
                TotalPrice = decimal.Round(totalPrice, 2)
            });
        }

        expense.DuplicateFingerprint = BuildDuplicateFingerprint(
            expense.ExpenseDate,
            expense.TotalAmount,
            expense.CurrencyCode,
            expense.MerchantName,
            expense.Description,
            expense.ReceiptText);

        _dbContext.ExpenseEntries.Add(expense);
        await _dbContext.SaveChangesAsync(ct);
        await _dbContext.Entry(expense).Reference(item => item.ExpenseGroup).LoadAsync(ct);
        return expense;
    }

    private async Task<ExpenseRecord?> FindDuplicateExpenseAsync(ExpenseDuplicateCheckRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.ImportSourceReference))
        {
            var exactImportMatch = await _dbContext.ExpenseEntries
                .AsNoTracking()
                .Include(item => item.ExpenseGroup)
                .FirstOrDefaultAsync(
                    item => item.ImportSourceReference != null && item.ImportSourceReference == request.ImportSourceReference,
                    ct);

            if (exactImportMatch != null)
            {
                return MapExpense(exactImportMatch);
            }
        }

        var normalizedCurrency = NormalizeCurrency(request.CurrencyCode);
        var requestText = GetBestDuplicateText(request.MerchantName, request.Description, null);
        var fingerprint = BuildDuplicateFingerprint(
            request.ExpenseDate,
            request.TotalAmount,
            normalizedCurrency,
            request.MerchantName,
            request.Description,
            null);

        var candidates = await _dbContext.ExpenseEntries
            .AsNoTracking()
            .Include(item => item.ExpenseGroup)
            .Where(item =>
                item.ExpenseDate == request.ExpenseDate &&
                item.TotalAmount == request.TotalAmount &&
                item.CurrencyCode == normalizedCurrency)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(ct);

        var match = candidates.FirstOrDefault(item =>
            string.Equals(item.DuplicateFingerprint, fingerprint, StringComparison.Ordinal) ||
            string.Equals(
                BuildDuplicateFingerprint(
                    item.ExpenseDate,
                    item.TotalAmount,
                    item.CurrencyCode,
                    item.MerchantName,
                    item.Description,
                    item.ReceiptText),
                fingerprint,
                StringComparison.Ordinal) ||
            string.Equals(
                NormalizeText(GetBestDuplicateText(item.MerchantName, item.Description, item.ReceiptText)),
                NormalizeText(requestText),
                StringComparison.Ordinal) ||
            MerchantTextsMatch(
                GetBestDuplicateText(item.MerchantName, item.Description, item.ReceiptText),
                requestText));

        return match == null ? null : MapExpense(match);
    }

    private async Task<ExpenseRecord?> FindPotentialDuplicateExpenseAsync(ExpenseDuplicateCheckRequest request, CancellationToken ct)
    {
        var normalizedCurrency = NormalizeCurrency(request.CurrencyCode);
        var requestText = GetBestDuplicateText(request.MerchantName, request.Description, null);
        var candidates = await _dbContext.ExpenseEntries
            .AsNoTracking()
            .Include(item => item.ExpenseGroup)
            .Where(item =>
                item.TotalAmount == request.TotalAmount &&
                item.CurrencyCode == normalizedCurrency &&
                item.ExpenseDate >= request.ExpenseDate.AddDays(-7) &&
                item.ExpenseDate <= request.ExpenseDate.AddDays(7) &&
                item.ExpenseDate != request.ExpenseDate)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(ct);

        var match = candidates.FirstOrDefault(item =>
            MerchantTextsMatch(
                GetBestDuplicateText(item.MerchantName, item.Description, item.ReceiptText),
                requestText));

        return match == null ? null : MapExpense(match);
    }

    private static ExpenseImportApprovalPayload BuildApprovalPayload(
        ImportNormalizedExpenseRowsRequest request,
        ExpenseCsvNormalizedRow row,
        int? matchedGroupId,
        IReadOnlyList<ExpenseGroup> groups,
        string modelKeyUsed,
        decimal? analysisConfidence)
    {
        var groupName = matchedGroupId == null
            ? null
            : groups.FirstOrDefault(group => group.Id == matchedGroupId)?.Name;

        return new ExpenseImportApprovalPayload(
            row.RowNumber,
            request.SourceType,
            request.ImportFileName,
            request.SchemaKey,
            row.ExpenseDate,
            row.TotalAmount,
            row.CurrencyCode,
            row.MerchantName,
            row.Description,
            row.Notes,
            row.PaymentMethod,
            row.Location,
            row.SourceReference,
            matchedGroupId,
            groupName,
            modelKeyUsed,
            analysisConfidence,
            row.RawColumns);
    }

    private static string BuildDuplicateFingerprint(
        DateOnly expenseDate,
        decimal totalAmount,
        string? currencyCode,
        string? merchantName,
        string? description,
        string? receiptText)
    {
        var bestText = NormalizeMerchantCoreText(GetBestDuplicateText(merchantName, description, receiptText));

        return string.Join('|',
            expenseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            totalAmount.ToString("0.00", CultureInfo.InvariantCulture),
            NormalizeCurrency(currencyCode),
            bestText);
    }

    private static string? GetBestDuplicateText(string? merchantName, string? description, string? receiptText) =>
        !string.IsNullOrWhiteSpace(merchantName)
            ? merchantName
            : !string.IsNullOrWhiteSpace(description)
                ? description
                : receiptText;

    private static bool MerchantTextsMatch(string? left, string? right)
    {
        var leftCore = NormalizeMerchantCoreText(left);
        var rightCore = NormalizeMerchantCoreText(right);
        if (string.IsNullOrEmpty(leftCore) || string.IsNullOrEmpty(rightCore))
        {
            return false;
        }

        if (string.Equals(leftCore, rightCore, StringComparison.Ordinal))
        {
            return true;
        }

        var shorter = leftCore.Length <= rightCore.Length ? leftCore : rightCore;
        var longer = leftCore.Length <= rightCore.Length ? rightCore : leftCore;
        if (shorter.Length >= 6 && longer.StartsWith(shorter, StringComparison.Ordinal))
        {
            return true;
        }

        var leftBrand = GetLeadingMerchantToken(leftCore);
        var rightBrand = GetLeadingMerchantToken(rightCore);
        return !string.IsNullOrEmpty(leftBrand) &&
               string.Equals(leftBrand, rightBrand, StringComparison.Ordinal) &&
               (string.Equals(leftCore, leftBrand, StringComparison.Ordinal) ||
                string.Equals(rightCore, rightBrand, StringComparison.Ordinal));
    }

    private static string GetLeadingMerchantToken(string value)
    {
        var separatorIndex = value.IndexOf(' ');
        return separatorIndex >= 0 ? value[..separatorIndex] : value;
    }

    private static string NormalizeMerchantCoreText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToUpperInvariant()
            .Replace("WAL-MART", "WALMART", StringComparison.Ordinal)
            .Replace("WAL MART", "WALMART", StringComparison.Ordinal);

        var tokens = normalized
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
            .ToArray();

        var parts = new string(tokens)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static part => part.Trim())
            .Where(static part => part.Length > 0)
            .Where(static part => !part.All(char.IsDigit))
            .Where(part => !MerchantNoiseTokens.Contains(part))
            .ToList();

        if (parts.Count == 0)
        {
            return NormalizeText(value);
        }

        return string.Join(' ', parts);
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value.Trim().ToUpperInvariant()
            .Where(char.IsLetterOrDigit)
            .Take(80)
            .ToArray();

        return new string(chars);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Coalesce(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred) ? preferred.Trim() : NullIfWhiteSpace(fallback);
}

internal sealed record PersistExpenseRequest(
    int? ExpenseGroupId,
    string SourceType,
    DateOnly ExpenseDate,
    decimal TotalAmount,
    decimal? SubtotalAmount,
    decimal? TaxAmount,
    decimal? TipAmount,
    string CurrencyCode,
    string? MerchantName,
    string? Location,
    string? PaymentMethod,
    string? Description,
    string? Notes,
    string? ReceiptFileName,
    byte[]? ReceiptImageBytes,
    string? ReceiptImageContentType,
    string? ModelKeyUsed,
    decimal? AnalysisConfidence,
    string? ReceiptText,
    string? RawExtractionJson,
    bool IsWorkExpense = false,
    bool IsReimbursable = false,
    string? ImportSchemaKey = null,
    string? ImportSourceReference = null,
    string? ImportFileName = null,
    IReadOnlyList<ReceiptLineItemExtraction>? LineItems = null);

internal sealed record ExpenseDuplicateCheckRequest(
    DateOnly ExpenseDate,
    decimal TotalAmount,
    string CurrencyCode,
    string? MerchantName,
    string? Description,
    string? ImportSourceReference);

internal sealed record ImportNormalizedExpenseRowsRequest(
    string? ModelKey,
    string ImportFileName,
    string SourceType,
    string SchemaKey,
    string SchemaDisplayName,
    IReadOnlyList<ExpenseCsvNormalizedRow> Rows);

internal static class ExpenseAnalysisChatClientFactory
{
    public static IChatClient Create(HomeAppAgentModelConfiguration model, ILoggerFactory loggerFactory)
    {
        var apiKey = !string.IsNullOrWhiteSpace(model.ApiKey)
            ? model.ApiKey
            : model.ProviderType == AiProviderType.Ollama
                ? "ollama"
                : throw new InvalidOperationException(
                    $"Provider '{model.ProviderName}' requires an API key environment variable to be configured.");

        var client = new OpenAIChatClient(
            model.RemoteModelId,
            new System.ClientModel.ApiKeyCredential(apiKey),
            new OpenAI.OpenAIClientOptions
            {
                Endpoint = new Uri(model.BaseUrl, UriKind.Absolute)
            });

        return client
            .AsIChatClient()
            .AsBuilder()
            .UseHomeAppTelemetry(model, loggerFactory)
            .Build();
    }
}

public sealed record ExpenseQuery(
    string? Search = null,
    int? ExpenseGroupId = null,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    string? SourceType = null);

public sealed record ExpenseDashboardSnapshot(
    IReadOnlyList<ExpenseRecord> Expenses,
    IReadOnlyList<ExpenseGroupRecord> Groups,
    ExpenseSettingsSnapshot Settings,
    IReadOnlyList<ExpenseModelOption> Models,
    ExpenseSummary Summary);

public sealed record ExpenseRecord(
    int Id,
    int? ExpenseGroupId,
    string? ExpenseGroupName,
    string? ExpenseGroupColor,
    string SourceType,
    DateOnly ExpenseDate,
    decimal TotalAmount,
    decimal? SubtotalAmount,
    decimal? TaxAmount,
    decimal? TipAmount,
    string CurrencyCode,
    string? MerchantName,
    string? Location,
    string? PaymentMethod,
    string? Description,
    string? Notes,
    string? ReceiptFileName,
    string? ModelKeyUsed,
    decimal? AnalysisConfidence,
    string? ReceiptText,
    bool IsWorkExpense,
    bool IsReimbursable,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record ExpenseGroupRecord(
    int Id,
    string Key,
    string Name,
    string Color,
    bool IsEnabled,
    int DisplayOrder);

public sealed record ExpenseSettingsSnapshot(
    string? ModelKey,
    string DefaultCurrencyCode,
    string? ExtractionPrompt);

public sealed record ExpenseModelOption(
    string Key,
    string Name,
    string ModelId,
    string ProviderName,
    bool IsDefault);

public sealed record ExpenseSummary(
    decimal TotalAmount,
    int ExpenseCount,
    decimal AverageAmount,
    DateOnly? LatestExpenseDate,
    IReadOnlyList<ExpenseGroupTotal> TotalsByGroup);

public sealed record ExpenseGroupTotal(
    int? ExpenseGroupId,
    string Name,
    string Color,
    decimal TotalAmount,
    int ExpenseCount);

public sealed record ExpenseSettingsUpdate(
    string? ModelKey,
    string? DefaultCurrencyCode,
    string? ExtractionPrompt,
    IReadOnlyList<ExpenseGroupUpsert> Groups);

public sealed record ExpenseGroupUpsert(
    int Id,
    string Key,
    string Name,
    string Color,
    bool IsEnabled);

public sealed record CreateManualExpenseRequest(
    int? ExpenseGroupId,
    DateOnly? ExpenseDate,
    decimal TotalAmount,
    decimal? SubtotalAmount,
    decimal? TaxAmount,
    decimal? TipAmount,
    string? CurrencyCode,
    string? MerchantName,
    string? Location,
    string? PaymentMethod,
    string? Description,
    string? Notes);

public sealed record AnalyzeExpenseRequest(
    string? ModelKey,
    int? ExpenseGroupId,
    DateOnly? ExpenseDate,
    decimal? TotalAmount,
    decimal? SubtotalAmount,
    decimal? TaxAmount,
    decimal? TipAmount,
    string? CurrencyCode,
    string? MerchantName,
    string? Location,
    string? PaymentMethod,
    string? Description,
    string? Notes,
    string? ReceiptText,
    string? FileName,
    string? ImageContentType,
    ReadOnlyMemory<byte> ImageBytes);

public sealed record AnalyzedExpenseRecord(
    ExpenseRecord Expense,
    ReceiptExpenseExtraction Extraction);

public sealed record ExpensePreviewRecord(
    int? ExpenseGroupId,
    DateOnly ExpenseDate,
    decimal? TotalAmount,
    decimal? SubtotalAmount,
    decimal? TaxAmount,
    decimal? TipAmount,
    string CurrencyCode,
    string? MerchantName,
    string? Location,
    string? PaymentMethod,
    string? Description,
    string? Notes,
    string? ReceiptText,
    string ModelKeyUsed,
    ReceiptExpenseExtraction Extraction);

public sealed record ExpenseDetailRecord(
    ExpenseRecord Expense,
    string? RawExtractionJson,
    string? ReceiptImageDataUrl,
    string? ReceiptImageContentType,
    IReadOnlyList<ExpenseLineItemRecord> Items);

public sealed record ExpenseLineItemRecord(
    int Id,
    string Name,
    string CanonicalName,
    decimal? Quantity,
    string? Unit,
    decimal? UnitPrice,
    decimal TotalPrice);

public sealed record ExpenseItemComparisonRecord(
    string Name,
    string NormalizedName,
    int PurchaseCount,
    decimal? LowestPrice,
    string? LowestPriceMerchant,
    IReadOnlyList<ExpenseItemPurchaseRecord> Purchases);

public sealed record ExpenseItemPurchaseRecord(
    int Id,
    int ExpenseId,
    string Name,
    string CanonicalName,
    decimal? Quantity,
    string? Unit,
    decimal? UnitPrice,
    decimal TotalPrice,
    decimal? ComparablePrice,
    string CurrencyCode,
    string? MerchantName,
    string? Location,
    DateOnly ExpenseDate);

public sealed record ExpenseItemReclassificationRecord(
    string CanonicalName,
    string NormalizedName,
    int UpdatedCount);

public sealed record UpdateExpenseRequest(
    int? ExpenseGroupId,
    DateOnly ExpenseDate,
    decimal TotalAmount,
    decimal? SubtotalAmount,
    decimal? TaxAmount,
    decimal? TipAmount,
    string CurrencyCode,
    string? MerchantName,
    string? Location,
    string? PaymentMethod,
    string? Description,
    string? Notes,
    string? ReceiptText,
    bool IsWorkExpense,
    bool IsReimbursable);

public sealed class ReceiptExpenseExtraction
{
    public string? ClassificationGroupKey { get; set; }
    public DateOnly? ExpenseDate { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? SubtotalAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? TipAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? MerchantName { get; set; }
    public string? Location { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string? ReceiptText { get; set; }
    public decimal? Confidence { get; set; }
    public List<ReceiptLineItemExtraction> Items { get; set; } = [];
}

public sealed class ReceiptLineItemExtraction
{
    public string? Name { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }
}
