Portal: https://yzportal.38569123.xyz
Aspire Dashboard: https://aspire.38569123.xyz
Server: https://yzportalserver.38569123.xyz/openapi/v1.json

---

## 📊 Backtesting Framework Feature

This repository includes a **distributed backtesting framework** for supply/demand zone trading strategies.

### Quick Start

1. **Run infrastructure**: `dotnet run --project AspireApp1.AppHost`
2. **Upload data**: `curl -X POST http://localhost:5000/api/market-data/upload ...`
3. **Submit backtest**: `curl -X POST http://localhost:5000/api/backtests ...`
4. **Monitor results**: Open `http://localhost:3000`

### Documentation

- **[USAGE_GUIDE.md](specs/001-backtesting-service/USAGE_GUIDE.md)** - Complete workflow guide with examples and n8n automation
- **[spec.md](specs/001-backtesting-service/spec.md)** - Feature specification and acceptance criteria
- **[plan.md](specs/001-backtesting-service/plan.md)** - Technical architecture and design decisions
- **[research.md](specs/001-backtesting-service/research.md)** - Implementation rationale
- **[quickstart.md](specs/001-backtesting-service/quickstart.md)** - Development setup guide
