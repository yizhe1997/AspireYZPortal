# Feature Specification: Backtesting Framework as a Service

**Feature Branch**: `001-backtesting-service`  
**Created**: January 3, 2026  
**Status**: Draft  
**Input**: User description: "Backtesting Framework as a Service - grab the details from here H:\My Drive\yizhechin97 Obsidian Vault\Life Planner\003 Projects\Backtesting Framework as a Service.md"

## Clarifications

### Session 2026-01-03

- Q: What is the initial account capital amount for backtest execution? → A: Fixed at $50,000 USD
- Q: What authentication mechanism should be used for API access? → A: API key authentication via header (e.g., X-API-Key) - simple, stateless, MVP-appropriate
- Q: What are the specific time boundaries for trading session filters? → A: CME standard hours: Asia 18:00-02:00 UTC, Europe 02:00-08:00 UTC, NY_AM 08:00-12:00 UTC, NY_PM 12:00-18:00 UTC
- Q: At what queue depth should administrators be alerted? → A: Alert at 50 jobs - balanced early warning with ~2-3 hours of queued work at normal execution speed
- Q: How should equity curve be generated when backtest produces zero trades? → A: Generate equity curve points for each bar with constant $50,000 value - consistent API structure, charts render flat line

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - Submit and Execute Backtest (Priority: P1)

As a trader/analyst, I need to submit a backtest job with my strategy parameters and historical data, then see it execute and return results so I can evaluate my supply/demand zone trading strategy on historical Gold futures data.

**Why this priority**: This is the core value proposition - without the ability to execute backtests and get results, the service has no utility. This represents the complete end-to-end workflow.

**Independent Test**: Can be fully tested by uploading GC historical data CSV, submitting a backtest with default parameters, monitoring execution status, and viewing basic results (metrics and equity curve). Delivers immediate value by proving strategy performance.

**Acceptance Scenarios**:

1. **Given** I have uploaded 2 years of GC 1-hour historical data, **When** I submit a backtest with symbol "GC", timeframe "1h", date range within uploaded data, and default strategy parameters, **Then** I receive a unique run_id, see the job queued status, and can monitor progress
2. **Given** my backtest job is in "running" status, **When** I poll for status updates every 5 seconds, **Then** I see progress percentage incrementing and the job completes within 5 minutes for a standard 2-year dataset
3. **Given** my backtest has completed successfully, **When** I view the results page, **Then** I see key metrics (win rate, Sharpe ratio, max drawdown, profit factor), an equity curve chart showing equity and drawdown over time, and a summary of total trades executed
4. **Given** my backtest execution encounters an error, **When** I check the job status, **Then** I see status "failed" with a clear error message explaining what went wrong

---

### User Story 2 - Upload Historical Market Data (Priority: P1)

As a trader/analyst, I need to upload my historical price data in CSV format so that the system has the data required to run my backtests.

**Why this priority**: Without historical data, backtests cannot execute. This is a prerequisite for the primary workflow and must be part of the initial MVP.

**Independent Test**: Can be tested by preparing a valid CSV file with OHLCV data, uploading it via the API, and verifying the data is stored correctly and available for backtest submission. Delivers value by making data ready for analysis.

**Acceptance Scenarios**:

1. **Given** I have a CSV file with columns (timestamp, open, high, low, close, volume) for GC 1-hour data, **When** I upload the file to the market data endpoint, **Then** the system validates the schema, imports valid rows, reports count of bars imported and any skipped duplicates, and confirms the date range available
2. **Given** my CSV contains duplicate timestamps, **When** I upload the file, **Then** the system skips duplicates, imports unique records, and reports how many duplicates were skipped
3. **Given** my CSV has data gaps (missing hours/days), **When** I upload the file, **Then** the system imports the data successfully but warns me about the gaps with specific date ranges
4. **Given** my CSV exceeds size limits (>50MB or >100k rows), **When** I attempt to upload, **Then** I receive a clear validation error before processing begins
5. **Given** I want to know what data is available, **When** I query the market data list endpoint, **Then** I see all symbols/timeframes I've uploaded with their date ranges and any reported gaps

---

### User Story 3 - View Detailed Trade Results (Priority: P2)

