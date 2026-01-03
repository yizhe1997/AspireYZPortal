namespace AspireApp1.BacktestWorker.Strategy;

public class Zone
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; }
    public ZoneType Type { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Strength { get; set; }
    public int Touches { get; set; }
    public int AgeInBars { get; set; }
}

public enum ZoneType
{
    Supply,
    Demand
}

public class ZoneDetector
{
    private readonly int _lookbackBars;
    private readonly int _minTouches;
    private readonly decimal _widthAtrMultiple;
    private readonly int _maxAgeBars;

    public ZoneDetector(int lookbackBars, int minTouches, decimal widthAtrMultiple, int maxAgeBars)
    {
        _lookbackBars = lookbackBars;
        _minTouches = minTouches;
        _widthAtrMultiple = widthAtrMultiple;
        _maxAgeBars = maxAgeBars;
    }

    public List<Zone> DetectZones(List<BarData> bars, int currentIndex, decimal currentATR)
    {
        var zones = new List<Zone>();
        
        if (currentIndex < _lookbackBars)
        {
            return zones;
        }

        var lookbackBars = bars.Skip(currentIndex - _lookbackBars).Take(_lookbackBars).ToList();
        
        // Find supply zones (swing highs)
        var supplyZones = FindSupplyZones(lookbackBars, currentATR);
        zones.AddRange(supplyZones);

        // Find demand zones (swing lows)
        var demandZones = FindDemandZones(lookbackBars, currentATR);
        zones.AddRange(demandZones);

        return zones;
    }

    private List<Zone> FindSupplyZones(List<BarData> bars, decimal atr)
    {
        var zones = new List<Zone>();
        var zoneWidth = atr * _widthAtrMultiple;

        for (int i = 2; i < bars.Count - 2; i++)
        {
            var current = bars[i];
            
            // Check if swing high (higher than 2 bars on each side)
            if (current.High > bars[i - 1].High && 
                current.High > bars[i - 2].High &&
                current.High > bars[i + 1].High && 
                current.High > bars[i + 2].High)
            {
                var zoneHigh = current.High;
                var zoneLow = current.High - zoneWidth;
                
                // Count touches
                var touches = CountZoneTouches(bars, zoneLow, zoneHigh, i);
                
                if (touches >= _minTouches)
                {
                    zones.Add(new Zone
                    {
                        Timestamp = current.Timestamp,
                        Type = ZoneType.Supply,
                        High = zoneHigh,
                        Low = zoneLow,
                        Strength = CalculateZoneStrength(touches, bars.Count - i),
                        Touches = touches,
                        AgeInBars = bars.Count - i
                    });
                }
            }
        }

        return zones;
    }

    private List<Zone> FindDemandZones(List<BarData> bars, decimal atr)
    {
        var zones = new List<Zone>();
        var zoneWidth = atr * _widthAtrMultiple;

        for (int i = 2; i < bars.Count - 2; i++)
        {
            var current = bars[i];
            
            // Check if swing low (lower than 2 bars on each side)
            if (current.Low < bars[i - 1].Low && 
                current.Low < bars[i - 2].Low &&
                current.Low < bars[i + 1].Low && 
                current.Low < bars[i + 2].Low)
            {
                var zoneLow = current.Low;
                var zoneHigh = current.Low + zoneWidth;
                
                // Count touches
                var touches = CountZoneTouches(bars, zoneLow, zoneHigh, i);
                
                if (touches >= _minTouches)
                {
                    zones.Add(new Zone
                    {
                        Timestamp = current.Timestamp,
                        Type = ZoneType.Demand,
                        High = zoneHigh,
                        Low = zoneLow,
                        Strength = CalculateZoneStrength(touches, bars.Count - i),
                        Touches = touches,
                        AgeInBars = bars.Count - i
                    });
                }
            }
        }

        return zones;
    }

    private int CountZoneTouches(List<BarData> bars, decimal zoneLow, decimal zoneHigh, int zoneIndex)
    {
        var touches = 0;
        
        for (int i = zoneIndex + 1; i < bars.Count; i++)
        {
            var bar = bars[i];
            
            // Check if bar touched the zone
            if (bar.Low <= zoneHigh && bar.High >= zoneLow)
            {
                touches++;
            }
        }

        return touches;
    }

    private decimal CalculateZoneStrength(int touches, int age)
    {
        // Simple strength calculation: more touches = stronger, but decay over time
        var touchScore = Math.Min(touches / 5.0m, 1.0m); // Max 1.0 at 5+ touches
        var ageDecay = Math.Max(1.0m - (age / (decimal)_maxAgeBars), 0.1m); // Min 0.1
        
        return touchScore * ageDecay;
    }

    public List<Zone> FilterActiveZones(List<Zone> zones, int currentBarIndex)
    {
        return zones.Where(z => z.AgeInBars <= _maxAgeBars).ToList();
    }
}

public class BarData
{
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
}
