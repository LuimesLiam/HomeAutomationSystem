using System.Text;
using System.Text.Json;
using HomeApp.AI;
using HomeApp.AI.Services;
using HomeApp.AI.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace HomeApp.AI.Controllers;

[ApiController]
[Route("api/ai/chat")]
public sealed class AiChatController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AiChatService _chatService;
    private readonly IHomeAppAgentFactory _agentFactory;

    public AiChatController(AiChatService chatService, IHomeAppAgentFactory agentFactory)
    {
        _chatService = chatService;
        _agentFactory = agentFactory;
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<List<AiChatSessionSummaryDto>>> GetSessions(CancellationToken ct)
    {
        var sessions = await _chatService.GetSessionsAsync(ct);
        return Ok(sessions.Select(MapSessionSummary).ToList());
    }

    [HttpPost("sessions")]
    public async Task<ActionResult<AiChatSessionDetailDto>> CreateSession([FromBody] CreateAiChatSessionRequestDto request, CancellationToken ct)
    {
        var session = await _chatService.CreateSessionAsync(request.Title, request.ModelKey, ct);
        var loaded = await _chatService.GetSessionAsync(session.Id, ct);
        return Ok(MapSessionDetail(loaded!));
    }

    [HttpGet("sessions/{sessionId:int}")]
    public async Task<ActionResult<AiChatSessionDetailDto>> GetSession(int sessionId, CancellationToken ct)
    {
        var session = await _chatService.GetSessionAsync(sessionId, ct);
        return session == null ? NotFound() : Ok(MapSessionDetail(session));
    }

    [HttpPost("sessions/{sessionId:int}/messages/stream")]
    public async Task StreamMessage(int sessionId, [FromBody] StreamAiChatMessageRequestDto request, CancellationToken ct)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "application/x-ndjson";

        var (session, _, model) = await _chatService.AppendUserMessageAsync(sessionId, request.Content, request.ModelKey, ct);
        await WriteEventAsync(new { type = "session", session = MapSessionSummary(session) }, ct);

        var build = await _agentFactory.CreateAsync(
            new HomeAppAgentBuildRequest
            {
                Name = "HomeAppChat",
                Description = "Persisted HomeApp chat session",
                Instructions = "You are a helpful assistant inside HomeApp. Be concise, clear, and practical.",
                ModelKeyOverride = model.ModelKey
            },
            ct);

        var history = await _chatService.GetConversationMessagesAsync(session.Id, ct);
        var buffer = new StringBuilder();

        await foreach (var update in build.Agent.RunStreamingAsync(history, session: null, options: null, cancellationToken: ct))
        {
            var text = update.Text;
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            buffer.Append(text);
            await WriteEventAsync(new { type = "chunk", content = text }, ct);
        }

        var assistantMessage = await _chatService.AppendAssistantMessageAsync(session.Id, buffer.ToString(), build.Model, ct);
        await WriteEventAsync(new
        {
            type = "completed",
            session = MapSessionSummary(await _chatService.GetSessionAsync(session.Id, ct) ?? session),
            message = MapMessage(assistantMessage)
        }, ct);
    }

    [HttpPost("messages/stream")]
    public Task StreamNewSessionMessage([FromBody] StreamAiChatMessageRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new InvalidOperationException("Chat content is required.");
        }

        return StreamNewSessionCoreAsync(request, ct);
    }

    private async Task StreamNewSessionCoreAsync(StreamAiChatMessageRequestDto request, CancellationToken ct)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.ContentType = "application/x-ndjson";

        var (session, _, model) = await _chatService.AppendUserMessageAsync(null, request.Content, request.ModelKey, ct);
        await WriteEventAsync(new { type = "session", session = MapSessionSummary(session) }, ct);

        var build = await _agentFactory.CreateAsync(
            new HomeAppAgentBuildRequest
            {
                Name = "HomeAppChat",
                Description = "Persisted HomeApp chat session",
                Instructions = "You are a helpful assistant inside HomeApp. Be concise, clear, and practical.",
                ModelKeyOverride = model.ModelKey
            },
            ct);

        var history = await _chatService.GetConversationMessagesAsync(session.Id, ct);
        var buffer = new StringBuilder();

        await foreach (var update in build.Agent.RunStreamingAsync(history, session: null, options: null, cancellationToken: ct))
        {
            var text = update.Text;
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            buffer.Append(text);
            await WriteEventAsync(new { type = "chunk", content = text }, ct);
        }

        var assistantMessage = await _chatService.AppendAssistantMessageAsync(session.Id, buffer.ToString(), build.Model, ct);
        await WriteEventAsync(new
        {
            type = "completed",
            session = MapSessionSummary(await _chatService.GetSessionAsync(session.Id, ct) ?? session),
            message = MapMessage(assistantMessage)
        }, ct);
    }

    private async Task WriteEventAsync(object payload, CancellationToken ct)
    {
        await Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions), ct);
        await Response.WriteAsync("\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    private static AiChatSessionSummaryDto MapSessionSummary(AiChatSession session)
    {
        return new AiChatSessionSummaryDto
        {
            Id = session.Id,
            Title = session.Title,
            ModelKey = session.ModelKey,
            CreatedAtUtc = session.CreatedAtUtc,
            UpdatedAtUtc = session.UpdatedAtUtc
        };
    }

    private static AiChatSessionDetailDto MapSessionDetail(AiChatSession session)
    {
        return new AiChatSessionDetailDto
        {
            Id = session.Id,
            Title = session.Title,
            ModelKey = session.ModelKey,
            CreatedAtUtc = session.CreatedAtUtc,
            UpdatedAtUtc = session.UpdatedAtUtc,
            Messages = session.Messages
                .OrderBy(message => message.DisplayOrder)
                .ThenBy(message => message.Id)
                .Select(MapMessage)
                .ToList()
        };
    }

    private static AiChatMessageDto MapMessage(AiChatSessionMessage message)
    {
        return new AiChatMessageDto
        {
            Id = message.Id,
            Role = message.Role,
            Content = message.Content,
            ModelKey = message.ModelKey,
            ModelName = message.ModelName,
            ProviderName = message.ProviderName,
            CreatedAtUtc = message.CreatedAtUtc
        };
    }
}

public sealed class CreateAiChatSessionRequestDto
{
    public string? Title { get; set; }
    public string? ModelKey { get; set; }
}

public sealed class StreamAiChatMessageRequestDto
{
    public string? ModelKey { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class AiChatSessionSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ModelKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class AiChatSessionDetailDto : AiChatSessionSummaryDto
{
    public List<AiChatMessageDto> Messages { get; set; } = [];
}

public sealed class AiChatMessageDto
{
    public int Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ModelKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
