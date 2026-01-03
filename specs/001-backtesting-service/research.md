# Research & Technical Decisions: Backtesting Framework as a Service

**Feature**: 001-backtesting-service  
**Date**: 2026-01-03  
**Status**: Complete

## Overview

This document captures architectural decisions and technology choices for the backtesting platform. All technical unknowns from the specification have been resolved through analysis of requirements, constitution constraints, and best practices.

---

## Architecture Pattern

### Decision: Distributed Worker Architecture with Queue-Based Job Processing

**Rationale**:
- **Separation of Concerns**: API service handles user requests (fast, stateless), workers handle compute-intensive backtesting (long-running, stateful)
- **Scalability**: Workers can be horizontally scaled independently of API based on queue depth
- **Resilience**: Worker failures don't affect API availability; jobs can be retried
- **Resource Isolation**: Backtest execution doesn't compete with API request handling for CPU/memory

**Alternatives Considered**:
- **Monolithic API with inline processing**: Rejected because long-running backtests (up to 5 minutes) would block API threads and prevent horizontal scaling of compute vs request handling
- **Serverless functions (Azure Functions/AWS Lambda)**: Rejected due to 5-minute timeout limits and complexity of managing long-running state in function execution model
- **Actor model (Orleans/Akka.NET)**: Rejected as over-engineered for MVP; adds complexity without clear benefits over simpler queue pattern

**Implementation**: ASP.NET Core API service + dedicated Worker service, both managed by Aspire AppHost

---

## Queue Technology

### Decision: Redis Lists with Idempotency Keys

**Rationale**:
- **Simplicity**: Redis LPUSH/BRPOP provides reliable FIFO queue with minimal code
- **Existing Infrastructure**: Redis already available in AspireApp1 for caching
- **Idempotency Support**: Redis SET with NX flag enables idempotency key storage without additional database
- **Performance**: In-memory operations handle 100s of jobs/second easily
- **Observability**: Queue depth can be monitored with LLEN command

**Alternatives Considered**:
- **RabbitMQ/Azure Service Bus**: Rejected as over-engineered for MVP requirements; adds infrastructure complexity and learning curve
- **Database polling (PostgreSQL)**: Rejected due to polling overhead and lack of push notification; requires custom retry/DLQ logic
- **Redis Streams**: Considered but LPUSH/BRPOP simpler for single consumer group pattern; Streams add unnecessary complexity for MVP

**Implementation**: 
- Producer: API service enqueues job JSON to `backtests:queue` list
- Consumer: Worker service uses BRPOP with 5-second timeout
- Idempotency: Store SHA256(params+dates) in Redis SET with 7-day TTL

---

## Data Access Pattern

### Decision: Entity Framework Core with Code-First Migrations

**Rationale**:
- **Type Safety**: EF Core provides strongly-typed LINQ queries matching C# domain models
- **Migration Management**: Code-first migrations enable version-controlled schema changes
- **Constitution Alignment**: Standard .NET data access pattern, no additional complexity
- **Aspire Integration**: EF Core has first-class Aspire support for connection management and health checks

**Alternatives Considered**:
- **Dapper (micro-ORM)**: Rejected due to manual SQL and lack of migration tooling; benefits (slight performance gain) don't outweigh costs
- **Repository pattern**: Rejected as unnecessary abstraction for CRUD operations; DbContext already provides unit of work and repository patterns

**Implementation**:
- Shared `BacktestDbContext` in separate `AspireApp1.Backtest.Data` project
- Referenced by both API and Worker projects
- Migrations applied via Aspire resource init or standalone migration tool

---

## Frontend State Management

### Decision: TanStack Query for Server State, React useState for Local UI State

**Rationale**:
- **Caching & Stale-While-Revalidate**: TanStack Query automatically caches API responses and refetches stale data
- **Polling Support**: Built-in polling for backtest status with automatic backoff on errors
- **Minimal Boilerplate**: Eliminates manual loading/error state management compared to raw fetch
- **Constitution Alignment**: Widely adopted library (100k+ GitHub stars), well-maintained, excellent TypeScript support

