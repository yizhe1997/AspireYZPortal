# Data Model: Backtesting Framework as a Service

**Feature**: 001-backtesting-service  
**Date**: 2026-01-03  
**Status**: Complete

## Overview

This document defines the domain entities, their relationships, validation rules, and state transitions for the backtesting platform. The data model supports historical market data storage, backtest execution tracking, and results persistence.

---

## Entity Relationship Diagram

```
┌─────────────┐
│  Strategy   │
│             │
│ - id (PK)   │
│ - name      │
│ - version   │
│ - code_ref  │
└──────┬──────┘
       │
       │ 1:N
       │
┌──────▼────────────┐        ┌────────────────┐
│  BacktestRun      │◄───────│  MarketData    │
│                   │  N:1   │                │
│ - id (PK)         │        │ - symbol (PK)  │
│ - strategy_id (FK)│        │ - timeframe    │
│ - symbol          │        │ - timestamp    │
│ - timeframe       │        │ - open/high/   │
│ - start_date      │        │   low/close    │
│ - end_date        │        │ - volume       │
│ - parameters      │        └────────────────┘
│ - status          │
│ - progress        │
│ - worker_id       │
└────┬─────┬────┬───┘
     │     │    │
     │     │    │ 1:N
     │     │    │
     │     │    ▼
     │     │   ┌──────────────┐
     │     │   │DetectedZone  │
     │     │   │              │
     │     │   │- id (PK)     │
     │     │   │- run_id (FK) │
     │     │   │- timestamp   │
     │     │   │- zone_type   │
     │     │   │- high/low    │
     │     │   │- strength    │
     │     │   │- touches     │
     │     │   └──────────────┘
     │     │
     │     │ 1:N
     │     │
     │     ▼
     │    ┌──────────────┐
     │    │ RunMetrics   │
     │    │              │
     │    │- run_id (FK) │
     │    │- metric_name │
     │    │- metric_value│
     │    └──────────────┘
     │
     │ 1:N
     │
     ▼
    ┌──────────────┐        ┌──────────────┐
    │EquityCurve   │        │    Trade     │
    │              │        │              │
    │- run_id (FK) │        │- id (PK)     │
    │- timestamp   │        │- run_id (FK) │
    │- equity      │        │- entry_time  │
    │- drawdown_pct│        │- exit_time   │
    └──────────────┘        │- side        │
                            │- prices      │
                            │- pnl         │
                            │- r_multiple  │
                            │- mae/mfe     │
                            │- zone_info   │
                            └──────────────┘
```

---

## Core Entities

### 1. Strategy

Represents a backtesting strategy configuration and its versioning.

**Properties**:
- `Id` (Guid, PK): Unique identifier
- `Name` (string, required, max 255): Strategy name (e.g., "Supply/Demand Zones v1")
- `Version` (string, required, max 50): Semantic version (e.g., "1.0.0")
- `Description` (string, optional): Human-readable description
- `CodeRef` (string, optional): Git commit SHA or reference to strategy code
- `CreatedAt` (DateTime, UTC): Creation timestamp
- `UpdatedAt` (DateTime, UTC): Last modification timestamp

**Relationships**:
- Has many `BacktestRun`s (1:N)

**Validation**:
- Name is required and trimmed
- Version follows semantic versioning pattern (regex: `^\d+\.\d+\.\d+$`)
- CodeRef is optional Git SHA (40 hex chars) or branch name

**State Transitions**: Immutable after creation (versioning creates new strategy record)

---

### 2. MarketData

Represents historical OHLCV (Open-High-Low-Close-Volume) price bars.

**Properties**:
- `Symbol` (string, PK composite, max 20): Ticker symbol (e.g., "GC")
- `Timeframe` (string, PK composite, max 10): Bar period (e.g., "1h", "4h", "1d")
- `Timestamp` (DateTime, PK composite, UTC): Bar start time
- `Open` (decimal(18,8)): Opening price
- `High` (decimal(18,8)): Highest price
- `Low` (decimal(18,8)): Lowest price
- `Close` (decimal(18,8)): Closing price
- `Volume` (decimal(18,2)): Trading volume

