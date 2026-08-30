using HomeApp.Expenses.Data;
using HomeApp.Expenses.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace HomeApp.Expenses.Controllers;

[ApiController]
[Route("api/expenses")]
public sealed class ExpensesController : ControllerBase
{
    private readonly ExpenseService _expenseService;

    public ExpensesController(ExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    [HttpGet]
    public async Task<ActionResult<ExpenseDashboardResponseDto>> GetDashboard(
        [FromQuery] string? search,
        [FromQuery] int? expenseGroupId,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] string? sourceType,
        CancellationToken ct)
    {
        var snapshot = await _expenseService.GetDashboardAsync(
            new ExpenseQuery(search, expenseGroupId, dateFrom, dateTo, sourceType),
            ct);

        return Ok(MapDashboard(snapshot));
    }

    [HttpGet("{expenseId:int}")]
    public async Task<ActionResult<ExpenseDetailDto>> GetExpense(int expenseId, CancellationToken ct)
    {
        var expense = await _expenseService.GetExpenseAsync(expenseId, ct);
        return expense == null ? NotFound() : Ok(MapExpenseDetail(expense));
    }

    [HttpGet("items")]
    public async Task<ActionResult<List<ExpenseItemComparisonDto>>> SearchItems(
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var items = await _expenseService.SearchItemsAsync(search, ct);
        return Ok(items.Select(item => new ExpenseItemComparisonDto
        {
            Name = item.Name,
            NormalizedName = item.NormalizedName,
            PurchaseCount = item.PurchaseCount,
            LowestPrice = item.LowestPrice,
            LowestPriceMerchant = item.LowestPriceMerchant,
            Purchases = item.Purchases.Select(purchase => new ExpenseItemPurchaseDto
            {
                Id = purchase.Id,
                ExpenseId = purchase.ExpenseId,
                Name = purchase.Name,
                CanonicalName = purchase.CanonicalName,
                Quantity = purchase.Quantity,
                Unit = purchase.Unit,
                UnitPrice = purchase.UnitPrice,
                TotalPrice = purchase.TotalPrice,
                ComparablePrice = purchase.ComparablePrice,
                CurrencyCode = purchase.CurrencyCode,
                MerchantName = purchase.MerchantName,
                Location = purchase.Location,
                ExpenseDate = purchase.ExpenseDate
            }).ToList()
        }).ToList());
    }

    [HttpPut("items/{itemId:int}/classification")]
    public async Task<ActionResult<ExpenseItemReclassificationDto>> ReclassifyItem(
        int itemId,
        [FromBody] ReclassifyExpenseItemRequestDto request,
        CancellationToken ct)
    {
        var result = await _expenseService.ReclassifyItemAsync(
            itemId,
            request.CanonicalName,
            request.ApplyToMatching,
            ct);
        return result == null
            ? NotFound()
            : Ok(new ExpenseItemReclassificationDto
            {
                CanonicalName = result.CanonicalName,
                NormalizedName = result.NormalizedName,
                UpdatedCount = result.UpdatedCount
            });
    }

    [HttpPut("settings")]
    public async Task<ActionResult<ExpenseDashboardResponseDto>> SaveSettings(
        [FromBody] SaveExpenseSettingsRequestDto request,
        CancellationToken ct)
    {
        var snapshot = await _expenseService.SaveSettingsAsync(
            new ExpenseSettingsUpdate(
                request.Settings.ModelKey,
                request.Settings.DefaultCurrencyCode,
                request.Settings.ExtractionPrompt,
                request.Groups.Select(group => new ExpenseGroupUpsert(
                    group.Id,
                    group.Key,
                    group.Name,
                    group.Color,
                    group.IsEnabled)).ToList()),
            ct);

        return Ok(MapDashboard(snapshot));
    }

    [HttpPost("manual")]
    public async Task<ActionResult<ExpenseDto>> CreateManual(
        [FromBody] CreateManualExpenseRequestDto request,
        CancellationToken ct)
    {
        var expense = await _expenseService.CreateManualExpenseAsync(
            new CreateManualExpenseRequest(
                request.ExpenseGroupId,
                request.ExpenseDate,
                request.TotalAmount,
                request.SubtotalAmount,
                request.TaxAmount,
                request.TipAmount,
                request.CurrencyCode,
                request.MerchantName,
                request.Location,
                request.PaymentMethod,
                request.Description,
                request.Notes),
            ct);

        return Ok(MapExpense(expense));
    }

