using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace FloatGridSim;

public class DrawingSurface : FrameworkElement
{
    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(nameof(Background), typeof(Brush), typeof(DrawingSurface),
            new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush Background
    {
        get => (Brush)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Simulation? Sim { get; set; }
    public TransformState Xform { get; } = new();

    readonly Pen gridMinor = new(new SolidColorBrush(Color.FromRgb(36, 42, 50)), 1);
    readonly Pen gridMajor = new(new SolidColorBrush(Color.FromRgb(54, 60, 70)), 1.25);
    readonly Pen axesPen = new(new SolidColorBrush(Color.FromRgb(140, 140, 140)), 1);

    readonly SolidColorBrush zBrush = new(Color.FromRgb(255, 122, 47));     // Z: #FF7A2F
    readonly SolidColorBrush vBrush = new(Color.FromRgb(30, 144, 255));     // V: #1E90FF

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        dc.DrawRectangle(Background, null, new Rect(0, 0, w, h));
        if (Sim == null) return;

        var m = Xform.BuildMatrix(w, h);
        DrawGrid(dc, m, w, h);
        DrawAxes(dc, m, w, h);

        if (Sim.ZTrail.Count > 1) DrawTrail(dc, m, Sim.ZTrail, zBrush);
        if (Sim.VTrail.Count > 1) DrawTrail(dc, m, Sim.VTrail, vBrush);

        var zPix = m.Transform(new Point(Sim.ZStrategy.Point.X, Sim.ZStrategy.Point.Y));
        var vPix = m.Transform(new Point(Sim.VStrategy.Point.X, Sim.VStrategy.Point.Y));
        dc.DrawEllipse(zBrush, null, zPix, 4, 4);
        dc.DrawEllipse(vBrush, null, vPix, 4, 4);
    }

    void DrawTrail(DrawingContext dc, Matrix m, IReadOnlyList<WorldPoint> trail, Brush brush)
    {
        var geo = new StreamGeometry();
        using (var gc = geo.Open())
        {
            var p0 = m.Transform(new Point(trail[0].X, trail[0].Y));
            gc.BeginFigure(p0, false, false);
            for (int i = 1; i < trail.Count; i++)
            {
                var p = m.Transform(new Point(trail[i].X, trail[i].Y));
                gc.LineTo(p, true, false);
            }
        }
        geo.Freeze();
        dc.DrawGeometry(null, new Pen(brush, 1.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, geo);
    }

    void DrawAxes(DrawingContext dc, Matrix m, double w, double h)
    {
        var o = m.Transform(new Point(0, 0));
        dc.DrawLine(axesPen, new Point(0, o.Y), new Point(w, o.Y));
        dc.DrawLine(axesPen, new Point(o.X, 0), new Point(o.X, h));
    }

    void DrawGrid(DrawingContext dc, Matrix m, double w, double h)
    {
        var stepWorld = Xform.GridStepWorld();
        var inv = m; inv.Invert();
        var topLeft = inv.Transform(new Point(0, 0));
        var bottomRight = inv.Transform(new Point(w, h));

        var xStart = Math.Floor(topLeft.X / stepWorld) * stepWorld;
        var xEnd = Math.Ceiling(bottomRight.X / stepWorld) * stepWorld;
        var yStart = Math.Floor(topLeft.Y / stepWorld) * stepWorld;
        var yEnd = Math.Ceiling(bottomRight.Y / stepWorld) * stepWorld;

        for (double x = xStart; x <= xEnd; x += stepWorld)
        {
            var p = m.Transform(new Point(x, yStart));
            var pen = Math.Abs(x) < 1e-9 ? gridMajor : gridMinor;
            dc.DrawLine(pen, new Point(p.X, 0), new Point(p.X, h));
        }
        for (double y = yStart; y <= yEnd; y += stepWorld)
        {
            var p = m.Transform(new Point(xStart, y));
            var pen = Math.Abs(y) < 1e-9 ? gridMajor : gridMinor;
            dc.DrawLine(pen, new Point(0, p.Y), new Point(w, p.Y));
        }

        var ft = new FormattedText($"grid: {stepWorld:0.###}",
            CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Consolas"), 12, Brushes.Gray, 1.0);
        dc.DrawText(ft, new Point(8, 8));
    }
}

public class TransformState
{
    public double Scale { get; set; } = 60.0;
    public Vector Offset { get; set; } = new Vector(0, 0);

    public Matrix BuildMatrix(double w, double h)
    {
        var mx = Matrix.Identity;
        mx.Scale(Scale, -Scale);
        mx.Translate(w * 0.5 + Offset.X, h * 0.5 + Offset.Y);
        return mx;
    }

    public double GridStepWorld()
    {
        var px = 80.0;
        var world = px / Scale;
        var pow10 = Math.Pow(10, Math.Floor(Math.Log10(world)));
        var n = world / pow10;
        double step = n < 2 ? 2 : n < 5 ? 5 : 10;
        return step * pow10;
    }
}
