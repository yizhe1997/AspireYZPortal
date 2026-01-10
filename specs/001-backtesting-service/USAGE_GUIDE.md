# Usage Guide: Backtesting Framework

**Last Updated**: January 10, 2026  
**Target Audience**: Traders, analysts, developers using the backtesting service  
**Quick Reference**: See [Quick Workflow](#quick-workflow) for the TL;DR

---

## Table of Contents

1. [System Overview](#system-overview)
2. [Complete Workflow](#complete-workflow)
3. [Step-by-Step Guide](#step-by-step-guide)
4. [Real-World Example](#real-world-example)
5. [n8n Automation](#n8n-automation)
6. [API Reference Summary](#api-reference-summary)
7. [FAQ & Troubleshooting](#faq--troubleshooting)

---

## System Overview

The backtesting framework is a **distributed system** with three main components:

```
┌─────────────────────────────────────────────────────────┐
│                   YOUR WORKFLOW                         │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  1. Upload OHLCV Data (CSV)                            │
│  2. Submit Backtest Parameters                         │
│  3. Wait for Execution                                 │
│  4. Retrieve & Analyze Results                         │
│                                                         │
└──────────────────────┬──────────────────────────────────┘
                       │
        ┌──────────────┴──────────────┐
        │                             │
    ┌───▼──────────────┐    ┌────────▼────────┐
    │  BacktestApi     │    │ BacktestWorker  │
    │  (REST Server)   │    │ (Background Job)│
    │  Port: 5000      │    │ Processes Queue │
    └────────┬─────────┘    └────────┬────────┘
             │                       │
             └───────────┬───────────┘
                         │
                    ┌────▼─────────┐
                    │  Redis Queue │
                    │  PostgreSQL  │
                    └──────────────┘
```

**What each component does:**
- **BacktestApi**: Receives your requests, validates data, stores jobs in queue
- **BacktestWorker**: Continuously polls queue, executes backtests, stores results
- **Redis + PostgreSQL**: Queue management + result storage

---

## Complete Workflow

### Visual Flow

```
START
  │
  ├─► [Prepare OHLCV CSV]
  │        │
  │        ▼
  │   [Upload to API]
  │   POST /market-data/upload
  │        │
  │        ▼
  │   [API Validates & Stores]
  │   PostgreSQL: MarketData table
  │        │
  │        ▼
  ├─► [Define Strategy Parameters]
  │        │
  │        ▼
  │   [Submit Backtest]
  │   POST /api/backtests
  │        │
  │        ├──→ Status: "queued"
  │        │    Queue Position: N
  │        │    Estimated Start: HH:MM
  │        │
  │        ▼
  │   [Job Pushed to Redis Queue]
  │        │
  │        ▼
  │   [BacktestWorker Dequeues]
  │        │
  │        ├──→ Status: "running"
  │        │
  │        ▼
  │   [StrategyEngine Executes]
  │   • Fetches market data
  │   • Iterates through bars
  │   • Simulates trades
  │   • Calculates metrics
  │        │
  │        ▼
  │   [Results Persisted]
  │   PostgreSQL:
  │   • BacktestRun (metadata)
  │   • Trades (individual fills)
  │   • RunMetrics (win rate, Sharpe, etc)
  │   • EquityCurve (daily points)
  │        │
  │        ├──→ Status: "completed"
  │        │
  │        ▼
├─► [Poll for Status]
│   GET /api/backtests/{runId}
│        │
│        ▼
├─► [Retrieve Results]
│   GET /api/backtests/{runId}/equity
│   GET /api/backtests/{runId}/trades
│        │
│        ▼
├─► [Analyze & Compare]
│   POST /api/backtests/compare
│   (Multi-run comparison)
│        │
│        ▼
└─► END (Results in PostgreSQL, Charts in Frontend)
```

---

## Step-by-Step Guide

### Prerequisites

**Before you start**, ensure:
- ✅ Aspire is running: `dotnet run --project AspireApp1.AppHost`
- ✅ All services are healthy (check http://localhost:15888)
- ✅ You have market data (CSV format)
- ✅ API Key: `dev_key_12345` (development only)

---

### Phase 1: Prepare & Upload Market Data

**You need**: OHLCV CSV file with columns: `timestamp,open,high,low,close,volume`

**Example CSV (GOLD_1H.csv):**
```csv
timestamp,open,high,low,close,volume
2024-01-01T00:00:00Z,2050.25,2055.00,2048.00,2053.50,12500
2024-01-01T01:00:00Z,2053.50,2058.75,2052.00,2057.25,11200
2024-01-01T02:00:00Z,2057.25,2062.50,2056.00,2060.00,13100
2024-01-01T03:00:00Z,2060.00,2064.25,2058.50,2061.75,12800
```

**Upload via API:**
```bash
curl -X POST http://localhost:5000/api/market-data/upload \
  -H "X-API-Key: dev_key_12345" \
  -F "file=@GOLD_1H.csv"
```

**Response:**
```json
{
  "symbol": "GC",
  "timeframe": "1H",
  "bars_uploaded": 17520,
  "duplicates_skipped": 0,
  "date_range": {
    "start": "2024-01-01",
    "end": "2026-01-09"
  }
}
```

**What to check:**
- ✅ `bars_uploaded > 0` - Data was accepted
- ✅ `duplicates_skipped = 0` - No bad data
- ✅ `date_range` covers your intended backtest period

---

### Phase 2: Submit Backtest

**Define your strategy parameters:**

```bash
curl -X POST http://localhost:5000/api/backtests \
  -H "X-API-Key: dev_key_12345" \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "GC",
    "timeframe": "1H",
    "start_date": "2024-01-01",
    "end_date": "2024-12-31",
    "strategy_name": "ZoneReversal",
    "parameters": {
      "ma_period": 20,
      "risk_pct": 1.5,
      "zone_sensitivity": 0.65,
      "session_filter": "ASIA",
      "volume_threshold": 50,
      "take_profit_atr": 2.0,
      "stop_loss_atr": 1.5
    }
  }'
```

**Response:**
```json
{
  "run_id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "queued",
  "queue_position": 1,
  "estimated_start_time": "2026-01-10T14:32:45Z"
}
```

**What to remember:**
- Save `run_id` - you'll use this to check status and fetch results
- `queue_position: 1` means it's next in line
- ETA is when execution will START (not when it will finish)

---

### Phase 3: Monitor Execution

**Poll for status** (check every 5-10 seconds):

```bash
curl http://localhost:5000/api/backtests/550e8400-e29b-41d4-a716-446655440000 \
  -H "X-API-Key: dev_key_12345"
```

**Response while queued:**
```json
{
  "run_id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "queued",
  "queue_position": 1,
  "estimated_start_time": "2026-01-10T14:32:45Z"
}
```

**Response while running:**
```json
{
  "run_id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "running",
  "queue_position": 0,
  "progress_pct": 45
}
```

**Response when completed:**
```json
{
  "run_id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "completed",
  "symbol": "GC",
  "strategy_name": "ZoneReversal",
  "final_equity": 104500.50,
  "total_trades": 47,
  "win_rate": 0.68,
  "profit_factor": 2.15,
  "sharpe_ratio": 1.82,
  "max_drawdown": 0.12,
  "completed_at": "2026-01-10T14:30:15Z"
}
```

**Status values:**
- `queued` - Waiting in queue
- `running` - Currently executing
- `completed` - Done successfully ✅
- `failed` - Execution error
- `cancelled` - User cancelled

---

### Phase 4: Retrieve Results

**Get equity curve:**
```bash
curl http://localhost:5000/api/backtests/550e8400-e29b-41d4-a716-446655440000/equity \
  -H "X-API-Key: dev_key_12345"
```

**Response:**
```json
{
  "equity_curve": [
    {"timestamp": "2024-01-01T00:00:00Z", "equity": 50000.00},
    {"timestamp": "2024-01-02T00:00:00Z", "equity": 50450.25},
    {"timestamp": "2024-01-03T00:00:00Z", "equity": 49800.50},
    ...
    {"timestamp": "2024-12-31T23:00:00Z", "equity": 104500.50}
  ]
}
```

**Get trade details:**
```bash
curl "http://localhost:5000/api/backtests/550e8400-e29b-41d4-a716-446655440000/trades?limit=10&offset=0" \
  -H "X-API-Key: dev_key_12345"
```

**Response:**
```json
{
  "data": [
    {
      "trade_id": "guid-123",
      "entry_time": "2024-01-05T10:30:00Z",
      "entry_price": 2050.25,
      "exit_time": "2024-01-05T14:45:00Z",
      "exit_price": 2057.50,
      "side": "buy",
      "quantity": 1,
      "profit": 730.00,
      "profit_pct": 0.356,
      "zone_type": "support",
      "mae": -0.0035,
      "mfe": 0.0085,
      "holding_bars": 5
    },
    ...
  ],
  "total": 47,
  "page": 1,
  "page_size": 10
}
```

**View in web UI:**
Navigate to: `http://localhost:3000`

---

## Real-World Example

### Scenario: Trader Validates Supply/Demand Zone Strategy

**Goal**: Test if zone reversal strategy works on 2024 Gold data

**Step 1: Prepare Data**
```bash
# Download 2 years of GC 1H data from your broker
# Save as: GOLD_2024_2025.csv
# Columns: timestamp, open, high, low, close, volume
```

**Step 2: Upload Data**
```bash
curl -X POST http://localhost:5000/api/market-data/upload \
  -H "X-API-Key: dev_key_12345" \
  -F "file=@GOLD_2024_2025.csv"

# Response: 17520 bars uploaded, date range 2024-01-01 to 2025-12-31 ✅
```

**Step 3: Submit Backtest for 2024**
```bash
curl -X POST http://localhost:5000/api/backtests \
  -H "X-API-Key: dev_key_12345" \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "GC",
    "timeframe": "1H",
    "start_date": "2024-01-01",
    "end_date": "2024-12-31",
    "strategy_name": "ZoneReversal",
    "parameters": {
      "ma_period": 20,
      "risk_pct": 1.5,
      "zone_sensitivity": 0.65,
      "session_filter": "ASIA",
      "volume_threshold": 50,
      "take_profit_atr": 2.0,
      "stop_loss_atr": 1.5
    }
  }'

# run_id: abc-123-def
```

**Step 4: Wait & Monitor**
```bash
# Check every 10 seconds for 5 minutes
for i in {1..30}; do
  curl http://localhost:5000/api/backtests/abc-123-def \
    -H "X-API-Key: dev_key_12345" | jq '.status'
  sleep 10
done

# Output:
# "queued"
# "queued"
# "running"
# "running"
# "running"
# "completed"
```

**Step 5: Analyze Results**
```bash
# Get metrics
curl http://localhost:5000/api/backtests/abc-123-def \
  -H "X-API-Key: dev_key_12345" | jq '.win_rate, .sharpe_ratio, .max_drawdown'

# Output:
# 0.68
# 1.82
# 0.12

# Win Rate: 68% ✅
# Sharpe Ratio: 1.82 (good) ✅
# Max Drawdown: 12% (acceptable) ✅
```

**Step 6: Test Parameter Variations**
```bash
# Test with tighter stop loss (1.0x ATR instead of 1.5x)
curl -X POST http://localhost:5000/api/backtests \
  -H "X-API-Key: dev_key_12345" \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "GC",
    "timeframe": "1H",
    "start_date": "2024-01-01",
    "end_date": "2024-12-31",
    "strategy_name": "ZoneReversal-v2",
    "parameters": {
      "ma_period": 20,
      "risk_pct": 1.5,
      "zone_sensitivity": 0.65,
      "session_filter": "ASIA",
      "volume_threshold": 50,
      "take_profit_atr": 2.0,
      "stop_loss_atr": 1.0
    }
  }'

# run_id: xyz-456-uvw
```

**Step 7: Compare Results**
```bash
# Compare original vs tighter stop loss
curl -X POST http://localhost:5000/api/backtests/compare \
  -H "X-API-Key: dev_key_12345" \
  -H "Content-Type: application/json" \
  -d '{
    "run_ids": ["abc-123-def", "xyz-456-uvw"]
  }'

# Response includes:
# - Equity curves overlaid (see which is better)
# - Parameter differences highlighted
# - Metrics side-by-side (win rate, Sharpe, drawdown)
```

**Conclusion**: Trader decides which parameter set performs better and deploys to live trading

---

## n8n Automation

### Use Case: Daily Backtest Monitoring

**Goal**: Every morning, run backtest on latest data and get Slack notification

### n8n Workflow Setup

**Node 1: Cron Trigger**
```
Type: Cron
Schedule: 0 8 * * * (Every day at 8 AM UTC)
```

**Node 2: HTTP - Submit Backtest**
```
Method: POST
URL: http://localhost:5000/api/backtests
Headers: 
  X-API-Key: dev_key_12345
  Content-Type: application/json
Body (JSON):
{
  "symbol": "GC",
  "timeframe": "1H",
  "start_date": "2024-01-01",
  "end_date": "2026-01-10",
  "strategy_name": "ZoneReversal",
  "parameters": {
    "ma_period": 20,
    "risk_pct": 1.5,
    "zone_sensitivity": 0.65,
    "session_filter": "ASIA",
    "volume_threshold": 50,
    "take_profit_atr": 2.0,
    "stop_loss_atr": 1.5
  }
}

Output: Set variable $runId = response.run_id
```

**Node 3: Loop - Poll Until Complete**
```
Type: Loop
Max Iterations: 30
Delay: 10 seconds

Inside Loop:
  HTTP GET: http://localhost:5000/api/backtests/$runId
  Condition: until response.status === "completed"
```

**Node 4: HTTP - Fetch Results**
```
Method: GET
URL: http://localhost:5000/api/backtests/$runId/equity
Headers: X-API-Key: dev_key_12345
```

**Node 5: Slack Notification**
```
Type: Slack
Channel: #trading-alerts
Message: 
📊 Daily Backtest Complete
Strategy: {{ $json.strategy_name }}
Win Rate: {{ ($json.win_rate * 100).toFixed(2) }}%
Sharpe Ratio: {{ $json.sharpe_ratio.toFixed(2) }}
Max Drawdown: {{ ($json.max_drawdown * 100).toFixed(2) }}%
Final Equity: ${{ $json.final_equity.toFixed(2) }}

Total Trades: {{ $json.total_trades }}
Profit Factor: {{ $json.profit_factor.toFixed(2) }}
```

**Node 6: Save to Database (Optional)**
```
Type: PostgreSQL
Query:
INSERT INTO daily_backtests (
  run_id, date, win_rate, sharpe_ratio, 
  max_drawdown, final_equity, total_trades
) VALUES (
  $1, NOW(), $2, $3, $4, $5, $6
)
Parameters:
  $1: $runId
  $2: $json.win_rate
  $3: $json.sharpe_ratio
  $4: $json.max_drawdown
  $5: $json.final_equity
  $6: $json.total_trades
```

### Result

Every morning at 8 AM:
1. ✅ Backtest automatically submits
2. ✅ n8n waits for completion (polls every 10 seconds)
3. ✅ Results fetched
4. ✅ Slack notification sent to team
5. ✅ Results stored in database for historical analysis

---

## API Reference Summary

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/market-data/upload` | POST | Upload OHLCV CSV |
| `/api/market-data` | GET | List available symbols/timeframes |
| `/api/backtests` | POST | Submit new backtest |
| `/api/backtests` | GET | List all backtests (with filters) |
| `/api/backtests/{id}` | GET | Get backtest status & metrics |
| `/api/backtests/{id}/equity` | GET | Get equity curve points |
| `/api/backtests/{id}/trades` | GET | Get individual trades (paginated) |
| `/api/backtests/{id}/cancel` | POST | Cancel queued/running job |
| `/api/backtests/compare` | POST | Compare multiple runs |
| `/api/backtests/queue/depth` | GET | Get queue depth |

**All requests require header**: `X-API-Key: dev_key_12345`

---

## FAQ & Troubleshooting

### Q: How long does a backtest take?
**A:** Approximately 5 minutes for 2 years of 1H data. The worker processes each bar through the strategy engine sequentially.

### Q: Can I run multiple backtests simultaneously?
**A:** Yes - they queue in Redis and workers process them. With 1 worker, you'll see FIFO execution. With multiple workers, they run in parallel.

### Q: My backtest status is stuck on "queued"
**A:** Check:
1. Is the BacktestWorker running? (Check Aspire Dashboard)
2. Is Redis running? (`redis-cli ping` should respond with "PONG")
3. Are there errors in worker logs? (Check Aspire Dashboard for errors)

### Q: How do I cancel a backtest?
**A:** 
```bash
curl -X POST http://localhost:5000/api/backtests/{runId}/cancel \
  -H "X-API-Key: dev_key_12345"
```
Works only if status is "queued" or "running". Completed backtests cannot be cancelled.

### Q: My CSV upload failed with validation error
**A:** Check:
1. Column headers are exactly: `timestamp,open,high,low,close,volume`
2. Timestamps are ISO 8601 format with UTC (e.g., `2024-01-01T00:00:00Z`)
3. Prices are valid (high >= low, close between high and low)
4. File size < 50MB and rows < 100k
5. No duplicate timestamps

### Q: How do I get the backtest data programmatically?
**A:** Use the API endpoints:
```python
import requests
import json

API_KEY = "dev_key_12345"
RUN_ID = "550e8400-e29b-41d4-a716-446655440000"

# Get status
response = requests.get(
    f"http://localhost:5000/api/backtests/{RUN_ID}",
    headers={"X-API-Key": API_KEY}
)
status = response.json()

# Get equity curve
equity = requests.get(
    f"http://localhost:5000/api/backtests/{RUN_ID}/equity",
    headers={"X-API-Key": API_KEY}
).json()

# Get trades
trades = requests.get(
    f"http://localhost:5000/api/backtests/{RUN_ID}/trades?limit=50",
    headers={"X-API-Key": API_KEY}
).json()
```

### Q: Can I compare backtests with different date ranges?
**A:** Yes - comparison works across any backtests. You'll see how different date ranges and parameters performed.

### Q: Where is my data stored?
**A:** PostgreSQL database running in Docker (managed by Aspire). When Aspire stops, data persists. To reset:
```bash
# Stop Aspire
# Then delete the Docker volume:
docker volume rm aspireapp1_postgres_data
```

### Q: What if I need to use real-time trading data?
**A:** This system is for **historical backtesting only**. For live trading, you'd connect to a broker API separately and feed data to a live trading engine.

---

## Next Steps

1. **Get market data**: Download OHLCV CSV from your broker
2. **Upload to system**: Use the upload endpoint
3. **Define parameters**: Create a backtest submission
4. **Monitor & analyze**: Use the web UI or API
5. **Optimize**: Run multiple parameter sets and compare
6. **Automate**: Set up n8n workflow for daily monitoring

---

**Questions?** Check the [spec.md](spec.md) for detailed requirements or [research.md](research.md) for technical decisions.
