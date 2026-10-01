# Workflow Engine Domain Model

**Status:** Target domain model for a reusable definition-driven runtime. The current persistence model still contains purchase-specific fields and must be migrated toward these concepts.

## Modeling focus

The engine owns process execution, not the business records owned by an ERP, CRM, HR, or other host system. A workflow definition describes how JSON input moves through steps, how decisions choose routes, what work is assigned to people, and what output is returned. A workflow instance is one durable execution of one immutable definition revision.

## Core concepts

### Workflow Definition and Revision

A definition has a stable identifier and one or more immutable revisions. Each revision declares:

- a versioned input contract and output contract;
- a graph of uniquely identified steps and transitions;
- supported step configuration and task assignment rules;
- conditional expressions and output mappings in the engine's bounded expression language.

Publishing validates graph shape, references, step configuration, data contracts, and expression safety before making a revision startable. Existing instances remain pinned to the revision they started with.

### Workflow Instance

One execution of a definition revision, with validated input data, runtime variables, current status, current step/token position, initiator, timestamps, and eventual output. It can wait durably without holding a request or process memory. Domain-specific values such as amount, currency, requester reference, and purchase outcome belong in input/output data, not fixed columns in the generic runtime model.

### Step Execution

One attempt to execute a node in an instance. It records step identity and type, attempt number, status, input/output snapshots or references, timing, and failure details needed for retry and history. Runtime progress must be recoverable after process restart. Re-entry and retry semantics are explicit for each supported step type.

### Transition

A directed edge between definition steps. An unconditional transition advances directly. A conditional transition evaluates a safe expression over instance data and routes along a selected edge. The engine rejects ambiguous or missing routes according to the node's declared policy; it never evaluates arbitrary C#, JavaScript, or shell code from a definition.

### Human Work Item

A generic unit of work created by a user-task step. It contains a title/description, assignment policy, task-specific input, allowed completion fields, lifecycle status, and completion data. An approval task is one use of a user task whose completion payload carries a decision. The runtime authorizes the assignee and commits task completion with instance progress and history.

### Action Attempt

A request to execute a registered engine action (for example, an HTTP callback or connector operation). Definitions select a known action and provide data; they cannot upload executable code. Attempts record idempotency, status, retry timing, and result. Side effects require durable dispatch and reconciliation semantics.

### Workflow Output

Data emitted by an end step according to the definition's output mapping. It is distinct from the generic instance lifecycle status (`completed`, `failed`, or `cancelled`) and can express business outcomes such as `approved`, `rejected`, or `needs-more-information`.

### Execution Event

An append-only fact such as instance started, step entered/completed, route selected, work assigned/completed, action failed/retried, or instance ended. It records actor, time, step, and structured data. Events explain execution; current instance and step state provide efficient reads. This is not a mandate for full event sourcing.

## Invariants to preserve

1. Every instance references one immutable definition revision.
2. Every running instance has a recoverable execution position and validated runtime data.
3. A user task can be completed only according to its assignment/authorization policy and only once.
4. Every externally visible state change has a corresponding execution event.
5. Retried API commands or worker deliveries cannot silently duplicate a logical transition or side effect.
6. Instance state, work/action updates, and corresponding events are committed consistently.
7. Definition expressions and action references are validated against the supported engine contract; definitions cannot execute arbitrary code.
8. End-step output conforms to the published output contract and remains queryable after completion.

## First generic execution slice

The first definition-driven runtime should support start, variable assignment, exclusive conditional routing, user task, and end steps. It must support arbitrary JSON input and configurable mappings for task input and end output. This small set already supports distinct processes such as purchase approval, leave requests, and employee onboarding without embedding those business concepts in the engine. Service actions, timers, parallel gateways, loops, and visual authoring follow only after their durable semantics are specified.

## Deliberate boundaries

The runtime does not own the authoritative employee, invoice, purchase order, or customer record. It does not embed industry policy, arbitrary scripting, or a promise of universal connector compatibility. It coordinates process state and work; host systems remain the systems of record.