**Relationships**:
- Referenced by `BacktestRun` (N:1 via symbol/timeframe, not enforced FK)

**Validation**:
- High >= Low (price logic)
- High >= Open, High >= Close
- Low <= Open, Low <= Close
- All prices > 0
- Volume >= 0
- Timestamp must be in UTC

**Indexes**:
- Composite primary key: (Symbol, Timeframe, Timestamp)
- Index on (Symbol, Timeframe, Timestamp) for range queries

**State Transitions**: Immutable after insertion (duplicates skipped during upload)

---

### 3. BacktestRun

Represents a single execution of a backtesting strategy.

**Properties**:
- `Id` (Guid, PK): Unique run identifier
- `StrategyId` (Guid, FK): Reference to Strategy
- `Symbol` (string, required, max 20): Symbol being backtested
- `Timeframe` (string, required, max 10): Timeframe (1h/4h/1d)
- `StartDate` (DateOnly): Backtest start date (inclusive)
- `EndDate` (DateOnly): Backtest end date (inclusive)
- `Parameters` (JSON): Strategy parameters object
- `Status` (enum): queued | running | completed | failed | cancelled
- `Progress` (int, 0-100): Execution progress percentage
- `StartedAt` (DateTime?, UTC): When worker began execution
- `FinishedAt` (DateTime?, UTC): When execution completed/failed/cancelled
- `WorkerId` (string?, max 100): Worker instance that processed this run
- `ErrorMessage` (string?, max 1000): Error details if failed
- `CreatedAt` (DateTime, UTC): When run was submitted
- `UserId` (Guid): User who submitted the run (for isolation)
- `InitialCapital` (decimal(18,2)): Starting account balance ($50,000 USD)

**Relationships**:
- Belongs to `Strategy` (N:1)
- Has many `Trade`s (1:N)
- Has many `RunMetrics` (1:N)
- Has many `EquityCurve` points (1:N)
- Has many `DetectedZone`s (1:N)

**Validation**:
- EndDate > StartDate
- EndDate - StartDate <= 3 years (max span)
- Symbol and Timeframe must have data in MarketData table for date range
- Parameters JSON must validate against schema (see Parameters section)
- Status transitions follow state machine (see below)
- Progress 0-100

**Indexes**:
- Primary key: Id
- Index on (Status, CreatedAt) for queue ordering
- Index on (UserId, CreatedAt) for user's run list

**State Transitions**:
```
[Submitted] → queued → running → completed
                  ↓         ↓
                  ↓         ├─→ failed
                  ↓         └─→ cancelled
                  └──────────────→ cancelled

Valid transitions:
- queued → running (worker picks up job)
- queued → cancelled (user cancels before execution)
- running → completed (success)
- running → failed (error during execution)
- running → cancelled (user cancels during execution)

Invalid transitions:
- completed/failed/cancelled → any other state (terminal states)
- running → queued (no rollback)
```

---

### 4. Trade

Represents a single completed trade during backtest execution.

**Properties**:
- `Id` (Guid, PK): Unique trade identifier
- `RunId` (Guid, FK): Reference to BacktestRun
- `Symbol` (string, required, max 20): Symbol traded
- `EntryTime` (DateTime, UTC): Entry timestamp
- `ExitTime` (DateTime, UTC): Exit timestamp
- `Side` (enum): long | short
- `EntryPrice` (decimal(18,8)): Entry price
- `ExitPrice` (decimal(18,8)): Exit price
- `Quantity` (decimal(18,8)): Position size (contracts/shares)
- `Pnl` (decimal(18,2)): Profit/loss in dollars
- `PnlPct` (decimal(8,4)): Profit/loss percentage
- `RMultiple` (decimal(8,4)): R-multiple (pnl / initial risk)
- `Mae` (decimal(8,4)): Maximum Adverse Excursion (%)
- `Mfe` (decimal(8,4)): Maximum Favorable Excursion (%)
- `HoldingBars` (int): Number of bars held
- `ZoneType` (string, max 50): "supply" | "demand"
- `ZoneStrength` (decimal(8,4)): Zone strength score
- `FillType` (string, max 20): "limit" | "market" (future extensibility)
- `Notes` (string?, max 500): Optional trade notes

