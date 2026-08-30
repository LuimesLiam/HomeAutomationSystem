using Microsoft.Agents.AI;

namespace HomeApp.AI;

public sealed record HomeAppAgentResult(
    string Text,
    string ModelKey,
    string ModelName,
    string ProviderKey,
    AgentResponse RawResponse);
