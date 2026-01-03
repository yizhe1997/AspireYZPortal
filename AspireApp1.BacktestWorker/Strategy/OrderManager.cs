namespace AspireApp1.BacktestWorker.Strategy;

public class Position
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime EntryTime { get; set; }
    public TradeSide Side { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal StopLoss { get; set; }
    public decimal TakeProfit { get; set; }
    public decimal InitialRisk { get; set; }
    public decimal HighestEquity { get; set; }
    public decimal LowestEquity { get; set; }
    public Zone? EntryZone { get; set; }
}

public enum TradeSide
{
    Long,
    Short
}

public class OrderManager
{
    private readonly decimal _stopLossAtrMultiple;
    private readonly decimal _takeProfitRMultiple;
    private readonly decimal _riskPerTradePct;
    private readonly int _maxConcurrentTrades;
    private readonly int _limitOrderOffsetTicks;
    private readonly decimal _tickSize = 0.1m; // GC futures tick size

    public OrderManager(
        decimal stopLossAtrMultiple,
        decimal takeProfitRMultiple,
        decimal riskPerTradePct,
        int maxConcurrentTrades,
        int limitOrderOffsetTicks)
    {
        _stopLossAtrMultiple = stopLossAtrMultiple;
        _takeProfitRMultiple = takeProfitRMultiple;
        _riskPerTradePct = riskPerTradePct;
        _maxConcurrentTrades = maxConcurrentTrades;
        _limitOrderOffsetTicks = limitOrderOffsetTicks;
    }

    public bool CanEnterTrade(List<Position> activePositions)
    {
        return activePositions.Count < _maxConcurrentTrades;
    }

    public Position? TryEnterLong(BarData bar, Zone demandZone, decimal atr, decimal currentEquity)
    {
        // Limit order at zone high + offset
        var limitPrice = demandZone.High + (_limitOrderOffsetTicks * _tickSize);
        
        // Check if limit order would fill
        if (bar.Low <= limitPrice)
        {
            var entryPrice = Math.Min(limitPrice, bar.High);
            var stopLoss = demandZone.Low - (_stopLossAtrMultiple * atr);
            var initialRisk = entryPrice - stopLoss;
            
            if (initialRisk <= 0)
            {
                return null; // Invalid risk
            }

            var riskAmount = currentEquity * (_riskPerTradePct / 100m);
            var quantity = riskAmount / initialRisk;
            
            var takeProfit = entryPrice + (initialRisk * _takeProfitRMultiple);

            return new Position
            {
                EntryTime = bar.Timestamp,
                Side = TradeSide.Long,
                EntryPrice = entryPrice,
                Quantity = quantity,
                StopLoss = stopLoss,
                TakeProfit = takeProfit,
                InitialRisk = initialRisk,
                HighestEquity = entryPrice * quantity,
                LowestEquity = entryPrice * quantity,
                EntryZone = demandZone
            };
        }

        return null;
    }

    public Position? TryEnterShort(BarData bar, Zone supplyZone, decimal atr, decimal currentEquity)
    {
        // Limit order at zone low - offset
        var limitPrice = supplyZone.Low - (_limitOrderOffsetTicks * _tickSize);
        
        // Check if limit order would fill
        if (bar.High >= limitPrice)
        {
            var entryPrice = Math.Max(limitPrice, bar.Low);
            var stopLoss = supplyZone.High + (_stopLossAtrMultiple * atr);
            var initialRisk = stopLoss - entryPrice;
            
            if (initialRisk <= 0)
            {
                return null; // Invalid risk
            }

            var riskAmount = currentEquity * (_riskPerTradePct / 100m);
            var quantity = riskAmount / initialRisk;
            
            var takeProfit = entryPrice - (initialRisk * _takeProfitRMultiple);

            return new Position
            {
                EntryTime = bar.Timestamp,
                Side = TradeSide.Short,
                EntryPrice = entryPrice,
                Quantity = quantity,
                StopLoss = stopLoss,
                TakeProfit = takeProfit,
                InitialRisk = initialRisk,
                HighestEquity = entryPrice * quantity,
                LowestEquity = entryPrice * quantity,
                EntryZone = supplyZone
            };
        }

        return null;
    }

    public void UpdatePositionExtremes(Position position, BarData bar)
    {
        if (position.Side == TradeSide.Long)
        {
            var currentEquity = bar.Close * position.Quantity;
            position.HighestEquity = Math.Max(position.HighestEquity, bar.High * position.Quantity);
            position.LowestEquity = Math.Min(position.LowestEquity, bar.Low * position.Quantity);
        }
        else
        {
            var currentEquity = (2 * position.EntryPrice - bar.Close) * position.Quantity;
            position.HighestEquity = Math.Max(position.HighestEquity, (2 * position.EntryPrice - bar.Low) * position.Quantity);
            position.LowestEquity = Math.Min(position.LowestEquity, (2 * position.EntryPrice - bar.High) * position.Quantity);
        }
    }
}
