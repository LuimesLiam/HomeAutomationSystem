using System.ClientModel;
using HomeApp.AI.Data;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace HomeApp.AI;

public sealed class HomeAppAgentFactory : IHomeAppAgentFactory
{
    private readonly IHomeAppAgentModelCatalog _modelCatalog;
    private readonly IServiceProvider _services;
    private readonly ILoggerFactory _loggerFactory;

    public HomeAppAgentFactory(
        IHomeAppAgentModelCatalog modelCatalog,
        IServiceProvider services,
        ILoggerFactory loggerFactory)
    {
        _modelCatalog = modelCatalog;
        _services = services;
        _loggerFactory = loggerFactory;
    }

    public async Task<HomeAppAgentBuildResult> CreateAsync(HomeAppAgentBuildRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var model = await _modelCatalog.GetResolvedModelAsync(request.ModelKeyOverride, ct);
        var chatClient = CreateChatClient(model);

        var chatOptions = new ChatOptions
        {
            Instructions = request.Instructions,
            Temperature = model.Temperature is null ? null : (float)model.Temperature.Value,
            MaxOutputTokens = model.MaxOutputTokens,
            Tools = [.. request.Tools]
        };

        var agent = chatClient.AsAIAgent(
            new ChatClientAgentOptions
            {
                Name = request.Name,
                Description = request.Description,
                ChatOptions = chatOptions
            },
            clientFactory: client => new ChatClientBuilder(client)
                .UseHomeAppTelemetry(model, _loggerFactory)
                .UseFunctionInvocation()
                .Build(),
            loggerFactory: _loggerFactory,
            services: _services);

        return new HomeAppAgentBuildResult(agent, model);
    }

    private static ChatClient CreateChatClient(HomeAppAgentModelConfiguration model)
    {
        var apiKey = ResolveApiKey(model);
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(model.BaseUrl, UriKind.Absolute)
        };

        return new ChatClient(model.RemoteModelId, new ApiKeyCredential(apiKey), options);
    }

    private static string ResolveApiKey(HomeAppAgentModelConfiguration model)
    {
        if (!string.IsNullOrWhiteSpace(model.ApiKey))
        {
            return model.ApiKey;
        }

        return model.ProviderType == AiProviderType.Ollama
            ? "ollama"
            : throw new InvalidOperationException(
                $"Provider '{model.ProviderName}' requires an API key environment variable to be configured.");
    }
}
