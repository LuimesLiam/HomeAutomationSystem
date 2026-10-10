using HomeApp.AI.Services;
using Microsoft.AspNetCore.Mvc;

namespace HomeApp.AI.Controllers;

[ApiController]
[Route("api/settings/ai")]
public sealed class AiSettingsController : ControllerBase
{
    private readonly AiSettingsService _aiSettingsService;

    public AiSettingsController(AiSettingsService aiSettingsService) => _aiSettingsService = aiSettingsService;

    [HttpGet]
    public async Task<ActionResult<AiSettingsResponseDto>> Get(CancellationToken ct) =>
        Ok(MapResponse(await _aiSettingsService.GetSnapshotAsync(ct)));

    [HttpPut]
    public async Task<ActionResult<AiSettingsResponseDto>> Save(
        [FromBody] AiSettingsSaveRequestDto request,
        CancellationToken ct)
    {
        if (request.Llms is null || request.Llms.Any(llm => llm is null))
            return BadRequest(new { message = "Provide an LLM configuration list with valid entries." });
        try
        {
            var snapshot = await _aiSettingsService.SaveAsync(
                new AiSettingsUpdate(request.Llms.Select(llm => new LlmWriteModel(
                    llm.Id, llm.Key, llm.Name, llm.ModelName, llm.Provider, llm.BaseUrl,
                    llm.ApiKeyName, llm.ParamsJson, llm.IsEnabled, llm.IsDefault)).ToList()), ct);
            return Ok(MapResponse(snapshot));
        }
        catch (AiSettingsValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private static AiSettingsResponseDto MapResponse(AiSettingsSnapshot snapshot) => new()
    {
        Llms = snapshot.Llms.Select(llm => new LlmDto
        {
            Id = llm.Id,
            Key = llm.Key,
            Name = llm.Name,
            ModelName = llm.ModelName,
            Provider = llm.Provider,
            BaseUrl = llm.BaseUrl,
            ApiKeyName = llm.ApiKeyName,
            ParamsJson = llm.ParamsJson,
            IsEnabled = llm.IsEnabled,
            IsDefault = llm.IsDefault
        }).ToList()
    };
}

public sealed class AiSettingsResponseDto
{
    public List<LlmDto> Llms { get; set; } = [];
}

public sealed class AiSettingsSaveRequestDto
{
    public List<LlmDto> Llms { get; set; } = [];
}

public sealed class LlmDto
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKeyName { get; set; } = string.Empty;
    public string? ParamsJson { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; }
}
