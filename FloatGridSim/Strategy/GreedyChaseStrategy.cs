namespace FloatGridSim.Strategy;

public sealed class GreedyChaseStrategy : IActorStrategy
{
    readonly double _step;

    public GreedyChaseStrategy(double x, double y)
    {
        Point = new WorldPoint(x, y);
        _step = 1;
    }

    public WorldPoint Point { get; set; }
    
    public WorldPoint Next(WorldPoint other, long iteration)
    {
        var R = Math.Max(_step, iteration * _step);
        var ox = other.X;
        var oy = other.Y;
        var d = Math.Sqrt(ox * ox + oy * oy);

        if (d < 1e-12)
        {
            return MoveToward(new WorldPoint(R, 0));
        }

        var r = _step;

        if (d > R + r || d < Math.Abs(R - r))
        {
            return MoveToward(other);
        }

        var a = (R * R - r * r + d * d) / (2.0 * d);
        var h2 = R * R - a * a;
        if (h2 < 0) h2 = 0;
        var h = Math.Sqrt(h2);

        var ux = ox / d;
        var uy = oy / d;
        var px = ux * a;
        var py = uy * a;

        var rx = -uy;
        var ry = ux;

        var x1 = px + h * rx;
        var y1 = py + h * ry;
        var x2 = px - h * rx;
        var y2 = py - h * ry;

        var cand1 = new WorldPoint(x1, y1);
        var cand2 = new WorldPoint(x2, y2);

        var dist1 = Sqr(cand1.X - Point.X) + Sqr(cand1.Y - Point.Y);
        var dist2 = Sqr(cand2.X - Point.X) + Sqr(cand2.Y - Point.Y);

        var target = dist1 >= dist2 ? cand1 : cand2;
        return MoveToward(target);
    }
    
    WorldPoint MoveToward(WorldPoint target)
    {
        var dx = target.X - Point.X;
        var dy = target.Y - Point.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-12)
            return Point;

        var k = _step / len;
        var next = new WorldPoint(Point.X + dx * k, Point.Y + dy * k);
        Point = next;
        return next;
    }

    static double Sqr(double v) => v * v;
}