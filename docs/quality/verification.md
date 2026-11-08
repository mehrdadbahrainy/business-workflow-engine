# Verification Report — Business Workflow Engine

**Date:** 2026-11-08
**Status:** Automated tests passed. This is not a production-readiness certification.
**Tested revision:** db49272525075da222d77648b770cbf68122cff0
**Evidence:** [Successful GitHub Actions run](https://github.com/mehrdadbahrainy/business-workflow-engine/actions/runs/42513663051)

## Environment and method

Tests run on a GitHub-hosted Ubuntu runner with .NET 10, real PostgreSQL 17, and EF Core migrations at API startup. xUnit verifies business definition and schema behavior. Python standard-library tests exercise the running ASP.NET Core API as an external client. Development identities are used for role scenarios. A disposable loopback-only HTTPS server with a locally trusted test certificate exercises external actions without sending traffic to real customer systems. The API process is stopped and restarted during a separate recovery scenario while the database remains available.

## Verified behavior

| Test group | Coverage | Observed result |
| --- | --- | --- |
| Unit tests (10) | Both bundled definitions; distinct generic nodes; invalid roles, cycles, HTTP target, schemas, input fields, unsupported decision | 10 passed |
| Purchase approval | Under-threshold automatic outcome; manager approval and rejection; output and event timeline | Passed |
| Leave workflow | Short request to team lead; extended request to department manager followed by HR | Passed |
| Authentication and role boundary | Anonymous start rejected; unauthorized completion rejected; invalid and repeated completion rejected | Passed with Development identities |
| Request idempotency | Repeated start returns same ID; different payload with same key gets conflict | Passed sequentially |
| Versioned definitions | Published definition cannot be overwritten | Passed |
| Generic engine | New third workflow defined via API, published, and executed through two branches without code changes | Passed |
| HTTPS action | Real request to local HTTPS fixture; mapped response; stable idempotency key received | Passed |
| HTTP retry | First request returns 503, second succeeds; retry recorded | Passed |
| HTTP exhaustion | Repeated 503 responses cause two retries, then a failed workflow | Passed |
| Network destination policy | Connection to hostname not on allowlist is rejected | Passed |
| Restart recovery | Pending work item survives stopping/restarting API and is completed afterward | Passed |

**Measured suite result:** 10 xUnit tests + 15 API acceptance tests = **25 passing tests**, plus an additional successful restart check. No failures in the referenced completed CI run.

## Test sources and repeatability

- [xUnit tests](../../tests/BusinessWorkflowEngine.Tests/WorkflowContractTests.cs)
- [Business acceptance tests](../../tests/business/test_workflows.py)
- [Local HTTPS fixture](../../tests/business/mock_https.py)
- [CI environment and commands](../../.github/workflows/tests.yml)

Run the unit suite with the .NET 10 SDK using: dotnet test tests/BusinessWorkflowEngine.Tests/BusinessWorkflowEngine.Tests.csproj -c Release.

For API scenarios, the reference implementation is the CI workflow. Use only a disposable Development environment: the tests create workflow definitions, instances, and integration connections. The stand-in HTTPS service intentionally uses an ephemeral local certificate. Do not run these scenarios against real production data.

## Important limits

Passing tests demonstrate the behaviors exercised by this suite. They do **not** prove full reliability or production readiness. Unverified areas include:

1. Production JWT/OIDC identity-provider integration and organization/tenant boundaries. Test identities are Development-only.
2. Concurrent duplicate starts under contention and multiple worker instances claiming work simultaneously.
3. Database outages, interrupted transactions, point-in-time recovery, upgrade migrations, backup and restore, and rolling deployments.
4. Real external idempotency. The local fixture captures the key but does not prove another business system deduplicates side effects.
5. A crash after the external service acts but before the workflow records the acknowledgement.
6. Load, latency, throughput, memory pressure, and long-running workflows at scale.
7. Browser-level UI automation, real user acceptance, threat modeling, and security penetration testing.

## Suggested next verification gates

Add integration tests against a test OIDC provider, concurrent worker/idempotency stress tests, transactional and HTTP crash injection, a downstream receiver with durable deduplication, release migration/restore rehearsals, measured load tests, and end-to-end UI automation. Record observations and links to real CI runs before claiming those gates passed.

See the [architecture case study](../architecture/case-study.md) for the design reasoning behind these boundaries.
