using Microsoft.Extensions.AI;

namespace HomeApp.AI;

public sealed class HomeAppAgentBuildRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Instructions { get; init; } = string.Empty;
    public string? ModelKeyOverride { get; init; }
    public IReadOnlyList<AITool> Tools { get; init; } = [];
}
