namespace HomeApp.AI.Data;

public sealed class AiChatSessionMessage
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public AiChatSession? Session { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ModelKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
