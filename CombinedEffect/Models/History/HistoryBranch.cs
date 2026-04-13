namespace CombinedEffect.Models.History;

public sealed class HistoryBranch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid HeadSnapshotId { get; set; } = Guid.Empty;
}