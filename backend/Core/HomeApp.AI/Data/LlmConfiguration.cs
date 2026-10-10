namespace HomeApp.AI.Data;

public sealed class LlmConfiguration
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
