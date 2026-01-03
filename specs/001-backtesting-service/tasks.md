# Tasks: Backtesting Framework as a Service

**Input**: Design documents from `/specs/001-backtesting-service/`  
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Not requested in spec; no explicit test tasks included. Add as needed during implementation.

**Organization**: Tasks grouped by user story to keep each slice independently implementable and testable.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and wiring of core projects.

- [X] T001 Create AspireApp1.BacktestApi, AspireApp1.BacktestWorker, and AspireApp1.Backtest.Data projects; add to AspireApp1.slnx and set output paths
- [X] T002 Wire PostgreSQL and Redis resources in AspireApp1.AppHost/AppHost.cs and configure connection strings in AspireApp1.AppHost/appsettings.json
- [X] T003 [P] Add shared Directory.Build.props for nullable enable, analyzers, and C# code style across Backtest projects
- [X] T004 [P] Install frontend dependencies (Recharts, @tanstack/react-query, axios) in frontend/package.json and lockfile

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure required before any user story.

- [X] T005 Add EF Core + Npgsql + Redis client packages to AspireApp1.BacktestApi.csproj and AspireApp1.BacktestWorker.csproj
- [X] T006 [P] Create shared BacktestDbContext and entity configurations per data-model in AspireApp1.Backtest.Data/BacktestDbContext.cs
- [X] T007 [P] Add initial EF migration and tooling scripts in AspireApp1.Backtest.Data/Migrations (baseline schema)
- [X] T008 Implement API key middleware and registration in AspireApp1.BacktestApi/Middleware/ApiKeyAuthMiddleware.cs and Program.cs
- [X] T009 Configure global error handling/logging and OpenTelemetry setup in AspireApp1.BacktestApi/Extensions.cs and Program.cs
- [X] T010 Setup Redis connection/resilience helpers for producer/consumer in AspireApp1.BacktestApi/Queue/RedisQueueProducer.cs and AspireApp1.BacktestWorker/Queue/RedisQueueConsumer.cs

**Checkpoint**: Foundation ready — user stories can start in parallel.

---

## Phase 3: User Story 1 - Submit and Execute Backtest (Priority: P1) 🎯 MVP

**Goal**: Users submit backtests, jobs queue and execute, and results (status, metrics, equity curve) are returned.

**Independent Test**: Upload seed GC data, submit backtest with default parameters, monitor status to completion (<5 minutes), retrieve metrics and equity curve.

### Implementation