As a trader/analyst, I need to see individual trade details including entry/exit times, prices, profit/loss, and zone information so I can analyze which setups worked and which didn't.

**Why this priority**: Aggregate metrics alone are insufficient for strategy refinement. Traders need granular trade data to understand strategy behavior, but this can be delivered after core execution works.

**Independent Test**: Can be tested by running a backtest that generates trades, then viewing the trades table with filters and sorting, and exporting to CSV. Delivers value by enabling deep analysis of strategy performance.

**Acceptance Scenarios**:

1. **Given** my completed backtest has executed 50 trades, **When** I view the trades page, **Then** I see a paginated table (default 50 per page) showing entry time, exit time, side (long/short), entry price, exit price, quantity, P&L amount, P&L percentage, R-multiple, holding bars, zone type, and zone strength for each trade
2. **Given** I want to analyze only winning trades, **When** I filter the trades table by P&L > 0, **Then** I see only trades with positive profit and loss
3. **Given** I want trades sorted by performance, **When** I sort by R-multiple descending, **Then** trades are reordered with highest R-multiple trades first
4. **Given** I need to analyze trades in my own tools, **When** I click the CSV export button, **Then** I download a CSV file with all trade details including 8-decimal precision for prices
5. **Given** I want to see trade entries on the chart, **When** I view the charts page, **Then** I see candlesticks with detected supply/demand zones overlaid and markers showing where trades entered and exited

---

### User Story 4 - Monitor Queue and Cancel Jobs (Priority: P2)

As a trader/analyst, I need to see my job's position in the queue with an estimated wait time and be able to cancel queued or running jobs so I don't waste time on incorrect configurations.

**Why this priority**: Queue visibility and cancellation improve user experience but aren't required for the core workflow. Users can work around this by waiting for completion.

**Independent Test**: Can be tested by submitting multiple jobs to create a queue, viewing queue position and ETA, then canceling a job and confirming it stops executing. Delivers value by giving users control over their resources.

**Acceptance Scenarios**:

1. **Given** I submit a backtest when 3 other jobs are queued, **When** I view my job status immediately after submission, **Then** I see status "queued", queue position 4, and an estimated time until execution starts
2. **Given** my job is queued or running, **When** I click the cancel button and confirm, **Then** the job status changes to "cancelled" and does not consume further resources
3. **Given** my job is already completed, **When** I attempt to cancel it, **Then** I receive a message that completed jobs cannot be cancelled
4. **Given** I cancel a running job that has partial results, **When** I check the results page, **Then** I see no results displayed (partial data is discarded per the spec)

---

### User Story 5 - Compare Multiple Backtest Runs (Priority: P3)

As a trader/analyst, I need to view multiple backtest runs side-by-side with parameter differences highlighted and equity curves overlaid so I can identify which parameter changes improve performance.

**Why this priority**: This is an advanced analysis feature valuable for optimization but not required for initial validation of a strategy. Post-MVP scope.

**Independent Test**: Can be tested by running 3 backtests with different parameters, selecting them for comparison, and viewing the comparison page with overlaid equity curves and parameter diff table. Delivers value by accelerating strategy optimization.

**Acceptance Scenarios**:

1. **Given** I have completed 3 backtest runs with different stop-loss ATR multiples (1.5x, 2.0x, 2.5x), **When** I select all 3 runs and click "Compare", **Then** I see a comparison page with a table showing parameters side-by-side, metrics side-by-side, and an equity chart with all 3 equity curves overlaid in different colors
2. **Given** I'm viewing the comparison page, **When** I look at the parameters table, **Then** parameters that differ across runs are highlighted or marked clearly
3. **Given** I want to identify the best performer, **When** I look at the metrics comparison table, **Then** I can sort by any metric (Sharpe, max drawdown, etc.) to identify the top run

---

### Edge Cases

