namespace FloatGridSim.Strategy;

public interface IActorStrategy
{
    public WorldPoint Point { get; set; }
    
    public WorldPoint Next(WorldPoint other, long iteration);
}