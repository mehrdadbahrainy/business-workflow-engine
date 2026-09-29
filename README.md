# Business Workflow Engine

**Status: early implementation. Workflow execution is not implemented yet.**

Business requests should not disappear between people, applications, and manual follow-up.

In many operational processes, a request starts in one application, waits for a human decision, and continues through another action or system. When each part is tracked in a different place, it becomes difficult to know who owns the next step, what has already happened, and whether the request reached its intended outcome.

This project explores an open-source, self-hostable workflow runtime that application teams can integrate into existing business software, paired with an operational interface for people to handle approvals and inspect request history.

## What this project is exploring

- Start and query business requests through an application-facing API.
- Persist each request's workflow state while it waits for a human decision.
- Give approvers a clear queue of work and operators a readable execution history.
- Keep the workflow alongside existing business applications instead of replacing them.

The current product hypothesis focuses on request-driven workflows that cross an application boundary and include a human handoff. It is not yet validated with target users, and the project does not claim a differentiated market position.

## First use-case candidate

The initial vertical-slice candidate is purchase-request approval: submit a request, apply a simple threshold rule, wait for a manager when required, and record the approval outcome. The workflow does not place an order or authorize a payment. The candidate and its assumptions are described in [the use case](docs/product/first-use-case.md).

## Project status

The repository contains product and architecture proposals plus initial .NET and Angular application scaffolds. The first MVP is intended to demonstrate that an existing application can start a request, the workflow can wait durably for an assigned approver, and both the host application and an operational UI can inspect the same final outcome. The approval runtime and usable UI are not implemented yet.

## Development scaffold

The API and web application can be started independently:

```powershell
dotnet restore BusinessWorkflowEngine.sln
dotnet run --project src/server/BusinessWorkflowEngine.Api
```

The API scaffold exposes `GET /healthz` and a development-only OpenAPI document. To start the Angular application, run these commands from `src/web/business-workflow-engine-web`:

```powershell
npm ci
npm start
```

The Angular shell does not contain workflow screens yet.

See the [MVP scope](docs/product/mvp-scope.md) for planned behavior and explicit exclusions. Do not treat this repository as production software yet.

## Design notes

- [Problem statement and product hypothesis](docs/product/problem-statement.md)
- [First use case: purchase approval](docs/product/first-use-case.md)
- [MVP scope](docs/product/mvp-scope.md)
- [Initial domain model](docs/architecture/domain-model.md)
- [Initial architecture](docs/architecture/overview.md)
- [Repository structure](docs/architecture/repository-structure.md)
- [Architecture decision records](docs/architecture/decisions/)

The current implementation direction is C#/.NET, ASP.NET Core, EF Core, PostgreSQL, and Angular in a modular monolith. These are implementation choices for the product hypothesis; they are not the problem statement.

## Non-goals for the first version

- A drag-and-drop automation designer or an n8n clone.
- Replacing CRM, ERP, procurement, or finance systems.
- A catalog of integrations for every vendor.
- Microservices or a message broker without a demonstrated need.
- AI agents, chatbots, or AI-generated workflows.

## License

This project is licensed under the [Apache License 2.0](LICENSE).
