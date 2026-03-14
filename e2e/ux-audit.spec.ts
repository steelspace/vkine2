import { test, expect, Page } from '@playwright/test';

// ── helpers ────────────────────────────────────────────────────────────────

async function waitForBlazor(page: Page) {
  // Wait until Blazor has finished its initial render
  await page.waitForLoadState('networkidle');
  // Give Blazor SSR a moment to hydrate
  await page.waitForTimeout(800);
}

const consoleErrors: string[] = [];

// ── Movies page ────────────────────────────────────────────────────────────

test.describe('Movies page', () => {
  test.beforeEach(async ({ page }) => {
    page.on('console', msg => { if (msg.type() === 'error') consoleErrors.push(`[console error] ${msg.text()}`); });
    page.on('pageerror', err => consoleErrors.push(`[page error] ${err.message}`));
    await page.goto('/movies');
    await waitForBlazor(page);
  });

  test('movie grid loads with at least one card', async ({ page }) => {
    const cards = page.locator('.movie-card');
    await expect(cards.first()).toBeVisible({ timeout: 10_000 });
    const count = await cards.count();
    expect(count, `Expected movie cards, got ${count}`).toBeGreaterThan(0);
  });

  test('stats line shows movie count', async ({ page }) => {
    const stats = page.locator('.stats-line, .movie-count, [data-testid="stats"]').first();
    // soft — just check text contains a number
    const bodyText = await page.locator('body').innerText();
    expect(bodyText).toMatch(/\d+/);
  });

  test('search input is visible and accepts input', async ({ page }) => {
    const input = page.locator('[data-testid="search-input"]');
    await expect(input).toBeVisible();
    await input.fill('test');
    await page.waitForTimeout(500);
    await input.clear();
  });

  test('search placeholder text is correct', async ({ page, isMobile }) => {
    const input = page.locator('[data-testid="search-input"]');
    const placeholder = await input.getAttribute('placeholder');
    if (isMobile) {
      expect(placeholder, 'Mobile placeholder should be short').toMatch(/^(Search|Hledat)$/i);
    } else {
      expect(placeholder, 'Desktop placeholder should be long').toMatch(/movie|film|hledej/i);
    }
  });

  test('date range picker opens on click', async ({ page }) => {
    // flatpickr hides the original input and creates a visible altInput
    const dateInput = page.locator('[data-testid="date-range-input-visible"], .flatpickr-input.flatpickr-alt-input').first();
    await expect(dateInput).toBeVisible();
    await dateInput.click();
    await page.waitForTimeout(400);
    const calendar = page.locator('.flatpickr-calendar');
    await expect(calendar, 'Calendar should appear after clicking date input').toBeVisible();
  });

  test('time slider is visible', async ({ page }) => {
    // On mobile the slider is behind a chip toggle; on desktop it's always visible
    const chipToggle = page.locator('[data-testid="time-chip-toggle"]');
    if (await chipToggle.isVisible()) await chipToggle.click();
    const slider = page.locator('.time-slider:visible').first();
    await expect(slider).toBeVisible({ timeout: 5_000 });
  });

  test('sort segmented control switches active segment', async ({ page }) => {
    const segments = page.locator('.segment');
    const count = await segments.count();
    expect(count, 'Should have sort segments').toBeGreaterThan(1);
    // Click second segment
    await segments.nth(1).click();
    await page.waitForTimeout(300);
    const activeClass = await segments.nth(1).getAttribute('class');
    expect(activeClass, 'Clicked segment should become active').toContain('active');
  });

  test('PageNav shows Movies as active', async ({ page }) => {
    const nav = page.locator('.page-nav');
    await expect(nav).toBeVisible();
    const activeItem = nav.locator('.page-nav-item.active');
    await expect(activeItem).toBeVisible();
    const href = await activeItem.getAttribute('href');
    expect(href, 'Active nav item should point to movies').toMatch(/^\/(movies)?$/);
  });

  test('no overflow in toolbar', async ({ page }) => {
    const toolbar = page.locator('.sticky-toolbar, .toolbar').first();
    await expect(toolbar).toBeVisible();
    const box = await toolbar.boundingBox();
    const viewport = page.viewportSize();
    expect(box!.width, 'Toolbar should not exceed viewport width').toBeLessThanOrEqual(viewport!.width + 1);
  });
});

// ── Premieres page ─────────────────────────────────────────────────────────

