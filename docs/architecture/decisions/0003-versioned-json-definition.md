# ADR 0003: Use a Small Versioned JSON Definition for the First Workflow

- **Status:** Accepted for the MVP
- **Date:** 2026-09-30

## Context

The runtime needs a definition revision so an instance has stable rules for its entire lifetime. The first vertical slice has one purchase-approval flow and does not need arbitrary workflow authoring.

## Decision

Represent the seeded purchase-approval definition as a versioned JSON document. Validate it against a typed schema when loading it. The MVP supports only the fields and behavior needed by this flow: definition identity and revision, approval threshold, and the designated approver subject. Persist each published revision and pin every instance to one revision.

The first release does not expose a general definition editor or definition-management API. The example definition is the source for the initial seeded revision.

## Options considered

1. **Small versioned JSON document:** externalized process parameters and immutable revisions with no visual designer.
2. **C# workflow code:** strongly typed behavior compiled and deployed with the host.
3. **BPMN:** use an existing standard and implement a broader process model and execution semantics.

## Why this option

The runtime is a separate application integrated through REST, so a definition should not require the host application to compile workflow plugins. A small JSON document is easy to inspect and version alongside the example. BPMN would add a much larger execution contract than the purchase-approval slice needs.

## Trade-offs

- This is a project-specific schema and will need compatibility rules as it evolves.
- The MVP can configure only the supported purchase-approval behavior; JSON does not imply arbitrary workflows.
- Editing a seeded definition is a deployment-time operation until a validated management workflow justifies an API or UI.
- A typed C# workflow extension could be more expressive, but would couple process changes to code deployment.

## Revisit when

A second real workflow needs behavior the existing schema cannot express. Extend the schema only for a demonstrated process need; evaluate a standard such as BPMN only if the interoperability or modeling benefit pays for its semantic and execution cost.
