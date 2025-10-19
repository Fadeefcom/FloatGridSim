using System.Windows;

namespace FloatGridSim.Strategy;

public class RunAwayStrategy : IActorStrategy
{
    public RunAwayStrategy(double x, double y)
    {
        Point = new WorldPoint(x, y);
    }
    
    int _step = 1;
    Vector _dir;
    Vector _offsetDir;
    int _stepsLeft;
    private WorldPoint _lastPoint;

    public WorldPoint Point { get; set; }

    public WorldPoint Next(WorldPoint other, long iteration)
    {
        if (iteration <= 1)
        {
            Point = AnyPointAtUnitDistance(other);
            _lastPoint = other;
            return _lastPoint;
        }

        if (_stepsLeft <= 0)
        {
            var rx = Point.X - other.X;
            var ry = Point.Y - other.Y;
            var len = Math.Sqrt(rx * rx + ry * ry);
            if (len < 1e-12)
            {
                Point = AnyPointAtUnitDistance(Point);
                _lastPoint = other;
                return _lastPoint;
            }
            _lastPoint = Point;

            rx /= len;
            ry /= len;
            _dir = new Vector(rx, ry);
            
            var add = int.MaxValue - (int)Math.Floor(len);
            if (add < 1) add = 1;
            
            var delta = Math.Sin(1.0 / (len + add));
            _offsetDir = Rotate(_dir, delta);
            var k = (int)Math.Floor(len) + add;
            _stepsLeft = Math.Max(1, k);
        }
        
        var _tempPoint = new WorldPoint(Point.X, Point.Y);
        Point = new WorldPoint(Point.X + _offsetDir.X * _step, Point.Y + _offsetDir.Y * _step); 
        _stepsLeft--;

        var ox = other.X;
        var oy = other.Y;

        var ux = _dir.X;
        var uy = _dir.Y;

        var px = Point.X - ox;
        var py = Point.Y - oy;

        var s = px * ux + py * uy;
        var proj = new WorldPoint(ox + ux * s, oy + uy * s);

        var dx = Point.X - proj.X;
        var dy = Point.Y - proj.Y;
        var dist = Math.Sqrt(dx * dx + dy * dy);

        if (dist > 1)
            return _tempPoint;
        
        return proj;
    }
    
    private static WorldPoint AnyPointAtUnitDistance(WorldPoint p)
    {
        var u = Random.Shared.NextDouble();
        var a = 2.0 * Math.PI * u;
        return new WorldPoint(p.X + Math.Cos(a), p.Y + Math.Sin(a));
    }
    
    static Vector Rotate(Vector v, double ang)
    {
        var c = Math.Cos(ang);
        var s = Math.Sin(ang);
        return new Vector(c * v.X - s * v.Y, s * v.X + c * v.Y);
    }
}