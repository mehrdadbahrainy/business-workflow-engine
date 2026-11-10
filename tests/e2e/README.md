# Browser E2E

Real Chromium browser acceptance tests against Angular, ASP.NET Core and a disposable PostgreSQL
database. The HTTP connector uses only the loopback-only HTTPS fixture in tests/business/mock_https.py.

Covered: development demo sign-in/out, reload, UI workflow authoring/publish/start/completion, role routing
and completion, integration creation and non-mutating HEAD probe, mobile viewport.

Run in CI via .github/workflows/e2e.yml. Locally, start the API (Development) and Angular dev server,
provide isolated PostgreSQL, trust the fixture's test TLS certificate, install:
  npm install --prefix tests/e2e --no-package-lock
  ./tests/e2e/node_modules/.bin/playwright install chromium
Then: cd tests/e2e && npm test

Tests create real database records: **never point them at production**.

Not covered: production OIDC, identity-provider registration, MFA, real federation, token renewal,
or production deployment. These require a separate safe OIDC staging environment and are listed in
docs/operations/authentication-setup.md. Playwright CI stores failed traces/screenshots for review.