- **What happens when the CSV upload contains invalid timestamps (non-UTC, malformed dates)?** System rejects the upload with clear validation error specifying the row number and issue.
- **What happens when I submit a backtest with a date range that has no uploaded data?** System returns a validation error before queueing the job, listing available date ranges for the symbol/timeframe.
- **What happens when a backtest runs but generates zero trades?** System completes successfully but metrics show 0 trades with N/A for trade-dependent metrics; equity curve generates data points for each bar with constant value at initial capital ($50,000), resulting in a flat line chart with zero drawdown.
- **What happens if I submit the exact same backtest parameters and date range twice?** The idempotency mechanism detects this and returns the existing run_id or a 409 conflict response with reference to the existing run.
- **What happens if a worker crashes mid-execution?** The job retry mechanism (up to 3 attempts) automatically retries the job; if all retries fail, the job moves to the dead-letter queue with status "failed" and an error message.
- **What happens when the market data has extremely wide gaps (e.g., 2 months missing)?** System warns during upload about gaps and allows submission, but backtest results will only cover periods with data; equity curve will have discontinuities.
- **What happens if uploaded OHLCV data has invalid price relationships (e.g., high < low)?** System validates price logic during upload and rejects rows with invalid OHLC relationships, reporting specific row numbers.
- **What happens when queue depth exceeds system capacity?** System returns a 503 Service Unavailable error for new submissions and alerts administrators; queue depth monitoring triggers alerts before this state.
- **What happens if I try to export trades for a backtest that failed?** System returns an empty result with a message that no trades are available because execution did not complete.

## Requirements *(mandatory)*

### Functional Requirements

**Authentication and Authorization**
- **FR-001**: System MUST require API key authentication for all endpoints via X-API-Key header
- **FR-002**: System MUST validate API keys against stored credentials before processing requests
- **FR-003**: System MUST reject requests with missing or invalid API keys with 401 Unauthorized status
- **FR-004**: System MUST associate each API key with a user identity to enforce data isolation
- **FR-005**: System MUST ensure users can only access their own uploaded data, backtest runs, and results

**Data Management**
- **FR-006**: System MUST accept CSV uploads with required columns (timestamp, open, high, low, close, volume) in UTC format
- **FR-007**: System MUST validate uploaded data for schema correctness, duplicate timestamps, and OHLCV price logic before import
- **FR-008**: System MUST enforce upload limits of maximum 50MB file size and 100,000 rows per upload
- **FR-009**: System MUST store historical market data with symbol, timeframe, timestamp, and OHLCV values with 8-decimal precision for prices
- **FR-010**: System MUST detect and report data gaps in uploaded datasets with specific date ranges affected
- **FR-011**: System MUST provide an endpoint to list available symbols/timeframes with date ranges and gap information

**Backtest Submission and Validation**
- **FR-012**: System MUST accept backtest submissions with symbol, timeframe, start date, end date, and strategy parameters
- **FR-013**: System MUST validate that requested date range falls within available market data before queueing
- **FR-014**: System MUST validate strategy parameters against defined ranges (see parameter ranges in source document)
- **FR-015**: System MUST enforce maximum date span of 3 years per backtest run
- **FR-016**: System MUST apply server-side parameter defaults when parameters are not provided
- **FR-017**: System MUST enforce maximum concurrent runs per user limit
- **FR-018**: System MUST generate unique run_id for each backtest submission
- **FR-019**: System MUST implement idempotency for backtest submissions to prevent duplicate runs with identical parameters

**Queue Management and Execution**
- **FR-020**: System MUST queue submitted backtests and return queue position and estimated time to execution
- **FR-021**: System MUST process queued jobs in FIFO order unless priority is specified
- **FR-022**: System MUST update job status through lifecycle: queued → running → completed/failed/cancelled
- **FR-023**: System MUST implement retry mechanism with exponential backoff (up to 3 attempts) for failed jobs
- **FR-024**: System MUST move jobs to dead-letter queue after exhausting all retries with error details
- **FR-025**: System MUST support job cancellation for queued or running backtests on user request
- **FR-026**: System MUST check for cancellation flags periodically during execution and terminate gracefully
- **FR-027**: System MUST report progress percentage during backtest execution
- **FR-028**: System MUST persist strategy version and code reference with each run for reproducibility

