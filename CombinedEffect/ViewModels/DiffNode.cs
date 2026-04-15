using CombinedEffect.Models;

namespace CombinedEffect.ViewModels;

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