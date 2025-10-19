using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace FloatGridSim;

public partial class MainWindow : Window
{
    readonly Simulation _sim = new();
    readonly DispatcherTimer _timer;
    bool _dragging;
    Point _dragStart;
    Vector _offsetStart;

    public MainWindow()
    {
        InitializeComponent();
        Surface.Sim = _sim;
        _sim.ResetTrails();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, __) =>
        {
            _sim.Tick();
            if (AutoZoomBox.IsChecked == true)
            {
                if (!IsVisibleOnScreen(_sim.ZStrategy.Point) || !IsVisibleOnScreen(_sim.VStrategy.Point))
                    EnsureActorsVisible();
            }
            
            UpdateHud();
            Surface.InvalidateVisual();
        };
        _timer.Start();
        UpdateHud();
        Surface.Focus();
    }

    void UpdateHud()
    {
        InfoText.Text = $"Z=({_sim.ZStrategy.Point.X:0.###},{_sim.ZStrategy.Point.Y:0.###})  V=({_sim.VStrategy.Point.X:0.###},{_sim.VStrategy.Point.Y:0.###})";
        DistText.Text = $"dist(V→Z)={_sim.CurrentDistance:0.###}";
        MinDistText.Text = $"minDist={_sim.MinDistance:0.###}";
        IterText.Text = $"iter={_sim.Iteration}";
        ZoomText.Text = $"zoom={Surface.Xform.Scale:0.##}";
        SpeedText.Text = $"speed={_sim.IterationsPerTick}";
    }

    void StartBtn_Click(object sender, RoutedEventArgs e) { _sim.Start(); UpdateHud(); }
    void PauseBtn_Click(object sender, RoutedEventArgs e) { _sim.Pause(); UpdateHud(); }

    void ApplySpeed_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(SpeedBox.Text, out var n) && n > 0) _sim.IterationsPerTick = n;
        UpdateHud();
    }

    void ApplyStride_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(StrideBox.Text, out var s) && s >= 1) _sim.TrailStride = s;
    }

    void RunUntil_Click(object sender, RoutedEventArgs e)
    {
        if (long.TryParse(RunUntilBox.Text.Replace("_", ""), out var n) && n > _sim.Iteration)
        {
            _sim.RunUntil(n);
            UpdateHud();
        }
    }

    void CancelUntil_Click(object sender, RoutedEventArgs e)
    {
        _sim.StopAtIteration = null;
        UpdateHud();
    }

    void AddCircle_Click(object sender, RoutedEventArgs e)
    {
        var m = Surface.Xform.BuildMatrix(Surface.ActualWidth, Surface.ActualHeight);
        var inv = m; inv.Invert();
        var world = inv.Transform(Mouse.GetPosition(Surface));
        _sim.AddCircleAt(world.X, world.Y, 1.0);
        Surface.InvalidateVisual();
        UpdateHud();
    }

    void ClearCircles_Click(object sender, RoutedEventArgs e)
    {
        _sim.ClearCircles();
        Surface.InvalidateVisual();
        UpdateHud();
    }

    void Surface_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var before = Mouse.GetPosition(Surface);
        var m0 = Surface.Xform.BuildMatrix(Surface.ActualWidth, Surface.ActualHeight);
        var inv0 = m0; inv0.Invert();
        var world = inv0.Transform(before);
        var factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
        Surface.Xform.Scale = Math.Max(5.0, Surface.Xform.Scale * factor);
        var m1 = Surface.Xform.BuildMatrix(Surface.ActualWidth, Surface.ActualHeight);
        var after = m1.Transform(world);
        var delta = before - after;
        Surface.Xform.Offset += (Vector)delta;
        Surface.InvalidateVisual();
        UpdateHud();
    }

    void Surface_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Surface.CaptureMouse();
        _dragging = true;
        _dragStart = e.GetPosition(Surface);
        _offsetStart = Surface.Xform.Offset;

        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
        {
            var m = Surface.Xform.BuildMatrix(Surface.ActualWidth, Surface.ActualHeight);
            var inv = m; inv.Invert();
            var world = inv.Transform(_dragStart);
            _sim.SetZ(world.X, world.Y);
        }
        else if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
        {
            var m = Surface.Xform.BuildMatrix(Surface.ActualWidth, Surface.ActualHeight);
            var inv = m; inv.Invert();
            var world = inv.Transform(_dragStart);
            _sim.SetV(world.X, world.Y);
        }
    }

    void Surface_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        Surface.ReleaseMouseCapture();
        Surface.InvalidateVisual();
        UpdateHud();
    }

    void Surface_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift) &&
            !Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
        {
            var cur = e.GetPosition(Surface);
            var delta = cur - _dragStart;
            Surface.Xform.Offset = _offsetStart + (Vector)delta;
            Surface.InvalidateVisual();
        }
    }
    
    void Reset_Click(object sender, RoutedEventArgs e)
    {
        _sim.ResetAll();
        Surface.Xform.Scale = 60.0;
        Surface.Xform.Offset = new Vector(0, 0);
        AutoZoomBox.IsChecked = false;
        Surface.InvalidateVisual();
        UpdateHud();
    }
    
    void EnsureActorsVisible(double paddingPx = 40, double lerp = 0.25)
    {
        if (Surface.ActualWidth <= 1 || Surface.ActualHeight <= 1) return;

        var w = Surface.ActualWidth;
        var h = Surface.ActualHeight;

        // Мировые точки, которые должны быть в кадре
        var xMin = Math.Min(_sim.ZStrategy.Point.X, _sim.VStrategy.Point.X);
        var xMax = Math.Max(_sim.ZStrategy.Point.X, _sim.VStrategy.Point.X);
        var yMin = Math.Min(_sim.ZStrategy.Point.Y, _sim.VStrategy.Point.Y);
        var yMax = Math.Max(_sim.ZStrategy.Point.Y, _sim.VStrategy.Point.Y);

        // Если точки совпали — даём минимальный мир. размер, чтобы не делить на ноль
        var minWorld = 1e-3;
        var worldW = Math.Max(xMax - xMin, minWorld);
        var worldH = Math.Max(yMax - yMin, minWorld);

        // Требуемый масштаб с учётом отступов в пикселях
        var scaleX = (w - 2 * paddingPx) / worldW;
        var scaleY = (h - 2 * paddingPx) / worldH;
        var targetScale = Math.Max(5.0, Math.Min(scaleX, scaleY));

        // Центр «бокса» в мире должен попасть в центр экрана
        var cxWorld = 0.5 * (xMin + xMax);
        var cyWorld = 0.5 * (yMin + yMax);

        // Текущая матрица и смещение для targetScale
        var cur = Surface.Xform;
        var newScale = double.IsFinite(targetScale) ? targetScale : cur.Scale;

        // Целевой оффсет (с учётом того, что ось Y у нас направлена вверх в мире, а в пикселях вниз)
        // Xform.BuildMatrix делает: Scale(Scale, -Scale) + Translate(w/2+Offset.X, h/2+Offset.Y)
        // Хотим, чтобы (cxWorld, cyWorld) -> (w/2, h/2)
        // Решаем относительно Offset: Offset = (w/2, h/2) - Scale*(cxWorld, -cyWorld) - (w/2, h/2) = (-Scale*cxWorld, Scale*cyWorld)
        var targetOffset = new Vector(-newScale * cxWorld, newScale * cyWorld);

        // Плавно интерполируем (камера «подъезжает»)
        cur.Scale = cur.Scale + (newScale - cur.Scale) * lerp;
        cur.Offset = cur.Offset + (targetOffset - cur.Offset) * lerp;

        Surface.InvalidateVisual();
    }
    
    bool IsVisibleOnScreen(WorldPoint p)
    {
        var m = Surface.Xform.BuildMatrix(Surface.ActualWidth, Surface.ActualHeight);
        var sp = m.Transform(new Point(p.X, p.Y));
        return sp.X >= 0 && sp.X <= Surface.ActualWidth &&
               sp.Y >= 0 && sp.Y <= Surface.ActualHeight;
    }
}
