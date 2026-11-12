using System.Windows;
using System.Windows.Media;

namespace LNXcounter;

/// <summary>A gold rounded bar that fills a fraction (0 to 1) of the width it is given. Used by the goal progress.</summary>
public class FractionBar : FrameworkElement
{
    public static readonly DependencyProperty FractionProperty = DependencyProperty.Register(nameof(Fraction), typeof(double), typeof(FractionBar),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Fraction { get => (double)GetValue(FractionProperty); set => SetValue(FractionProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth * Math.Clamp(Fraction, 0, 1);
        if (w <= 0) return;
        var brush = (Brush)Application.Current.FindResource("GoldBrush");
        dc.DrawRoundedRectangle(brush, null, new Rect(0, 0, Math.Max(w, ActualHeight), ActualHeight), ActualHeight / 2, ActualHeight / 2);
    }
}
