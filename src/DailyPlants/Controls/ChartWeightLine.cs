using DailyPlants.ViewModels;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace DailyPlants.Controls;

/// <summary>
/// A weight trend line: polyline, soft area fill, three labelled y-axis
/// gridlines, an emphasised endpoint, and an optional dashed goal line.
/// Positions are computed in pixels from <see cref="Points"/>, so the chart
/// redraws itself on every size and data change rather than relying on
/// layout-relative XAML shapes.
/// </summary>
public sealed class ChartWeightLine : Canvas
{
    public static readonly DependencyProperty PointsProperty =
        DependencyProperty.Register(nameof(Points), typeof(IReadOnlyList<WeightDataPoint>), typeof(ChartWeightLine),
            new PropertyMetadata(null, OnDataChanged));

    /// <summary>No goal line is drawn when this is <see cref="double.NaN"/>.</summary>
    public static readonly DependencyProperty GoalWeightProperty =
        DependencyProperty.Register(nameof(GoalWeight), typeof(double), typeof(ChartWeightLine),
            new PropertyMetadata(double.NaN, OnDataChanged));

    public static readonly DependencyProperty WeightUnitProperty =
        DependencyProperty.Register(nameof(WeightUnit), typeof(string), typeof(ChartWeightLine),
            new PropertyMetadata("kg", OnDataChanged));

