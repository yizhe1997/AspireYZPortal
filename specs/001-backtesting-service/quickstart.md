# Quickstart Guide: Backtesting Framework

**Feature**: 001-backtesting-service  
**Last Updated**: 2026-01-03

## Overview

This guide helps developers set up the backtesting framework locally and run their first backtest. You'll upload market data, submit a backtest, and visualize results.

**Estimated Setup Time**: 15 minutes

---

## Prerequisites

Install the following on your development machine:

1. **.NET 10 SDK**: [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
2. **Node.js 20+**: [Download](https://nodejs.org/)
3. **Docker Desktop**: [Download](https://www.docker.com/products/docker-desktop/)
4. **.NET Aspire Workload**:
   ```powershell
   dotnet workload install aspire
   ```
5. **Git**: For cloning the repository

**Verify Installation**:
```powershell
dotnet --version       # Should show 10.x
node --version         # Should show 20.x+
docker --version       # Should show 20.x+
```

---

## Step 1: Clone Repository

```powershell
git clone https://github.com/yourorg/AspireApp1.git
cd AspireApp1
git checkout 001-backtesting-service
```

---

## Step 2: Start Infrastructure

Use .NET Aspire to orchestrate PostgreSQL, Redis, API, Worker, and Frontend:

```powershell
cd AspireApp1.AppHost
dotnet run
```

**What Happens**:
- **Aspire Dashboard** opens at `http://localhost:15888`
- **PostgreSQL 16** starts (port 5432)
- **Redis 7** starts (port 6379)
- **BacktestApi** starts (port 5000)
- **BacktestWorker** starts (background service)
- **Frontend** starts (port 3000)

**Health Check**: Navigate to Aspire Dashboard and ensure all services show "Running" status.

---

## Step 3: Seed Test Data

Run database migrations and seed sample market data:

```powershell
cd ../AspireApp1.BacktestApi
dotnet ef database update
dotnet run --seed-data
```

**Seeded Data**:
- **Symbol**: GC (Gold Futures)
- **Timeframe**: 1h
- **Date Range**: 2022-01-01 to 2024-12-31 (3 years)
- **Bars**: ~26,280 rows
- **Strategy**: "Supply/Demand Zones v1" (ID: `550e8400-e29b-41d4-a716-446655440000`)

---

## Step 4: Upload Market Data (Optional)

If you want to test with your own CSV data:

**CSV Format**:
```csv
timestamp,open,high,low,close,volume
2024-01-01T00:00:00Z,2050.25,2055.00,2048.00,2053.50,12500
2024-01-01T01:00:00Z,2053.50,2058.75,2052.00,2057.25,11200
```

**Upload via API**:
```powershell
curl -X POST http://localhost:5000/api/market-data/upload `
  -H "X-API-Key: dev_key_12345" `
  -F "file=@market_data.csv" `
  -F "symbol=GC" `
  -F "timeframe=1h"
```

**Expected Response** (202 Accepted):
```json
{
  "job_id": "550e8400-e29b-41d4-a716-446655440000",
  "message": "Upload accepted. Processing 50,000 rows.",
  "estimated_completion": "2026-01-03T10:05:00Z"
}
```

---

## Step 5: Submit Your First Backtest

**API Request**:
```powershell
curl -X POST http://localhost:5000/api/backtests `
  -H "Content-Type: application/json" `
  -H "X-API-Key: dev_key_12345" `
  -d @- <<'EOF'
{
  "strategy_id": "550e8400-e29b-41d4-a716-446655440000",
  "symbol": "GC",
  "timeframe": "1h",
  "start_date": "2022-01-01",
  "end_date": "2023-12-31",
  "parameters": {
    "zone_lookback_bars": 100,
    "zone_min_touches": 2,
    "zone_width_atr_multiple": 0.5,
    "zone_max_age_bars": 500,
    "require_confirmation": true,
    "stop_loss_atr_multiple": 2.0,
    "take_profit_r_multiple": 2.0,
    "risk_per_trade_pct": 1.0,
    "max_concurrent_trades": 2,
    "session_filter": ["NY_AM"],
    "limit_order_offset_ticks": 1
  },
  "initial_capital": 50000.00
}
EOF
```

**Expected Response** (202 Accepted):
```json
{
  "run_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "status": "queued",
  "queue_position": 1,
  "estimated_start": "2026-01-03T10:00:30Z"
}
```

---

## Step 6: Monitor Backtest Status

**Poll for Status**:
```powershell
$runId = "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
curl http://localhost:5000/api/backtests/$runId `
  -H "X-API-Key: dev_key_12345"
```

**Response (Running)**:
```json
{
  "run_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "status": "running",
  "progress": 45,
  "started_at": "2026-01-03T10:00:35Z"
}
```

**Response (Completed)**:
```json
{
  "run_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "status": "completed",
  "progress": 100,
  "finished_at": "2026-01-03T10:03:20Z",
  "metrics": {
    "win_rate": 52.3,
    "avg_r_multiple": 1.85,
    "sharpe_ratio": 1.42,
    "max_drawdown": -12.5,
    "profit_factor": 1.75,
    "total_trades": 150
  }
}
```

**Estimated Completion Time**: 2-year backtest typically completes in 3-5 minutes.

---

## Step 7: View Results

### 7.1 Open Frontend

Navigate to: `http://localhost:3000`

**Landing Page** shows:
- List of all backtest runs
- Status badges (queued/running/completed)
- Quick metrics (win rate, final equity)

**Click on Run** to view:
- Equity curve chart (Recharts)
- Drawdown chart
- Trade history table
- Detected zones visualization

### 7.2 Fetch Results via API

**Equity Curve**:
```powershell
curl http://localhost:5000/api/backtests/$runId/equity `
  -H "X-API-Key: dev_key_12345"
```

**Trade History**:
```powershell
curl http://localhost:5000/api/backtests/$runId/trades?limit=10 `
  -H "X-API-Key: dev_key_12345"
```

**Chart Data (Combined)**:
```powershell
curl http://localhost:5000/api/backtests/$runId/charts `
  -H "X-API-Key: dev_key_12345"
```

---

## Step 8: Experiment with Parameters

Try different strategy parameters to see how they affect results:

**Higher Risk**:
```json
{
  "risk_per_trade_pct": 2.0,
  "stop_loss_atr_multiple": 1.5,
  "take_profit_r_multiple": 3.0
}
```

**Stricter Zone Detection**:
```json
{
  "zone_lookback_bars": 200,
  "zone_min_touches": 3,
  "zone_width_atr_multiple": 0.3
}
```

**Session Filtering**:
```json
{
  "session_filter": ["NY_AM", "NY_PM"]  // Trade both NY sessions
}
```

Submit multiple backtests with different parameters and compare results in the frontend.

---

## Troubleshooting

### PostgreSQL Connection Failed
**Error**: `Npgsql.NpgsqlException: Connection refused`

**Fix**:
1. Ensure Docker Desktop is running
2. Check Aspire Dashboard for PostgreSQL health
3. Restart AppHost: `dotnet run`

### Redis Queue Not Processing
**Error**: Backtest stuck in "queued" status

**Fix**:
1. Check worker logs in Aspire Dashboard
2. Ensure BacktestWorker service is running
3. Restart worker: Navigate to `AspireApp1.BacktestWorker` and run `dotnet run`

### Market Data Missing
**Error**: `404 Market data unavailable for date range`

**Fix**:
1. Run seed script again: `dotnet run --seed-data`
2. Verify data exists: `curl http://localhost:5000/api/market-data`
3. Check date range matches uploaded data

### Frontend Not Loading
**Error**: `ERR_CONNECTION_REFUSED` on `http://localhost:3000`

**Fix**:
1. Navigate to `frontend` folder
2. Install dependencies: `npm install`
3. Start dev server: `npm run dev`
4. Check Aspire Dashboard for frontend service status

---

## Development Workflow

### Running Tests

**Unit Tests**:
```powershell
cd AspireApp1.BacktestApi.Tests
dotnet test
```

**Integration Tests** (requires running infrastructure):
```powershell
cd AspireApp1.BacktestApi.IntegrationTests
dotnet test
```

### Hot Reload

.NET Aspire supports hot reload for both backend and frontend:
- **Backend**: Edit C# files → auto-recompile
- **Frontend**: Edit React files → Vite HMR updates instantly

### Debugging

**Attach to API**:
1. Open `AspireApp1.sln` in Visual Studio or VS Code
2. Set breakpoint in `BacktestController.cs`
3. Attach to `AspireApp1.BacktestApi` process (PID in Aspire Dashboard)

**Attach to Worker**:
1. Set breakpoint in `BacktestWorker.cs`
2. Attach to `AspireApp1.BacktestWorker` process

---

## Next Steps

1. **Read API Documentation**: See [openapi.yaml](contracts/openapi.yaml) for full endpoint reference
2. **Explore Data Model**: Review [data-model.md](data-model.md) for entity relationships
3. **Review Architecture Decisions**: Check [research.md](research.md) for technical rationale
4. **Implement Custom Strategy**: Fork strategy code and modify zone detection logic
5. **Deploy to Production**: See [deployment-guide.md](deployment-guide.md) (created in next phase)

---

## Common Commands Cheat Sheet

| Action | Command |
|--------|---------|
| Start all services | `cd AspireApp1.AppHost && dotnet run` |
| Run migrations | `cd AspireApp1.BacktestApi && dotnet ef database update` |
| Seed test data | `dotnet run --seed-data` |
| Submit backtest | `curl -X POST http://localhost:5000/api/backtests ...` |
| Check queue status | `curl http://localhost:5000/api/backtests?status=queued` |
| View run details | `curl http://localhost:5000/api/backtests/{run_id}` |
| Delete run | `curl -X DELETE http://localhost:5000/api/backtests/{run_id}` |
| Open frontend | Navigate to `http://localhost:3000` |
| Open Aspire Dashboard | Navigate to `http://localhost:15888` |

---

## API Key Management

**Development API Key**: `dev_key_12345` (hardcoded for local testing)

**Production**: Replace with secure key generation:
```csharp
// In AspireApp1.BacktestApi/Services/ApiKeyService.cs
public string GenerateApiKey()
{
    var keyBytes = RandomNumberGenerator.GetBytes(32);
    return $"sk_live_{Convert.ToBase64String(keyBytes)}";
}
```

Store keys securely in Azure Key Vault or environment variables.

---

## Support

**Documentation**: [docs/](../docs/)  
**Issues**: [GitHub Issues](https://github.com/yourorg/AspireApp1/issues)  
**Slack**: #backtesting-framework  
**Email**: support@backtest.example.com

---

**Happy Backtesting! 🚀**
