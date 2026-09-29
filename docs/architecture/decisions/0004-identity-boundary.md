# ADR 0004: Use Host-Issued Bearer Identity for API Actions

- **Status:** Accepted for the MVP
- **Date:** 2026-09-30

## Context

The engine must record who requested work and must reject approval decisions from an actor other than the task's assignee. The project does not need to own an organization's user directory or password lifecycle.

## Decision

In non-development environments, validate JWT bearer tokens issued by the host organization's identity provider. Use the validated `sub` claim as the actor identifier for requests and approval decisions. An approval task is actionable only when its configured assignee subject matches the authenticated actor.

For local development only, provide a clearly marked demo identity mechanism so the sample requester and manager can exercise the workflow without an external identity provider. The development mechanism is disabled outside the Development environment.

## Options considered

1. **Validate host-issued JWT bearer tokens:** the host keeps user lifecycle; the engine enforces task-to-subject matching.
2. **Own user accounts and credentials:** add local registration, password reset, account lifecycle, and administration.
3. **Trust caller-supplied actor IDs:** accept an identity in request data without authenticating it.

## Why this option

The product is intended to integrate into existing business applications. Trusting a validated host token avoids creating another user directory while preserving a verifiable actor for audit and task assignment. Caller-supplied actor IDs alone would allow impersonation and cannot satisfy the approval invariant.

## Trade-offs

- Self-hosters must configure a compatible token issuer, audience, and signing keys in non-development environments.
- The MVP does not provide a login screen, identity-provider setup wizard, user provisioning, role administration, or enterprise SSO integration.
- Host-issued subject values become part of task assignments and must remain stable for the lifetime of an instance.
- The development demo identity is intentionally unsuitable for production.

## Revisit when

Validated users require a built-in identity store, delegated administration, multiple organizations, or a provider-specific integration. Do not add those capabilities to avoid configuring a standard issuer.