    public static readonly DependencyProperty LineBrushProperty =
        DependencyProperty.Register(nameof(LineBrush), typeof(Brush), typeof(ChartWeightLine),
            new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty GridlineBrushProperty =
        DependencyProperty.Register(nameof(GridlineBrush), typeof(Brush), typeof(ChartWeightLine),
            new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty MutedTextBrushProperty =
        DependencyProperty.Register(nameof(MutedTextBrush), typeof(Brush), typeof(ChartWeightLine),
            new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty EmphasisTextBrushProperty =
        DependencyProperty.Register(nameof(EmphasisTextBrush), typeof(Brush), typeof(ChartWeightLine),
            new PropertyMetadata(null, OnDataChanged));

    public static readonly DependencyProperty GoalBrushProperty =
        DependencyProperty.Register(nameof(GoalBrush), typeof(Brush), typeof(ChartWeightLine),
            new PropertyMetadata(null, OnDataChanged));

    public IReadOnlyList<WeightDataPoint>? Points
    {
        get => (IReadOnlyList<WeightDataPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public double GoalWeight
    {
        get => (double)GetValue(GoalWeightProperty);
        set => SetValue(GoalWeightProperty, value);
    }

    public string WeightUnit
    {
        get => (string)GetValue(WeightUnitProperty);
        set => SetValue(WeightUnitProperty, value);
    }

    /// <summary>Set from XAML as {ThemeResource DpGreenBrush} - the polyline, area fill and endpoint.</summary>
    public Brush? LineBrush
    {
        get => (Brush?)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    /// <summary>Set from XAML as {ThemeResource DpHairlineBrush}.</summary>
    public Brush? GridlineBrush
    {
        get => (Brush?)GetValue(GridlineBrushProperty);
        set => SetValue(GridlineBrushProperty, value);
    }

    /// <summary>Set from XAML as {ThemeResource DpInkMutedBrush} - gridline value labels.</summary>
    public Brush? MutedTextBrush
    {
        get => (Brush?)GetValue(MutedTextBrushProperty);
        set => SetValue(MutedTextBrushProperty, value);
    }

    /// <summary>Set from XAML as {ThemeResource DpInkBrush} - the endpoint's value label.</summary>
    public Brush? EmphasisTextBrush
    {
        get => (Brush?)GetValue(EmphasisTextBrushProperty);
        set => SetValue(EmphasisTextBrushProperty, value);
    }

    /// <summary>Set from XAML as {ThemeResource DpClayBrush} - the dashed goal line and its label.</summary>
    public Brush? GoalBrush
    {
        get => (Brush?)GetValue(GoalBrushProperty);
        set => SetValue(GoalBrushProperty, value);
    }

    public ChartWeightLine()
    {
        SizeChanged += (_, _) => Redraw();
    }

    private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ChartWeightLine)d).Redraw();

    private void Redraw()
    {
        Children.Clear();

        var points = Points;
        var width = ActualWidth;
        var height = ActualHeight;
        if (points is not { Count: > 0 } || width <= 0 || height <= 0)
        {
            return;
        }

        if (LineBrush is null || GridlineBrush is null || MutedTextBrush is null || EmphasisTextBrush is null || GoalBrush is null)
        {
            return;
        }

        const double labelGutter = 44;
        const double topPad = 10;
        const double bottomPad = 22;
        var plotLeft = labelGutter;
        var plotWidth = Math.Max(1, width - labelGutter - 8);
        var plotTop = topPad;
        var plotBottom = height - bottomPad;
        var plotHeight = Math.Max(1, plotBottom - plotTop);

        var rawMin = points.Min(p => p.Weight);
        var rawMax = points.Max(p => p.Weight);
        var hasGoal = !double.IsNaN(GoalWeight);
        if (hasGoal)
        {
            rawMin = Math.Min(rawMin, GoalWeight);
            rawMax = Math.Max(rawMax, GoalWeight);
        }
        var rawMid = (rawMin + rawMax) / 2;

        var scaleMin = rawMin;
        var scaleMax = rawMax;
        if (scaleMax - scaleMin < 0.5)
        {
            scaleMax += 1;
            scaleMin -= 1;
        }
        else
        {
            var pad = (scaleMax - scaleMin) * 0.15;
            scaleMax += pad;
            scaleMin -= pad;
        }

        double XFor(int index) => points.Count == 1
            ? plotLeft + plotWidth / 2
            : plotLeft + plotWidth * index / (points.Count - 1);
        double YFor(double weight) => plotTop + plotHeight * (1 - (weight - scaleMin) / (scaleMax - scaleMin));

        DrawGridlines(rawMax, rawMid, rawMin, plotLeft, plotWidth, YFor);

        var linePoints = new PointCollection();
        var areaPoints = new PointCollection();
        areaPoints.Add(new Point(XFor(0), plotBottom));
        for (var i = 0; i < points.Count; i++)
        {
            var p = new Point(XFor(i), YFor(points[i].Weight));
            linePoints.Add(p);
            areaPoints.Add(p);
        }
        areaPoints.Add(new Point(XFor(points.Count - 1), plotBottom));

        Children.Add(new Polygon { Points = areaPoints, Fill = LineBrush, Opacity = 0.10 });
        Children.Add(new Polyline
        {
            Points = linePoints,
            Stroke = LineBrush,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        });

        if (hasGoal)
        {
            DrawGoalLine(GoalWeight, plotLeft, plotWidth, YFor);
        }

        DrawEndpoint(points[^1], XFor(points.Count - 1), YFor(points[^1].Weight), plotLeft);
    }

    private void DrawGridlines(double rawMax, double rawMid, double rawMin, double plotLeft, double plotWidth, Func<double, double> yFor)
    {
        foreach (var value in new[] { rawMax, rawMid, rawMin }.Distinct())
        {
            var y = yFor(value);

            Children.Add(new Line
            {
                X1 = plotLeft,
                Y1 = y,
                X2 = plotLeft + plotWidth,
                Y2 = y,
                Stroke = GridlineBrush,
                StrokeThickness = 1
            });

            var label = CreateLabel($"{value:F1}", MutedTextBrush!);
            label.Width = plotLeft - 8;
            label.TextAlignment = TextAlignment.Right;
            SetLeft(label, 0);
            SetTop(label, y - 8);
            Children.Add(label);
        }
    }

    private void DrawGoalLine(double goal, double plotLeft, double plotWidth, Func<double, double> yFor)
    {
        var y = yFor(goal);

        Children.Add(new Line
        {
            X1 = plotLeft,
            Y1 = y,
            X2 = plotLeft + plotWidth,
            Y2 = y,
            Stroke = GoalBrush,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 }
        });

        var label = CreateLabel($"Goal {goal:F1} {WeightUnit}", GoalBrush!);
        SetLeft(label, plotLeft);
        SetTop(label, y - 20);
        Children.Add(label);
    }

    private void DrawEndpoint(WeightDataPoint point, double x, double y, double plotLeft)
    {
        const double radius = 4; // 8px dot

        var dot = new Ellipse { Width = radius * 2, Height = radius * 2, Fill = LineBrush };
        SetLeft(dot, x - radius);
        SetTop(dot, y - radius);
        Children.Add(dot);

        var label = CreateLabel($"{point.Weight:F1} {WeightUnit}", EmphasisTextBrush!);
        label.Width = 64;
        label.TextAlignment = TextAlignment.Right;
        SetLeft(label, Math.Max(plotLeft, x - 68));
        SetTop(label, y - 20);
        Children.Add(label);
    }

    private static TextBlock CreateLabel(string text, Brush foreground) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources["DpCaptionTextBlockStyle"],
        Foreground = foreground
    };
}
