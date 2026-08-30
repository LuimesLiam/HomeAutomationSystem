using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace HomeApp.AI;

public abstract class HomeAppAgentBase
{
    private readonly IHomeAppAgentFactory _agentFactory;

    protected HomeAppAgentBase(IHomeAppAgentFactory agentFactory)
    {
        _agentFactory = agentFactory;
    }

    protected virtual string Name => GetType().Name;
    protected virtual string Description => string.Empty;
    protected abstract string Instructions { get; }
    protected virtual string? ModelKeyOverride => null;
    protected virtual IReadOnlyList<AITool> Tools => [];

    protected Task<HomeAppAgentResult> RunAsync(string prompt, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        return RunAsync([new ChatMessage(ChatRole.User, prompt)], ct);
    }

    protected async Task<HomeAppAgentResult> RunAsync(IEnumerable<ChatMessage> messages, CancellationToken ct = default)
    {
        var build = await _agentFactory.CreateAsync(
            new HomeAppAgentBuildRequest
            {
                Name = Name,
                Description = Description,
                Instructions = Instructions,
                ModelKeyOverride = ModelKeyOverride,
                Tools = Tools
            },
            ct);

        var response = await build.Agent.RunAsync(
            messages,
            session: null,
            options: null,
            cancellationToken: ct);

        return new HomeAppAgentResult(
            response.Text ?? string.Empty,
            build.Model.ModelKey,
            build.Model.ModelName,
            build.Model.ProviderKey,
            response);
    }
}
