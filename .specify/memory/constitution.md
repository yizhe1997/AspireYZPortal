<!--
SYNC IMPACT REPORT
==================
Version Change: Initial → 1.0.0
Constitution Type: MINOR (new constitution creation)

Modified Principles:
- NEW: I. Clean Code
- NEW: II. Simple UX
- NEW: III. Responsive Design
- NEW: IV. Minimal Dependencies

Added Sections:
- Core Principles (4 principles)
- Technology Stack Constraints
- Development Standards
- Governance

Templates Status:
✅ plan-template.md - Constitution Check section compatible
✅ spec-template.md - Requirements/scope alignment validated
✅ tasks-template.md - Task categorization fits principles
✅ agent-file-template.md - Reviewed
✅ checklist-template.md - Reviewed

Follow-up Actions: None - all placeholders resolved
==================
-->

# AspireApp1 Constitution

## Core Principles

### I. Clean Code

All code MUST be maintainable, readable, and follow industry-standard best practices. This includes:
- Meaningful variable and function names that express intent
- Single Responsibility Principle - each class/function does one thing well
- DRY (Don't Repeat Yourself) - no code duplication without explicit justification
- Proper error handling with clear, actionable error messages
- Comprehensive inline documentation for complex logic
- Consistent code formatting enforced by automated tools (ESLint for TypeScript/React, .editorconfig for C#)

**Rationale**: Clean code reduces technical debt, enables faster onboarding of new developers, simplifies debugging, and ensures long-term maintainability as the project scales across multiple teams and services.

### II. Simple UX

User experience MUST prioritize simplicity and clarity over feature complexity. This includes:
- Intuitive navigation requiring minimal clicks to complete tasks
- Clear visual hierarchy and consistent design patterns
- Progressive disclosure - show only what users need when they need it
- Accessible design meeting WCAG 2.1 Level AA standards
- Fast loading times with optimistic UI updates
- Error states that guide users toward resolution

**Rationale**: Simple UX increases user adoption, reduces support burden, and ensures the application serves all users effectively regardless of technical expertise. Complexity is the enemy of usability.

### III. Responsive Design

All user interfaces MUST work seamlessly across devices and screen sizes. This includes:
- Mobile-first design approach starting from 320px viewport width
- Fluid layouts using relative units (rem, %, viewport units)
- Touch-friendly interface elements (minimum 44×44px tap targets)
- Performance optimization for low-bandwidth and mobile networks
- Graceful degradation for older browsers while embracing modern capabilities
- Testing on physical devices, not just emulators

**Rationale**: Users access applications from diverse devices in various contexts. Responsive design ensures consistent functionality and user experience regardless of device, maximizing reach and usability.

### IV. Minimal Dependencies

Dependency choices MUST be intentional and justified. This includes:
- Favor built-in platform capabilities over third-party libraries
- Each dependency MUST provide significant value that outweighs maintenance cost
- Regular audits to remove unused or redundant dependencies
- Pin dependency versions and maintain security updates
- Evaluate bundle size impact before adding frontend dependencies
- Document rationale for each major dependency in project README

**Rationale**: Minimal dependencies reduce security surface area, decrease bundle sizes, simplify updates and maintenance, and avoid supply chain vulnerabilities. Dependencies are long-term commitments that compound maintenance burden.

## Technology Stack Constraints

This project consists of a distributed application architecture with:
- **.NET 10.0 / ASP.NET Core** backend services (AspireApp1.Server)
- **React 19** + **TypeScript 5.9** + **Vite 7** frontend (frontend/)
- **.NET Aspire 13.1** orchestration and service discovery (AspireApp1.AppHost)
- **Redis** for output caching and distributed state
- **OpenTelemetry** for observability and distributed tracing

**Stack Principles**:
- Backend MUST use C# nullable reference types (`<Nullable>enable</Nullable>`)
- Backend MUST implement resilience patterns (retry, circuit breaker via Microsoft.Extensions.Http.Resilience)
- Frontend MUST use TypeScript strict mode with no implicit any
- All services MUST emit structured logs and OpenTelemetry traces
- New services added in the future MUST integrate with Aspire AppHost for discovery and orchestration

**Prohibited**:
- No jQuery or legacy JavaScript libraries in frontend
- No unmaintained or deprecated NuGet/npm packages
- No inline secrets or configuration - use environment variables and configuration management

## Development Standards

### Code Quality Gates

- All code MUST pass linting (ESLint for frontend, Roslyn analyzers for backend)
- TypeScript MUST compile with zero errors
- C# MUST compile with zero warnings
- All public APIs MUST have XML documentation comments (C#) or JSDoc (TypeScript)
- Complex algorithms (cyclomatic complexity > 10) MUST include explanatory comments

### Testing Requirements

While TDD is not mandated, test coverage expectations:
- Critical business logic SHOULD have unit tests
- API endpoints SHOULD have contract/integration tests
- UI components with complex state SHOULD have component tests
- Tests MUST be runnable in CI/CD pipeline
- Breaking changes MUST be validated by test updates

### Review Process

- All changes MUST go through pull request review
- PRs MUST include description linking to spec or issue
- Reviewers MUST verify compliance with all four core principles
- Breaking changes MUST be explicitly called out and justified

## Governance

This Constitution supersedes all informal practices and defines the non-negotiable standards for AspireApp1.

**Amendment Process**:
- Amendments require documentation of rationale and impact analysis
- Version increment follows semantic versioning:
  - **MAJOR**: Remove or redefine core principles (backward-incompatible governance changes)
  - **MINOR**: Add new principles, expand existing sections materially
  - **PATCH**: Clarifications, typo fixes, non-semantic refinements
- All amended versions MUST be committed with Sync Impact Report

**Compliance**:
- All PRs and code reviews MUST verify adherence to these principles
- Violations require explicit justification documented in PR description
- Complexity that contradicts principles MUST be approved and tracked
- Constitution check gates appear in all plan-template.md Phase 0 validations

**Living Document**: This Constitution evolves with the project. When practices conflict with these principles, update the practices or amend the Constitution - never ignore it.

**Version**: 1.0.0 | **Ratified**: 2026-01-02 | **Last Amended**: 2026-01-02
