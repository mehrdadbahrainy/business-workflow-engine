# ADR 0006: Build a Definition-Driven General-Purpose Workflow Runtime

- **Status:** Accepted
- **Date:** 2026-10-01

## Context

The repository's first vertical slice implements a purchase-approval flow with purchase-specific API routes, database columns, task types, and UI. That is useful as a demonstration, but it does not meet the product objective: users must be able to define different business processes, their routes, and their outputs without a code change for each process. Purchase approval is an example definition, not the engine's product boundary.

The existing decision to externalize a few purchase parameters as JSON is not a generic process model. Expanding the engine requires a stable execution contract before additional domain-specific flows are added.

## Decision

Make immutable, versioned workflow definitions the source of executable process structure. A definition declares an input contract, a graph of supported step types, transitions and conditions, task data/assignment rules, and output mapping. A workflow instance stores generic JSON data and is pinned to one published revision for its lifetime.

The first generic execution slice supports:

1. **Start:** validate arbitrary definition-specific JSON input.
2. **Set variables:** apply deterministic, validated data mappings.
3. **Exclusive route:** choose one outgoing transition by evaluating bounded conditions over the instance data.
4. **Role-based user task:** persist work assigned to a configured workflow role; authorize completion through identity-provider claims and resume with definition-specific data.
5. **HTTP/REST action:** call an external system through a durable worker using mapped request/response data and a credential reference. Record attempts and enforce timeout and bounded retry policy.
6. **End:** persist lifecycle completion, a definition-selected business outcome, and output data conforming to the definition's output contract.

Definitions are submitted and published through an API as JSON. The engine validates graph connectivity, step configuration, data contracts, references, expressions, and output mappings before publication. Published revisions are immutable. Running a definition never evaluates arbitrary scripts, C#, JavaScript, or shell commands supplied by users. Approval is modeled as a user task and sample process, not as a built-in engine concept.

The Angular web application provides the workflow authoring experience: users visually create and connect supported nodes, configure roles, routes, data mappings, and HTTP actions, validate, and publish. Definitions remain portable JSON internally, but ordinary authors are not expected to edit JSON directly. HTTP credentials are stored in deployment-managed secrets and referenced by name; secret values are never embedded in a definition or shown after entry.

The current purchase-specific implementation is a transitional vertical slice. It should be migrated to generic instance, step execution, work-item, and definition models. Keep it only as a compatibility example during the transition; new engine behavior must not add purchase-specific columns or routes.

The authoring experience is part of the intended product: users define and publish workflows in the web application. Human steps can target workflow-defined roles, and service steps can communicate with external systems through configured integrations. The first connector is generic HTTP/REST; provider-specific connector catalogs are not an MVP requirement.

## Options considered

1. **Extend the purchase workflow in place:** quick, but cements domain-specific entities and APIs and does not deliver reusable definitions.
2. **Definition-driven graph with a bounded step catalog:** selected; separates process data and topology from generic execution while keeping execution semantics reviewable.
3. **Arbitrary scripts/plugins in definitions:** rejected for the core runtime because it makes safety, determinism, upgrades, and operations depend on untrusted executable code.
4. **Adopt BPMN immediately:** defer. First implement and validate the minimum execution contract; evaluate BPMN if interoperability or authoring evidence justifies its broader semantics and implementation cost.

## Consequences

- Generic JSON data replaces fixed purchase request fields in the runtime model and API.
- Approval-specific task endpoints evolve into generic workflow instance and work-item APIs.
- The operations UI must render tasks from definition-provided schemas/configuration rather than assume approval fields.
- Input/output contracts and expressions need explicit versioning, validation, and compatibility guarantees.
- A second independent example definition becomes acceptance evidence that new processes require no engine code changes.
- Durable external actions, retry scheduling, timers, parallel execution, and a visual designer remain staged capabilities; the definition schema must not imply support before runtime semantics exist.
- Existing purchase examples and data require a deliberate migration path; they cannot be silently interpreted as generic instances.

## Implementation order

1. Specify and validate the versioned definition document and safe expression subset.
2. Replace purchase-specific persistence with generic definitions, instances, step executions, user tasks, and events.
3. Implement generic start, deterministic runner, user-task wait/resume, routing, and end-output handling.
4. Expose definition publish/start/query/work-item APIs and add a visual web workflow designer for the supported step types.
5. Add workflow roles mapped to authenticated identity claims and an HTTP service step with secret-safe credential handling.
6. Convert purchase approval and a second unrelated process into example definitions; prove both run through the same engine without process-specific backend code.

## Revisit when

The supported graph/expression contract cannot safely represent validated user processes, or standards-based definition exchange and tooling are proven adoption requirements. Revisit individual step types and connector types when a real use case establishes their execution and failure semantics.
