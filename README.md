# Business Workflow Engine

[![Build](https://github.com/mehrdadbahrainy/business-workflow-engine/actions/workflows/build.yml/badge.svg)](https://github.com/mehrdadbahrainy/business-workflow-engine/actions/workflows/build.yml)

An open-source, self-hostable workflow engine for defining and running business processes. Definitions specify their input data, steps, routing rules, and outputs; running instances persist progress and expose work and execution history through an API and operations interface.

**Project status: early implementation.** The current runtime is a purchase-approval vertical slice and is not yet generic. The product direction is a reusable engine where purchase approval is one example definition, not a product boundary. The project has not yet been validated with target users or hardened for production use.

## What works today

- Start a purchase request through a versioned HTTP API.
- Apply the seeded amount threshold: requests at or below USD 1,000 complete automatically; requests above it wait for the configured approver.
- Persist workflow state, approval tasks, and execution history in PostgreSQL using EF Core migrations.
- Return the original request for a matching `Idempotency-Key` retry, reject reuse of that key with different content, and prevent decisions by anyone except the assigned approver.
- Review a personal request list, pending approval queue, and request timeline in the Angular operations UI.
- Complete an approval or rejection and retrieve the recorded outcome.

The current API and persistence model are still purchase-specific. Turning them into a definition-driven runtime is the next core engineering objective. A visual designer, purchasing system, and payment processor are not part of the engine's purpose.

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

In one terminal, start the API. On first startup it applies the checked-in EF migration and seeds the purchase-approval definition from [`examples/purchase-approval/definition.json`](examples/purchase-approval/definition.json).

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

Open the Angular dev-server URL shown in the terminal. Its `/api` requests proxy to the local API. In Development only, the UI sends an `X-Demo-User` identity header. Switch to `manager@example.test` in the top bar to see and decide requests waiting for the seeded approver. This demo identity handler is not enabled outside the Development environment. For a secured deployment, provide a JWT from the configured host identity provider in the **Host API token** field; the UI keeps it in the current browser tab's session storage. The project does not implement an identity-provider login flow.

The API's OpenAPI document is available at `http://localhost:5195/openapi/v1.json` while running in Development. It describes the HTTP operations, the required `Idempotency-Key` header, and the Development-only `X-Demo-User` authentication scheme; OpenAPI is not exposed by the production configuration.

## Self-host with Docker Compose

The production Compose stack runs PostgreSQL, the .NET API, and the Angular operations UI as separate containers. It requires an existing OIDC-compatible identity provider; the engine validates bearer tokens but does not issue them or manage user accounts.

```powershell
Copy-Item .env.example .env
```

Edit `.env` before starting the stack. Set a long random hexadecimal `POSTGRES_PASSWORD`, the issuer URL and audience accepted by your identity provider, and `PURCHASE_APPROVER_SUBJECT` to the exact `sub` value of the approver. Configure your TLS-terminating reverse proxy to forward to the web port (8080 by default).

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

All workflow routes require authentication. The API uses the authenticated subject to scope request lists and approval decisions.

For `POST /api/v1/purchase-requests`, retrying with the same actor, key, and normalized request content returns the original instance. Reusing the key with different content or a different actor returns `409 Conflict`.

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/healthz` | Liveness check; confirms the API process is running |
| `GET` | `/readyz` | Readiness check; returns `503` when the workflow database cannot be reached |
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

The API project's [`BusinessWorkflowEngine.Api.http`](src/server/BusinessWorkflowEngine.Api/BusinessWorkflowEngine.Api.http) file contains a runnable local request, retry, list, history, and approval walkthrough for the VS Code REST Client extension. The task ID in the final decision request is a placeholder; replace it with one returned by the pending-approvals request.

The seeded threshold is denominated in USD; the first implementation accepts USD requests only. There is no purchase order creation, external side effect, outbound webhook, or workflow designer.

## Design notes

- [Contributing](CONTRIBUTING.md)
- [Security policy](SECURITY.md)
- [Self-host backup and restore runbook](docs/operations/backup-and-restore.md)
- [Problem statement and product hypothesis](docs/product/problem-statement.md)
- [Product discovery plan](docs/product/validation-plan.md)
- [First use case: purchase approval](docs/product/first-use-case.md)
- [MVP scope](docs/product/mvp-scope.md)
- [Initial domain model](docs/architecture/domain-model.md)
- [Initial architecture](docs/architecture/overview.md)
- [Repository structure](docs/architecture/repository-structure.md)
- [Architecture decision records](docs/architecture/decisions/)

The product hypothesis and market position are not validated. The repository does not claim that a .NET workflow engine alone is differentiated. See the [MVP scope](docs/product/mvp-scope.md) for acceptance criteria and explicit exclusions.

## License

This project is licensed under the [Apache License 2.0](LICENSE).
