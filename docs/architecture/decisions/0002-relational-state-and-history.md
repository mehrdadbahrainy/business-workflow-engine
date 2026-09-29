# ADR 0002: Persist Current State and Execution History Together

- **Status:** Accepted for the MVP
- **Date:** 2026-09-30

## Context

The product must show where a request is now and explain the decisions and transitions that brought it there. The MVP includes one relational database and one application boundary. It does not need replay, temporal queries, or event-driven projections to satisfy the purchase-approval use case.

## Decision

Persist the current workflow-instance and approval-task state in PostgreSQL. Record meaningful execution events in an append-only history. Commit each state transition and its matching history record in the same database transaction. Use the event history to explain execution; do not rebuild current state by replaying the history.

## Options considered

1. **Current state plus append-only history:** relational state is optimized for commands and queries; history records the explanation of transitions.
2. **Full event sourcing:** events are the sole source of truth and current state is rebuilt or projected from them.
3. **Current state only:** store the latest status and discard detailed transition history.

## Why this option

The first use case needs a reliable current state and a trustworthy timeline. A relational model can enforce uniqueness, assignment, and concurrency constraints directly. Storing history in the same transaction keeps the UI from showing a state change without the decision that caused it.

Full event sourcing adds replay, schema evolution, projection, and event-contract responsibilities that have no demonstrated benefit for this MVP. Storing current state only would fail the product's requirement to explain how a request reached its outcome.

## Trade-offs

- History is useful for operational explanation but is not automatically a general integration event stream.
- History schema changes need deliberate migration and retention handling.
- Current state and history can diverge if code bypasses the transactional transition boundary; the implementation must centralize state changes.
- The design does not provide arbitrary time-travel queries or event replay.

## Revisit when

Validated requirements need consumers to react to domain events, historical reconstruction, temporal process analysis, or independent projections. Add only the capability required; do not infer that need from an append-only history table.
