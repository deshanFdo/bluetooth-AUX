using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AudioSwitch.Controls;

public partial class CircularGauge : UserControl
{
    public static readonly DependencyProperty PercentProperty =
        DependencyProperty.Register(nameof(Percent), typeof(double), typeof(CircularGauge),
            new PropertyMetadata(0.0, OnVisualChanged));

    public static readonly DependencyProperty RingBrushProperty =
        DependencyProperty.Register(nameof(RingBrush), typeof(System.Windows.Media.Brush), typeof(CircularGauge),
            new PropertyMetadata(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x34, 0xD3, 0x99))));

    public static readonly DependencyProperty RingThicknessProperty =
        DependencyProperty.Register(nameof(RingThickness), typeof(double), typeof(CircularGauge),
            new PropertyMetadata(4.5, OnVisualChanged));

    public static readonly DependencyProperty DisplayTextProperty =
        DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(CircularGauge),
            new PropertyMetadata("--%"));

    public static readonly DependencyProperty GlyphDataProperty =
        DependencyProperty.Register(nameof(GlyphData), typeof(Geometry), typeof(CircularGauge),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CompactModeProperty =
        DependencyProperty.Register(nameof(CompactMode), typeof(bool), typeof(CircularGauge),
            new PropertyMetadata(false, OnCompactChanged));

    public double Percent
    {
        get => (double)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    public System.Windows.Media.Brush RingBrush
    {
        get => (System.Windows.Media.Brush)GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }

    public double RingThickness
    {
        get => (double)GetValue(RingThicknessProperty);
        set => SetValue(RingThicknessProperty, value);
    }

    public string DisplayText
    {
        get => (string)GetValue(DisplayTextProperty);
        set => SetValue(DisplayTextProperty, value);
    }

    public Geometry? GlyphData
    {
        get => (Geometry?)GetValue(GlyphDataProperty);
        set => SetValue(GlyphDataProperty, value);
    }

    public bool CompactMode
    {
        get => (bool)GetValue(CompactModeProperty);
        set => SetValue(CompactModeProperty, value);
    }

    public CircularGauge()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateArc();
        Loaded += (_, _) => UpdateArc();
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CircularGauge g) g.UpdateArc();
    }

    private static void OnCompactChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CircularGauge g)
        {
            bool compact = (bool)e.NewValue;
            g.ValueText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            g.CenterGlyph.Margin = compact ? new Thickness(0) : new Thickness(0, 0, 0, 2);
            g.CenterGlyph.Width = compact ? 10 : 13;
            g.CenterGlyph.Height = compact ? 10 : 13;
        }
    }

    private void UpdateArc()
    {
        double w = ActualWidth > 0 ? ActualWidth : Width;
        double h = ActualHeight > 0 ? ActualHeight : Height;
        if (double.IsNaN(w) || double.IsNaN(h) || w <= 4 || h <= 4) return;

        double pct = Math.Clamp(Percent, 0, 100);
        if (pct <= 0.1)
        {
            ProgressArc.Data = Geometry.Empty;
            return;
        }

        double thickness = RingThickness;
        double radius = Math.Max(2, (Math.Min(w, h) - thickness) / 2.0);
        var center = new System.Windows.Point(w / 2.0, h / 2.0);

        // Clamp slightly under 360 so PathGeometry ArcSegment draws a full ring without degenerating
        double angle = pct >= 99.9 ? 359.9 : pct * 3.6;
        double startRad = -Math.PI / 2.0;
        double endRad = startRad + (angle * Math.PI / 180.0);

        var startPt = new System.Windows.Point(
            center.X + radius * Math.Cos(startRad),
            center.Y + radius * Math.Sin(startRad));
        var endPt = new System.Windows.Point(
            center.X + radius * Math.Cos(endRad),
            center.Y + radius * Math.Sin(endRad));

        var fig = new PathFigure { StartPoint = startPt, IsClosed = false };
        fig.Segments.Add(new ArcSegment
        {
            Point = endPt,
            Size = new System.Windows.Size(radius, radius),
            IsLargeArc = angle > 180.0,
            SweepDirection = SweepDirection.Clockwise
        });

        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        geo.Freeze();
        ProgressArc.Data = geo;
    }
}
