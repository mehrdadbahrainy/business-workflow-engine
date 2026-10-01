# ADR 0005: Package the First Self-Hosted Deployment with Docker Compose

- **Status:** Accepted for the MVP
- **Date:** 2026-09-30

## Context

The first workflow should be straightforward for a small organization to run on its own infrastructure. The stack has three runtime pieces: PostgreSQL for durable state, the .NET API, and an Angular operations interface. The MVP is designed for one organization and one API instance; it does not claim horizontal scaling or high availability.

## Decision

Provide a production Compose file that builds and runs PostgreSQL, one API container, and one Nginx container serving the Angular application and proxying API requests. Keep PostgreSQL data in a named volume. The API applies checked-in EF Core migrations at startup and validates JWT bearer tokens from the host's configured OIDC authority. The web container's health check uses API readiness, including workflow database connectivity. The public web port is intended to sit behind a TLS-terminating reverse proxy.

The existing base `compose.yaml` remains a development-only PostgreSQL service for running the API and Angular CLI on the host. The production stack is explicitly selected with `compose.production.yaml` and requires deployment-specific values from `.env`.

## Options considered

1. **Docker Compose on one host:** package the three processes and persistent database for a small self-hosted installation.
2. **Host-installed services:** install .NET, Node/Nginx, and PostgreSQL independently and document service-manager configuration.
3. **Kubernetes or another orchestrator:** define probes, rollout, storage, and identity integrations for a multi-node deployment.

## Why this option

The product hypothesis calls for a self-hosted runtime that can be evaluated without first operating a cluster. Compose makes the API, UI, database, network, and persistent volume visible in one deployment definition while preserving the modular-monolith boundary inside the API.

## Trade-offs

- A single host is a single availability and capacity boundary; no multi-instance execution guarantee is implied.
- Operators must supply an OIDC issuer and audience, a strong database secret, TLS termination, and a backup policy for the PostgreSQL volume.
- Database migrations run as the API starts. A coordinated migration job and multi-replica rollout policy are deferred until multiple API replicas are supported.
- Compose does not schedule backups or configure off-host retention, certificate renewal, identity-provider registration, or host firewall policy. A manual PostgreSQL dump and restore procedure is documented in the [backup and restore runbook](../../operations/backup-and-restore.md).
- The development Compose file uses local-only database credentials and must not be used as a production deployment.

## Revisit when

Validated deployments require managed PostgreSQL, multiple API replicas, independent service upgrades, automated backups, or high availability. Add orchestration only when those deployment needs justify its operational cost.
