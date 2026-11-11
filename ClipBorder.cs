using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LNXcounter;

/// <summary>A Border that also clips its child to the rounded corners (so art that fills the frame, or zooms on hover, stays inside).</summary>
public class ClipBorder : Border
{
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (Child is FrameworkElement c)
        {
            double r = Math.Max(0, CornerRadius.TopLeft - BorderThickness.Left);
            double w = Math.Max(0, ActualWidth - BorderThickness.Left - BorderThickness.Right);
            double h = Math.Max(0, ActualHeight - BorderThickness.Top - BorderThickness.Bottom);
            c.Clip = new RectangleGeometry(new Rect(0, 0, w, h), r, r);
        }
    }
}
