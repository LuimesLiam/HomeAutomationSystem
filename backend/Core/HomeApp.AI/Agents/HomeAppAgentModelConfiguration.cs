using HomeApp.AI.Data;

namespace HomeApp.AI;

public sealed record HomeAppAgentModelConfiguration(
    int ModelId,
    string ModelKey,
    string ModelName,
    string ProviderKey,
    string ProviderName,
    AiProviderType ProviderType,
    string RemoteModelId,
    string BaseUrl,
    string? ApiKey,
    double? Temperature,
    int? MaxOutputTokens,
    string? ProviderConfigurationJson,
    string? ModelConfigurationJson);
