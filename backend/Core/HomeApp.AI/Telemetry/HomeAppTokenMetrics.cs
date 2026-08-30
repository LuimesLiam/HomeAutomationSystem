using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Prometheus;

namespace HomeApp.AI;

internal static class HomeAppTokenMetrics
{
    private static readonly Counter Requests = Metrics.CreateCounter(
        "homeapp_ai_requests_total",
        "Total AI chat requests grouped by provider and model.",
        new CounterConfiguration
        {
            LabelNames = ["provider_key", "model_key", "model_id"]
        });

    private static readonly Counter InputTokens = Metrics.CreateCounter(
        "homeapp_ai_input_tokens_total",
        "Total AI input tokens grouped by provider and model.",
        new CounterConfiguration
        {
            LabelNames = ["provider_key", "model_key", "model_id"]
        });

    private static readonly Counter OutputTokens = Metrics.CreateCounter(
        "homeapp_ai_output_tokens_total",
        "Total AI output tokens grouped by provider and model.",
        new CounterConfiguration
        {
            LabelNames = ["provider_key", "model_key", "model_id"]
        });

    private static readonly Counter TotalTokens = Metrics.CreateCounter(
        "homeapp_ai_tokens_total",
        "Total AI tokens grouped by provider and model.",
        new CounterConfiguration
        {
            LabelNames = ["provider_key", "model_key", "model_id"]
        });

    public static void Record(HomeAppAgentModelConfiguration configuredModel, ChatResponse response)
    {
        ArgumentNullException.ThrowIfNull(configuredModel);
        ArgumentNullException.ThrowIfNull(response);

        var modelId = string.IsNullOrWhiteSpace(response.ModelId)
            ? configuredModel.RemoteModelId
            : response.ModelId!;

        var labels = new[]
        {
            configuredModel.ProviderKey,
            configuredModel.ModelKey,
            modelId
        };

        RecordUsage(
            labels,
            response.Usage?.InputTokenCount,
            response.Usage?.OutputTokenCount,
            response.Usage?.TotalTokenCount);
    }

    public static bool TryRecord(HomeAppAgentModelConfiguration configuredModel, ChatResponseUpdate update)
    {
        ArgumentNullException.ThrowIfNull(configuredModel);
        ArgumentNullException.ThrowIfNull(update);

        if (update.RawRepresentation is not StreamingChatCompletionUpdate rawUpdate || rawUpdate.Usage is null)
        {
            return false;
        }

        var modelId = string.IsNullOrWhiteSpace(update.ModelId)
            ? configuredModel.RemoteModelId
            : update.ModelId!;

        var labels = new[]
        {
            configuredModel.ProviderKey,
            configuredModel.ModelKey,
            modelId
        };

        RecordUsage(
            labels,
            rawUpdate.Usage.InputTokenCount,
            rawUpdate.Usage.OutputTokenCount,
            rawUpdate.Usage.TotalTokenCount);

        return true;
    }

    private static void RecordUsage(
        string[] labels,
        long? inputTokenCount,
        long? outputTokenCount,
        long? totalTokenCount)
    {
        Requests.WithLabels(labels).Inc();

        if (inputTokenCount is > 0)
        {
            InputTokens.WithLabels(labels).Inc(inputTokenCount.Value);
        }

        if (outputTokenCount is > 0)
        {
            OutputTokens.WithLabels(labels).Inc(outputTokenCount.Value);
        }

        if (totalTokenCount is > 0)
        {
            TotalTokens.WithLabels(labels).Inc(totalTokenCount.Value);
            return;
        }

        var fallbackTotal = (inputTokenCount ?? 0) + (outputTokenCount ?? 0);
        if (fallbackTotal > 0)
        {
            TotalTokens.WithLabels(labels).Inc(fallbackTotal);
        }
    }
}
