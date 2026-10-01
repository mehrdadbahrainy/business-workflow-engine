# MVP Scope: Definition-Driven Workflow Engine

**Status:** Product direction accepted; generic runtime capabilities are not yet implemented. Purchase approval is one example process, not the MVP boundary.

## MVP goal

Let an application team define, publish, start, and operate different business processes without adding process-specific backend code for each one. The engine must persist execution, route data through declared steps, wait for human work, and return definition-selected outputs.

The MVP is a self-hostable workflow runtime with an operational interface. A visual drag-and-drop designer and an unlimited connector catalog are not required to prove the engine; JSON definitions and APIs are sufficient.

## Included engine capabilities

### Define and publish processes

- Define a process using a versioned JSON document with an identifier, revision, input/output contracts, steps, routes, assignment policies, and mappings.
- Validate the graph and all expressions before publication; reject invalid or unsupported step types with actionable errors.
- Keep published revisions immutable and pin every instance to the revision with which it started.
- Use a bounded, documented expression/mapping model; never execute arbitrary scripts from definitions.

### Run and inspect instances

- Start any published workflow by identifier with definition-specific JSON input and an idempotency key.
- Support start, variable assignment, exclusive conditional routing, user task, and end steps.
- Persist current execution position, step attempts, data, task state, and append-only history so instances survive restarts and human wait periods.
- Resume an instance on authorized user-task completion, validate the completion payload, and continue through the same pinned definition.
- Complete with a generic lifecycle status, a definition-selected business outcome, and output JSON matching the definition's contract.
- Query instances, current work, outputs, and execution history through a versioned API and operations UI.

### Demonstrate generality

- Provide at least two meaningfully different definitions (for example, purchase approval and employee leave request) that execute through the same generic runtime.
- Show that adding an example process changes only its definition and input/output presentation, not process-specific engine endpoints, entity columns, or branching code.
- Keep the engine's core API and database model free of purchase-specific concepts such as currency, approval threshold, or purchase outcome.

## Staged after the first generic runtime

- Registered service/action steps with durable dispatch, idempotency, retries, and transactional outbox.
- Timers, escalation, parallel branches and joins, cancellation/compensation, and loops after their recovery semantics are specified.
- Visual workflow authoring, BPMN interoperability, broader identity administration, multi-tenancy, and high availability when validated use cases justify them.

## MVP acceptance evidence

1. Publish a valid definition through the API and reject structurally invalid definitions before they can be started.
2. Start a workflow with its own JSON contract, then retrieve the same durable instance using the returned ID.
3. Exercise at least two distinct routes from data-driven conditions.
4. Reach a generic user task, restart the application, and observe the same task still pending.
5. Complete work as an authorized actor, reject an unauthorized or repeated completion, and resume at the declared next step.
6. Reach the configured end step and retrieve its selected business outcome and mapped JSON output.
7. Run the purchase-approval and an unrelated example definition without adding backend code or schema columns for either process.
8. Inspect a chronological history explaining the input, steps, selected routes, work, and final output.

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

- A no-code drag-and-drop designer.
- Arbitrary user-supplied scripting or code plugins.
- A promise to support every BPMN construct or every third-party connector.
- Domain products such as procurement, HR management, payment processing, or ERP.
- AI-generated workflows, multi-tenant SaaS, high availability, or compliance certification.

## Current gap

The current code implements only a seeded purchase-approval path and contains purchase-specific persistence, routes, and UI. The next engineering milestone is to specify and implement the generic definition contract and execution model in [ADR 0006](../architecture/decisions/0006-definition-driven-general-purpose-runtime.md). The existing slice is an example to migrate, not proof that the generic engine already exists.
