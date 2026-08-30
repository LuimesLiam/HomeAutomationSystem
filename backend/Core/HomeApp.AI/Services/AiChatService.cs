using HomeApp.AI;
using HomeApp.AI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace HomeApp.AI.Services;

public sealed class AiChatService
{
    private readonly HomeAppAiDbContext _dbContext;
    private readonly IHomeAppAgentModelCatalog _modelCatalog;

    public AiChatService(HomeAppAiDbContext dbContext, IHomeAppAgentModelCatalog modelCatalog)
    {
        _dbContext = dbContext;
        _modelCatalog = modelCatalog;
    }

    public async Task<IReadOnlyList<AiChatSession>> GetSessionsAsync(CancellationToken ct = default)
    {
        return await _dbContext.AiChatSessions
            .AsNoTracking()
            .OrderByDescending(session => session.UpdatedAtUtc)
            .ThenByDescending(session => session.Id)
            .ToListAsync(ct);
    }

    public Task<AiChatSession?> GetSessionAsync(int sessionId, CancellationToken ct = default)
    {
        return _dbContext.AiChatSessions
            .AsNoTracking()
            .Include(session => session.Messages.OrderBy(message => message.DisplayOrder).ThenBy(message => message.Id))
            .FirstOrDefaultAsync(session => session.Id == sessionId, ct);
    }

    public async Task<AiChatSession> CreateSessionAsync(string? title, string? modelKey, CancellationToken ct = default)
    {
        var resolved = await _modelCatalog.GetResolvedModelAsync(modelKey, ct);
        var now = DateTimeOffset.UtcNow;
        var session = new AiChatSession
        {
            Title = NormalizeTitle(title) ?? "New chat",
            ModelKey = resolved.ModelKey,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.AiChatSessions.Add(session);
        await _dbContext.SaveChangesAsync(ct);
        return session;
    }

    public async Task<(AiChatSession Session, AiChatSessionMessage UserMessage, HomeAppAgentModelConfiguration Model)> AppendUserMessageAsync(
        int? sessionId,
        string content,
        string? requestedModelKey,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("Chat content is required.");
        }

        var session = sessionId.HasValue
            ? await _dbContext.AiChatSessions.Include(item => item.Messages).FirstOrDefaultAsync(item => item.Id == sessionId.Value, ct)
            : null;

        var resolved = await _modelCatalog.GetResolvedModelAsync(requestedModelKey ?? session?.ModelKey, ct);
        var now = DateTimeOffset.UtcNow;

        if (session == null)
        {
            session = new AiChatSession
            {
                Title = BuildTitle(content),
                ModelKey = resolved.ModelKey,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _dbContext.AiChatSessions.Add(session);
            await _dbContext.SaveChangesAsync(ct);
            session.Messages = [];
        }
        else
        {
            session.ModelKey = resolved.ModelKey;
            session.UpdatedAtUtc = now;
            if (session.Messages.Count == 0 || string.Equals(session.Title, "New chat", StringComparison.OrdinalIgnoreCase))
            {
                session.Title = BuildTitle(content);
            }
        }

        var userMessage = new AiChatSessionMessage
        {
            SessionId = session.Id,
            Role = "user",
            Content = content.Trim(),
            ModelKey = resolved.ModelKey,
            ModelName = resolved.ModelName,
            ProviderName = resolved.ProviderName,
            DisplayOrder = session.Messages.Count,
            CreatedAtUtc = now
        };

        _dbContext.AiChatSessionMessages.Add(userMessage);
        await _dbContext.SaveChangesAsync(ct);
        session.Messages.Add(userMessage);

        return (session, userMessage, resolved);
    }

    public async Task<AiChatSessionMessage> AppendAssistantMessageAsync(
        int sessionId,
        string content,
        HomeAppAgentModelConfiguration model,
        CancellationToken ct = default)
    {
        var session = await _dbContext.AiChatSessions
            .Include(item => item.Messages)
            .FirstAsync(item => item.Id == sessionId, ct);

        var now = DateTimeOffset.UtcNow;
        session.ModelKey = model.ModelKey;
        session.UpdatedAtUtc = now;

        var message = new AiChatSessionMessage
        {
            SessionId = session.Id,
            Role = "assistant",
            Content = content,
            ModelKey = model.ModelKey,
            ModelName = model.ModelName,
            ProviderName = model.ProviderName,
            DisplayOrder = session.Messages.Count,
            CreatedAtUtc = now
        };

        _dbContext.AiChatSessionMessages.Add(message);
        await _dbContext.SaveChangesAsync(ct);
        return message;
    }

    public async Task<IReadOnlyList<ChatMessage>> GetConversationMessagesAsync(int sessionId, CancellationToken ct = default)
    {
        return await _dbContext.AiChatSessionMessages
            .AsNoTracking()
            .Where(message => message.SessionId == sessionId)
            .OrderBy(message => message.DisplayOrder)
            .ThenBy(message => message.Id)
            .Select(message => new ChatMessage(
                message.Role == "assistant" ? ChatRole.Assistant : ChatRole.User,
                message.Content))
            .ToListAsync(ct);
    }

    private static string BuildTitle(string content)
    {
        var normalized = NormalizeTitle(content);
        return string.IsNullOrWhiteSpace(normalized) ? "New chat" : normalized;
    }

    private static string? NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var trimmed = title.Trim();
        return trimmed.Length <= 60 ? trimmed : $"{trimmed[..57]}...";
    }
}
