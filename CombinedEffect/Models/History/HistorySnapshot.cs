namespace CombinedEffect.Models.History;

internal sealed class HistorySnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ParentId { get; set; } = Guid.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Message { get; set; } = string.Empty;
    public string SerializedEffects { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
}