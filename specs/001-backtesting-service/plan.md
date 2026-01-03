# Implementation Plan: Backtesting Framework as a Service

**Branch**: `001-backtesting-service` | **Date**: 2026-01-03 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-backtesting-service/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

A distributed backtesting platform for supply/demand zone trading strategies on Gold futures (GC). Users upload historical OHLCV data via CSV, submit backtests with strategy parameters, and workers execute simulations with realistic limit-order fills. Results include metrics (win rate, Sharpe, max drawdown, profit factor), equity curves, and granular trade details. The system uses .NET Aspire for orchestration, ASP.NET Core for API services, React for frontend, PostgreSQL for durable storage, and Redis for queue management.

**Technical Approach**: Distributed worker architecture with Redis queue, PostgreSQL persistence, API-first design with OpenAPI contracts, React frontend with polling for status updates, and OpenTelemetry observability.

## Technical Context

**Language/Version**: C# 13 / .NET 10.0, TypeScript 5.9  
**Primary Dependencies**: 
- Backend: ASP.NET Core 10.0, Npgsql (PostgreSQL client), StackExchange.Redis, Microsoft.Extensions.Http.Resilience
- Frontend: React 19, Vite 7, Recharts (for equity curve visualization), TanStack Query (data fetching)
- Orchestration: .NET Aspire 13.1 AppHost
- Observability: OpenTelemetry with OTLP exporters

**Storage**: PostgreSQL 16+ for durable storage (backtest runs, trades, metrics, equity curves, market data)  
**Queue**: Redis 7+ (lists/streams for job queue, distributed state, idempotency keys)  
**Testing**: xUnit for backend, Vitest for frontend, REST Client/Postman for API contract testing  
**Target Platform**: Docker containers orchestrated via Aspire, deployable to Linux servers  
**Project Type**: Distributed web application with API backend, worker services, and React frontend  
**Performance Goals**: 
- API read latency p95 <200ms
- Backtest submission p95 <400ms
- Standard 2-year/1-hour GC dataset completes <5 minutes
- Queue wait <1 minute at low load

**Constraints**: 
- CSV upload max 50MB / 100k rows
- Max backtest date span 3 years
- Max 3 concurrent runs per user
- Queue depth alert at 50 jobs
- Data retention: runs/metrics 90 days, uploads 30 days, logs 30 days

**Scale/Scope**: 
- Target: 10-50 concurrent users during MVP
- Estimated 100-500 backtest runs per day
- Each run generates 10-200 trades on average
- Equity curve: 1 point per bar (17k points for 2y/1h dataset)
- Worker pool: 2-5 concurrent workers initially (horizontally scalable)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**I. Clean Code** ✅ PASS
- Backend services will use C# nullable reference types (already enabled in project)
- Structured logging with clear context (run_id, worker_id, strategy_id)
- Single Responsibility Principle applied to API controllers, workers, and strategy execution logic
- Comprehensive error handling with actionable messages for CSV validation, parameter validation, queue failures

**II. Simple UX** ✅ PASS  
- Upload → Submit → Monitor → Results flow is linear and intuitive
- Progressive disclosure: show queue position/ETA on submit, expand to progress details during execution
- Clear error states with resolution guidance (e.g., "Date range 2023-01-01 to 2025-12-31 has no data. Available: 2023-06-01 to 2024-05-31")
- Polling-based status updates acceptable for MVP (5-10 second intervals)

**III. Responsive Design** ✅ PASS
- React frontend with mobile-first approach (data tables, charts, forms adapt to viewport)
- Recharts library supports responsive equity curve rendering
- Forms optimized for touch (parameter inputs, file upload)
- CSV export and equity visualization work across devices

**IV. Minimal Dependencies** ⚠️ REVIEW NEEDED
- **Backend New Dependencies**: Npgsql (PostgreSQL client), StackExchange.Redis, TanStack Query for data fetching/caching
- **Frontend New Dependencies**: Recharts (charts), TanStack Query (data fetching/caching), React Router (navigation)
- **Rationale**: Recharts chosen over D3.js for simpler API and smaller bundle; TanStack Query provides caching and stale-while-revalidate for polling; Npgsql is the standard PostgreSQL driver for .NET
- **Alternatives**: Could use built-in fetch + useState for data fetching but loses caching/retry logic; could use Canvas API instead of Recharts but requires significantly more code

**Technology Stack Alignment** ✅ PASS
- Builds on existing .NET 10 / Aspire 13.1 / React 19 stack
- OpenTelemetry integration consistent with existing observability
- Redis already available in AspireApp1 AppHost for caching
- PostgreSQL to be added as Aspire resource (standard pattern)

**Development Standards** ✅ PASS
- All code subject to ESLint (frontend) and Roslyn analyzers (backend)
- TypeScript strict mode enforced
- C# XML documentation for public APIs
- OpenAPI schema generated for API contracts (enables contract testing)

### Gate Evaluation

