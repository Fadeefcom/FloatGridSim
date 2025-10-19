using FloatGridSim.Strategy;

namespace FloatGridSim;

public record WorldPoint(double X, double Y);
public record Circle(double X, double Y, double R);

public class Simulation
{
    public IActorStrategy ZStrategy { get; set; } = new RunAwayStrategy(0, 0);
    public IActorStrategy VStrategy { get; set; } = new GreedyChaseStrategy(0, 0);
    public List<Circle> Circles { get; } = new();

    public long Iteration { get; private set; }
    public bool Running { get; private set; }
    public double StepSize { get; set; } = 0.5;
    public int IterationsPerTick { get; set; } = 1;
    public long? StopAtIteration { get; set; }

    public double CurrentDistance { get; private set; }
    public double MinDistance { get; private set; } = double.PositiveInfinity;

    public int TrailStride { get; set; } = 1;
    public List<WorldPoint> ZTrail { get; } = new();
    public List<WorldPoint> VTrail { get; } = new();

    public void Start() => Running = true;
    public void Pause() => Running = false;

    public void ResetTrails()
    {
        ZTrail.Clear();
        VTrail.Clear();
        AddTrailPoint();
    }

    public void SetZ(double x, double y)
    {
        ZStrategy = new RunAwayStrategy(x, y);
        AddTrailPoint();
    }

    public void SetV(double x, double y)
    {
        VStrategy = new GreedyChaseStrategy(x, y);
        AddTrailPoint();
    }

    public void AddCircleAt(double x, double y, double r) => Circles.Add(new Circle(x, y, r));
    public void ClearCircles() => Circles.Clear();

    public void Tick()
    {
        if (!Running) return;

        for (int i = 0; i < IterationsPerTick; i++)
        {
            Step();
            if (StopAtIteration.HasValue && Iteration >= StopAtIteration.Value)
            {
                Running = false;
                break;
            }
        }
    }

    public void Step()
    {
        Iteration++;

        var zpoint= ZStrategy.Next(VStrategy.Point, Iteration);
        VStrategy.Next(zpoint, Iteration);

        CurrentDistance = Distance(ZStrategy.Point, VStrategy.Point);
        if (CurrentDistance < MinDistance) MinDistance = CurrentDistance;

        if (TrailStride <= 1 || (Iteration % TrailStride) == 0) AddTrailPoint();
    }

    public void RunSteps(long n)
    {
        for (long i = 0; i < n; i++) Step();
    }

    public void RunUntil(long target)
    {
        StopAtIteration = target;
        Running = true;
    }

    static double Distance(in WorldPoint a, in WorldPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    void AddTrailPoint()
    {
        ZTrail.Add(ZStrategy.Point);
        VTrail.Add(VStrategy.Point);
    }
    
    public void ResetAll(WorldPoint? z = null, WorldPoint? v = null)
    {
        Running = false;
        Iteration = 0;
        StopAtIteration = null;
        MinDistance = double.PositiveInfinity;

        z = z ?? new WorldPoint(0, 0);
        v = v ?? new WorldPoint(0, 0);
        
        ZStrategy = new RunAwayStrategy(z.X, z.Y);
        VStrategy = new GreedyChaseStrategy(v.X, v.Y);

        ZTrail.Clear();
        VTrail.Clear();

        CurrentDistance = Math.Sqrt((ZStrategy.Point.X - VStrategy.Point.X) * (ZStrategy.Point.X - VStrategy.Point.X) + (ZStrategy.Point.Y - VStrategy.Point.Y) * (ZStrategy.Point.Y - VStrategy.Point.Y));
        AddTrailPoint();
    }
}
