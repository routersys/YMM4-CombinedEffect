using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace CombinedEffect.Views;

internal sealed class DropInsertionAdorner : Adorner
{
    private static readonly Pen _pen = new(SystemColors.HighlightBrush, 2.0);

    static DropInsertionAdorner() => _pen.Freeze();

    public DropInsertionAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var w = AdornedElement.RenderSize.Width;
        var h = AdornedElement.RenderSize.Height;
        drawingContext.DrawLine(_pen, new Point(0, h), new Point(w, h));
    }
}
