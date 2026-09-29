# Repository Structure

**Status:** Current implementation layout for the first self-hosted vertical slice.

```text
.
├── CONTRIBUTING.md
├── LICENSE
├── README.md
├── SECURITY.md
├── .config/
│   └── dotnet-tools.json
├── .github/
│   └── workflows/
│       └── build.yml
├── deploy/
│   └── api.Dockerfile
├── docs/
│   ├── architecture/
│   │   ├── decisions/
│   │   ├── domain-model.md
│   │   └── overview.md
│   └── product/
├── examples/
│   └── purchase-approval/
│       └── definition.json
├── src/
│   ├── server/
│   │   └── BusinessWorkflowEngine.Api/
│   │       ├── Operations/
│   │       └── Workflows/
│   │           └── Migrations/
│   └── web/
│       └── business-workflow-engine-web/
│           └── src/app/
├── compose.yaml
└── compose.production.yaml
```

## Why one backend project initially

The MVP has one deployable backend and one consistency boundary. Keeping its workflow definition, execution, human-task, and execution-history behavior in one .NET project avoids project-reference choreography before there is a proven need for it. The folders express ownership while the modules share a process and relational database.

The Angular application is separate because it has a distinct toolchain and deployment asset. The purchase-approval definition lives under `examples/` so it remains inspectable and versioned independently from API implementation code.

The development Compose file starts PostgreSQL for host-based development. The production Compose file builds the API and web images and runs the single-host self-hosted stack. They are separate on purpose: development credentials are not production deployment settings.

## Rules for changing the layout

- Keep HTTP contracts at the API boundary; do not expose database entities as public responses.
- Keep domain behavior and persistence decisions close to the module that owns them.
- Keep request state and its execution history in one database consistency boundary for the single-instance MVP.
- Add a worker, connector, or independently deployed service only when a scoped behavior requires it.
- Separate a backend module into its own project only when compile-time isolation or independent ownership justifies it.
- Treat this layout as a current implementation, not a promise to preserve every folder as the product evolves.
