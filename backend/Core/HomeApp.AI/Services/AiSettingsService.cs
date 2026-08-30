using HomeApp.AI;
using HomeApp.AI.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeApp.AI.Services;

public sealed class AiSettingsService : IHomeAppAgentModelCatalog
{
    public static readonly IReadOnlyList<AiProviderTemplate> ProviderTemplates =
    [
        new(
            AiProviderType.OpenAI,
            "openai",
            "OpenAI",
            "https://api.openai.com/v1/",
            "OPENAI_API_KEY",
            "OpenAI-hosted models"),
        new(
            AiProviderType.Gemini,
            "gemini",
            "Gemini",
            "https://generativelanguage.googleapis.com/v1beta/openai/",
            "GEMINI_API_KEY",
            "Gemini via the OpenAI-compatible API"),
        new(
            AiProviderType.Ollama,
            "ollama",
            "Ollama",
            "http://localhost:11434/v1/",
            string.Empty,
            "Local Ollama models through the OpenAI-compatible endpoint")
    ];

    private readonly HomeAppAiDbContext _dbContext;

    public AiSettingsService(HomeAppAiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnsureSeededAsync(CancellationToken ct = default)
    {
        if (await _dbContext.AiProviders.AnyAsync(ct) || await _dbContext.AiModels.AnyAsync(ct))
        {
            return;
        }

        var providerEntities = ProviderTemplates
            .Select(template => new AiProviderConfiguration
            {
                Key = template.Key,
                Name = template.Name,
                ProviderType = template.ProviderType,
                BaseUrl = template.DefaultBaseUrl,
                ApiKeyEnvironmentVariableName = template.DefaultApiKeyEnvironmentVariableName,
                IsEnabled = true
            })
            .ToList();

        _dbContext.AiProviders.AddRange(providerEntities);
        await _dbContext.SaveChangesAsync(ct);

        var providersByKey = providerEntities.ToDictionary(provider => provider.Key, StringComparer.OrdinalIgnoreCase);
        _dbContext.AiModels.AddRange(
        [
            new AiModelConfiguration
            {
                ProviderId = providersByKey["openai"].Id,
                Key = "openai-gpt-4o-mini",
                Name = "GPT-4o mini",
                ModelId = "gpt-4o-mini",
                IsEnabled = true,
                IsDefault = true,
                Temperature = 0.2,
                MaxOutputTokens = 1200
            },
            new AiModelConfiguration
            {
                ProviderId = providersByKey["gemini"].Id,
                Key = "gemini-2-5-flash",
                Name = "Gemini 2.5 Flash",
                ModelId = "gemini-2.5-flash",
                IsEnabled = true,
                Temperature = 0.2,
                MaxOutputTokens = 1200
            },
            new AiModelConfiguration
            {
                ProviderId = providersByKey["ollama"].Id,
                Key = "ollama-llama3-2",
                Name = "Llama 3.2",
                ModelId = "llama3.2",
                IsEnabled = true,
                Temperature = 0.2,
                MaxOutputTokens = 1200
            }
        ]);

        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<AiSettingsSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var providers = await _dbContext.AiProviders
            .AsNoTracking()
            .OrderBy(provider => provider.Name)
            .ThenBy(provider => provider.Id)
            .ToListAsync(ct);

        var models = await _dbContext.AiModels
            .AsNoTracking()
            .OrderBy(model => model.Name)
            .ThenBy(model => model.Id)
            .ToListAsync(ct);

        return new AiSettingsSnapshot(providers, models);
    }

    public async Task<AiSettingsSnapshot> SaveAsync(AiSettingsUpdate update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var normalizedProviders = NormalizeProviders(update.Providers).ToList();
        var normalizedModels = NormalizeModels(update.Models).ToList();

        if (normalizedProviders.Count == 0)
        {
            throw new InvalidOperationException("At least one AI provider configuration is required.");
        }

        var existingProviders = await _dbContext.AiProviders
            .OrderBy(provider => provider.Id)
            .ToListAsync(ct);
        var existingProvidersById = existingProviders.ToDictionary(provider => provider.Id);
        var retainedProviderIds = new HashSet<int>();
        var providerIdMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in normalizedProviders)
        {
            if (provider.Id > 0 && existingProvidersById.TryGetValue(provider.Id, out var existing))
            {
                existing.Key = provider.Key;
                existing.Name = provider.Name;
                existing.ProviderType = provider.ProviderType;
                existing.BaseUrl = provider.BaseUrl;
                existing.ApiKeyEnvironmentVariableName = provider.ApiKeyEnvironmentVariableName;
                existing.IsEnabled = provider.IsEnabled;
                existing.ConfigurationJson = provider.ConfigurationJson;
                retainedProviderIds.Add(existing.Id);
                providerIdMap[provider.Key] = existing.Id;
            }
            else
            {
                var created = new AiProviderConfiguration
                {
                    Key = provider.Key,
                    Name = provider.Name,
                    ProviderType = provider.ProviderType,
                    BaseUrl = provider.BaseUrl,
                    ApiKeyEnvironmentVariableName = provider.ApiKeyEnvironmentVariableName,
                    IsEnabled = provider.IsEnabled,
                    ConfigurationJson = provider.ConfigurationJson
                };

                _dbContext.AiProviders.Add(created);
                await _dbContext.SaveChangesAsync(ct);
                retainedProviderIds.Add(created.Id);
                providerIdMap[provider.Key] = created.Id;
            }
        }

        var existingModels = await _dbContext.AiModels
            .OrderBy(model => model.Id)
            .ToListAsync(ct);
        var existingModelsById = existingModels.ToDictionary(model => model.Id);
        var retainedModelIds = new HashSet<int>();
        var defaultAssigned = false;

        foreach (var model in normalizedModels)
        {
            if (!providerIdMap.TryGetValue(model.ProviderKey, out var providerId))
            {
                throw new InvalidOperationException($"Model '{model.Name}' references unknown provider key '{model.ProviderKey}'.");
            }

            var isDefault = model.IsDefault && !defaultAssigned;
            defaultAssigned |= isDefault;

            if (model.Id > 0 && existingModelsById.TryGetValue(model.Id, out var existing))
            {
                existing.ProviderId = providerId;
                existing.Key = model.Key;
                existing.Name = model.Name;
                existing.ModelId = model.ModelId;
                existing.IsEnabled = model.IsEnabled;
                existing.IsDefault = isDefault;
                existing.Temperature = model.Temperature;
                existing.MaxOutputTokens = model.MaxOutputTokens;
                existing.ConfigurationJson = model.ConfigurationJson;
                retainedModelIds.Add(existing.Id);
            }
            else
            {
                _dbContext.AiModels.Add(new AiModelConfiguration
                {
                    ProviderId = providerId,
                    Key = model.Key,
                    Name = model.Name,
                    ModelId = model.ModelId,
                    IsEnabled = model.IsEnabled,
                    IsDefault = isDefault,
                    Temperature = model.Temperature,
                    MaxOutputTokens = model.MaxOutputTokens,
                    ConfigurationJson = model.ConfigurationJson
                });
            }
        }

        foreach (var provider in existingProviders.Where(provider => !retainedProviderIds.Contains(provider.Id)))
        {
            _dbContext.AiProviders.Remove(provider);
        }

        foreach (var model in existingModels.Where(model => !retainedModelIds.Contains(model.Id)))
        {
            _dbContext.AiModels.Remove(model);
        }

        await _dbContext.SaveChangesAsync(ct);

        if (!defaultAssigned)
        {
            var fallback = await _dbContext.AiModels
                .OrderByDescending(model => model.IsEnabled)
                .ThenBy(model => model.Id)
                .FirstOrDefaultAsync(ct);

            if (fallback != null)
            {
                fallback.IsDefault = true;
                await _dbContext.SaveChangesAsync(ct);
            }
        }

        return await GetSnapshotAsync(ct);
    }

