namespace HomeApp.Data;

    public class SecurityEvent
    {
        public int Id { get; set; }
        public string EventType { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string? Description { get; set; }
    }