import { test, expect } from '@playwright/test';

// Run with E2E_MODE=oidc against the completely isolated Compose quickstart.
// The ordinary Development-mode browser suite skips this group.
test.describe('@oidc local Keycloak quickstart', () => {
  test.skip(process.env.E2E_MODE !== 'oidc', 'Requires local Docker Keycloak stack');

  async function signIn(page, user) {
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Welcome to Flowforge' })).toBeVisible();
    await expect(page.getByText('New user? Select Register on the Keycloak login page.')).toBeVisible();
    await page.getByRole('button', { name: /Sign in with your organization/ }).click();
    await expect(page).toHaveURL(/localhost:8081\/realms\/flowforge\/protocol\/openid-connect\/auth/);
    await page.locator('#username').fill(user);
    await page.locator('#password').fill('DevOnly123!');
    await page.locator('#kc-login').click();
    await expect(page).toHaveURL(/^http:\/\/localhost:8080\/?$/);
    await expect(page.getByRole('heading', { name: 'Workflows', exact: true })).toBeVisible();
  }

  async function verifiedIdentity(page) {
    return await page.evaluate(async () => {
      const token = sessionStorage.getItem('flowforge-access-token');
      if (!token) throw new Error('No browser-issued access token');
      const response = await fetch('/api/v1/auth/me', {
        headers: { Authorization: 'Bearer ' + token }
      });
      return { status: response.status, body: await response.json() };
    });
  }

  test('seeded admin signs in via OIDC PKCE, has verified roles and can publish', async ({ page }) => {
    await signIn(page, 'flowadmin');
    const me = await verifiedIdentity(page);
    expect(me.status).toBe(200);
    expect(me.body.subject).toBeTruthy();
    expect(me.body.roles).toContain('workflow-admin');
    expect(me.body.roles).toContain('manager');

    const slug = 'oidc-e2e-' + Math.random().toString(36).slice(2, 10);
    await page.getByRole('button', { name: /New workflow/ }).click();
    await page.getByLabel('Workflow name').fill(slug);
    await page.getByLabel('Display title').fill('OIDC Browser ' + slug);
    await page.getByRole('button', { name: 'Validate and publish' }).click();
    const card = page.locator('.workflow-card').filter({ hasText: slug });
    await expect(card).toContainText('published');
  });

  test('requester signs in with verified token but cannot change definitions', async ({ page }) => {
    await signIn(page, 'requester');
    const me = await verifiedIdentity(page);
    expect(me.status).toBe(200);
    expect(me.body.roles).not.toContain('workflow-admin');
    await expect(page.locator('.workflow-card').filter({ hasText: 'purchase-approval' })).toBeVisible();
    const result = await page.evaluate(async () => {
      const token = sessionStorage.getItem('flowforge-access-token');
      const response = await fetch('/api/v1/workflow-definitions/forbidden-edit/1', {
        method: 'PUT',
        headers: { 'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json' },
        body: JSON.stringify({})
      });
      return response.status;
    });
    expect(result).toBe(403);
  });

  test('new visitor can self-register in Keycloak and start a session', async ({ page }) => {
    const username = 'newuser-' + Math.random().toString(36).slice(2, 10);
    const email = username + '@example.test';
    await page.goto('/');
    await page.getByRole('button', { name: /Sign in with your organization/ }).click();
    await page.getByRole('link', { name: /Register/i }).click();
    await expect(page).toHaveURL(/localhost:8081/);
    await page.locator('#firstName').fill('Demo');
    await page.locator('#lastName').fill('Registration');
    await page.locator('#email').fill(email);
    await page.locator('#username').fill(username);
    await page.locator('#password').fill('NewLocal123!');
    await page.locator('#password-confirm').fill('NewLocal123!');
    await page.locator('input[type="submit"], button[type="submit"]').first().click();
    await expect(page).toHaveURL(/^http:\/\/localhost:8080\/?$/);
    await expect(page.getByRole('heading', { name: 'Workflows', exact: true })).toBeVisible();
    const me = await verifiedIdentity(page);
    expect(me.status).toBe(200);
    expect(me.body.subject).toBeTruthy();
    expect(me.body.roles).not.toContain('workflow-admin');
  });

  test('Keycloak sign-in provides an account registration entry point', async ({ page }) => {
    await page.goto('/');
    await page.getByRole('button', { name: /Sign in with your organization/ }).click();
    await expect(page).toHaveURL(/localhost:8081\/realms\/flowforge\/protocol\/openid-connect\/auth/);
    await expect(page.getByRole('link', { name: /Register/i })).toBeVisible();
  });
});
