import { test, expect } from '@playwright/test';

test.describe('Movies page (vkine)', () => {
  test('loads and shows movie cards', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveTitle(/Movies/);
    await expect(page.locator('[data-testid="movie-card"]').first()).toBeVisible();
  });

  test('search filters results by title', async ({ page }) => {
    await page.goto('/');
    const firstTitle = (await page.locator('[data-testid="movie-title"]').first().textContent())?.trim();
    expect(firstTitle).toBeTruthy();

    await page.fill('[data-testid="search-input"]', firstTitle!);
    // small debounce wait
    await page.waitForTimeout(300);

    const titles = await page.locator('[data-testid="movie-title"]').allTextContents();
    expect(titles.some(t => t.includes(firstTitle!))).toBeTruthy();
  });

  test('open movie modal shows details and (optional) schedules/tickets', async ({ page }) => {
    await page.goto('/');

    const firstCard = page.locator('[data-testid="movie-card"]').first();
    const clickedTitle = (await page.locator('[data-testid="movie-title"]').first().textContent())?.trim();

    await firstCard.click();
    const modal = page.locator('[data-testid="movie-modal"]');
    await expect(modal).toBeVisible();

    await expect(page.locator('[data-testid="modal-title"]')).toContainText(clickedTitle || '');

    // wait for schedules or "no schedules" to appear
    await page.waitForSelector('[data-testid="modal-schedules"], [data-testid="modal-no-schedules"], [data-testid="modal-loading"]', { timeout: 5000 });

    const showtimeCount = await page.locator('[data-testid="showtime"]').count();
    if (showtimeCount > 0) {
      await expect(page.locator('[data-testid="showtime"]').first()).toBeVisible();
    }

    // if ticket links present, assert they look valid
    const ticketCount = await page.locator('[data-testid="ticket-link"]').count();
    if (ticketCount > 0) {
      const href = await page.locator('[data-testid="ticket-link"]').first().getAttribute('href');
      expect(href).toMatch(/^https?:\/\//);
    }

    // close with Escape
    await page.keyboard.press('Escape');
    await expect(modal).toBeHidden();
  });

  test('sort controls toggle active state', async ({ page }) => {
    await page.goto('/');
    await page.click('[data-testid="sort-name"]');
    await expect(page.locator('[data-testid="sort-name"].active')).toHaveCount(1);
  });
});
