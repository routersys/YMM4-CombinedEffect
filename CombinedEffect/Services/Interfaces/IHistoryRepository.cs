using CombinedEffect.Models.History;

namespace CombinedEffect.Services.Interfaces;

internal interface IHistoryRepository
{
    Task<List<HistoryBranch>> LoadBranchesAsync(Guid presetId);
    void SaveBranches(Guid presetId, List<HistoryBranch> branches);
    Task<HistorySnapshot?> LoadSnapshotAsync(Guid presetId, Guid snapshotId);
    void SaveSnapshot(Guid presetId, HistorySnapshot snapshot);
    Task<List<HistorySnapshot>> LoadAllSnapshotsAsync(Guid presetId);
}