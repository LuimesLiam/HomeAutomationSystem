namespace HomeApp.AI.Data;

public sealed class AiModelConfiguration
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public AiProviderConfiguration? Provider { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; }
    public double? Temperature { get; set; }
    public int? MaxOutputTokens { get; set; }
    public string? ConfigurationJson { get; set; }
}