**Strategy Execution**
- **FR-029**: System MUST initialize each backtest run with starting capital of $50,000 USD
- **FR-030**: System MUST calculate ATR (Average True Range) indicator for zone detection and stop-loss sizing
- **FR-031**: System MUST detect supply and demand zones based on swing highs/lows, touches count, and strength parameters
- **FR-032**: System MUST validate zone age against max_age_bars parameter and exclude stale zones
- **FR-033**: System MUST apply session filters when specified to limit entry times using CME standard hours: Asia (18:00-02:00 UTC), Europe (02:00-08:00 UTC), NY_AM (08:00-12:00 UTC), NY_PM (12:00-18:00 UTC)
- **FR-034**: System MUST generate limit orders at zone boundaries with configurable tick offset for entries
- **FR-035**: System MUST simulate realistic limit order fills based on price wick depth (order must be touched by price action)
- **FR-036**: System MUST enforce position sizing based on risk_per_trade_pct of current account equity
- **FR-037**: System MUST enforce max_concurrent_trades limit to prevent over-exposure
- **FR-038**: System MUST place stop-loss orders at distance calculated from ATR multiple
- **FR-039**: System MUST place take-profit orders based on R-multiple of initial risk
- **FR-040**: System MUST track MAE (Maximum Adverse Excursion) and MFE (Maximum Favorable Excursion) for each trade
- **FR-041**: System MUST close positions when stop-loss or take-profit levels are hit
- **FR-042**: System MUST record all detected zones with timestamp, type, high, low, strength, and touches count
- **FR-043**: System MUST record all trades with entry/exit times, prices, quantity, P&L, R-multiple, MAE, MFE, holding bars, zone information, and fill type

**Results and Metrics**
- **FR-044**: System MUST calculate and store core metrics: win rate, average R-multiple, Sharpe ratio, max drawdown, profit factor
- **FR-045**: System MUST generate equity curve data points with timestamp, equity value, and drawdown percentage for each bar in the backtest period, including zero-trade scenarios where equity remains constant at initial capital
- **FR-046**: System MUST calculate win rate as (winning_trades / total_trades) * 100
- **FR-047**: System MUST calculate Sharpe ratio as annualized (mean daily returns / std dev daily returns) * sqrt(252)
- **FR-048**: System MUST calculate max drawdown as maximum peak-to-trough decline in equity percentage
- **FR-049**: System MUST calculate profit factor as gross_profit / gross_loss
- **FR-050**: System MUST persist all metrics, equity curve points, and trade records to durable storage

**Results Access and Export**
- **FR-051**: System MUST provide endpoint to retrieve backtest status with progress for any run_id
- **FR-052**: System MUST provide endpoint to retrieve all metrics for a completed run
- **FR-053**: System MUST provide endpoint to retrieve equity curve time series for completed runs
- **FR-054**: System MUST provide endpoint to retrieve paginated trades list with filtering by side and zone_type
- **FR-055**: System MUST provide endpoint to retrieve chart data including candles, zones, and trade markers
- **FR-056**: System MUST support CSV export of trades with full 8-decimal precision
- **FR-057**: System MUST support JSON and CSV export formats for equity curve data
- **FR-058**: System MUST return appropriate error messages when attempting to access results for failed or cancelled runs

**Observability and Monitoring**
- **FR-059**: System MUST emit structured logs for key lifecycle events (submission, queue, start, complete, fail, cancel)
- **FR-060**: System MUST emit distributed traces with run_id, strategy_id, and worker_id tags
- **FR-061**: System MUST track metrics for queue depth, job runtime percentiles (p95, p99), success rate, failure rate, retry count
- **FR-062**: System MUST alert administrators when queue depth exceeds 50 jobs

**Data Retention**
- **FR-063**: System MUST retain backtest runs, metrics, trades, and equity curves for 90 days
- **FR-064**: System MUST retain uploaded market data for 30 days
- **FR-065**: System MUST retain logs for 30 days
- **FR-066**: System MUST provide automated purge scripts for expired data

### Key Entities

- **Market Data**: Represents historical OHLCV bars for a symbol/timeframe combination. Key attributes: symbol, timeframe, timestamp, open, high, low, close, volume. Each bar is uniquely identified by symbol + timeframe + timestamp.

