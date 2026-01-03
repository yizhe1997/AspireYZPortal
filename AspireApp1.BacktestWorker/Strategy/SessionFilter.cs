namespace AspireApp1.BacktestWorker.Strategy;

public class SessionFilter
{
    private readonly bool _includeAsian;
    private readonly bool _includeLondon;
    private readonly bool _includeNewYork;

    public SessionFilter(bool includeAsian, bool includeLondon, bool includeNewYork)
    {
        _includeAsian = includeAsian;
        _includeLondon = includeLondon;
        _includeNewYork = includeNewYork;
    }

    public bool IsInActiveSession(DateTime timestamp)
    {
        // Convert to UTC for consistent session checks
        var utcTime = timestamp.ToUniversalTime();
        var hour = utcTime.Hour;

        // Asian session: 00:00-09:00 UTC
        var isAsian = hour >= 0 && hour < 9;
        
        // London session: 08:00-17:00 UTC
        var isLondon = hour >= 8 && hour < 17;
        
        // New York session: 13:00-22:00 UTC
        var isNewYork = hour >= 13 && hour < 22;

        if (_includeAsian && isAsian) return true;
        if (_includeLondon && isLondon) return true;
        if (_includeNewYork && isNewYork) return true;

        return false;
    }

    public string GetActiveSession(DateTime timestamp)
    {
        var utcTime = timestamp.ToUniversalTime();
        var hour = utcTime.Hour;

        if (hour >= 0 && hour < 9) return "Asian";
        if (hour >= 8 && hour < 13) return "London";
        if (hour >= 13 && hour < 17) return "London/NY Overlap";
        if (hour >= 17 && hour < 22) return "New York";
        
        return "Off-Hours";
    }
}