**Status**: ✅ PASS with Minor Review
- All core principles satisfied
- Minimal Dependencies requires justification (provided above - dependencies add significant value for charting and data fetching patterns)
- No complexity tracking violations requiring documentation

**Action**: Proceed to Phase 0 research

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
AspireApp1.AppHost/
├── AppHost.cs                    # Add PostgreSQL and Redis resources for backtesting
└── appsettings.json              # Connection strings, resource configuration

AspireApp1.BacktestApi/           # NEW: API Service
├── Controllers/
│   ├── MarketDataController.cs   # Upload, list market data
│   └── BacktestsController.cs    # Submit, status, results, cancel
├── Services/
│   ├── MarketDataService.cs      # CSV parsing, validation, persistence
│   ├── BacktestSubmissionService.cs  # Parameter validation, idempotency, queue
│   └── ResultsService.cs         # Retrieve metrics, equity, trades, charts
├── Models/
│   ├── MarketDataModels.cs       # Upload request/response, OHLCV
│   ├── BacktestModels.cs         # Submit request/response, status, parameters
│   └── ResultsModels.cs          # Metrics, equity curve, trades
├── Data/
│   ├── BacktestDbContext.cs      # EF Core context for PostgreSQL
│   └── Migrations/               # EF migrations
├── Queue/
│   └── RedisQueueProducer.cs     # Enqueue jobs to Redis
├── Middleware/
│   └── ApiKeyAuthMiddleware.cs   # X-API-Key authentication
├── Program.cs
└── AspireApp1.BacktestApi.csproj

AspireApp1.BacktestWorker/        # NEW: Worker Service
├── Workers/
│   └── BacktestProcessor.cs      # Poll Redis queue, execute backtests
├── Strategy/
│   ├── StrategyEngine.cs         # Main backtest loop
│   ├── IndicatorCalculator.cs    # ATR calculation
│   ├── ZoneDetector.cs           # Supply/demand zone detection
│   ├── OrderManager.cs           # Limit orders, fills, position sizing
│   ├── ExitManager.cs            # Stop-loss, take-profit, MAE/MFE
│   └── MetricsCalculator.cs      # Win rate, Sharpe, max DD, profit factor
├── Models/
│   ├── Zone.cs                   # Detected zone model
│   ├── Trade.cs                  # Trade model
│   └── Position.cs               # Active position model
├── Data/
│   └── BacktestDbContext.cs      # Shared EF Core context
├── Queue/
│   └── RedisQueueConsumer.cs     # Dequeue jobs from Redis
├── Program.cs
└── AspireApp1.BacktestWorker.csproj

frontend/
├── src/
│   ├── pages/
│   │   ├── MarketDataPage.tsx        # Upload CSV, view available data
│   │   ├── BacktestSubmitPage.tsx    # Submit form with parameters
│   │   ├── BacktestListPage.tsx      # List all runs with status
│   │   └── BacktestDetailPage.tsx    # Metrics, equity curve, trades table
│   ├── components/
│   │   ├── EquityCurveChart.tsx      # Recharts equity/drawdown visualization
│   │   ├── TradesTable.tsx           # Paginated trades with filters/sorting
│   │   ├── MetricsCard.tsx           # Display metric name/value
│   │   ├── BacktestForm.tsx          # Parameter inputs with validation
│   │   └── StatusBadge.tsx           # Status visualization (queued/running/etc)
│   ├── services/
│   │   ├── marketDataApi.ts          # Market data API client
│   │   ├── backtestsApi.ts           # Backtests API client
│   │   └── auth.ts                   # API key storage/header injection
│   ├── hooks/
│   │   ├── useBacktestStatus.ts      # Polling hook for status updates
│   │   └── useMarketData.ts          # TanStack Query hook for market data
│   ├── types/
│   │   ├── backtest.ts               # TypeScript types for backtest models
│   │   └── marketData.ts             # TypeScript types for market data
│   ├── App.tsx
│   └── main.tsx
└── package.json

tests/
├── AspireApp1.BacktestApi.Tests/    # NEW
│   ├── Controllers/                 # Controller unit tests
│   ├── Services/                    # Service unit tests
│   └── Integration/                 # API integration tests
├── AspireApp1.BacktestWorker.Tests/ # NEW
│   ├── Strategy/                    # Strategy execution tests
│   └── Integration/                 # Worker integration tests
└── frontend/
    └── __tests__/                   # Component tests with Vitest
```

**Structure Decision**: Web application (Option 2) with backend API service, worker service, and React frontend. The backend is split into two .NET projects:
- **AspireApp1.BacktestApi**: REST API for user interactions (upload, submit, query)
- **AspireApp1.BacktestWorker**: Background worker that consumes Redis queue and executes backtests

Both share data models and DbContext. Frontend is the existing React/Vite project with new pages and components. Aspire AppHost orchestrates all services.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No violations requiring justification. Constitution Check passed with minor dependency review (Recharts, TanStack Query justified for significant value in charting and data fetching patterns).
