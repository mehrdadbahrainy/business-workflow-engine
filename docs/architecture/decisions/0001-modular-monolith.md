# ADR 0001: Start as a Modular Monolith

- **Status:** Accepted for the MVP
- **Date:** 2026-09-30

## Context

The first workflow changes instance state, approval-task state, and execution history as one logical transition. The project has one bounded vertical slice and no validated need for independent scaling, deployment, or team ownership.

## Decision

Build one ASP.NET Core backend with explicit workflow, human-task, and execution-history boundaries, backed by one PostgreSQL database. Serve the operational experience with the Angular application. Keep the initial host self-contained and self-hostable.

## Options considered

1. **Modular monolith:** one deployable backend with clear internal module boundaries.
2. **Microservices:** independently deployed workflow, task, and integration services.
3. **Use an existing external workflow platform:** integrate this product's UI or sample app with a third-party runtime.

## Why this option

The MVP needs coherent updates to instance state, an approval decision, and history. One application and relational transaction make that consistency boundary easier to implement and operate. This also leaves the project focused on its product hypothesis instead of building service discovery, distributed transactions, or broker operations.

An existing workflow platform could solve the immediate application problem, but it would make this repository an integration/demo rather than the open-source runtime the project intends to evaluate.

## Trade-offs

- The application deploys and scales as one unit.
- Module boundaries are enforced by code organization and review conventions rather than network boundaries.
- A shared database requires care to prevent modules from coupling to each other's internal tables.
- If independent ownership or scaling later becomes a real need, extracting a module will require an explicit data and consistency design.

## Revisit when

A module demonstrates a separate availability, throughput, deployment, or ownership requirement that outweighs the operational and consistency cost of distribution. The mere presence of multiple domains or a familiar microservice pattern is not sufficient.
