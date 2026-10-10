using System.Text.Json;
using HomeApp.AI.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeApp.AI.Services;

public sealed class AiSettingsService : IHomeAppAgentModelCatalog
{
    private readonly HomeAppAiDbContext _dbContext;

    public AiSettingsService(HomeAppAiDbContext dbContext) => _dbContext = dbContext;

    public async Task<AiSettingsSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var llms = await _dbContext.Llms.AsNoTracking()
            .OrderByDescending(llm => llm.IsDefault)
            .ThenBy(llm => llm.Name)
            .ThenBy(llm => llm.Id)
            .ToListAsync(ct);
        return new AiSettingsSnapshot(llms);
    }

    public async Task<AiSettingsSnapshot> SaveAsync(AiSettingsUpdate update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        var normalizedLlms = NormalizeLlms(update.Llms).ToList();
        if (normalizedLlms.Count == 0)
        {
            throw new AiSettingsValidationException("At least one LLM configuration is required.");
        }

        var existingLlms = await _dbContext.Llms.OrderBy(llm => llm.Id).ToListAsync(ct);
        var existingById = existingLlms.ToDictionary(llm => llm.Id);
        var retainedIds = new HashSet<int>();
        var defaultAssigned = false;

        foreach (var llm in normalizedLlms)
        {
            var isDefault = llm.IsDefault && !defaultAssigned;
            defaultAssigned |= isDefault;

            if (llm.Id > 0 && existingById.TryGetValue(llm.Id, out var existing))
            {
                Copy(llm, existing, isDefault);
                retainedIds.Add(existing.Id);
                continue;
            }

            _dbContext.Llms.Add(new LlmConfiguration
            {
                Key = llm.Key,
                Name = llm.Name,
                ModelName = llm.ModelName,
                Provider = llm.Provider,
                BaseUrl = llm.BaseUrl,
                ApiKeyName = llm.ApiKeyName,
                ParamsJson = llm.ParamsJson,
                IsEnabled = llm.IsEnabled,
                IsDefault = isDefault
            });
        }

        foreach (var llm in existingLlms.Where(llm => !retainedIds.Contains(llm.Id)))
        {
            _dbContext.Llms.Remove(llm);
        }

        if (!defaultAssigned)
        {
            var fallback = normalizedLlms.FirstOrDefault(llm => llm.IsEnabled) ?? normalizedLlms[0];
            var trackedFallback = fallback.Id > 0 && existingById.TryGetValue(fallback.Id, out var existing)
                ? existing
                : _dbContext.Llms.Local.First(llm => llm.Key == fallback.Key);
            trackedFallback.IsDefault = true;
        }

        await _dbContext.SaveChangesAsync(ct);
        return await GetSnapshotAsync(ct);
    }

    public async Task<HomeAppAgentModelConfiguration> GetResolvedModelAsync(
        string? requestedModelKey,
        CancellationToken ct = default)
    {
        var models = _dbContext.Llms.AsNoTracking().Where(llm => llm.IsEnabled);
        LlmConfiguration? llm = null;

        if (!string.IsNullOrWhiteSpace(requestedModelKey))
        {
            llm = await models.FirstOrDefaultAsync(item => item.Key == requestedModelKey, ct);
        }

        llm ??= await models.OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Id)
            .FirstOrDefaultAsync(ct);

        if (llm == null)
        {
            throw new InvalidOperationException("No enabled LLM is configured. Add one on the AI settings page first.");
        }

        var parameters = ParseParams(llm.ParamsJson);
        return new HomeAppAgentModelConfiguration(
            llm.Id,
            llm.Key,
            llm.Name,
            NormalizeKey(llm.Provider, llm.Provider),
            llm.Provider,
            llm.ModelName,
            llm.BaseUrl,
            ResolveApiKey(llm.ApiKeyName),
            parameters.Temperature,
            parameters.MaxOutputTokens,
            llm.ParamsJson);
    }

    private static void Copy(LlmWriteModel source, LlmConfiguration target, bool isDefault)
    {
        target.Key = source.Key;
        target.Name = source.Name;
        target.ModelName = source.ModelName;
        target.Provider = source.Provider;
        target.BaseUrl = source.BaseUrl;
        target.ApiKeyName = source.ApiKeyName;
        target.ParamsJson = source.ParamsJson;
        target.IsEnabled = source.IsEnabled;
        target.IsDefault = isDefault;
    }

    private static string? ResolveApiKey(string apiKeyName)
    {
        if (string.IsNullOrWhiteSpace(apiKeyName))
        {
            return null;
        }

        var apiKey = Environment.GetEnvironmentVariable(apiKeyName)?.Trim();
        return !string.IsNullOrWhiteSpace(apiKey)
            ? apiKey
            : throw new InvalidOperationException(
                $"The API key environment variable '{apiKeyName}' is not configured.");
    }

    private static IEnumerable<LlmWriteModel> NormalizeLlms(IEnumerable<LlmWriteModel> llms)
    {
        if (llms is null) throw new AiSettingsValidationException("Provide an LLM configuration list.");
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenIds = new HashSet<int>();
        foreach (var llm in llms)
        {
            if (llm is null || string.IsNullOrWhiteSpace(llm.Name) ||
                string.IsNullOrWhiteSpace(llm.ModelName) ||
                string.IsNullOrWhiteSpace(llm.Provider))
            {
                throw new AiSettingsValidationException("Every LLM requires a name, model name and provider.");
            }

            if (llm.Id < 0 || (llm.Id > 0 && !seenIds.Add(llm.Id)))
                throw new AiSettingsValidationException("LLM IDs must be non-negative and unique.");

            var key = NormalizeKey(llm.Key, llm.Name);
            if (!seenKeys.Add(key))
            {
                throw new AiSettingsValidationException("LLM keys must be unique.");
            }

            var paramsJson = NormalizeOptional(llm.ParamsJson);
            _ = ParseParams(paramsJson);
            yield return llm with
            {
                Key = key,
                Name = llm.Name.Trim(),
                ModelName = llm.ModelName.Trim(),
                Provider = llm.Provider.Trim(),
                BaseUrl = NormalizeBaseUrl(llm.BaseUrl),
                ApiKeyName = llm.ApiKeyName?.Trim() ?? "",
                ParamsJson = paramsJson
            };
        }
    }

    private static LlmParams ParseParams(string? paramsJson)
    {
        if (string.IsNullOrWhiteSpace(paramsJson))
        {
            return new LlmParams(null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(paramsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new AiSettingsValidationException("LLM params must be a JSON object.");
            }

            var root = document.RootElement;
            double? temperature = null;
            int? maxOutputTokens = null;

            if (root.TryGetProperty("temperature", out var temperatureValue) &&
                temperatureValue.ValueKind != JsonValueKind.Null)
            {
                if (temperatureValue.ValueKind != JsonValueKind.Number ||
                    !temperatureValue.TryGetDouble(out var value) || !double.IsFinite(value))
                    throw new AiSettingsValidationException("temperature must be a finite number.");
                temperature = Math.Clamp(value, 0, 2);
            }

            if (root.TryGetProperty("maxOutputTokens", out var tokensValue) &&
                tokensValue.ValueKind != JsonValueKind.Null)
            {
                if (tokensValue.ValueKind != JsonValueKind.Number ||
                    !tokensValue.TryGetInt32(out var value) || value <= 0)
                    throw new AiSettingsValidationException("maxOutputTokens must be a positive 32-bit integer.");
                maxOutputTokens = value;
            }

            return new LlmParams(temperature, maxOutputTokens);
        }
        catch (JsonException exception)
        {
            throw new AiSettingsValidationException("LLM params must contain valid JSON.", exception);
        }
    }

    private static string NormalizeBaseUrl(string? value)
    {
        var trimmed = value?.Trim().TrimEnd('/') ?? "";
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new AiSettingsValidationException("Provide an absolute HTTP or HTTPS LLM base URL without credentials, a query or fragment.");
        }

        return $"{trimmed}/";
    }

    private static string NormalizeKey(string? requestedKey, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(requestedKey) ? fallback : requestedKey;
        var normalized = new string(source.Trim().ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? Guid.NewGuid().ToString("n") : normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}

public sealed record AiSettingsSnapshot(IReadOnlyList<LlmConfiguration> Llms);
public sealed record AiSettingsUpdate(IReadOnlyList<LlmWriteModel> Llms);

public sealed record LlmWriteModel(
    int Id,
    string Key,
    string Name,
    string ModelName,
    string Provider,
    string BaseUrl,
    string ApiKeyName,
    string? ParamsJson,
    bool IsEnabled,
    bool IsDefault);

internal sealed record LlmParams(double? Temperature, int? MaxOutputTokens);

public sealed class AiSettingsValidationException(string message, Exception? innerException = null)
    : ArgumentException(message, innerException);
