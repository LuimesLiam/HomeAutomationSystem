namespace HomeApp.AI;

public interface IHomeAppAgentFactory
{
    Task<HomeAppAgentBuildResult> CreateAsync(HomeAppAgentBuildRequest request, CancellationToken ct = default);
}
