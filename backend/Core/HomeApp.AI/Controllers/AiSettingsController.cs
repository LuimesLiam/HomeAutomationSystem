using HomeApp.AI.Services;
using HomeApp.AI.Data;
using Microsoft.AspNetCore.Mvc;

namespace HomeApp.AI.Controllers;

[ApiController]
[Route("api/settings/ai")]
public sealed class AiSettingsController : ControllerBase
{
    private readonly AiSettingsService _aiSettingsService;

    public AiSettingsController(AiSettingsService aiSettingsService)
    {
        _aiSettingsService = aiSettingsService;
    }

    [HttpGet]
    public async Task<ActionResult<AiSettingsResponseDto>> Get(CancellationToken ct)
    {
        var snapshot = await _aiSettingsService.GetSnapshotAsync(ct);
        return Ok(MapResponse(snapshot));
    }

    [HttpPut]
    public async Task<ActionResult<AiSettingsResponseDto>> Save([FromBody] AiSettingsSaveRequestDto request, CancellationToken ct)
    {
        var snapshot = await _aiSettingsService.SaveAsync(
            new AiSettingsUpdate(
                request.Providers.Select(provider => new AiProviderWriteModel(
                    provider.Id,
                    provider.Key,
                    provider.Name,
                    provider.ProviderType,
                    provider.BaseUrl,
                    provider.ApiKeyEnvironmentVariableName,
                    provider.IsEnabled,
                    provider.ConfigurationJson)).ToList(),
                request.Models.Select(model => new AiModelWriteModel(
                    model.Id,
                    model.Key,
                    model.Name,
                    model.ProviderKey,
                    model.ModelId,
                    model.IsEnabled,
                    model.IsDefault,
                    model.Temperature,
                    model.MaxOutputTokens,
                    model.ConfigurationJson)).ToList()),
            ct);

        return Ok(MapResponse(snapshot));
    }

    private static AiSettingsResponseDto MapResponse(AiSettingsSnapshot snapshot)
    {
        return new AiSettingsResponseDto
        {
            Providers = snapshot.Providers
                .Select(provider => new AiProviderDto
                {
                    Id = provider.Id,
                    Key = provider.Key,
                    Name = provider.Name,
                    ProviderType = provider.ProviderType,
                    BaseUrl = provider.BaseUrl,
                    ApiKeyEnvironmentVariableName = provider.ApiKeyEnvironmentVariableName,
                    IsEnabled = provider.IsEnabled,
                    ConfigurationJson = provider.ConfigurationJson
                })
                .OrderBy(provider => provider.Name)
                .ThenBy(provider => provider.Id)
                .ToList(),
            Models = snapshot.Models
                .Select(model => new AiModelDto
                {
                    Id = model.Id,
                    Key = model.Key,
                    Name = model.Name,
                    ProviderKey = snapshot.Providers.First(provider => provider.Id == model.ProviderId).Key,
                    ModelId = model.ModelId,
                    IsEnabled = model.IsEnabled,
                    IsDefault = model.IsDefault,
                    Temperature = model.Temperature,
                    MaxOutputTokens = model.MaxOutputTokens,
                    ConfigurationJson = model.ConfigurationJson
                })
                .OrderBy(model => model.Name)
                .ThenBy(model => model.Id)
                .ToList(),
            ProviderTypes = AiSettingsService.ProviderTemplates
                .Select(template => new AiProviderTypeDto
                {
                    Value = template.ProviderType,
                    Key = template.Key,
                    Name = template.Name,
                    DefaultBaseUrl = template.DefaultBaseUrl,
                    DefaultApiKeyEnvironmentVariableName = template.DefaultApiKeyEnvironmentVariableName,
                    Description = template.Description
                })
                .ToList()
        };
    }
}

public sealed class AiSettingsResponseDto
{
    public List<AiProviderDto> Providers { get; set; } = [];
    public List<AiModelDto> Models { get; set; } = [];
    public List<AiProviderTypeDto> ProviderTypes { get; set; } = [];
}

public sealed class AiSettingsSaveRequestDto
{
    public List<AiProviderDto> Providers { get; set; } = [];
    public List<AiModelDto> Models { get; set; } = [];
}

public sealed class AiProviderDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AiProviderType ProviderType { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKeyEnvironmentVariableName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string? ConfigurationJson { get; set; }
}

public sealed class AiModelDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; }
    public double? Temperature { get; set; }
    public int? MaxOutputTokens { get; set; }
    public string? ConfigurationJson { get; set; }
}

public sealed class AiProviderTypeDto
{
    public AiProviderType Value { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DefaultBaseUrl { get; set; } = string.Empty;
    public string DefaultApiKeyEnvironmentVariableName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