- **Backtest Run**: Represents a single execution of the backtesting strategy. Key attributes: run_id, strategy version, symbol, timeframe, date range, parameters (JSON), status, progress, timestamps, worker_id, error message. Related to multiple trades, metrics, equity curve points, and detected zones.

- **Trade**: Represents a single completed trade during a backtest. Key attributes: entry/exit times and prices, side (long/short), quantity, P&L (amount and percentage), R-multiple, MAE, MFE, holding bars, zone information (type, strength), fill type. Belongs to one backtest run.

- **Detected Zone**: Represents a supply or demand zone identified during backtest execution. Key attributes: timestamp of creation, type (supply/demand), price boundaries (high/low), strength score, touches count. Belongs to one backtest run.

- **Run Metrics**: Represents calculated performance metrics for a completed run. Key attributes: metric name (win_rate, sharpe_ratio, max_drawdown, profit_factor, etc.) and metric value. Each metric belongs to one backtest run.

- **Equity Curve Point**: Represents account equity at a specific point in time during backtest execution. Key attributes: timestamp, equity value, drawdown percentage. Forms a time series for one backtest run.

- **Strategy**: Represents the backtesting strategy configuration and versioning. Key attributes: strategy_id, name, version, description, code reference. Multiple runs reference the same strategy version.

## Success Criteria *(mandatory)*

### Measurable Outcomes

**Performance Targets**
- **SC-001**: Users receive response from data query and backtest status endpoints in under 200 milliseconds for 95% of requests
- **SC-002**: Users receive confirmation of backtest submission in under 400 milliseconds for 95% of requests
- **SC-003**: Standard backtest runs covering 2 years of 1-hour data complete execution within 5 minutes
- **SC-004**: Users waiting in an empty queue see their backtest start execution within 1 minute

**Reliability Targets**
- **SC-005**: 95% of backtest runs complete successfully without errors (excluding user-caused validation failures)
- **SC-006**: 98% of data uploads succeed without system errors
- **SC-007**: Failed jobs automatically retry and succeed within 3 retry attempts for transient failures
- **SC-008**: System recovers from worker crashes and resumes queued jobs without manual intervention

**Usability Targets**
- **SC-009**: Users can identify why their backtest submission failed through clear validation error messages
- **SC-010**: Users can determine data availability for their desired symbol/timeframe before submitting a backtest
- **SC-011**: Users can track backtest progress from submission through completion in under 10 seconds of status polling
- **SC-012**: Users successfully export trade results to CSV format with complete data on first attempt

**Data Quality Targets**
- **SC-013**: System correctly identifies and reports 100% of duplicate timestamps in uploaded data
- **SC-014**: System reports data gaps with accurate date range boundaries
- **SC-015**: Exported trade data maintains 8-decimal price precision without rounding errors

**Business Value Targets**
- **SC-016**: Users complete full workflow (upload data → submit backtest → view results → export trades) in under 15 minutes for standard 2-year dataset
- **SC-017**: System handles 10 concurrent backtest executions without performance degradation
- **SC-018**: Queue wait time remains under 5 minutes when system is at 80% capacity

## Assumptions *(optional)*

**Data Assumptions**
- Historical price data is available in CSV format with OHLCV structure
- Timestamps in uploaded data are in UTC timezone
- Price precision of 8 decimals is sufficient for Gold futures pricing
- Data retention of 90 days for runs and 30 days for uploads meets business needs
- Standard dataset size is approximately 2 years of 1-hour bars (roughly 17,520 bars assuming 24/7 markets)

**User Assumptions**
- Primary users are traders and analysts familiar with trading terminology (zones, R-multiples, MAE/MFE)
- Users understand the concept of supply/demand zones and strategy parameters
- API key authentication is sufficient for MVP security requirements
- Users will manage their API keys securely (key rotation and revocation capabilities provided)
- No PII (Personally Identifiable Information) is stored or processed in this system
- Users can tolerate polling-based status updates (5-10 second intervals) rather than real-time push notifications for MVP

