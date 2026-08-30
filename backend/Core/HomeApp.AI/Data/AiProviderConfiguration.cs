namespace HomeApp.AI.Data;

public sealed class AiProviderConfiguration
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AiProviderType ProviderType { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKeyEnvironmentVariableName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string? ConfigurationJson { get; set; }
    public List<AiModelConfiguration> Models { get; set; } = [];
}
