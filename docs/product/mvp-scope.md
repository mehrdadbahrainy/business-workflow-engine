# MVP Scope: Definition-Driven Workflow Engine

**Status:** Product direction accepted; the first generic runtime, browser designer, role tasks, and HTTP connections are implemented. Hardening and acceptance evidence remain. Purchase approval is one example process, not the MVP boundary.

## MVP goal

Let an application team define, publish, start, and operate different business processes without adding process-specific backend code for each one. The engine must persist execution, route data through declared steps, wait for human work, and return definition-selected outputs.

The MVP is a self-hostable workflow engine with a web authoring interface and an operational interface. Users can define workflows in the browser, set routing and outputs, assign work to distinct roles, and call other systems through a generic HTTP/REST integration step. A broad vendor connector catalog is not required for the first usable version.

## Included engine capabilities

### Define and publish processes

- Define a process using a versioned JSON document with an identifier, revision, input/output contracts, steps, routes, assignment policies, and mappings.
- Create and edit definitions in the web application using a visual node-and-edge workflow canvas; authors should not need to hand-edit JSON for normal workflows.
- Validate the graph and all expressions before publication; reject invalid or unsupported step types with actionable errors.
- Keep published revisions immutable and pin every instance to the revision with which it started.
- Use a bounded, documented expression/mapping model; never execute arbitrary scripts from definitions.

### Run and inspect instances

- Start any published workflow by identifier with definition-specific JSON input and an idempotency key.
- Support start, variable assignment, exclusive conditional routing, role-based user task, HTTP/REST action, and end steps.
- Configure distinct workflow roles and map authenticated identity claims to role membership. Enforce role membership on task listing and completion.
- Configure HTTP method, connected system, relative path, query/body mappings, and response handling for service actions. Keep credentials out of definition JSON and expose them through encrypted connection records.
- Start workflows through the authenticated API. Add a secured inbound webhook trigger and outbound callbacks after the first HTTP action semantics are reliable.
- Persist current execution position, step attempts, data, task state, and append-only history so instances survive restarts and human wait periods.
- Resume an instance on authorized user-task completion, validate the completion payload, and continue through the same pinned definition.
- Complete with a generic lifecycle status, a definition-selected business outcome, and output JSON matching the definition's contract.
- Query instances, current work, outputs, and execution history through a versioned API and operations UI.

### Demonstrate generality

- Provide at least two meaningfully different definitions (for example, purchase approval and employee leave request) that execute through the same generic runtime, designer, role model, and integration step.
- Show that adding an example process changes only its definition and input/output presentation, not process-specific engine endpoints, entity columns, or branching code.
- Keep the engine's core API and database model free of purchase-specific concepts such as currency, approval threshold, or purchase outcome.

## Staged after the first generic runtime

- Provider-specific connector catalog and reusable integration marketplace beyond generic HTTP/REST.
- Timers, escalation, parallel branches and joins, cancellation/compensation, and loops after their recovery semantics are specified.
- BPMN interoperability, broader identity administration, multi-tenancy, and high availability when validated use cases justify them.

## MVP acceptance evidence

1. Create, edit, validate, and publish a definition through the web designer without hand-editing JSON; reject structurally invalid graphs before publication.
2. Start a workflow with its own JSON contract, then retrieve the same durable instance using the returned ID.
3. Exercise at least two distinct routes from data-driven conditions.
4. Route work to different configured roles and verify only members of each role can see and complete its tasks.
5. Reach a user task, restart the application, and observe the same task still pending.
6. Call an external HTTP endpoint using mapped request data and a configured credential reference; inspect the recorded result and retry state.
7. Reach the configured end step and retrieve its selected business outcome and mapped JSON output.
8. Run the purchase-approval and an unrelated example definition without adding backend code or schema columns for either process.
9. Inspect a chronological history explaining the input, steps, selected routes, work, integration calls, and final output.

These are product acceptance conditions. They do not authorize running automated tests unless separately requested.

## Product guardrails

- A workflow definition is data interpreted by a fixed, versioned engine contract; it is not executable code.
- Business input and output are definition-specific. Generic runtime tables do not encode one industry's fields.
- Every state transition has a recoverable position, a clear cause, and a corresponding history record.
- Definitions used by running instances cannot change underneath them.
- Process authors choose routes and outcomes in the definition; the engine executes and records those choices.
- The engine coordinates work but does not replace the system of record for HR, finance, procurement, CRM, or other business domains.
- Do not claim generic behavior for a step type until validation, persistence, retry, failure, and authorization semantics are implemented.

## Explicitly outside this MVP

- A broad catalog of prebuilt vendor connectors.
- Arbitrary user-supplied scripting or code plugins.
- A promise to support every BPMN construct or every third-party connector.
- Domain products such as procurement, HR management, payment processing, or ERP.
- AI-generated workflows, multi-tenant SaaS, high availability, or compliance certification.

## Current gap

The generic designer, schema-driven role-task forms, HTTP connections, and durable runtime paths are implemented. Validation in this environment has not exercised migration, role authorization, execution recovery, or HTTP retry behavior against a live PostgreSQL deployment and identity provider. Integration encryption-key rotation also requires an operator procedure; credentials must be re-entered after a key change. The older purchase-specific API and database tables remain as a compatibility path and are not used by generic workflow execution. These operational gaps are tracked against [ADR 0006](../architecture/decisions/0006-definition-driven-general-purpose-runtime.md) and [ADR 0007](../architecture/decisions/0007-web-authoring-roles-and-http-integrations.md).
