namespace HomeApp.AI.Data;

public sealed class AiChatSession
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ModelKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public List<AiChatSessionMessage> Messages { get; set; } = [];
}
