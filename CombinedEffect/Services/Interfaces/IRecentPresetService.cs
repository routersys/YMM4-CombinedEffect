namespace CombinedEffect.Services.Interfaces;

internal interface IRecentPresetService
{
    Task<IReadOnlyList<Guid>> GetRecentIdsAsync();
    void Add(Guid id);
    void Remove(Guid id);
}