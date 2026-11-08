# Workflow verification matrix

This document distinguishes **implemented behavior**, **automated test coverage**, and **observed passing results**. Tests being committed does not mean that they passed.

## Test suites

- **Unit/contract tests:** `tests/BusinessWorkflowEngine.Tests` — xUnit, workflow graph validation, schema boundaries, example contracts, and credential encryption.
- **Business acceptance tests:** `tests/business/test_workflows.py` — black-box HTTP requests to the actual ASP.NET Core application using PostgreSQL, including role checks, process routing, idempotency, concurrency, and integration failures.
- **Process-restart scenario:** `prepare-restart` / `verify-restart` — a pending human task is persisted, the API process is restarted, and the task is completed.
- **HTTPS integration fixture:** `tests/business/mock_https.py` — deterministic success, transient failure, permanent failure, and idempotency-header inspection.

The automated environment is defined in [Unit and business acceptance tests](../../.github/workflows/tests.yml). GitHub Actions runs it on push, pull request, or manual dispatch. See [Actions](https://github.com/mehrdadbahrainy/business-workflow-engine/actions/workflows/tests.yml) for actual run conclusions and logs.

## Traceability

| Business or technical promise | Automated evidence | Important limitation |
| --- | --- | --- |
| Different workflows use one generic runtime | xUnit shipped-definition checks; business scenarios 01, 05, 06, 11 | Only sample node types and workflows are covered |
| Purchase routing and manager decisions | Business 02, 03, 04; boundary tests | No external purchasing system or policy validation |
| Leave approval and rejection | Business 05, 06, 20 | Example approval rules only |
| Input, completion, and graph validation | xUnit workflow contract and boundary tests; business 03, 07 | Full adversarial schema fuzzing not performed |
| Role-based task access and instance visibility | Business 03, 09, 21 | Development identity only; real IdP/JWT integration untested |
| Idempotent starts and actor isolation | Business 08, 16, 18, 19 | Cross-process stress and database fault injection untested |
| Single task completion under concurrent requests | Business 17 | Does not establish correctness at production concurrency levels |
| Published revision immutability | Business 10 | Revision upgrade and long-running migration coverage remains limited |
| HTTPS integration and bounded retries | Business 12–15 | Remote receiver's idempotency behavior is not proven |
| Durable pending human work across restart | Explicit prepare/verify restart sequence in CI | Does not test crash mid-transaction or restart during an HTTP action |
| Integration credential encryption | xUnit IntegrationCredentialTests | Not a security audit or penetration test |

## Reproduce in an isolated environment

The CI job provides PostgreSQL 17, .NET 10, a local HTTPS fixture, and the Development-only identity handler. **Do not use the Development identity mode on an Internet-facing deployment.**

```bash
dotnet restore tests/BusinessWorkflowEngine.Tests/BusinessWorkflowEngine.Tests.csproj
dotnet test tests/BusinessWorkflowEngine.Tests/BusinessWorkflowEngine.Tests.csproj -c Release
```

For the black-box tests, use the [CI workflow](../../.github/workflows/tests.yml) as the authoritative environment recipe; it starts the real API and local mock HTTPS server, runs `python3 -m unittest discover -s tests/business -p 'test_*.py' -v`, then restarts the API and verifies a pending task.

## Acceptance standard

Do not describe the engine as "tested and working" solely because this matrix exists. A defensible claim requires a successful, identifiable CI run on the target commit, with passing unit tests, all business scenarios, and the restart sequence. Production-readiness claims additionally require load tests, failure injection, security review, and operational evidence.

**Verification note (2026-11-09):** Test sources and the workflow were inspected and extended. The execution environment available to this review did not include the .NET SDK or a PostgreSQL service. No passing .NET or PostgreSQL test result was observed in this review; check the GitHub Actions run for the commit before claiming success.