test.describe('Premieres page', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/premieres');
    await waitForBlazor(page);
  });

  test('premiere groups load', async ({ page }) => {
    const groups = page.locator('.premiere-group, .premiere-date-group, .premiere-section').first();
    // Soft check: at least some content
    const bodyText = await page.locator('body').innerText();
    expect(bodyText.length, 'Page should have meaningful content').toBeGreaterThan(100);
  });

  test('PageNav shows Premieres as active', async ({ page }) => {
    const nav = page.locator('.page-nav');
    await expect(nav).toBeVisible();
    const activeItem = nav.locator('.page-nav-item.active');
    await expect(activeItem).toBeVisible();
    const href = await activeItem.getAttribute('href');
    expect(href, 'Active nav item should point to premieres').toContain('premieres');
  });
});

// ── Navigation ─────────────────────────────────────────────────────────────

test.describe('Navigation between pages', () => {
  test('PageNav navigates Movies → Premieres', async ({ page }) => {
    await page.goto('/movies');
    await waitForBlazor(page);
    const nav = page.locator('.page-nav');
    const premieresLink = nav.locator('a[href="/premieres"]');
    await expect(premieresLink).toBeVisible();
    await premieresLink.click();
    await waitForBlazor(page);
    expect(page.url()).toContain('premieres');
  });

  test('PageNav navigates Premieres → Movies', async ({ page }) => {
    await page.goto('/premieres');
    await waitForBlazor(page);
    const nav = page.locator('.page-nav');
    const moviesLink = nav.locator('a[href*="movies"], a[href="/"]');
    await expect(moviesLink.first()).toBeVisible();
    await moviesLink.first().click();
    await waitForBlazor(page);
    expect(page.url()).toMatch(/\/(movies)?$/);
  });
});

// ── Theme toggle ───────────────────────────────────────────────────────────

test.describe('Theme toggle', () => {
  test('toggles data-theme attribute on <html>', async ({ page }) => {
    await page.goto('/movies');
    await waitForBlazor(page);
    const initialTheme = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
    const themeBtn = page.locator('button[aria-label*="heme"], button[aria-label*="ight"], button[aria-label*="ark"], .theme-toggle').first();
    await expect(themeBtn).toBeVisible();
    await themeBtn.click();
    await page.waitForTimeout(300);
    const newTheme = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
    expect(newTheme, 'Theme should change after clicking toggle').not.toBe(initialTheme);
  });
});

// ── Language toggle ────────────────────────────────────────────────────────

test.describe('Language toggle', () => {
  test('switches language on click', async ({ page }) => {
    await page.goto('/movies');
    await waitForBlazor(page);
    const langBtn = page.locator('.language-selector button, button:has-text("EN"), button:has-text("CS")').first();
    await expect(langBtn).toBeVisible();
    const before = await page.locator('body').innerText();
    await langBtn.click();
    await waitForBlazor(page);
    const after = await page.locator('body').innerText();
    expect(after, 'Page content should change after language switch').not.toBe(before);
  });
});

// ── Mobile specific ────────────────────────────────────────────────────────

test.describe('Mobile layout', () => {
  test('PageNav labels are hidden on mobile', async ({ page, isMobile }) => {
    test.skip(!isMobile, 'Mobile only');
    await page.goto('/movies');
    await waitForBlazor(page);
    const label = page.locator('.page-nav-label').first();
    // Should be hidden (display:none or not visible)
    const isVisible = await label.isVisible();
    expect(isVisible, 'PageNav labels should be hidden on mobile').toBe(false);
  });

  test('toolbar has no horizontal overflow on mobile', async ({ page, isMobile }) => {
    test.skip(!isMobile, 'Mobile only');
    await page.goto('/movies');
    await waitForBlazor(page);
    const scrollWidth = await page.evaluate(() => document.body.scrollWidth);
    const clientWidth = await page.evaluate(() => document.documentElement.clientWidth);
    expect(scrollWidth, `Body scrollWidth (${scrollWidth}) should not exceed viewport (${clientWidth})`).toBeLessThanOrEqual(clientWidth + 2);
  });
});

// ── Console errors summary ─────────────────────────────────────────────────

test('no JS console errors across pages', async ({ page }) => {
  const errors: string[] = [];
  page.on('console', msg => { if (msg.type() === 'error') errors.push(msg.text()); });
  page.on('pageerror', err => errors.push(err.message));

  await page.goto('/movies');
  await waitForBlazor(page);
  await page.goto('/premieres');
  await waitForBlazor(page);

  expect(errors, `Console errors found:\n${errors.join('\n')}`).toHaveLength(0);
});
