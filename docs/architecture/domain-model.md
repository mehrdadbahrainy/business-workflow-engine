# Initial Domain Model

**Status:** Proposed model for the MVP. Names and boundaries should be revisited when the first workflow is implemented and exercised.

## Modeling focus

The MVP coordinates a request through a defined process, may wait for a human decision, and must retain a clear history. The domain therefore distinguishes the reusable process definition, one execution of that definition, the human work created during an execution, and the recorded facts about execution.

## Core concepts

### Workflow Definition

A published description of a process: its identity, revision, inputs, executable steps, and transitions. A definition is a plan for future executions, not a running process.

For the MVP, the definition needs only the purchase-approval path: validate input, evaluate a threshold, wait for one assigned approval when required, and finish with an outcome. Authoring remains controlled and small; this model does not imply a visual designer or arbitrary scripting.

### Workflow Instance

One execution created from a specific published definition revision, linked to its business request and input data. An instance tracks its current lifecycle state and current position in the process. It can remain waiting without holding an application request open.

An instance must continue under the revision with which it started, even if a newer definition is published later.

### Step Execution

The execution of one step for one workflow instance. It records the step identity, its state, and relevant input/output references. A definition step describes what may happen; a step execution records what did happen for this instance.

The MVP needs validation, a deterministic condition, a human approval, and completion. It does not need a general plugin system for arbitrary step types.

### Approval Task

A unit of human work created when an instance reaches an approval step. It records the assigned approver, the decision, an optional comment, and completion time. A task is actionable only while open and only by its assigned approver.

### Execution Event

An append-only record of a meaningful fact in the instance lifecycle, such as request accepted, validation failed, approval requested, approval decided, or instance completed. Events provide the chronological explanation shown to operators; the current instance state provides an efficient view of where work stands now.

## State model for the first slice

### Workflow instance

`Running` → `WaitingForApproval` → `Running` → `Completed`

The alternative approval outcome is:

`WaitingForApproval` → `Rejected`

The threshold path that does not require a manager can move from `Running` to `Completed` with an explicit policy outcome in history. Invalid input is rejected before an instance is accepted, with a clear API validation response.

### Approval task

`Open` → `Approved`

or

`Open` → `Rejected`

No other task transition is required for the MVP.

## Invariants to preserve

1. Every instance references one immutable definition revision.
2. A waiting-for-approval instance has exactly one open approval task in this MVP.
3. An approval task can be completed once; repeated or concurrent decisions cannot silently overwrite the recorded decision.
4. An actor who is not assigned to an approval task cannot complete it.
5. Every externally visible state transition has a corresponding execution event.
6. Repeating a request submission with the same idempotency key does not create another instance for that request.
7. Instance state, task completion, and their corresponding event are persisted consistently.

## Deliberate omissions

This model does not yet introduce a general event-sourced aggregate framework, saga orchestration, compensation, parallel tokens, arbitrary timers, process mining, business calendars, or a broad task-management domain. Those concepts should be added only when a concrete workflow requires them.

## Open modeling questions

- Should the business request be owned by the host application, or should the engine store a normalized request record in addition to workflow input?
- Is an approval task always a distinct domain object, or can it remain a specialized step execution until more human-task behavior is needed?
- Should instance state be maintained as a transactional projection of execution events, or updated alongside events in an ordinary relational model?
- Which definition representation keeps the initial model understandable while supporting a second real workflow later?
