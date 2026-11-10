# Business Workflow Engine

[![Build](https://github.com/mehrdadbahrainy/business-workflow-engine/actions/workflows/build.yml/badge.svg)](https://github.com/mehrdadbahrainy/business-workflow-engine/actions/workflows/build.yml) [![Business tests](https://github.com/mehrdadbahrainy/business-workflow-engine/actions/workflows/tests.yml/badge.svg)](https://github.com/mehrdadbahrainy/business-workflow-engine/actions/workflows/tests.yml)

An open-source, self-hostable workflow engine for defining and running business processes. The product direction includes a web workflow designer, role-based work routing, and HTTP integrations with external systems. Definitions specify their inputs, steps, routes, and outputs; instances persist progress and execution history.

**Project status: early implementation.** The first generic execution slice now includes web definition authoring, role tasks, and configurable HTTP connections. It is not yet production-hardened or validated with target users.

**Architecture case study:** [From a Business Request to a Durable Workflow Engine](docs/architecture/case-study.md) — the problem, the major decisions and trade-offs, execution and failure paths, and what remains unverified.

**Verification:** [Business acceptance and unit test matrix](docs/testing/verification-matrix.md) — test scenarios, automated CI environment, evidence requirements, and known gaps. See [GitHub Actions test runs](https://github.com/mehrdadbahrainy/business-workflow-engine/actions/workflows/tests.yml) for actual execution results.

## What works today

- Create workflow definitions in the browser, set input/output fields, connect supported nodes, add conditional routes, and publish immutable revisions.
- Run the included purchase-approval and employee-leave definitions through the same runtime, without purchase-specific fields in the generic runtime tables.
- Route durable user tasks to roles from the caller's identity claims; complete tasks through the generic work queue.
- Configure reusable HTTPS system connections in the UI. Integration secrets are AES-GCM encrypted in PostgreSQL; workflow HTTP nodes reference the connection key and cannot select a different host.
- Execute HTTP actions through a persisted queue with a 15-second timeout, a 1 MiB response limit, up to three retryable attempts, and an operator-managed hostname allowlist.
- Inspect instance input/output, step execution, action results, and event history in the operations interface.

The earlier purchase-specific routes remain as a compatibility/demo path. New process behavior belongs in versioned definitions and generic runtime code.

## Quickstart: Docker + Keycloak (recommended)

**Start from a fresh clone with Docker Desktop or Docker Engine + Compose.** You do not need to
install .NET, Node.js or Keycloak on your computer. This local-only stack runs real OIDC login
through an included Keycloak realm, together with PostgreSQL, the .NET API and the Angular web app.

```bash
git clone https://github.com/mehrdadbahrainy/business-workflow-engine.git
cd business-workflow-engine
docker compose -f compose.local.yaml up -d --build --wait --wait-timeout 300
```

Then open **http://localhost:8080**. Choose **Sign in with your organization** and enter:

| Local demo account | Password | Permissions |
| --- | --- | --- |
| `flowadmin` | `DevOnly123!` | Workflow admin and purchase manager |
| `requester` | `DevOnly123!` | Run workflows and see own instances |
| `teamlead` | `DevOnly123!` | Complete team-lead tasks |

To **create a new user**, choose **Register** on the Keycloak login screen. For role assignment
and user administration, open **http://localhost:8081/admin/** and use
`local-admin` / `local-admin-change-me`; select realm **flowforge** → **Users**.
The self-registered account does not automatically receive admin rights.

Stop without deleting databases:

```bash
docker compose -f compose.local.yaml down
```

**This is a development stack, not a secure deployment.** All ports bind to 127.0.0.1,
but the demo passwords, Keycloak dev storage and HTTP login are unsafe for public access.
Do not deploy it to an internet-facing host or reuse its credentials in production.
See the [step-by-step local quickstart](docs/operations/local-quickstart.md) for screenshots-independent
instructions, errors, roles, volume persistence and troubleshooting. The existing
[production Compose file](compose.production.yaml) remains separate and requires proper
HTTPS OIDC configuration and secrets.

## Technology

- .NET 10 / ASP.NET Core Minimal APIs
- Entity Framework Core 10 / PostgreSQL
- Angular 21
- Modular monolith deployment

Pull requests and pushes to `main` build the .NET API, Angular application, and production Docker Compose images in GitHub Actions. A separate [test workflow](.github/workflows/tests.yml) runs xUnit validation and PostgreSQL-backed business scenarios, including restart recovery and simulated HTTPS integrations. See the [verification report](docs/quality/verification.md) for test evidence and known gaps.

## Browser E2E tests

A dedicated [Playwright GitHub Actions workflow](.github/workflows/e2e.yml) launches real
Chromium, Angular, ASP.NET Core, a disposable PostgreSQL database, and a loopback-only HTTPS
integration fixture. It exercises the **user interface**, not mocked workflow API responses:
demo sign-in/out, definition authoring and execution, role-based human tasks, connection
reachability, and a mobile viewport smoke check.

See [tests/e2e/README.md](tests/e2e/README.md) for local prerequisites and the
[CI run history](https://github.com/mehrdadbahrainy/business-workflow-engine/actions/workflows/e2e.yml).
Development-mode browser E2E is supplemented by an OIDC browser suite against the local Keycloak Docker stack, described in [local quickstart](docs/operations/local-quickstart.md). Neither local test mode certifies production OIDC onboarding,
registration, MFA, or session federation. These must be checked with a staging identity
provider before the authentication-related PR can be safely merged.

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

Open the Angular dev-server URL shown in the terminal. Its `/api` requests proxy to the local API. In Development, the sign-in screen lets you choose a demo identity such as `manager@example.test` or `hr@example.test`; the demo role claims are **not** trusted in production. A development identity is persisted locally until you sign out. Outside Development, the app instead offers **Sign in with your organization** through OIDC Authorization Code + PKCE, then verifies the issued access token with `/api/v1/auth/me`. The identity provider manages registration, password reset and MFA. No local username/password accounts are implemented. See [authentication setup and go-live checks](docs/operations/authentication-setup.md).

The API's OpenAPI document is available at `http://localhost:5195/openapi/v1.json` while running in Development. It describes the HTTP operations, the required `Idempotency-Key` header, and the Development-only `X-Demo-User` authentication scheme; OpenAPI is not exposed by the production configuration.

## Production self-hosting with an existing identity provider

The **production** Compose stack (different from `compose.local.yaml`) runs PostgreSQL, the .NET API, and the Angular operations UI as separate containers. It requires an existing OIDC-compatible identity provider; the engine validates bearer tokens but does not issue them or manage user accounts.

```powershell
Copy-Item .env.example .env
```

Edit `.env` before starting the stack. Set a long random `POSTGRES_PASSWORD`, `OIDC_AUTHORITY` (your OIDC issuer URL), `OIDC_AUDIENCE` (the intended API audience), and `OIDC_CLIENT_ID` (a **public** browser SPA client registered with the provider). Set `OIDC_ROLE_CLAIM` to the JWT claim carrying workflow roles and `WORKFLOW_ADMIN_ROLE` to the role allowed to edit definitions and connections. Set `OIDC_SCOPE` to include any API-specific delegated scope required by your identity provider. An optional `OIDC_REGISTRATION_URL` can link to your provider's HTTPS account-creation page. Configure a TLS-terminating reverse proxy. **Do not deploy the new login flow until you have completed the [OIDC readiness checklist](docs/operations/authentication-setup.md).**

To add an external system connection, add its hostname to `INTEGRATION_ALLOWED_HOSTS` (comma-separated). To store a credential in the web UI, set `INTEGRATION_ENCRYPTION_KEY` to a stable base64-encoded 32-byte key. Generate one with PowerShell:

```powershell
[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Keep this key in deployment secrets and back it up separately. Losing or rotating it without re-entering saved credentials makes those credentials unreadable. The UI never returns a saved secret. Integration base URLs are HTTPS and immutable; create a new connection key to target another host.

```powershell
docker compose --env-file .env -f compose.production.yaml up --build --wait
```

Open the configured HTTPS site. The web UI redirects to your identity provider for sign-in; it does not accept a manually pasted JWT. Use `http://localhost:8080` only behind a suitable local TLS setup. The API applies EF Core migrations at startup. PostgreSQL data is retained in the `workflow-data` named volume when containers are stopped or recreated.

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
| `GET` | `/api/v1/auth/config` | Public information for browser sign-in; no credentials |
| `GET` | `/api/v1/auth/me` | Server-verified user subject and role list; requires authentication |
| `GET` | `/api/v1/integrations` | List connection metadata without secrets |
| `POST` | `/api/v1/integrations` | Create a secured HTTP connection; requires workflow administrator role |
| `PUT` | `/api/v1/integrations/{key}` | Update connection name or rotate/clear its secret; base URL is immutable |
| `POST` | `/api/v1/integrations/{key}/test` | Admin-only HTTPS HEAD probe for an allowlisted connection; does not prove business API permission |
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
- [Verification report](docs/quality/verification.md)
- [Architecture case study](docs/architecture/case-study.md)
- [Repository structure](docs/architecture/repository-structure.md)
- [Architecture decision records](docs/architecture/decisions/)

The product hypothesis and market position are not validated. The repository does not claim that a .NET workflow engine alone is differentiated. See the [MVP scope](docs/product/mvp-scope.md) for acceptance criteria and explicit exclusions.

## License

This project is licensed under the [Apache License 2.0](LICENSE).