    [HttpPut("{expenseId:int}")]
    public async Task<ActionResult<ExpenseDto>> UpdateExpense(
        int expenseId,
        [FromBody] UpdateExpenseRequestDto request,
        CancellationToken ct)
    {
        var expense = await _expenseService.UpdateExpenseAsync(
            expenseId,
            new UpdateExpenseRequest(
                request.ExpenseGroupId,
                request.ExpenseDate,
                request.TotalAmount,
                request.SubtotalAmount,
                request.TaxAmount,
                request.TipAmount,
                request.CurrencyCode,
                request.MerchantName,
                request.Location,
                request.PaymentMethod,
                request.Description,
                request.Notes,
                request.ReceiptText,
                request.IsWorkExpense,
                request.IsReimbursable),
            ct);

        return expense == null ? NotFound() : Ok(MapExpense(expense));
    }

    [HttpPost("analyze-preview")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<ExpensePreviewResponseDto>> AnalyzePreview(
        [FromForm] AnalyzeExpenseFormDto request,
        CancellationToken ct)
    {
        var processedImage = await ProcessReceiptImageAsync(request.File, ct);
        var preview = await _expenseService.AnalyzeExpensePreviewAsync(
            new AnalyzeExpenseRequest(
                request.ModelKey,
                request.ExpenseGroupId,
                request.ExpenseDate,
                request.TotalAmount,
                request.SubtotalAmount,
                request.TaxAmount,
                request.TipAmount,
                request.CurrencyCode,
                request.MerchantName,
                request.Location,
                request.PaymentMethod,
                request.Description,
                request.Notes,
                request.ReceiptText,
                request.File?.FileName,
                processedImage.ContentType,
                processedImage.Bytes),
            ct);

        return Ok(new ExpensePreviewResponseDto
        {
            ExpenseGroupId = preview.ExpenseGroupId,
            ExpenseDate = preview.ExpenseDate,
            TotalAmount = preview.TotalAmount,
            SubtotalAmount = preview.SubtotalAmount,
            TaxAmount = preview.TaxAmount,
            TipAmount = preview.TipAmount,
            CurrencyCode = preview.CurrencyCode,
            MerchantName = preview.MerchantName,
            Location = preview.Location,
            PaymentMethod = preview.PaymentMethod,
            Description = preview.Description,
            Notes = preview.Notes,
            ReceiptText = preview.ReceiptText,
            ModelKeyUsed = preview.ModelKeyUsed,
            Extraction = new ReceiptExtractionDto
            {
                ClassificationGroupKey = preview.Extraction.ClassificationGroupKey,
                ExpenseDate = preview.Extraction.ExpenseDate,
                TotalAmount = preview.Extraction.TotalAmount,
                SubtotalAmount = preview.Extraction.SubtotalAmount,
                TaxAmount = preview.Extraction.TaxAmount,
                TipAmount = preview.Extraction.TipAmount,
                CurrencyCode = preview.Extraction.CurrencyCode,
                MerchantName = preview.Extraction.MerchantName,
                Location = preview.Extraction.Location,
                PaymentMethod = preview.Extraction.PaymentMethod,
                Description = preview.Extraction.Description,
                Notes = preview.Extraction.Notes,
                ReceiptText = preview.Extraction.ReceiptText,
                Confidence = preview.Extraction.Confidence,
                Items = preview.Extraction.Items.Select(MapExtractionItem).ToList()
            }
        });
    }

    [HttpPost("analyze")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<AnalyzedExpenseResponseDto>> AnalyzeAndCreate(
        [FromForm] AnalyzeExpenseFormDto request,
        CancellationToken ct)
    {
        var processedImage = await ProcessReceiptImageAsync(request.File, ct);

        var result = await _expenseService.AnalyzeAndCreateExpenseAsync(
            new AnalyzeExpenseRequest(
                request.ModelKey,
                request.ExpenseGroupId,
                request.ExpenseDate,
                request.TotalAmount,
                request.SubtotalAmount,
                request.TaxAmount,
                request.TipAmount,
                request.CurrencyCode,
                request.MerchantName,
                request.Location,
                request.PaymentMethod,
                request.Description,
                request.Notes,
                request.ReceiptText,
                request.File?.FileName,
                processedImage.ContentType,
                processedImage.Bytes),
            ct);

        return Ok(new AnalyzedExpenseResponseDto
        {
            Expense = MapExpense(result.Expense),
            Extraction = new ReceiptExtractionDto
            {
                ClassificationGroupKey = result.Extraction.ClassificationGroupKey,
                ExpenseDate = result.Extraction.ExpenseDate,
                TotalAmount = result.Extraction.TotalAmount,
                SubtotalAmount = result.Extraction.SubtotalAmount,
                TaxAmount = result.Extraction.TaxAmount,
                TipAmount = result.Extraction.TipAmount,
                CurrencyCode = result.Extraction.CurrencyCode,
                MerchantName = result.Extraction.MerchantName,
                Location = result.Extraction.Location,
                PaymentMethod = result.Extraction.PaymentMethod,
                Description = result.Extraction.Description,
                Notes = result.Extraction.Notes,
                ReceiptText = result.Extraction.ReceiptText,
                Confidence = result.Extraction.Confidence,
                Items = result.Extraction.Items.Select(MapExtractionItem).ToList()
            }
        });
    }

    [HttpPost("import-csv")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<ExpenseCsvImportResponseDto>> ImportCsv(
        [FromForm] ImportExpenseCsvFormDto request,
        CancellationToken ct)
    {
        if (request.File is not { Length: > 0 })
        {
            return BadRequest("A CSV file is required.");
        }

        using var reader = new StreamReader(request.File.OpenReadStream());
        var csvContent = await reader.ReadToEndAsync(ct);
        var result = await _expenseService.ImportCsvExpensesAsync(
            new ImportExpenseCsvRequest(
                request.ModelKey,
                request.File.FileName,
                csvContent),
            ct);

        return Ok(new ExpenseCsvImportResponseDto
        {
            SchemaKey = result.SchemaKey,
            SchemaDisplayName = result.SchemaDisplayName,
            TotalRows = result.TotalRows,
            AddedCount = result.AddedCount,
            DuplicateCount = result.DuplicateCount,
            ReviewCount = result.ReviewCount,
            FailedCount = result.FailedCount,
            Items = result.Items.Select(item => new ExpenseCsvImportReportItemDto
            {
                RowNumber = item.RowNumber,
                Status = item.Status,
                Message = item.Message,
                MerchantName = item.MerchantName,
                Amount = item.Amount,
                CurrencyCode = item.CurrencyCode,
                ExpenseDate = item.ExpenseDate,
                ExpenseGroupName = item.ExpenseGroupName,
                SchemaKey = item.SchemaKey,
                Expense = item.Expense == null ? null : MapExpense(item.Expense),
                RawColumns = item.RawColumns == null ? null : new Dictionary<string, string>(item.RawColumns),
                ApprovalPayload = item.ApprovalPayload == null ? null : MapApprovalPayload(item.ApprovalPayload)
            }).ToList()
        });
    }

    [HttpPost("import-fetch")]
    [RequestSizeLimit(200_000)]
    public async Task<ActionResult<ExpenseCsvImportResponseDto>> ImportFetch(
        [FromBody] ImportExpenseFetchRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RequestText))
        {
            return BadRequest("A pasted fetch request is required.");
        }

        var result = await _expenseService.ImportFetchExpensesAsync(
            new ImportExpenseFetchRequest(
                request.ModelKey,
                request.RequestText),
            ct);

        return Ok(new ExpenseCsvImportResponseDto
        {
            SchemaKey = result.SchemaKey,
            SchemaDisplayName = result.SchemaDisplayName,
            TotalRows = result.TotalRows,
            AddedCount = result.AddedCount,
            DuplicateCount = result.DuplicateCount,
            ReviewCount = result.ReviewCount,
            FailedCount = result.FailedCount,
            Items = result.Items.Select(item => new ExpenseCsvImportReportItemDto
            {
                RowNumber = item.RowNumber,
                Status = item.Status,
                Message = item.Message,
                MerchantName = item.MerchantName,
                Amount = item.Amount,
                CurrencyCode = item.CurrencyCode,
                ExpenseDate = item.ExpenseDate,
                ExpenseGroupName = item.ExpenseGroupName,
                SchemaKey = item.SchemaKey,
                Expense = item.Expense == null ? null : MapExpense(item.Expense),
                RawColumns = item.RawColumns == null ? null : new Dictionary<string, string>(item.RawColumns),
                ApprovalPayload = item.ApprovalPayload == null ? null : MapApprovalPayload(item.ApprovalPayload)
            }).ToList()
        });
    }

    [HttpPost("import-approve")]
    public async Task<ActionResult<ExpenseCsvImportResponseDto>> ApproveImportRows(
        [FromBody] ApproveExpenseImportRowsRequestDto request,
        CancellationToken ct)
    {
        if (request.Rows == null || request.Rows.Count == 0)
        {
            return BadRequest("At least one review row is required.");
        }

        var result = await _expenseService.ApproveImportedExpenseRowsAsync(
            new ApproveExpenseImportRowsRequest(
                request.SchemaKey ?? "approved-import-rows",
                request.SchemaDisplayName ?? "Approved Import Rows",
                request.Rows.Select(MapApprovalPayload).ToList()),
            ct);

        return Ok(new ExpenseCsvImportResponseDto
        {
            SchemaKey = result.SchemaKey,
            SchemaDisplayName = result.SchemaDisplayName,
            TotalRows = result.TotalRows,
            AddedCount = result.AddedCount,
            DuplicateCount = result.DuplicateCount,
            ReviewCount = result.ReviewCount,
            FailedCount = result.FailedCount,
            Items = result.Items.Select(item => new ExpenseCsvImportReportItemDto
            {
                RowNumber = item.RowNumber,
                Status = item.Status,
                Message = item.Message,
                MerchantName = item.MerchantName,
                Amount = item.Amount,
                CurrencyCode = item.CurrencyCode,
                ExpenseDate = item.ExpenseDate,
                ExpenseGroupName = item.ExpenseGroupName,
                SchemaKey = item.SchemaKey,
                Expense = item.Expense == null ? null : MapExpense(item.Expense),
                RawColumns = item.RawColumns == null ? null : new Dictionary<string, string>(item.RawColumns),
                ApprovalPayload = item.ApprovalPayload == null ? null : MapApprovalPayload(item.ApprovalPayload)
            }).ToList()
        });
    }

    private static ExpenseDashboardResponseDto MapDashboard(ExpenseDashboardSnapshot snapshot)
    {
        return new ExpenseDashboardResponseDto
        {
            Expenses = snapshot.Expenses.Select(MapExpense).ToList(),
            Groups = snapshot.Groups.Select(group => new ExpenseGroupDto
            {
                Id = group.Id,
                Key = group.Key,
                Name = group.Name,
                Color = group.Color,
                IsEnabled = group.IsEnabled,
                DisplayOrder = group.DisplayOrder
            }).ToList(),
            Settings = new ExpenseSettingsDto
            {
                ModelKey = snapshot.Settings.ModelKey,
                DefaultCurrencyCode = snapshot.Settings.DefaultCurrencyCode,
                ExtractionPrompt = snapshot.Settings.ExtractionPrompt
            },
            Models = snapshot.Models.Select(model => new ExpenseModelDto
            {
                Key = model.Key,
                Name = model.Name,
                ModelId = model.ModelId,
                ProviderName = model.ProviderName,
                IsDefault = model.IsDefault
            }).ToList(),
            Summary = new ExpenseSummaryDto
            {
                TotalAmount = snapshot.Summary.TotalAmount,
                ExpenseCount = snapshot.Summary.ExpenseCount,
                AverageAmount = snapshot.Summary.AverageAmount,
                LatestExpenseDate = snapshot.Summary.LatestExpenseDate,
                TotalsByGroup = snapshot.Summary.TotalsByGroup.Select(group => new ExpenseGroupTotalDto
                {
                    ExpenseGroupId = group.ExpenseGroupId,
                    Name = group.Name,
                    Color = group.Color,
                    TotalAmount = group.TotalAmount,
                    ExpenseCount = group.ExpenseCount
                }).ToList()
            }
        };
    }

    private static ExpenseDto MapExpense(ExpenseRecord expense)
    {
        return new ExpenseDto
        {
            Id = expense.Id,
            ExpenseGroupId = expense.ExpenseGroupId,
            ExpenseGroupName = expense.ExpenseGroupName,
            ExpenseGroupColor = expense.ExpenseGroupColor,
            SourceType = expense.SourceType,
            ExpenseDate = expense.ExpenseDate,
            TotalAmount = expense.TotalAmount,
            SubtotalAmount = expense.SubtotalAmount,
            TaxAmount = expense.TaxAmount,
            TipAmount = expense.TipAmount,
            CurrencyCode = expense.CurrencyCode,
            MerchantName = expense.MerchantName,
            Location = expense.Location,
            PaymentMethod = expense.PaymentMethod,
            Description = expense.Description,
            Notes = expense.Notes,
            ReceiptFileName = expense.ReceiptFileName,
            ModelKeyUsed = expense.ModelKeyUsed,
            AnalysisConfidence = expense.AnalysisConfidence,
            ReceiptText = expense.ReceiptText,
            IsWorkExpense = expense.IsWorkExpense,
            IsReimbursable = expense.IsReimbursable,
            CreatedAtUtc = expense.CreatedAtUtc,
            UpdatedAtUtc = expense.UpdatedAtUtc
        };
    }

    private static ExpenseDetailDto MapExpenseDetail(ExpenseDetailRecord detail)
    {
        return new ExpenseDetailDto
        {
            Expense = MapExpense(detail.Expense),
            RawExtractionJson = detail.RawExtractionJson,
            ReceiptImageDataUrl = detail.ReceiptImageDataUrl,
            ReceiptImageContentType = detail.ReceiptImageContentType,
            Items = detail.Items.Select(item => new ExpenseLineItemDto
            {
                Id = item.Id,
                Name = item.Name,
                CanonicalName = item.CanonicalName,
                Quantity = item.Quantity,
                Unit = item.Unit,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice
            }).ToList()
        };
    }

    private static ReceiptLineItemDto MapExtractionItem(ReceiptLineItemExtraction item) => new()
    {
        Name = item.Name,
        Quantity = item.Quantity,
        Unit = item.Unit,
        UnitPrice = item.UnitPrice,
        TotalPrice = item.TotalPrice
    };

    private static async Task<ReadOnlyMemory<byte>> ReadBytesAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is not { Length: > 0 })
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);
        return memory.ToArray();
    }

    private static async Task<ProcessedReceiptImage> ProcessReceiptImageAsync(IFormFile? file, CancellationToken ct)
    {
        var bytes = await ReadBytesAsync(file, ct);
        if (bytes.IsEmpty)
        {
            return ProcessedReceiptImage.Empty;
        }

        try
        {
            using var image = Image.Load(bytes.Span);
            const int maxDimension = 1600;
            var resizeRatio = Math.Min(
                maxDimension / (double)image.Width,
                maxDimension / (double)image.Height);

            if (resizeRatio < 1d)
            {
                var width = Math.Max(1, (int)Math.Round(image.Width * resizeRatio));
                var height = Math.Max(1, (int)Math.Round(image.Height * resizeRatio));
                image.Mutate(ctx => ctx.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(width, height),
                    Sampler = KnownResamplers.Lanczos3
                }));
            }

            image.Metadata.ExifProfile = null;

            using var output = new MemoryStream();
            await image.SaveAsJpegAsync(output, new JpegEncoder
            {
                Quality = 75
            }, ct);

            return new ProcessedReceiptImage(output.ToArray(), "image/jpeg");
        }
        catch (UnknownImageFormatException)
        {
            var fallbackContentType = string.IsNullOrWhiteSpace(file?.ContentType)
                ? null
                : file.ContentType.Trim();
            return new ProcessedReceiptImage(bytes, fallbackContentType);
        }
    }

    private static ExpenseImportApprovalPayloadDto MapApprovalPayload(ExpenseImportApprovalPayload payload) =>
        new()
        {
            RowNumber = payload.RowNumber,
            SourceType = payload.SourceType,
            ImportFileName = payload.ImportFileName,
            SchemaKey = payload.SchemaKey,
            ExpenseDate = payload.ExpenseDate,
            TotalAmount = payload.TotalAmount,
            CurrencyCode = payload.CurrencyCode,
            MerchantName = payload.MerchantName,
            Description = payload.Description,
            Notes = payload.Notes,
            PaymentMethod = payload.PaymentMethod,
            Location = payload.Location,
            SourceReference = payload.SourceReference,
            ExpenseGroupId = payload.ExpenseGroupId,
            ExpenseGroupName = payload.ExpenseGroupName,
            ModelKeyUsed = payload.ModelKeyUsed,
            AnalysisConfidence = payload.AnalysisConfidence,
            RawColumns = payload.RawColumns == null ? null : new Dictionary<string, string>(payload.RawColumns)
        };

    private static ExpenseImportApprovalPayload MapApprovalPayload(ExpenseImportApprovalPayloadDto payload) =>
        new(
            payload.RowNumber,
            payload.SourceType ?? ExpenseSourceTypes.CsvImport,
            payload.ImportFileName ?? "approved-import",
            payload.SchemaKey ?? string.Empty,
            payload.ExpenseDate,
            payload.TotalAmount,
            payload.CurrencyCode ?? "USD",
            payload.MerchantName,
            payload.Description,
            payload.Notes,
            payload.PaymentMethod,
            payload.Location,
            payload.SourceReference,
            payload.ExpenseGroupId,
            payload.ExpenseGroupName,
            payload.ModelKeyUsed,
            payload.AnalysisConfidence,
            payload.RawColumns == null ? null : new Dictionary<string, string>(payload.RawColumns));
}

