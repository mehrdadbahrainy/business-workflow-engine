# Initial Repository Structure

**Status:** Proposed layout for the first implementation slice. Project and folder names are provisional until the project-naming step.

```text
.
├── docs/
│   ├── architecture/
│   │   ├── decisions/
│   │   ├── domain-model.md
│   │   └── overview.md
│   └── product/
├── examples/
│   └── purchase-approval/
├── src/
│   ├── server/
│   │   └── BusinessWorkflowEngine.Api/
│   │       ├── Modules/
│   │       │   ├── WorkflowDefinitions/
│   │       │   ├── WorkflowExecution/
│   │       │   ├── HumanTasks/
│   │       │   └── ExecutionHistory/
│   │       ├── Api/
│   │       └── Persistence/
│   └── web/
│       └── business-workflow-engine-web/
└── tests/
    ├── BusinessWorkflowEngine.UnitTests/
    └── BusinessWorkflowEngine.IntegrationTests/
```

## Why one backend project initially

The MVP has one deployable backend and one consistency boundary. Keeping its domain modules in one .NET project avoids project-reference choreography before there is a proven need for it. The module folders express ownership and keep API, persistence, and domain behavior discoverable without presenting a generic clean-architecture template as product value.

The Angular application remains a separate frontend project because it has a distinct toolchain and deployment asset. Purchase approval lives in `examples/` as an inspectable definition and sample data, not as the identity of the engine itself.

## Rules for changing the layout

- Keep HTTP contracts at the API boundary; do not make database entities the public API.
- Keep each module's commands, queries, and persistence decisions close to that module.
- Do not add a shared-kernel project until at least two modules need a stable shared concept.
- Separate a backend module into its own project only when compile-time isolation or independent ownership justifies it.
- Add worker, connector, and deployment projects only when a scoped behavior requires them.

The implementation may refine this structure when actual code exposes a better boundary. This document is a starting constraint, not a promise to preserve every folder.