**Alternatives Considered**:
- **Redux/Zustand**: Rejected as unnecessary for server-driven data; adds complexity for managing async state and cache invalidation
- **SWR (Vercel)**: Considered but TanStack Query has better TypeScript support and more granular cache control
- **Native fetch + useState**: Rejected due to lack of caching, retry logic, and polling utilities; would require reimplementing these patterns

**Implementation**:
- TanStack Query for API calls (market data, backtest status, results)
- React useState for form inputs, modal visibility, local UI toggles

---

## Chart Library

### Decision: Recharts for Equity Curve Visualization

**Rationale**:
- **React-First**: Built for React with declarative API matching React mental model
- **Bundle Size**: 96KB gzipped vs D3.js (240KB gzipped)
- **Responsive**: Built-in responsive container and mobile-friendly interactions
- **Time-Series Support**: Line charts with time-axis formatting out-of-the-box
- **Constitution Alignment**: Popular library (24k+ GitHub stars), actively maintained, no jQuery/legacy dependencies

**Alternatives Considered**:
- **D3.js**: Rejected due to larger bundle size, imperative API requiring manual DOM manipulation (conflicts with React), and steep learning curve
- **Chart.js**: Considered but canvas-based (accessibility issues), less React-friendly than Recharts
- **Victory**: Rejected due to smaller community and SVG performance concerns for large datasets (17k points)

**Implementation**:
- `<ResponsiveContainer>` with `<LineChart>` for equity and drawdown
- `<Tooltip>` for hovering over data points
- Custom X-axis formatter for timestamp display

---

## Authentication Strategy

### Decision: API Key Authentication via X-API-Key Header

**Rationale**:
- **Stateless**: No session management or token refresh logic required
- **Simple Implementation**: Single middleware checks header against stored keys
- **MVP-Appropriate**: Sufficient security for initial rollout; can upgrade to OAuth2/JWT post-MVP
- **Standard Practice**: Common pattern for API-first services (AWS, Stripe, SendGrid)

**Alternatives Considered**:
- **OAuth 2.0 / JWT**: Rejected as over-engineered for MVP; adds token issuance, refresh, and validation complexity
- **Basic Auth**: Rejected due to transmitting credentials with every request; API key rotation easier than password changes
- **No Auth**: Rejected due to security requirements; even internal MVP needs access control

**Implementation**:
- Middleware validates `X-API-Key` header against database/Redis cache
- Keys associated with user identity for data isolation
- Key rotation supported via management API (post-MVP)

---

## Error Handling & Retry Strategy

### Decision: Exponential Backoff with Dead-Letter Queue

**Rationale**:
- **Transient Failures**: Network blips, temporary database unavailability handled by retries
- **Poison Messages**: Jobs failing 3+ times moved to DLQ for manual inspection
- **Observability**: Retry count and DLQ depth tracked as metrics
- **Standard Pattern**: Industry best practice for queue-based systems

**Alternatives Considered**:
- **Fixed retry interval**: Rejected because doesn't back off on persistent failures (e.g., database down)
- **Infinite retries**: Rejected as it blocks queue with failing jobs
- **No retries**: Rejected as overly strict; many failures are transient

**Implementation**:
- Retry delays: 5s, 15s, 45s (exponential factor 3)
- After 3 failures: move job to `backtests:dlq` list with error metadata
- Alert admin when DLQ depth > 10

---

## Observability Approach

### Decision: OpenTelemetry with Structured Logging

**Rationale**:
- **Constitution Alignment**: OpenTelemetry already mandated for AspireApp1
- **Distributed Tracing**: Track requests across API → Queue → Worker with trace context
- **Vendor Neutral**: OTLP exporters work with Jaeger, Prometheus, Application Insights, etc.
- **Structured Logs**: Context (run_id, worker_id, strategy_id) enables precise filtering

**Alternatives Considered**:
- **Application Insights only**: Rejected to avoid vendor lock-in
- **Custom logging framework**: Rejected as reinventing the wheel; OpenTelemetry is industry standard

