using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using HomeApp.AI;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HomeApp.AI.Tests;

public sealed class AiTelemetryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FactoryExportsAgentModelAndToolSpansWithUsageMetricsInOneTrace(bool streaming)
    {
        var spans = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is HomeAppTelemetry.ActivitySourceName or HomeAppTelemetry.AgentSourceName
                || source.Name.StartsWith("Experimental.Microsoft.Extensions.AI"),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => spans.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);
        var measurements = new ConcurrentBag<(string Meter, string Name)>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Meter.Name.StartsWith("HomeApp.")) current.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<int>((instrument, value, tags, state) => measurements.Add((instrument.Meter.Name, instrument.Name)));
        meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => measurements.Add((instrument.Meter.Name, instrument.Name)));
        meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => measurements.Add((instrument.Meter.Name, instrument.Name)));
        meterListener.Start();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
        await using var server = builder.Build();
        var calls = 0;
        server.MapPost("/v1/chat/completions", async (HttpContext http) =>
        {
            var first = Interlocked.Increment(ref calls) == 1;
            using var request = await JsonDocument.ParseAsync(http.Request.Body);
            var useStream = request.RootElement.TryGetProperty("stream", out var value) && value.GetBoolean();
            if (useStream)
            {
                http.Response.ContentType = "text/event-stream";
                object delta = first
                    ? new { role = "assistant", tool_calls = new[] { new { index = 0, id = "call_1", type = "function", function = new { name = "TestTool", arguments = "{}" } } } }
                    : new { role = "assistant", content = "done" };
                var start = new { id = "test", @object = "chat.completion.chunk", created = 1, model = "test-model",
                    choices = new[] { new { index = 0, delta, finish_reason = (string?)null } } };
                var finish = new { id = "test", @object = "chat.completion.chunk", created = 1, model = "test-model",
                    choices = new[] { new { index = 0, delta = new { }, finish_reason = first ? "tool_calls" : "stop" } } };
                var usage = new { id = "test", @object = "chat.completion.chunk", created = 1, model = "test-model",
                    choices = Array.Empty<object>(), usage = new { prompt_tokens = 10, completion_tokens = 2, total_tokens = 12 } };
                foreach (var chunk in new object[] { start, finish, usage })
                    await http.Response.WriteAsync("data: " + JsonSerializer.Serialize(chunk) + "\n\n");
                await http.Response.WriteAsync("data: [DONE]\n\n");
            }
            else
            {
                await http.Response.WriteAsJsonAsync(new
                {
                    id = "test", @object = "chat.completion", created = 1, model = "test-model",
                    choices = new[] { new { index = 0, finish_reason = first ? "tool_calls" : "stop", message = new
                    {
                        role = "assistant", content = first ? null : "done",
                        tool_calls = first ? new[] { new { id = "call_1", type = "function", function = new { name = "TestTool", arguments = "{}" } } } : null
                    }}},
                    usage = new { prompt_tokens = 10, completion_tokens = 2, total_tokens = 12 }
                });
            }
        });
        await server.StartAsync();
        using var services = new ServiceCollection().BuildServiceProvider();
        using var logs = LoggerFactory.Create(_ => { });
        var factory = new HomeAppAgentFactory(new Catalog(server.Urls.Single()), services, logs);
        var toolCalled = false;
        var built = await factory.CreateAsync(new HomeAppAgentBuildRequest
        {
            Name = "TelemetryTest", Instructions = "Test",
            Tools = [AIFunctionFactory.Create(() => { toolCalled = true; return "ok"; }, "TestTool")]
        });
        Assert.IsType<OpenTelemetryAgent>(built.Agent);
        using var parentSource = new ActivitySource(HomeAppTelemetry.ActivitySourceName);
        using (var parent = parentSource.StartActivity("test-request"))
        {
            Assert.NotNull(parent);
            if (streaming)
                await foreach (var update in built.Agent.RunStreamingAsync("run tool")) { }
            else
                await built.Agent.RunAsync("run tool");
        }
        Assert.True(toolCalled);
        Assert.Equal(2, calls);
        Assert.Contains(spans, span => span.Source.Name == HomeAppTelemetry.AgentSourceName && span.GetTagItem("gen_ai.operation.name")?.ToString() == "invoke_agent");
        Assert.Contains(spans, span => span.GetTagItem("gen_ai.operation.name")?.ToString() == "execute_tool");
        var modelSpans = spans.Where(span => span.Source.Name == HomeAppTelemetry.ActivitySourceName && span.GetTagItem("gen_ai.operation.name")?.ToString() == "chat").ToList();
        Assert.Equal(2, modelSpans.Count);
        Assert.All(modelSpans, span =>
        {
            Assert.Equal("telemetry-test-model", span.GetTagItem("homeapp.ai.model_key"));
            Assert.Equal(10L, span.GetTagItem("gen_ai.usage.input_tokens"));
            Assert.Equal(2L, span.GetTagItem("gen_ai.usage.output_tokens"));
        });
        Assert.Single(spans.Select(span => span.TraceId).Distinct());
        Assert.Contains(measurements, measurement => measurement.Meter == HomeAppTelemetry.ActivitySourceName && measurement.Name.Contains("token"));
        Assert.Contains(measurements, measurement => measurement.Meter == HomeAppTelemetry.AgentSourceName && measurement.Name.Contains("duration"));
        (built.Agent as IDisposable)?.Dispose();
    }

    private sealed class Catalog(string url) : IHomeAppAgentModelCatalog
    {
        public Task<HomeAppAgentModelConfiguration> GetResolvedModelAsync(string? key, CancellationToken ct = default) =>
            Task.FromResult(new HomeAppAgentModelConfiguration(1, "telemetry-test-model", "Test", "local", "Local", "test-model",
                url + "/v1/", "synthetic-key", null, null, null));
    }
}
