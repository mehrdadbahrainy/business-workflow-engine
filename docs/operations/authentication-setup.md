# OIDC authentication setup and release gate

The browser implements OIDC Authorization Code + PKCE; the API validates bearer JWTs.
**Accounts are owned by your existing identity provider.** Flowforge does not offer a local
registration database, password reset, invitations, or first-admin bootstrap.

## Provision a public SPA client

1. Choose a trustworthy OIDC provider that supports public browser clients and Authorization Code + PKCE.
2. Register the **public** client ID (no client secret in the browser) and allow the exact HTTPS
   redirect URL `https://YOUR-FLOWFORGE-HOST/auth/callback`.
3. Configure allowed browser origins, discovery document access and cross-origin token calls for
   the SPA as required by the provider. Only use HTTPS for production origins.
4. Configure the API's issuer and **API audience**, and grant the SPA an access-token scope that
   actually produces a JWT intended for that API. Include this scope in `OIDC_SCOPE`.
5. Configure role claims issued by the provider. Ensure designated admin users possess the
   role value of `WORKFLOW_ADMIN_ROLE`; users performing tasks need the matching workflow roles.
6. Optionally set `OIDC_REGISTRATION_URL` to the provider's HTTPS self-registration page if that
   feature is enabled. Otherwise the sign-in page correctly directs users to the identity admin.
7. Provide a TLS reverse proxy. Avoid exposing a production workflow host only over HTTP.

## Environment

```dotenv
OIDC_AUTHORITY=https://YOUR-ISSUER/
OIDC_AUDIENCE=YOUR-API-AUDIENCE
OIDC_CLIENT_ID=YOUR-PUBLIC-SPA-CLIENT-ID
OIDC_SCOPE=openid profile email YOUR-API-SCOPE
OIDC_ROLE_CLAIM=role
WORKFLOW_ADMIN_ROLE=workflow-admin
# Optional; must be an HTTPS URL and actually enabled at your provider
OIDC_REGISTRATION_URL=
```

Public browser config (`/api/v1/auth/config`) includes issuer, client ID, requested scopes and
registration link. **Never put client secrets, passwords or signing keys in that response.**

The SPA stores an access token in the tab's session storage, verifies it via `/api/v1/auth/me`,
and clears it on explicit sign-out, expiry, or 401 errors. Sign-out currently ends only the
local app session; the provider SSO session may remain active. Automatic refresh tokens
and federated single logout are not implemented.

## Release gate: verify before switching production traffic

- [ ] Authentication config contains a real issuer/client ID, not `identity.example.com` placeholders.
- [ ] Fresh browser login redirects to the trusted provider and returns to `/auth/callback`.
- [ ] Callback rejects tampered state, missing code, and stale login attempts.
- [ ] Browser can fetch OIDC discovery and redeem the authorization code (CORS configured).
- [ ] API accepts a correctly issued JWT with the expected audience and rejects wrong audience,
      expired JWTs and anonymous access.
- [ ] `/api/v1/auth/me` returns the correct subject and **server-derived** role claims.
- [ ] A user without workflow-admin cannot create definitions or connections.
- [ ] An administrator can create a workflow and a connection without entering credentials in the UI.
- [ ] Sign-out clears the local session; expiry is handled without silently sending anonymous writes.
- [ ] A new user can register **at the identity provider** (when enabled) and obtain appropriate
      role membership and API access, or the product gives an accurate "contact admin" path.
- [ ] Relevant web UI pages and login are checked on desktop and mobile.

## Connection diagnosis

- Use the readiness indicator to differentiate an unreachable API from a missing database.
- The connection test sends **HEAD only** to the allowlisted HTTPS base URL on port 443, through
  the API server. It never executes a workflow, sends a POST, or guarantees downstream permissions.
- A 401/403 from the target means credentials or permissions need investigation; 405 means the
  target does not support HEAD. A 200 means the host responded, not that your business API works.
- The API owner must set `INTEGRATION_ALLOWED_HOSTS`. For encrypted credentials it must also
  set and back up a stable `INTEGRATION_ENCRYPTION_KEY` (base64, 32 bytes).
- Verify a representative external workflow action in an isolated environment with a safe
  test endpoint; do not probe production financial or destructive integrations.

## Evidence and limits

CI exercises the development-mode sign-in contract and a deterministic loopback HTTPS connection
probe. It **does not validate production OIDC account provisioning, provider CORS, roles or
end-user browser UX**. Treat this release gate as incomplete until tested in a target deployment.
