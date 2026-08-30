using Microsoft.Agents.AI;

namespace HomeApp.AI;

public sealed record HomeAppAgentBuildResult(
    ChatClientAgent Agent,
    HomeAppAgentModelConfiguration Model);
