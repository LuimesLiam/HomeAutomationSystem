using Microsoft.Agents.AI;

namespace HomeApp.AI;

public sealed record HomeAppAgentBuildResult(
    AIAgent Agent,
    HomeAppAgentModelConfiguration Model);