**Implementation**:
- Traces: Parent span for API request, child spans for queue enqueue and worker processing
- Logs: Structured JSON with semantic conventions (trace_id, span_id, run_id)
- Metrics: Histograms for latency, counters for success/failure, gauges for queue depth

---

## CSV Parsing Strategy

### Decision: Stream-Based Parsing with CsvHelper Library

**Rationale**:
- **Memory Efficiency**: Stream parsing avoids loading entire 50MB file into memory
- **Validation**: Row-by-row validation enables reporting specific line numbers for errors
- **Duplicate Detection**: Can build HashSet of timestamps during parse to identify duplicates
- **Library Maturity**: CsvHelper is the de facto standard for .NET CSV parsing (30k+ GitHub stars)

**Alternatives Considered**:
- **Manual string splitting**: Rejected due to edge cases (quoted fields, escaping) and lack of type conversion
- **Load entire file then parse**: Rejected due to memory pressure for 50MB files

**Implementation**:
- ASP.NET Core IFormFile → Stream → CsvHelper reader
- Batch insert to PostgreSQL every 1000 rows (performance optimization)
- Track skipped duplicates and gaps during parse, return in response

---

## Database Schema Design

### Decision: Normalized Schema with EF Core Conventions

**Rationale**:
- **Referential Integrity**: Foreign keys ensure trades/metrics/equity points always reference valid runs
- **Cascade Delete**: Simplifies cleanup (delete run → auto-delete trades/metrics)
- **Indexing**: Composite indexes on (symbol, timeframe, timestamp) for fast market data queries
- **JSONB for Parameters**: Flexible storage for strategy parameters without schema changes

**Alternatives Considered**:
- **Denormalized schema**: Rejected due to data redundancy and update anomalies
- **NoSQL (MongoDB)**: Rejected because relational model fits requirements; no need for document flexibility

**Implementation**: See database schema in spec.md Appendix B

---

## Session Filter Implementation

### Decision: CME Standard Hours with UTC Timestamps

**Rationale**:
- **Industry Standard**: CME hours align with Gold futures (GC) trading conventions
- **Clarity**: Explicit hour ranges eliminate ambiguity (Asia 18:00-02:00 UTC, etc.)
- **Timezone Safety**: All timestamps stored as UTC; no DST edge cases

**Alternatives Considered**:
- **User-configurable sessions**: Rejected for MVP due to added parameter complexity
- **Fixed 6-hour blocks**: Rejected as not aligned with actual market hours

**Implementation**: 
- Filter logic checks if bar timestamp falls within configured session ranges
- Sessions stored as configuration in `appsettings.json`

---

## Summary of Key Decisions

| Area | Decision | Rationale |
|------|----------|-----------|
| Architecture | Distributed worker with queue | Scalability, resilience, resource isolation |
| Queue | Redis Lists | Simplicity, existing infrastructure, performance |
| Data Access | EF Core Code-First | Type safety, migrations, Aspire integration |
| Frontend State | TanStack Query | Caching, polling, minimal boilerplate |
| Charts | Recharts | React-first, bundle size, responsive |
| Authentication | API Key via header | Stateless, simple, MVP-appropriate |
| Error Handling | Exponential backoff + DLQ | Handles transient failures, prevents poison messages |
| Observability | OpenTelemetry + structured logs | Constitution mandated, vendor neutral, distributed tracing |
| CSV Parsing | Stream-based with CsvHelper | Memory efficient, mature library |
| Database | Normalized schema with JSONB params | Referential integrity, flexibility |
| Sessions | CME standard hours (UTC) | Industry standard, clarity |

---

## Open Questions (Post-MVP)

- **Multi-symbol portfolios**: How to model cross-symbol relationships and margin calculations?
- **Real-time progress**: SignalR vs Server-Sent Events vs WebSockets for push updates?
- **Parameter optimization**: Grid search parallelization strategy (map-reduce vs scatter-gather)?
- **Walk-forward analysis**: Window rolling strategy and out-of-sample validation approach?

These are explicitly out of scope for MVP but documented for future phases.