**Relationships**:
- Belongs to `BacktestRun` (N:1)

**Validation**:
- ExitTime > EntryTime
- Side is "long" or "short"
- For long: Pnl = (ExitPrice - EntryPrice) * Quantity
- For short: Pnl = (EntryPrice - ExitPrice) * Quantity
- PnlPct = (Pnl / (EntryPrice * Quantity)) * 100
- RMultiple = Pnl / InitialRisk (where InitialRisk = ATR * stop_loss_atr_multiple)
- Mae <= 0, Mfe >= 0
- HoldingBars > 0

**Indexes**:
- Primary key: Id
- Index on (RunId, EntryTime) for time-ordered retrieval
- Index on (RunId, Side) for filtering
- Index on (RunId, ZoneType) for filtering

**State Transitions**: Immutable after insertion (trades never updated, only created)

---

### 5. DetectedZone

Represents a supply or demand zone identified during backtest execution.

**Properties**:
- `Id` (Guid, PK): Unique zone identifier
- `RunId` (Guid, FK): Reference to BacktestRun
- `Timestamp` (DateTime, UTC): When zone was detected
- `ZoneType` (enum): supply | demand
- `High` (decimal(18,8)): Zone upper boundary price
- `Low` (decimal(18,8)): Zone lower boundary price
- `Strength` (decimal(8,4)): Zone strength score (0-1)
- `Touches` (int): Number of times price touched zone
- `CreatedAt` (DateTime, UTC): When zone was first detected

**Relationships**:
- Belongs to `BacktestRun` (N:1)

**Validation**:
- High > Low
- ZoneType is "supply" or "demand"
- Strength between 0 and 1
- Touches >= 1

**Indexes**:
- Primary key: Id
- Index on (RunId, Timestamp) for time-ordered retrieval

**State Transitions**: Immutable after insertion

---

### 6. RunMetrics

Represents calculated performance metrics for a backtest run.

**Properties**:
- `RunId` (Guid, PK composite, FK): Reference to BacktestRun
- `MetricName` (string, PK composite, max 100): Metric name (e.g., "win_rate", "sharpe_ratio")
- `MetricValue` (decimal(18,8)): Calculated value

**Relationships**:
- Belongs to `BacktestRun` (N:1)

**Validation**:
- MetricName is one of predefined set: win_rate, avg_r_multiple, sharpe_ratio, max_drawdown, profit_factor, total_trades, winning_trades, losing_trades, gross_profit, gross_loss
- MetricValue constraints vary by metric (e.g., win_rate 0-100, max_drawdown negative)

**Indexes**:
- Composite primary key: (RunId, MetricName)

**Metric Calculations**:
```
win_rate = (winning_trades / total_trades) * 100
avg_r_multiple = avg((exit - entry) / (entry - stop_loss))
sharpe_ratio = (mean(daily_returns) / std(daily_returns)) * sqrt(252)
max_drawdown = min((equity - peak) / peak) * 100
profit_factor = gross_profit / gross_loss
```

**State Transitions**: Immutable after insertion (metrics calculated once at run completion)

---

### 7. EquityCurve

Represents account equity at specific points in time during backtest.

**Properties**:
- `RunId` (Guid, PK composite, FK): Reference to BacktestRun
- `Timestamp` (DateTime, PK composite, UTC): Bar timestamp
- `Equity` (decimal(18,2)): Account equity at this point
- `DrawdownPct` (decimal(8,4)): Drawdown percentage from peak

**Relationships**:
- Belongs to `BacktestRun` (N:1)

**Validation**:
- Equity > 0
- DrawdownPct <= 0 (drawdown is negative or zero)
- First point: Equity = InitialCapital, DrawdownPct = 0
- DrawdownPct = ((Equity - Peak) / Peak) * 100 where Peak is max equity up to this point