internal readonly record struct ProcessedReceiptImage(
    ReadOnlyMemory<byte> Bytes,
    string? ContentType)
{
    public static ProcessedReceiptImage Empty => new(ReadOnlyMemory<byte>.Empty, null);
}

public sealed class ExpenseDashboardResponseDto
{
    public List<ExpenseDto> Expenses { get; set; } = [];
    public List<ExpenseGroupDto> Groups { get; set; } = [];
    public ExpenseSettingsDto Settings { get; set; } = new();
    public List<ExpenseModelDto> Models { get; set; } = [];
    public ExpenseSummaryDto Summary { get; set; } = new();
}

public sealed class ExpenseDto
{
    public int Id { get; set; }
    public int? ExpenseGroupId { get; set; }
    public string? ExpenseGroupName { get; set; }
    public string? ExpenseGroupColor { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public DateOnly ExpenseDate { get; set; }
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
    public string? ModelKeyUsed { get; set; }
    public decimal? AnalysisConfidence { get; set; }
    public string? ReceiptText { get; set; }
    public bool IsWorkExpense { get; set; }
    public bool IsReimbursable { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ExpenseDetailDto
{
    public ExpenseDto Expense { get; set; } = new();
    public string? RawExtractionJson { get; set; }
    public string? ReceiptImageDataUrl { get; set; }
    public string? ReceiptImageContentType { get; set; }
    public List<ExpenseLineItemDto> Items { get; set; } = [];
}

public sealed class ExpenseLineItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CanonicalName { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public sealed class ExpenseItemComparisonDto
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public int PurchaseCount { get; set; }
    public decimal? LowestPrice { get; set; }
    public string? LowestPriceMerchant { get; set; }
    public List<ExpenseItemPurchaseDto> Purchases { get; set; } = [];
}

public sealed class ExpenseItemPurchaseDto
{
    public int Id { get; set; }
    public int ExpenseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CanonicalName { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal? ComparablePrice { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public string? MerchantName { get; set; }
    public string? Location { get; set; }
    public DateOnly ExpenseDate { get; set; }
}

public sealed class ReclassifyExpenseItemRequestDto
{
    public string CanonicalName { get; set; } = string.Empty;
    public bool ApplyToMatching { get; set; } = true;
}

public sealed class ExpenseItemReclassificationDto
{
    public string CanonicalName { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public int UpdatedCount { get; set; }
}

public sealed class ExpenseGroupDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#2563eb";
    public bool IsEnabled { get; set; } = true;
    public int DisplayOrder { get; set; }
}

public sealed class ExpenseModelDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public sealed class ExpenseSettingsDto
{
    public string? ModelKey { get; set; }
    public string DefaultCurrencyCode { get; set; } = "USD";
    public string? ExtractionPrompt { get; set; }
}

public sealed class ExpenseSummaryDto
{
    public decimal TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
    public decimal AverageAmount { get; set; }
    public DateOnly? LatestExpenseDate { get; set; }
    public List<ExpenseGroupTotalDto> TotalsByGroup { get; set; } = [];
}

public sealed class ExpenseGroupTotalDto
{
    public int? ExpenseGroupId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#64748b";
    public decimal TotalAmount { get; set; }
    public int ExpenseCount { get; set; }
}

public sealed class SaveExpenseSettingsRequestDto
{
    public ExpenseSettingsDto Settings { get; set; } = new();
    public List<ExpenseGroupDto> Groups { get; set; } = [];
}

public sealed class CreateManualExpenseRequestDto
{
    public int? ExpenseGroupId { get; set; }
    public DateOnly? ExpenseDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? SubtotalAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? TipAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? MerchantName { get; set; }
    public string? Location { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
}

public sealed class AnalyzeExpenseFormDto
{
    public string? ModelKey { get; set; }
    public int? ExpenseGroupId { get; set; }
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
    public IFormFile? File { get; set; }
}

public sealed class AnalyzedExpenseResponseDto
{
    public ExpenseDto Expense { get; set; } = new();
    public ReceiptExtractionDto Extraction { get; set; } = new();
}

public sealed class ExpensePreviewResponseDto
{
    public int? ExpenseGroupId { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? SubtotalAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? TipAmount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public string? MerchantName { get; set; }
    public string? Location { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string? ReceiptText { get; set; }
    public string ModelKeyUsed { get; set; } = string.Empty;
    public ReceiptExtractionDto Extraction { get; set; } = new();
}

public sealed class UpdateExpenseRequestDto
{
    public int? ExpenseGroupId { get; set; }
    public DateOnly ExpenseDate { get; set; }
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
    public string? ReceiptText { get; set; }
    public bool IsWorkExpense { get; set; }
    public bool IsReimbursable { get; set; }
}

public sealed class ImportExpenseCsvFormDto
{
    public string? ModelKey { get; set; }
    public IFormFile? File { get; set; }
}

public sealed class ImportExpenseFetchRequestDto
{
    public string? ModelKey { get; set; }
    public string? RequestText { get; set; }
}

public sealed class ExpenseCsvImportResponseDto
{
    public string SchemaKey { get; set; } = string.Empty;
    public string SchemaDisplayName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int AddedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int ReviewCount { get; set; }
    public int FailedCount { get; set; }
    public List<ExpenseCsvImportReportItemDto> Items { get; set; } = [];
}

public sealed class ExpenseCsvImportReportItemDto
{
    public int RowNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? MerchantName { get; set; }
    public decimal? Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public DateOnly? ExpenseDate { get; set; }
    public string? ExpenseGroupName { get; set; }
    public string? SchemaKey { get; set; }
    public ExpenseDto? Expense { get; set; }
    public Dictionary<string, string>? RawColumns { get; set; }
    public ExpenseImportApprovalPayloadDto? ApprovalPayload { get; set; }
}

public sealed class ApproveExpenseImportRowsRequestDto
{
    public string? SchemaKey { get; set; }
    public string? SchemaDisplayName { get; set; }
    public List<ExpenseImportApprovalPayloadDto> Rows { get; set; } = [];
}

public sealed class ExpenseImportApprovalPayloadDto
{
    public int RowNumber { get; set; }
    public string? SourceType { get; set; }
    public string? ImportFileName { get; set; }
    public string? SchemaKey { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? MerchantName { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Location { get; set; }
    public string? SourceReference { get; set; }
    public int? ExpenseGroupId { get; set; }
    public string? ExpenseGroupName { get; set; }
    public string? ModelKeyUsed { get; set; }
    public decimal? AnalysisConfidence { get; set; }
    public Dictionary<string, string>? RawColumns { get; set; }
}

public sealed class ReceiptExtractionDto
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
    public List<ReceiptLineItemDto> Items { get; set; } = [];
}

public sealed class ReceiptLineItemDto
{
    public string? Name { get; set; }
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }
}
