# ADR 0007: Provide Web Workflow Authoring, Role Routing, and HTTP Integrations

- **Status:** Accepted
- **Date:** 2026-10-01

## Context

The product must let users define and operate workflows in the web application. A generic runtime API alone does not satisfy that product goal. Workflows must route tasks to different roles and exchange data with external business systems, while keeping the process definition understandable and safe to operate.

The current application has an Angular operations UI, one hard-coded approval role, and no generic integration step. This decision defines the intended user-facing authoring model and initial integration boundary.

## Decision

The web application will include a visual node-and-edge workflow designer. Authors create a versioned definition, connect supported steps, configure conditions and data mappings, assign user tasks to workflow roles, validate the graph, and publish an immutable revision. The UI authors the same portable JSON definition consumed by the runtime; normal usage does not require manual JSON editing.

The first usable designer supports these node types:

- start with a definition-specific input contract;
- variable mapping;
- exclusive conditional route;
- user task assigned to a named workflow role;
- HTTP/REST action with mapped request and response data;
- end with a configured business outcome and mapped output.

Role membership is derived from authenticated identity claims configured for the deployment. A task is visible and actionable only to a subject authorized for its assigned role. Workflow definitions name roles; they do not provision users or contain provider-specific identity credentials.

HTTP actions call a configured endpoint through a durable worker. Definitions reference a credential by stable key; secrets are configured and stored outside the definition document and are never returned to the browser after entry. Each action has bounded timeouts and retry policy, a durable attempt record, and an idempotency strategy appropriate to its configured operation. Arbitrary scripts and user-defined executable plugins are not allowed.

The first integration surface is generic HTTP/REST. A built-in catalog for specific vendors, inbound webhook triggers, and outbound completion callbacks can follow after the generic action's authorization, failure, and delivery behavior is operational.

## Consequences

- The current approval-only UI must become definition-driven and render work-item fields from task configuration/contracts.
- The runtime needs durable HTTP action dispatch, result persistence, and explicit retry behavior before claiming reliable external integration.
- Operators need a deployment mechanism for named integration credentials. Secret values cannot live in ordinary workflow JSON or be logged.
- Role claim mapping and authorization become core configuration and require clear setup documentation.
- Definition validation must be shared between authoring and publication so the UI cannot publish a graph the runtime interprets differently.
- The first canvas may be deliberately small; it needs to make route, role, and integration behavior inspectable without promising BPMN completeness.

## Implementation order

1. Define the versioned graph schema, role assignment contract, and bounded expression/mapping syntax.
2. Add server-side definition validation and immutable publish revisions.
3. Implement generic runtime persistence and execution for start, mapping, route, role task, and end nodes.
4. Build the web canvas to author those nodes and validate drafts through the API.
5. Add the durable HTTP action worker and deployment-managed credentials.
6. Migrate purchase approval into an example and create a second independent workflow that exercises different roles and an external HTTP action.

## Revisit when

Validated users require BPMN interchange, provider-specific connectors, complex identity federation, or integration triggers beyond the HTTP action and API start contract.
