namespace CombinedEffect.ViewModels;

public enum DiffType
{
    Unchanged,
    Added,
    Removed,
    Modified
}

internal sealed class DiffNode
{
    public string Text { get; }
    public DiffType Type { get; }

    public DiffNode(string text, DiffType type)
    {
        Text = text;
        Type = type;
    }
}