**Technical Assumptions**
- Standard execution time of under 5 minutes for 2-year/1-hour dataset is achievable with reasonable compute resources
- Queue-based architecture with Redis provides sufficient durability and performance
- Session filters use CME standard hours: Asia (18:00-02:00 UTC), Europe (02:00-08:00 UTC), NY_AM (08:00-12:00 UTC), NY_PM (12:00-18:00 UTC)
- Limit order fill simulation based on wick depth is sufficiently realistic for strategy validation (order-book simulation not required for MVP)
- Maximum concurrent runs limit of 3 per user prevents system overload
- Worker horizontal scaling can handle increased load (workers can be added as needed)

**Strategy Assumptions**
- Supply/demand zone strategy is the only strategy type required for MVP (extensibility for other strategies post-MVP)
- Single symbol and single timeframe per backtest run is sufficient (multi-symbol portfolios post-MVP)
- Initial capital is fixed at $50,000 USD for all backtest runs to enable consistent comparison across different parameter sets
- Position sizing based on fixed risk percentage of current account equity without portfolio-level margin calculations is acceptable
- Zone detection algorithm based on swing highs/lows with touch counting is well-defined
- ATR-based stop-loss and R-multiple take-profit are standard strategy exit mechanisms

**Business Assumptions**
- Manual backtesting is the current painful process being replaced
- Reproducibility of backtest results is critical for trust and compliance
- Parameter experimentation and comparison features are valuable but can be delivered post-MVP
- Walk-forward analysis and Monte Carlo robustness testing are post-MVP enhancements
- Live trading integration is explicitly out of scope (backtest-only service)

## Scope *(optional)*

### In Scope (MVP)

**Core Functionality**
- Upload historical OHLCV data via CSV with validation
- Submit backtests with configurable strategy parameters
- Queue-based job execution with status monitoring and progress tracking
- Supply/demand zone detection based on swing highs/lows
- Limit order entry simulation with wick-depth fill logic
- ATR-based stop-loss and R-multiple take-profit exits
- Position sizing by risk percentage
- Trade tracking with entry/exit prices, P&L, R-multiples, MAE/MFE
- Core performance metrics calculation (win rate, Sharpe, max DD, profit factor)
- Equity curve generation
- Results viewing via API endpoints
- Trade export to CSV with full precision
- Job cancellation for queued and running backtests
- Retry mechanism with dead-letter queue for failed jobs
- Basic observability (logs, traces, metrics)

**Supported Markets**
- Gold futures (GC symbol) as primary use case
- Single symbol per backtest run
- Timeframes: 1-hour, 4-hour, 1-day

**User Capabilities**
- Trader/analyst role with full access to own data and backtests
- Administrator role with system health visibility

### Out of Scope (MVP)

**Advanced Features (Post-MVP)**
- Parameter sweep and grid search for optimization
- Multi-run comparison with equity curve overlays
- Walk-forward analysis
- Monte Carlo robustness testing
- Real-time progress streaming via WebSockets/SignalR
- Multi-symbol portfolio backtesting
- Custom strategy code upload and execution
- Tick-level or order-book simulation
- Advanced fill models (slippage, partial fills, queue position)

**Market Support**
- Options, forex, stocks, cryptocurrencies (focus is futures/GC for MVP)
- Multiple symbols in a single backtest
- Portfolio-level margin and risk calculations
- Cross-symbol correlations

**Live Trading**
- Live execution or brokerage integration
- Real-time data feeds
- Order routing
- Account management

**Platform Features**
- User registration and profile management (minimal auth sufficient for MVP)
- Team collaboration and shared backtests
- Scheduled/recurring backtests
- Alert notifications (email, SMS, webhooks)
- Mobile app

### Future Enhancements

**Phase 2 Candidates**
- Parameter optimization tools with visual comparison
- Walk-forward analysis to validate out-of-sample performance
- Monte Carlo simulation for robustness testing
- Multi-symbol portfolio support
- Real-time progress streaming
- Enhanced fill models with configurable slippage

