# Local Docker quickstart with real Keycloak login

This is the default **clone → start → sign in** path for evaluating Flowforge on one
trusted development computer. It uses real OIDC Authorization Code + PKCE and JWT
validation, not the Development identity-header shortcut.

## 1. Prerequisites

- Git
- Docker Desktop (Windows/macOS) or Docker Engine + Docker Compose v2 (Linux)
- Docker must be running; allow approximately 4 GB RAM for the initial builds.

You do **not** need .NET, Node, PostgreSQL or Keycloak installed separately.

## 2. First start

```bash
git clone https://github.com/mehrdadbahrainy/business-workflow-engine.git
cd business-workflow-engine
docker compose -f compose.local.yaml up -d --build --wait --wait-timeout 300
```

Docker downloads the images and builds Angular and .NET. Keycloak imports
`deploy/keycloak/flowforge-realm.json` **once** into its local data volume. It creates
the public PKCE client, API audience mapper, role mapper and three demo users.

| URL | Service | Notes |
| --- | --- | --- |
| http://localhost:8080 | Flowforge | Open this first; use **localhost**, not 127.0.0.1 |
| http://localhost:8081 | Keycloak | Realm sign-in and self-registration |
| http://localhost:8081/admin/ | Keycloak admin | User and role management |

No application/Keycloak database ports are published. All externally published web and
Keycloak ports are limited to **127.0.0.1**. Do not use this stack for public hosting.

## 3. Sign in

Open **http://localhost:8080**, click **Sign in with your organization**.
The browser redirects to Keycloak. Select one of the seeded accounts:

| Username | Password | Roles |
| --- | --- | --- |
| `flowadmin` | `DevOnly123!` | `workflow-admin`, `manager` |
| `requester` | `DevOnly123!` | Authenticated requester without admin rights |
| `teamlead` | `DevOnly123!` | `team-lead` |

Flowforge redirects back to `http://localhost:8080/auth/callback`, redeems the one-time
code using PKCE and verifies the JWT audience and subject on the API. Only the local
development stack uses HTTP for OIDC. In production use HTTPS and a secure IdP.

**Demo accounts are public, intentionally weak and never suitable for real data.**

## 4. Create a new user

### Self-registration

On the Keycloak sign-in screen click **Register**. Complete the registration form and
sign in. Self-registered users can read and start published workflows, but they are not
automatically authors or approvers. If registration is not visible, verify that the
`flowforge` realm has **Realm settings → Login → User registration** enabled.

### Admin-managed accounts and roles

1. Open **http://localhost:8081/admin/**.
2. Sign into the **master** administration realm with `local-admin` and
   `local-admin-change-me`.
3. Change the selected realm to **flowforge** (top-left realm menu).
4. Open **Users → Add user**, enter a username/email and create.
5. On that user, open **Credentials → Set password**, turn **Temporary** off
   for a local demo (for real deployments use a change-password policy).
6. Open **Role mapping → Assign role** and select realm role(s):
   - `workflow-admin`: edit/publish workflow definitions and configure connections
   - `manager`: purchase approvals
   - `team-lead`: short leave approvals
   - `department-manager`: extended leave approvals
   - `hr`: human resources work
7. Sign out of Flowforge and sign in as the new user. Role changes only take
   effect when a new access token is issued.

The Keycloak **bootstrap admin** account and the **Flowforge workflow-admin** role
are separate concepts: one manages identity, the other manages workflows.

## 5. Manage the stack

```bash
docker compose -f compose.local.yaml ps
docker compose -f compose.local.yaml logs --tail=100 api keycloak web
docker compose -f compose.local.yaml down
```

The named volumes persist users and workflows across stop/start/recreate. To **delete all
local users, workflows and data** and return to the original sample realm:

```bash
docker compose -f compose.local.yaml down -v
docker compose -f compose.local.yaml up -d --build --wait --wait-timeout 300
```

**Warning:** `down -v` permanently deletes only this quickstart's named volumes.
Never run it against a stack with data you care about.

## 6. Troubleshooting

- **"Authentication setup required"**: you probably ran
  `compose.production.yaml` with placeholder `.env.example`. Stop that stack,
  then run `compose.local.yaml`. Never expect `.env.example` to create an IdP.
- **Port 8080 or 8081 already in use**: stop the older stack first. With previous
  `compose.production.yaml`: `docker compose -f compose.production.yaml down`
  (when using `.env.example`, include `--env-file .env.example`). This does not
  delete the original production volume. Prefer not to change the quickstart
  localhost ports, because OIDC issuer/redirects must match exactly.
- **Sign-in redirects but fails**: use `http://localhost:8080` exactly, clear
  old tab session storage, verify Keycloak health and read
  `docker compose -f compose.local.yaml logs keycloak api`.
- **Not authorized to create a workflow**: use `flowadmin` or assign
  `workflow-admin` in Keycloak → Users → Role mapping, then sign in again.
- **Keycloak user/role import changes did not apply**: realm import occurs on
  the first initialization of its volume. Make changes in the admin console
  instead of editing the JSON and expecting existing users to be overwritten.
- **Connections to external HTTP APIs fail**: local demo has an intentionally
  empty `Integrations__AllowedHosts`; an operator must explicitly allowlist
  specific HTTPS hosts before using them. Live credentials are not preconfigured.
- **API/database errors**: `docker compose -f compose.local.yaml ps` and
  `docker compose -f compose.local.yaml logs --tail=200 api postgres`.

## Architecture: why the Keycloak hostname matters

The browser sees issuer `http://localhost:8081/realms/flowforge`; the API runs
in Docker, where `localhost` would refer to **the API container**. The local
stack therefore uses internal discovery
`http://keycloak:8080/realms/flowforge/.well-known/openid-configuration`.
Keycloak's dynamic backchannel URLs make JWKS retrieval possible internally
while keeping the public JWT issuer stable. The API permits HTTP metadata
**only** in the explicit `Local` environment. The production stack retains
HTTPS-only metadata validation. The API also validates the expected
`business-workflow-engine` audience and requires a dedicated `role` claim.

## Limits of this quickstart

It is for local evaluation only. No production TLS, reliable IdP storage,
backups, MFA, password recovery or distributed deployment are configured.
Production remains a separate, deliberate setup in
[authentication-setup.md](authentication-setup.md).