    public async Task<HomeAppAgentModelConfiguration> GetResolvedModelAsync(string? requestedModelKey, CancellationToken ct = default)
    {
        var models = _dbContext.AiModels
            .AsNoTracking()
            .Include(model => model.Provider)
            .Where(model => model.IsEnabled && model.Provider != null && model.Provider.IsEnabled);

        AiModelConfiguration? model = null;

        if (!string.IsNullOrWhiteSpace(requestedModelKey))
        {
            model = await models.FirstOrDefaultAsync(item => item.Key == requestedModelKey, ct);
        }

        model ??= await models
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Id)
            .FirstOrDefaultAsync(ct);

        if (model?.Provider == null)
        {
            throw new InvalidOperationException("No enabled AI model is configured. Update the AI settings page first.");
        }

        return new HomeAppAgentModelConfiguration(
            model.Id,
            model.Key,
            model.Name,
            model.Provider.Key,
            model.Provider.Name,
            model.Provider.ProviderType,
            model.ModelId,
            model.Provider.BaseUrl,
            ResolveApiKey(model.Provider),
            model.Temperature,
            model.MaxOutputTokens,
            model.Provider.ConfigurationJson,
            model.ConfigurationJson);
    }

    private static string? ResolveApiKey(AiProviderConfiguration provider)
    {
        if (string.IsNullOrWhiteSpace(provider.ApiKeyEnvironmentVariableName))
        {
            return null;
        }

        return Environment.GetEnvironmentVariable(provider.ApiKeyEnvironmentVariableName)?.Trim();
    }

    private static IEnumerable<AiProviderWriteModel> NormalizeProviders(IEnumerable<AiProviderWriteModel> providers)
    {
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in providers)
        {
            var key = NormalizeKey(provider.Key, provider.Name);
            if (!seenKeys.Add(key))
            {
                continue;
            }

            yield return provider with
            {
                Key = key,
                Name = provider.Name.Trim(),
                BaseUrl = NormalizeBaseUrl(provider.BaseUrl),
                ApiKeyEnvironmentVariableName = provider.ApiKeyEnvironmentVariableName.Trim(),
                ConfigurationJson = NormalizeOptional(provider.ConfigurationJson)
            };
        }
    }

    private static IEnumerable<AiModelWriteModel> NormalizeModels(IEnumerable<AiModelWriteModel> models)
    {
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in models)
        {
            if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.ModelId))
            {
                continue;
            }

            var key = NormalizeKey(model.Key, model.Name);
            if (!seenKeys.Add(key))
            {
                continue;
            }

            yield return model with
            {
                Key = key,
                Name = model.Name.Trim(),
                ProviderKey = NormalizeKey(model.ProviderKey, model.ProviderKey),
                ModelId = model.ModelId.Trim(),
                Temperature = NormalizeTemperature(model.Temperature),
                MaxOutputTokens = model.MaxOutputTokens is > 0 ? model.MaxOutputTokens : null,
                ConfigurationJson = NormalizeOptional(model.ConfigurationJson)
            };
        }
    }

    private static string NormalizeBaseUrl(string value)
    {
        var trimmed = value.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Provider base URL is required.");
        }

        return $"{trimmed}/";
    }

    private static string NormalizeKey(string? requestedKey, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(requestedKey) ? fallback : requestedKey;
        var normalized = new string(source
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray())
            .Trim('-');

        return string.IsNullOrWhiteSpace(normalized)
            ? Guid.NewGuid().ToString("n")
            : normalized;
    }

    private static double? NormalizeTemperature(double? temperature)
    {
        if (temperature is null)
        {
            return null;
        }

        return Math.Clamp(temperature.Value, 0, 2);
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}

public sealed record AiSettingsSnapshot(
    IReadOnlyList<AiProviderConfiguration> Providers,
    IReadOnlyList<AiModelConfiguration> Models);

public sealed record AiSettingsUpdate(
    IReadOnlyList<AiProviderWriteModel> Providers,
    IReadOnlyList<AiModelWriteModel> Models);

public sealed record AiProviderWriteModel(
    int Id,
    string Key,
    string Name,
    AiProviderType ProviderType,
    string BaseUrl,
    string ApiKeyEnvironmentVariableName,
    bool IsEnabled,
    string? ConfigurationJson);

public sealed record AiModelWriteModel(
    int Id,
    string Key,
    string Name,
    string ProviderKey,
    string ModelId,
    bool IsEnabled,
    bool IsDefault,
    double? Temperature,
    int? MaxOutputTokens,
    string? ConfigurationJson);

public sealed record AiProviderTemplate(
    AiProviderType ProviderType,
    string Key,
    string Name,
    string DefaultBaseUrl,
    string DefaultApiKeyEnvironmentVariableName,
    string Description);
