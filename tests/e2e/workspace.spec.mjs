import { test, expect } from '@playwright/test';

function unique(prefix) {
  return prefix + '-' + Math.random().toString(36).slice(2, 10);
}
async function enterDemo(page, identity = 'requester@example.test') {
  await expect(page.getByRole('heading', { name: 'Explore the development workspace' })).toBeVisible();
  await page.getByLabel('Test identity').selectOption(identity);
  await page.getByRole('button', { name: /Enter demo workspace/ }).click();
  await expect(page.getByRole('heading', { name: 'Workflows', exact: true })).toBeVisible();
}
async function signOut(page) {
  await page.locator('.identity summary').click();
  await page.getByRole('button', { name: /Sign out/ }).click();
  await expect(page.getByRole('heading', { name: 'Explore the development workspace' })).toBeVisible();
}
async function startWorkflow(page, name, input) {
  const card = page.locator('.workflow-card').filter({ hasText: name });
  await expect(card).toBeVisible();
  await card.getByRole('button', { name: 'Start workflow' }).click();
  await page.locator('textarea[name="workflow-input"]').fill(JSON.stringify(input, null, 2));
  await page.locator('.start-panel').getByRole('button', { name: 'Start workflow' }).click();
  await expect(page.locator('.alerts .alert.success')).toContainText('Workflow started:');
  const message = await page.locator('.alerts .alert.success').textContent();
  const id = message?.match(/Workflow started:\s*([0-9a-f-]{36})/i)?.[1];
  expect(id).toBeTruthy();
  return id;
}
async function awaitOutcome(page, id, outcome) {
  await expect.poll(async () => {
    await page.locator('.page-heading').getByRole('button', { name: /Refresh/ }).click();
    const row = page.locator('.instance-row').filter({ hasText: id });
    await row.click();
    return (await page.locator('.detail-summary strong').textContent())?.trim();
  }, { timeout: 30_000, intervals: [500, 800, 1200, 2000] }).toBe(outcome);
}

test('new visitor signs in to a real development workspace and signs out', async ({ page }) => {
  const requestsBeforeLogin = [];
  page.on('request', req => {
    if (req.url().includes('/api/v1/workflow-definitions')) requestsBeforeLogin.push(req.url());
  });
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Explore the development workspace' })).toBeVisible();
  expect(requestsBeforeLogin).toHaveLength(0);
  await enterDemo(page);
  await expect(page.locator('.health-bar')).toContainText('API and database are available');
  await expect(page.locator('.workflow-card').filter({ hasText: 'purchase-approval' })).toBeVisible();
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Workflows', exact: true })).toBeVisible();
  await signOut(page);
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Explore the development workspace' })).toBeVisible();
});

test('author publishes and runs a workflow from the browser', async ({ page }) => {
  await page.goto('/');
  await enterDemo(page);
  const slug = unique('browser-e2e');
  const title = 'Browser test ' + slug;
  await page.getByRole('button', { name: /New workflow/ }).click();
  await page.getByLabel('Workflow name').fill(slug);
  await page.getByLabel('Display title').fill(title);
  await page.getByRole('button', { name: 'Validate and publish' }).click();
  const card = page.locator('.workflow-card').filter({ hasText: slug });
  await expect(card).toContainText(title);
  await expect(card).toContainText('published');
  const instance = await startWorkflow(page, slug, {});
  await awaitOutcome(page, instance, 'completed');
  await expect(page.locator('.detail-panel')).toContainText('workflow completed');
});

test('role-assigned work is completed by team lead and shown to requester', async ({ page }) => {
  await page.goto('/');
  await enterDemo(page);
  const employeeId = unique('emp');
  const employee = 'Automation ' + employeeId;
  const instance = await startWorkflow(page, 'employee-leave', {
    employeeId, employeeName: employee, days: 3, reason: 'E2E verification'
  });
  await page.getByRole('button', { name: 'My work' }).click();
  await expect(page.locator('.work-card').filter({ hasText: employee })).toHaveCount(0);
  await signOut(page);
  await enterDemo(page, 'teamlead@example.test');
  await page.getByRole('button', { name: 'My work' }).click();
  await expect.poll(async () => {
    await page.getByRole('button', { name: 'My work' }).click();
    return page.locator('.work-card').filter({ hasText: employee }).count();
  }, { timeout: 30_000, intervals: [500, 800, 1200, 2000] }).toBe(1);
  const task = page.locator('.work-card').filter({ hasText: employee });
  await expect(task).toContainText('Review short leave request');
  await task.locator('select').selectOption('approved');
  await task.locator('input[type="text"]').fill('Team lead E2E');
  await task.getByRole('button', { name: 'Complete task' }).click();
  await expect(page.locator('.alerts .alert.success')).toContainText('completed');

  await signOut(page);
  await enterDemo(page);
  await page.getByRole('button', { name: 'Instances' }).click();
  await awaitOutcome(page, instance, 'approved');
  await expect(page.locator('.detail-panel')).toContainText('Team lead E2E');
});

test('connection is configured and probed using loopback HTTPS service', async ({ page }) => {
  await page.goto('/');
  await enterDemo(page);
  await page.getByRole('button', { name: 'Connections' }).click();
  const key = unique('e2e-service');
  await page.getByLabel('Connection key').fill(key);
  await page.getByLabel('Display name').fill('E2E HTTPS fixture');
  await page.getByLabel('HTTPS base URL').fill('https://localhost/');
  await page.getByLabel('Credential header').fill('');
  await page.getByRole('button', { name: 'Save connection' }).click();
  await expect(page.locator('.alerts .alert.success')).toContainText('Connection saved');
  const row = page.locator('.integration-row').filter({ hasText: key });
  await expect(row).toBeVisible();
  await row.getByRole('button', { name: 'Test reachability' }).click();
  await expect(row.locator('.connection-result')).toContainText('HTTP 200');
  await expect(row.locator('.connection-result')).toContainText('The server responded');
});

test('mobile viewport preserves navigation and does not overflow', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/');
  await enterDemo(page);
  await page.getByRole('button', { name: 'Connections' }).click();
  await expect(page.getByRole('heading', { name: 'Connections', exact: true })).toBeVisible();
  const widths = await page.evaluate(() => ({
    document: document.documentElement.scrollWidth,
    viewport: window.innerWidth
  }));
  expect(widths.document).toBeLessThanOrEqual(widths.viewport + 4);
  await expect(page.getByRole('button', { name: /New connection/ })).toBeVisible();
});
