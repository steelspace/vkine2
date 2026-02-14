import { test, expect } from '@playwright/test';

test('theme toggle persists across reload', async ({ page }) => {
  await page.goto('/');

  // initial applied theme (system default or persisted)
  const initialMode = await page.evaluate(() => document.documentElement.dataset.vkineThemeMode || 'system');

  // toggle using client-side API (reliable for tests)
  await page.evaluate(() => window.vkineTheme.toggle());
  // wait for persisted mode in localStorage to update
  await page.waitForFunction((initial) => (localStorage.getItem('vkine-theme') || 'system') !== initial, initialMode, { timeout: 2000 });
  const newMode = await page.evaluate(() => localStorage.getItem('vkine-theme') || 'system');
  expect(newMode).toBeTruthy();
  expect(newMode).not.toBe(initialMode);

  // reload and ensure mode persisted
  await page.reload();
  const persistedMode = await page.evaluate(() => document.documentElement.dataset.vkineThemeMode || 'system');
  expect(persistedMode).toBe(newMode);

  // applied theme should match attribute as well
  const applied = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
  expect(applied).toBeTruthy();
});

test('default follows system preference when no user choice', async ({ browser }) => {
  // dark preference
  const darkCtx = await browser.newContext({ colorScheme: 'dark' });
  const darkPage = await darkCtx.newPage();
  await darkPage.goto('/');
  const darkTheme = await darkPage.evaluate(() => document.documentElement.getAttribute('data-theme'));
  expect(darkTheme).toBe('dark');
  await darkCtx.close();

  // light preference
  const lightCtx = await browser.newContext({ colorScheme: 'light' });
  const lightPage = await lightCtx.newPage();
  await lightPage.goto('/');
  const lightTheme = await lightPage.evaluate(() => document.documentElement.getAttribute('data-theme'));
  expect(lightTheme).toBe('light');
  await lightCtx.close();
});
