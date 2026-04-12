using CombinedEffect.Infrastructure;

namespace CombinedEffect.Models;

public sealed class PresetGroup : ObservableBase
{
    private string _name = string.Empty;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public List<Guid> PresetIds { get; set; } = [];
}