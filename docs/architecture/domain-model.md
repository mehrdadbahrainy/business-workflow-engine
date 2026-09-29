# Initial Domain Model

**Status:** Initial model, updated to reflect the first purchase-approval implementation. Names and boundaries should be revisited as more workflows are exercised.

## Modeling focus

The MVP coordinates a request through a defined process, may wait for a human decision, and must retain a clear history. The domain distinguishes a reusable process definition, one execution of that definition, the human work created during an execution, and the recorded facts about execution.

## Core concepts

### Workflow Definition

A published description of a process: its identity, revision, inputs, supported behavior, and transitions. The initial purchase-approval definition is a versioned JSON document containing the approval threshold and approver subject. The loaded revision is persisted and each instance records the revision it started with.

The MVP supports only the seeded purchase-approval path. It does not provide a visual designer, general workflow language, or definition-management API.

### Workflow Instance

One execution created from a specific definition revision, linked to its business request and input data. The instance stores current lifecycle state and outcome. It can remain waiting without holding an application request open.

An instance must continue under the revision with which it started, even if a newer definition is published later.

### Step Execution

A separate record for the execution of one step in a workflow instance is not persisted in the first implementation. Instance state and execution events represent the single approval handoff. Add first-class step records only when a concrete workflow needs multiple independently tracked execution steps.

### Approval Task

A unit of human work created when an instance reaches an approval step. It records the assigned approver, decision, optional comment, and completion time. A task is actionable only while pending and only by its assigned approver.

### Execution Event

An append-only record of a meaningful fact in the instance lifecycle, such as request started, approval requested, or a decision recorded. Events provide the chronological explanation shown to operators; current instance state provides an efficient view of where work stands now. `ActorSubject` identifies who performed the action: human-initiated events use the authenticated subject, while deterministic routing and policy events use the stable `system:workflow-engine` subject. Event payloads are returned as structured JSON to API clients; the current database stores that payload as serialized JSON text.

## State model for the first slice

### Workflow instance

- A request at or below the configured USD 1,000 threshold reaches `completed` with outcome `auto-approved`.
- A request above the threshold enters `awaiting-approval` and has one pending approval task.
- An approval decision moves the instance to `completed` with outcome `approved` or `rejected`.

Rejection is a business outcome; `completed` remains the terminal workflow state. None of these outcomes means an order was placed or fulfilled. Invalid input is rejected before an instance is accepted.

### Approval task

- `pending` transitions once to `approve` or `reject`.
- A repeated or concurrent decision cannot silently overwrite the recorded decision.

## Invariants to preserve

1. Every instance references one immutable definition revision.
2. A waiting-for-approval instance has exactly one pending approval task in this MVP.
3. An approval task can be completed once; repeated or concurrent decisions cannot silently overwrite the recorded decision.
4. An actor who is not assigned to an approval task cannot complete it.
5. Every externally visible state transition has a corresponding execution event.
6. Repeating a request submission with the same actor, idempotency key, and normalized content does not create another instance; different content with the same key is rejected.
7. Instance state, task completion, and their corresponding event are persisted consistently.

## Deliberate omissions

This model does not introduce a general event-sourced aggregate framework, saga orchestration, compensation, parallel tokens, arbitrary timers, process mining, business calendars, or a broad task-management domain. Those concepts should be added only when a concrete workflow requires them.

## Open modeling questions

- Should the business request be owned by the host application, or should the engine store a normalized request record in addition to workflow input?
- When a second workflow needs independently tracked steps, should they be represented as step executions or as a more general runtime construct?
- Should instance state be maintained as a transactional projection of execution events, or updated alongside events in an ordinary relational model?
