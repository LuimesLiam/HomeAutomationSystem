namespace HomeApp.AI;

public interface IHomeAppAgentModelCatalog
{
    Task<HomeAppAgentModelConfiguration> GetResolvedModelAsync(string? requestedModelKey, CancellationToken ct = default);
}