**Indexes**:
- Composite primary key: (RunId, Timestamp)

**State Transitions**: Immutable after insertion (one point per bar, generated during backtest)

---

## Aggregate Root

**BacktestRun** is the aggregate root. All child entities (Trades, Metrics, EquityCurve, DetectedZones) are lifecycle-bound to the run. Deleting a BacktestRun cascades to all child entities.

---

## Parameters Schema

The `BacktestRun.Parameters` JSON field follows this schema:

```json
{
  "zone_lookback_bars": 100,          // int [20, 500]
  "zone_min_touches": 2,              // int [2, 10]
  "zone_width_atr_multiple": 0.5,     // decimal [0.1, 3.0]
  "zone_max_age_bars": 500,           // int [50, 2000]
  "require_confirmation": true,       // bool
  "stop_loss_atr_multiple": 2.0,      // decimal [0.5, 10.0]
  "take_profit_r_multiple": 2.0,      // decimal [0.5, 10.0]
  "risk_per_trade_pct": 1.0,          // decimal [0.1, 10.0]
  "max_concurrent_trades": 2,         // int [1, 10]
  "session_filter": ["NY_AM"],        // array of: "NY_AM" | "NY_PM" | "Asia" | "Europe"
  "limit_order_offset_ticks": 1       // int [0, 20]
}
```

Validation enforced at API layer before queueing. Server-side defaults applied for missing fields.

---

## Data Retention Policy

- **BacktestRun + children**: 90 days after FinishedAt
- **MarketData**: 30 days after last use (tracked via access timestamp)
- **Strategy**: Retained indefinitely (immutable versions)

Automated purge scripts run daily to enforce retention.

---

## EF Core Implementation Notes

**DbContext**:
```csharp
public class BacktestDbContext : DbContext
{
    public DbSet<Strategy> Strategies { get; set; }
    public DbSet<MarketData> MarketData { get; set; }
    public DbSet<BacktestRun> BacktestRuns { get; set; }
    public DbSet<Trade> Trades { get; set; }
    public DbSet<DetectedZone> DetectedZones { get; set; }
    public DbSet<RunMetrics> RunMetrics { get; set; }
    public DbSet<EquityCurve> EquityCurve { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Composite keys
        modelBuilder.Entity<MarketData>()
            .HasKey(m => new { m.Symbol, m.Timeframe, m.Timestamp });
            
        modelBuilder.Entity<RunMetrics>()
            .HasKey(m => new { m.RunId, m.MetricName });
            
        modelBuilder.Entity<EquityCurve>()
            .HasKey(e => new { e.RunId, e.Timestamp });
        
        // JSON column
        modelBuilder.Entity<BacktestRun>()
            .Property(r => r.Parameters)
            .HasColumnType("jsonb");
        
        // Cascade deletes
        modelBuilder.Entity<BacktestRun>()
            .HasMany(r => r.Trades)
            .WithOne()
            .OnDelete(DeleteBehavior.Cascade);
            
        // Indexes
        modelBuilder.Entity<BacktestRun>()
            .HasIndex(r => new { r.Status, r.CreatedAt });
            
        modelBuilder.Entity<Trade>()
            .HasIndex(t => new { t.RunId, t.EntryTime });
    }
}
```

**Nullable Reference Types**: Enabled (`<Nullable>enable</Nullable>`) - all nullable fields explicitly marked with `?`.

**Enums**: Use string-based enums for Status, Side, ZoneType (easier debugging and database inspection).

---

## Summary

The data model supports:
- ✅ Historical market data storage with composite key integrity
- ✅ Backtest run lifecycle tracking with state machine
- ✅ Granular trade details for analysis
- ✅ Zone detection persistence for visualization
- ✅ Equity curve generation for charting
- ✅ Flexible strategy parameters via JSON
- ✅ Data isolation per user
- ✅ Cascade deletes for aggregate root pattern
- ✅ Efficient querying via indexes
- ✅ Immutable historical records (trades, metrics, equity)