**Phase 3 Candidates**
- Custom strategy code upload (Python/C# scripts)
- Additional asset classes (stocks, forex, crypto)
- Advanced analytics and machine learning integration
- API rate limiting and user quotas
- Team collaboration features

## Risks & Mitigations *(optional)*

### Technical Risks

**Risk: Large uploads stall workers or exhaust memory**
- **Impact**: High - System becomes unresponsive or crashes
- **Likelihood**: Medium - Users may attempt very large uploads
- **Mitigation**: Enforce strict size limits (50MB, 100k rows); stream-parse CSV instead of loading entirely into memory; implement background ingestion with progress tracking; reject oversized uploads early with clear error messages

**Risk: Long-running backtests block queue and frustrate users**
- **Impact**: High - Poor user experience, resource starvation
- **Likelihood**: Medium - Some parameter combinations or date ranges may run slowly
- **Mitigation**: Cap maximum date span at 3 years; implement progress heartbeats to show activity; add periodic cancellation checks; monitor p95/p99 execution times and alert on anomalies; horizontal worker scaling

**Risk: Data quality issues (gaps, duplicates, invalid prices) corrupt results**
- **Impact**: High - Users receive incorrect backtest results and lose trust
- **Likelihood**: Medium - Real-world data is often messy
- **Mitigation**: Validate OHLC relationships during upload; detect and report duplicates with skip count; warn about gaps but allow submission; implement rounding to 8 decimals consistently; provide data quality summary after upload

**Risk: Worker crashes leave jobs in inconsistent state**
- **Impact**: Medium - Jobs fail and users see no results
- **Likelihood**: Medium - Bugs, infrastructure issues, OOM
- **Mitigation**: Implement retry mechanism with exponential backoff (up to 3 attempts); use dead-letter queue for terminal failures; ensure jobs are idempotent; emit structured logs for debugging; monitor worker health and restart automatically

**Risk: Idempotency mechanism fails and duplicate runs are created**
- **Impact**: Medium - Wastes compute resources, confuses users
- **Likelihood**: Low - Implementation complexity
- **Mitigation**: Use idempotency keys based on hash of parameters + date range; implement database-level unique constraints; return existing run_id on duplicate submission; test thoroughly with concurrent submissions

### Business Risks

**Risk: Strategy overfitting - users optimize on historical data without validation**
- **Impact**: High - Users make poor trading decisions based on overfit results
- **Likelihood**: High - Common pitfall in backtesting
- **Mitigation**: Document best practices in user guide; provide walk-forward analysis tools in Phase 2; add warnings about parameter optimization dangers; show out-of-sample vs in-sample performance comparisons when available

**Risk: Users expect live trading capability (scope creep)**
- **Impact**: Medium - Misaligned expectations, scope expansion
- **Likelihood**: Medium - Natural progression from backtesting
- **Mitigation**: Clearly document that MVP is backtest-only; set expectations during onboarding; plan live trading as separate product/phase with distinct pricing and compliance requirements

**Risk: Single-symbol limitation frustrates portfolio traders**
- **Impact**: Medium - Limits addressable market, user churn
- **Likelihood**: Medium - Portfolio strategies are common
- **Mitigation**: Prioritize multi-symbol support in Phase 2 roadmap; communicate limitation clearly; document workarounds (run separate backtests and manually aggregate)

### Operational Risks

**Risk: Queue depth grows unbounded under heavy load**
- **Impact**: High - System degradation, long wait times
- **Likelihood**: Medium - Depends on adoption rate
- **Mitigation**: Implement queue depth monitoring with alerts at 50 jobs threshold; enforce per-user concurrent run limits; add rate limiting on submission endpoint; auto-scale workers based on queue depth; reject submissions with 503 when at capacity

**Risk: Data retention policies conflict with compliance or user needs**
- **Impact**: Medium - Data loss, compliance violations
- **Likelihood**: Low - Policies are documented but may need adjustment
- **Mitigation**: Document retention policies clearly (90d runs, 30d uploads, 30d logs); provide export functionality before expiration; implement automated purge with audit logging; review retention requirements with stakeholders before launch

**Risk: Insufficient observability makes debugging production issues difficult**
- **Impact**: Medium - Slow incident response, extended downtime
- **Likelihood**: Medium - Distributed systems are complex
- **Mitigation**: Implement structured logging with run_id/worker_id context; emit OTLP traces for end-to-end visibility; track key metrics (queue depth, runtime percentiles, error rates); set up alerting on SLO violations; create runbooks for common failure scenarios


