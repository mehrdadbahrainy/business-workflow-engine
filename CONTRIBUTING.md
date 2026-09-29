# Contributing

Thanks for helping improve Business Workflow Engine. The project is an early, single-organization MVP for application teams that need to hand a business request to a person and retain its outcome. Its product hypothesis has not yet been validated with target users.

## Before proposing a change

- For a product capability, describe the real process or operational problem it addresses. Keep examples anonymized and leave confidential business data out of issues and pull requests.
- Check the [MVP scope](docs/product/mvp-scope.md) and [product discovery plan](docs/product/validation-plan.md). The current implementation supports one seeded purchase-approval workflow; a general workflow language, designer, or connector framework is not an established project direction.
- For a change to a domain boundary, persistence model, identity boundary, or deployment model, update the relevant architecture note or add an ADR under `docs/architecture/decisions/`.
- For changes to persisted data, include an EF Core migration and explain compatibility and deployment implications in the pull request.

## Local build

Install the .NET 10 SDK, Node.js 24, and npm. Docker Compose and PostgreSQL are needed to run the API locally; see the [README](README.md) for the development setup.

From the repository root, build the API:

```powershell
dotnet restore BusinessWorkflowEngine.sln
dotnet build BusinessWorkflowEngine.sln --configuration Release --no-restore
```

Build the web application:

```powershell
Set-Location src/web/business-workflow-engine-web
npm ci
npm run build
```

The current GitHub Actions check runs these API and web builds. It does not run an automated test suite yet; describe the relevant manual verification in your pull request when behavior changes.

## Pull requests

- Keep the change focused and explain the user or operator problem it solves.
- Describe API, database, authentication, and deployment effects where applicable.
- Include migration and configuration notes when needed; never include secrets or real user data.
- Update documentation and runnable examples when behavior or setup changes.
- Report what you built and how you verified it. Do not describe a build as a test.

By submitting a contribution, you agree that it is provided under the project's [Apache License 2.0](LICENSE).