- [X] T011 [US1] Define submission/request/response DTOs and parameter validation ranges in AspireApp1.BacktestApi/Models/BacktestModels.cs
- [X] T012 [P] [US1] Implement BacktestSubmissionService with idempotency, date-range validation, and market data availability checks in AspireApp1.BacktestApi/Services/BacktestSubmissionService.cs
- [X] T016 [P] [US1] Implement StrategyEngine with session filters, zone detection, limit-order fills, position sizing, MAE/MFE tracking in AspireApp1.BacktestWorker/Strategy/StrategyEngine.cs
- [X] T017 [US1] Map entity persistence for BacktestRun, Trade, DetectedZone, RunMetrics, EquityCurve with indexes and cascades in AspireApp1.Backtest.Data/Entities/*.cs
- [X] T018 [US1] Add progress reporting and status updates during execution in AspireApp1.BacktestWorker/Workers/BacktestProcessor.cs
- [X] T019 [US1] Implement GET /api/backtests listing with filters in AspireApp1.BacktestApi/Controllers/BacktestsController.cs
- [X] T020 [US1] Implement GET /api/backtests/{id}/equity returning equity/drawdown series and metrics in AspireApp1.BacktestApi/Controllers/BacktestsController.cs

**Checkpoint**: US1 end-to-end submit → execute → status → metrics/equity works independently.

---

## Phase 4: User Story 2 - Upload Historical Market Data (Priority: P1)

**Goal**: Users upload CSV OHLCV data with validation; system stores bars and reports availability.

**Independent Test**: Upload valid CSV (50k rows), receive accepted response; query market data list to see symbol/timeframe/date range; duplicates skipped and reported.

### Implementation

- [X] T021 [P] [US2] Implement MarketData entity config (composite key, precision) in AspireApp1.Backtest.Data/Entities/MarketData.cs
- [X] T022 [P] [US2] Implement MarketDataService for CSV validation, streaming parse, duplicates/gap detection in AspireApp1.BacktestApi/Services/MarketDataService.cs
- [X] T023 [US2] Implement POST /api/market-data/upload and GET /api/market-data endpoints in AspireApp1.BacktestApi/Controllers/MarketDataController.cs
- [X] T024 [US2] Add storage deduplication/indexing and gap reporting in AspireApp1.BacktestApi/Services/MarketDataService.cs

**Checkpoint**: Market data upload/list endpoints functional and validated.

---

## Phase 5: User Story 3 - View Detailed Trade Results (Priority: P2)

**Goal**: Users view and filter trade details with pagination and CSV export; frontend displays trades.

**Independent Test**: For a completed run, call trades endpoint with filters (side/zone_type) and pagination; CSV export returns full precision; frontend table shows filtered trades.

### Implementation

- [X] T025 [P] [US3] Implement trades query service with filters, pagination, CSV export in AspireApp1.BacktestApi/Services/ResultsService.cs
- [X] T026 [US3] Implement GET /api/backtests/{id}/trades with filters/pagination/export in AspireApp1.BacktestApi/Controllers/BacktestsController.cs
- [X] T027 [P] [US3] Build TradesTable component and data hook in frontend/src/components/TradesTable.tsx and frontend/src/hooks/useTrades.ts
- [X] T028 [US3] Integrate trades tab in BacktestDetailPage with filter controls in frontend/src/pages/BacktestDetailPage.tsx

**Checkpoint**: Trades retrieval and UI functional independently of other stories.

---

## Phase 6: User Story 4 - Monitor Queue and Cancel Jobs (Priority: P2)

**Goal**: Users see queue position/ETA and can cancel queued or running jobs.

**Independent Test**: Submit multiple jobs to create queue; status shows position/ETA; cancel queued job removes from queue; cancel running job stops execution and marks cancelled.

### Implementation

- [ ] T029 [US4] Expose queue depth and ETA in status/list responses using Redis metrics in AspireApp1.BacktestApi/Controllers/BacktestsController.cs
- [ ] T030 [P] [US4] Implement cancellation handling (API + worker checks) in AspireApp1.BacktestApi/Controllers/BacktestsController.cs and AspireApp1.BacktestWorker/Workers/BacktestProcessor.cs
- [ ] T031 [P] [US4] Add cancel controls and status badges to BacktestListPage and detail header in frontend/src/pages/BacktestListPage.tsx and frontend/src/pages/BacktestDetailPage.tsx

**Checkpoint**: Queue visibility and cancellation work end-to-end.

---

## Phase 7: User Story 5 - Compare Multiple Backtest Runs (Priority: P3)

**Goal**: Users compare runs side-by-side with parameter diffs and overlaid equity curves.

**Independent Test**: Select 3 runs; compare endpoint returns metrics, parameters diff, and downsampled equity curves; frontend renders comparison table and overlaid chart.

### Implementation

- [ ] T032 [P] [US5] Implement comparison query aggregating metrics/parameters/equity (downsample) in AspireApp1.BacktestApi/Controllers/BacktestsController.cs
- [ ] T033 [P] [US5] Build comparison page with parameter diff table and overlaid Recharts equity curves in frontend/src/pages/BacktestComparePage.tsx
- [ ] T034 [US5] Wire frontend routing and API client for compare endpoint in frontend/src/services/backtestsApi.ts and frontend/src/App.tsx

**Checkpoint**: Comparison feature functional and independently testable.

---

## Final Phase: Polish & Cross-Cutting

**Purpose**: Hardening and cleanup across stories.

- [ ] T035 [P] Add OpenTelemetry spans and structured logs (run_id, worker_id) across API/Worker in AspireApp1.BacktestApi and AspireApp1.BacktestWorker
- [ ] T036 Security/validation sweep (rate limits, payload size, auth failures) and documentation updates in specs/001-backtesting-service
- [ ] T037 [P] Run quickstart.md validation and update any setup commands or sample payloads in specs/001-backtesting-service/quickstart.md

---

## Dependencies & Execution Order

- **Phase Dependencies**: Setup → Foundational → User Stories (3→7) → Polish. User stories can run in parallel after Foundational; recommended priority order US1 → US2 → US3 → US4 → US5.
- **User Story Dependencies**:
  - US1 depends on Foundational only.
  - US2 depends on Foundational; US1 not required but recommended before backtest submissions that need data.
  - US3 depends on US1 (trades persisted) and Foundational.
  - US4 depends on US1 (status endpoints) and Foundational.
  - US5 depends on US1 (metrics/equity) and Foundational.

---

## Parallel Execution Examples

- **Foundational**: T006, T007, T010 can proceed in parallel once packages (T005) are installed.
- **US1**: T012, T013, T015, T016 can run in parallel; T014 waits for submission service; T017 depends on DbContext scaffolding.
- **US2**: T021 and T022 can run in parallel before wiring endpoints (T023).
- **US3**: T025 and T027 can run in parallel; T026 depends on T025; T028 depends on API readiness.
- **US4**: T030 and T031 can proceed in parallel after status fields (T029).
- **US5**: T032 and T033 can run in parallel; routing (T034) after both API and UI exist.

---

## Implementation Strategy

- **MVP First**: Complete Setup → Foundational → US1. Ship submit/execute/status/equity as MVP.
- **Incremental Delivery**: Add US2 (upload), then US3 (trade details), then US4 (queue controls), then US5 (comparison).
- **Hardening**: Finish with Polish tasks (telemetry, security, docs, quickstart validation).

---

## Additional Non-Functional, Retention, and Accessibility Tasks

- [ ] T038 Implement scheduled purge for runs/metrics/trades/equity after 90d (API/worker job; configurable threshold)
- [ ] T039 [P] Purge market data after 30d since last access; add last_access tracking and purge script
- [ ] T040 [P] Log retention/rotation to 30d; add configuration and verify rollover
- [ ] T041 Emit purge metrics/report (deleted counts, last run) for monitoring
- [ ] T042 [P] Emit queue depth metric and alert at >50 jobs; include DLQ depth alert >10; export via OpenTelemetry
- [ ] T043 [P] Performance validation: p95 read <200ms, submit <400ms; 2y/1h run <5 minutes on standard dataset
- [ ] T044 [P] Reliability validation: retry/dead-letter scenarios, worker crash recovery, success-rate SLO check
- [ ] T045 [P] Usability validation: error message clarity, status polling <10 seconds user experience
- [ ] T046 [P] Data quality validation: duplicate/gap detection accuracy; 8-decimal precision in exports
- [ ] T047 [P] Capacity validation: 10 concurrent runs; queue wait <5 minutes at ~80% load
- [ ] T048 [P] Responsive design pass on MarketDataPage, BacktestList/Detail, Compare (mobile/tablet/desktop layouts)
- [ ] T049 [P] Accessibility audit (WCAG 2.1 AA): keyboard navigation, focus states, aria labeling, contrast; chart aria-hidden/alt text as appropriate
