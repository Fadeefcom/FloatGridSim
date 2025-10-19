# FloatGridSim — стратегии акторов

## Требуется .NET SDK 8

Минимальная версия для сборки и запуска: **.NET SDK 8.0**  
Скачать: https://dotnet.microsoft.com/en-us/download/dotnet/8.0

### Быстрый старт
Проверка установки:
```bash
cd FloatGridSim
dotnet --info
dotnet restore && dotnet build -c Release && dotnet run -c Release
```
Для разработки достаточно установить .NET SDK 8.0.

## Обзор
В проекте моделируются два актора на плоскости:
- **Заяц (Z)** — убегает и на каждом шаге может предоставить волку любую точку в радиусе `1` от своей текущей позиции.
- **Волк (V)** — пытается догнать зайца, используя координату, которую тот ему передал на текущем шаге.

Поведение акторов инкапсулировано в стратегиях, реализующих единый интерфейс.

## Интерфейс стратегий
```csharp
public interface IActorStrategy
{
    public WorldPoint Point { get; set; }
    public WorldPoint Next(WorldPoint other, long iteration);
}
```
`Point` — фактическая позиция актора в мировых координатах.  
`Next(other, iteration)` — вычисляет и возвращает новую позицию актора. Параметр `other` — точка, которую актору предоставил оппонент на этом шаге.

## Реализации

### RunAwayStrategy (заяц)
Идея: заяц движется по «слегка смещённой» прямой, но наружу возвращает точку на фиксированной базовой прямой `other → _lastPoint`, соблюдая ограничение на радиус (не дальше 1 от реального движения/проекции). В начале каждой фазы:
1) Базовое направление берётся от `other` к текущей точке.  
2) Вычисляется небольшой угол смещения `delta = sin(1/(len+add))`, где `len` — текущее расстояние `|Point - other|`, `add` — максимально возможная добавка к длине фазы.  
3) Реальная позиция смещается по вектору, повёрнутому на `delta`.  
4) Во внешнем протоколе заяц отдаёт точку на базовой прямой, соответствующую текущему положению (ортогональная проекция), либо предыдущую точку, если перпендикулярное расстояние > 1.

Фрагмент (сокращённо, без комментариев внутри функций):
```csharp
public class RunAwayStrategy : IActorStrategy
{
    public RunAwayStrategy(double x, double y)
    {
        Point = new WorldPoint(x, y);
    }

    int _step = 1;
    Vector _dir, _offsetDir;
    int _stepsLeft;
    WorldPoint _lastPoint;

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

        var temp = new WorldPoint(Point.X, Point.Y);
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

        if (dist > 1) return temp;
        return proj;
    }

    static WorldPoint AnyPointAtUnitDistance(WorldPoint p)
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
```

### GreedyChaseStrategy (волк)
Идея: волк использует точку, предоставленную зайцем, и двигается так, чтобы минимизировать расстояние. Используется геометрия окружностей: большая окружность радиуса `R = iteration * step` вокруг начала координат и малая окружность радиуса `step` вокруг точки `other`. Если есть пересечения, выбирается «лучшая» цель среди двух точек; иначе — прямой шаг к `other`.

Фрагмент (сокращённо):
```csharp
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

        if (d < 1e-12) return MoveToward(new WorldPoint(R, 0));

        var r = _step;
        if (d > R + r || d < Math.Abs(R - r)) return MoveToward(other);

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

        var dist1 = (cand1.X - Point.X) * (cand1.X - Point.X) + (cand1.Y - Point.Y) * (cand1.Y - Point.Y);
        var dist2 = (cand2.X - Point.X) * (cand2.X - Point.X) + (cand2.Y - Point.Y) * (cand2.Y - Point.Y);

        var target = dist1 >= dist2 ? cand1 : cand2;
        return MoveToward(target);
    }

    WorldPoint MoveToward(WorldPoint target)
    {
        var dx = target.X - Point.X;
        var dy = target.Y - Point.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-12) return Point;
        var k = _step / len;
        var next = new WorldPoint(Point.X + dx * k, Point.Y + dy * k);
        Point = next;
        return next;
    }
}
```

## Пошаговая динамика симуляции
```csharp
public void Step()
{
    Iteration++;
    var zpoint = ZStrategy.Next(VStrategy.Point, Iteration);
    VStrategy.Next(zpoint, Iteration);
    CurrentDistance = Distance(ZStrategy.Point, VStrategy.Point);
    if (CurrentDistance < MinDistance) MinDistance = CurrentDistance;
    if (TrailStride <= 1 || (Iteration % TrailStride) == 0) AddTrailPoint();
}
```
1) Заяц делает ход и отдаёт волку допустимую точку `zpoint`.  
2) Волк делает ход, используя `zpoint`.  
3) Обновляются расстояния и трассы.

## Как добавить свою стратегию
1) Создайте класс, реализующий `IActorStrategy`.  
2) В конструкторе задайте начальную `Point`.  
3) Реализуйте `Next(other, iteration)` согласно вашим правилам.  
4) Подставьте в симуляцию для Z или V.

## Управление и сборка
- Управление зумом, панорамой и скоростью симуляции доступно в UI.  
- Автозум можно включить всегда или через флаг.  
- Для больших прогонов используйте прореживание трассы.

Сборка и запуск:
```
dotnet clean && dotnet build -c Release && dotnet run -c Release
```

## Видео-демонстрация
<video src="demo.mp4" controls width="720"></video>
