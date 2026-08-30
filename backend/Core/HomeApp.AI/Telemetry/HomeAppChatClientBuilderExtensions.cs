using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace HomeApp.AI;

public static class HomeAppChatClientBuilderExtensions
{
    public static ChatClientBuilder UseHomeAppTelemetry(
        this ChatClientBuilder builder,
        HomeAppAgentModelConfiguration model,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        return builder
            .UseOpenTelemetry(loggerFactory, HomeAppTelemetry.ActivitySourceName, _ => { })
            .UseHomeAppTokenMetrics(model);
    }

    public static ChatClientBuilder UseHomeAppTokenMetrics(
        this ChatClientBuilder builder,
        HomeAppAgentModelConfiguration model)
    {
        static async IAsyncEnumerable<ChatResponseUpdate> RecordStreamingMetricsAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options,
            IChatClient inner,
            HomeAppAgentModelConfiguration model,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var recorded = false;

            await foreach (var update in inner.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                if (!recorded && HomeAppTokenMetrics.TryRecord(model, update))
                {
                    recorded = true;
                }

                yield return update;
            }
        }

        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(model);

        return builder.Use(async (messages, options, inner, cancellationToken) =>
            {
                var response = await inner.GetResponseAsync(messages, options, cancellationToken);
                HomeAppTokenMetrics.Record(model, response);
                return response;
            },
            (messages, options, inner, cancellationToken) =>
                RecordStreamingMetricsAsync(messages, options, inner, model, cancellationToken));
    }
}
