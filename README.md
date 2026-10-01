# Business Workflow Engine

[![Build](https://github.com/mehrdadbahrainy/business-workflow-engine/actions/workflows/build.yml/badge.svg)](https://github.com/mehrdadbahrainy/business-workflow-engine/actions/workflows/build.yml)

An open-source, self-hostable workflow engine for defining and running business processes. The product direction includes a web workflow designer, role-based work routing, and HTTP integrations with external systems. Definitions specify their inputs, steps, routes, and outputs; instances persist progress and execution history.

**Project status: early implementation.** The first generic execution slice now includes web definition authoring, role tasks, and configurable HTTP connections. It is not yet production-hardened or validated with target users.

## What works today

- Create workflow definitions in the browser, set input/output fields, connect supported nodes, add conditional routes, and publish immutable revisions.
- Run the included purchase-approval and employee-leave definitions through the same runtime, without purchase-specific fields in the generic runtime tables.
- Route durable user tasks to roles from the caller's identity claims; complete tasks through the generic work queue.
- Configure reusable HTTPS system connections in the UI. Integration secrets are AES-GCM encrypted in PostgreSQL; workflow HTTP nodes reference the connection key and cannot select a different host.
- Execute HTTP actions through a persisted queue with a 15-second timeout, a 1 MiB response limit, up to three retryable attempts, and an operator-managed hostname allowlist.
- Inspect instance input/output, step execution, action results, and event history in the operations interface.

The earlier purchase-specific routes remain as a compatibility/demo path. New process behavior belongs in versioned definitions and generic runtime code.

## Technology

- .NET 10 / ASP.NET Core Minimal APIs
- Entity Framework Core 10 / PostgreSQL
- Angular 21
- Modular monolith deployment

Pull requests and pushes to `main` build the .NET API, Angular application, and production Docker Compose images in GitHub Actions. The workflow is a build gate; it does not currently run automated tests.

## Run locally

Prerequisites: .NET 10 SDK, Node.js 24, npm, and Docker Compose.

Start PostgreSQL from the repository root:

```powershell
docker compose up -d --wait postgres
```

In one terminal, start the API. On first startup it applies checked-in EF migrations, keeps the legacy purchase demo available, and seeds the generic [`purchase approval`](examples/purchase-approval/workflow.json) and [`employee leave`](examples/employee-leave/workflow.json) definitions.

```powershell
dotnet restore BusinessWorkflowEngine.sln
dotnet run --project src/server/BusinessWorkflowEngine.Api
```

The API listens on `http://localhost:5195`. In a second terminal, run the operations interface:

```powershell
cd src/web/business-workflow-engine-web
npm ci
npm start
```

Open the Angular dev-server URL shown in the terminal. Its `/api` requests proxy to the local API. In Development only, the UI sends `X-Demo-User` and `X-Demo-Roles` identity headers. The subjects `manager@example.test`, `teamlead@example.test`, `department@example.test`, and `hr@example.test` receive the matching demo role. Other development roles can be entered in the top-bar identity menu. This demo identity handler is not enabled outside Development. For a secured deployment, provide a JWT from the configured host identity provider in the **Host API token** field; the UI keeps it in the current browser tab's session storage. The project does not implement an identity-provider login flow.

The API's OpenAPI document is available at `http://localhost:5195/openapi/v1.json` while running in Development. It describes the HTTP operations, the required `Idempotency-Key` header, and the Development-only `X-Demo-User` authentication scheme; OpenAPI is not exposed by the production configuration.

## Self-host with Docker Compose

The production Compose stack runs PostgreSQL, the .NET API, and the Angular operations UI as separate containers. It requires an existing OIDC-compatible identity provider; the engine validates bearer tokens but does not issue them or manage user accounts.

```powershell
Copy-Item .env.example .env
```

Edit `.env` before starting the stack. Set a long random hexadecimal `POSTGRES_PASSWORD`, the issuer URL and audience accepted by your identity provider, `OIDC_ROLE_CLAIM` to the JWT claim containing role ids, and `WORKFLOW_ADMIN_ROLE` to the role allowed to edit definitions and connections. Configure your TLS-terminating reverse proxy to forward to the web port (8080 by default).

To add an external system connection, add its hostname to `INTEGRATION_ALLOWED_HOSTS` (comma-separated). To store a credential in the web UI, set `INTEGRATION_ENCRYPTION_KEY` to a stable base64-encoded 32-byte key. Generate one with PowerShell:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Keep this key in deployment secrets and back it up separately. Losing or rotating it without re-entering saved credentials makes those credentials unreadable. The UI never returns a saved secret. Integration base URLs are HTTPS and immutable; create a new connection key to target another host.

```powershell
docker compose --env-file .env -f compose.production.yaml up --build --wait
```

Open `http://localhost:8080` (or your configured `WEB_PORT`) and enter a host-issued JWT in the **Host API token** field. The API applies EF Core migrations at startup. PostgreSQL data is retained in the `workflow-data` named volume when containers are stopped or recreated.

To stop the containers while keeping stored workflow data:

```powershell
docker compose --env-file .env -f compose.production.yaml down
```

For manual database backups and a restore procedure, see the [backup and restore runbook](docs/operations/backup-and-restore.md). The Compose stack does not schedule backups or provide off-host retention.

The default local database credentials in `appsettings.json` and `compose.yaml` are for development only. Configure `ConnectionStrings__WorkflowDatabase`, `Authentication__Authority`, and `Authentication__Audience` for a hosted deployment. Outside Development, the API validates JWT bearer tokens from the configured authority and uses the authenticated `sub` claim as the actor identity.

## API overview

All workflow routes require authentication. Definition and integration writes also require the configured workflow administrator role. Role tasks are visible only to subjects with the assigned role claim.

For `POST /api/v1/purchase-requests`, retrying with the same actor, key, and normalized request content returns the original instance. Reusing the key with different content or a different actor returns `409 Conflict`.

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/healthz` | Liveness check; confirms the API process is running |
| `GET` | `/readyz` | Readiness check; returns `503` when the workflow database cannot be reached |
| `GET` | `/api/v1/workflow-definitions` | List published definitions; workflow authors also see drafts |
| `GET` | `/api/v1/workflow-definitions/{name}/{revision}` | Read a published definition or an authorized author's draft |
| `PUT` | `/api/v1/workflow-definitions/{name}/{revision}` | Save a validated draft; requires workflow administrator role |
| `POST` | `/api/v1/workflow-definitions/{name}/{revision}/publish` | Publish an immutable revision |
| `GET` | `/api/v1/integrations` | List connection metadata without secrets |
| `POST` | `/api/v1/integrations` | Create a secured HTTP connection; requires workflow administrator role |
| `PUT` | `/api/v1/integrations/{key}` | Update connection name or rotate/clear its secret; base URL is immutable |
| `POST` | `/api/v1/workflows/{name}/instances` | Start a definition with JSON input and `Idempotency-Key` |
| `GET` | `/api/v1/workflow-instances` | List instances started by the caller |
| `GET` | `/api/v1/workflow-instances/{id}` | Read an authorized instance, input/output, steps, and events |
| `GET` | `/api/v1/work-items` | List pending work assigned to the caller's roles |
| `POST` | `/api/v1/work-items/{id}/complete` | Complete a role-assigned task with JSON data |
| `POST` | `/api/v1/purchase-requests` | Start a purchase request; requires `Idempotency-Key` |
| `GET` | `/api/v1/purchase-requests` | List requests started by the caller |
| `GET` | `/api/v1/purchase-requests/{id}` | Read a request and its event history when the caller initiated it or is assigned to it |
| `GET` | `/api/v1/approval-tasks` | List pending tasks assigned to the caller |
| `POST` | `/api/v1/approval-tasks/{id}/decision` | Approve or reject an assigned task |

Example request body:

```json
{
  "requesterReference": "REQ-1001",
  "description": "Replacement laptops",
  "amount": 1800,
  "currency": "USD"
}
```

Example decision body:

```json
{
  "decision": "approve",
  "comment": "Within budget."
}
```

The API project's [`BusinessWorkflowEngine.Api.http`](src/server/BusinessWorkflowEngine.Api/BusinessWorkflowEngine.Api.http) file contains generic and compatibility request examples. The supported definition nodes and mapping paths are documented in the [workflow definition v1 contract](docs/architecture/workflow-definition-v1.md). HTTP action delivery is at least once; external systems should honor the idempotency key supplied by the engine.

## Design notes

- [Contributing](CONTRIBUTING.md)
- [Security policy](SECURITY.md)
- [Self-host backup and restore runbook](docs/operations/backup-and-restore.md)
- [Problem statement and product hypothesis](docs/product/problem-statement.md)
- [Product discovery plan](docs/product/validation-plan.md)
- [Example process: purchase approval](docs/product/first-use-case.md)
- [MVP scope](docs/product/mvp-scope.md)
- [Workflow definition v1](docs/architecture/workflow-definition-v1.md)
- [Domain model](docs/architecture/domain-model.md)
- [Architecture](docs/architecture/overview.md)
- [Repository structure](docs/architecture/repository-structure.md)
- [Architecture decision records](docs/architecture/decisions/)

The product hypothesis and market position are not validated. The repository does not claim that a .NET workflow engine alone is differentiated. See the [MVP scope](docs/product/mvp-scope.md) for acceptance criteria and explicit exclusions.

## License

This project is licensed under the [Apache License 2.0](LICENSE).
