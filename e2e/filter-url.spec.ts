/**
 * Verify that clearing filters also clears filter params from the URL and
 * sessionStorage, so navigating back from a movie detail doesn't re-apply
 * stale filters.
 */
import { test, expect, Page } from '@playwright/test';

async function waitForBlazor(page: Page) {
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(800);
}

function urlFilterParams(url: string) {
  const p = new URL(url).searchParams;
  return { from: p.get('from'), to: p.get('to'), time: p.get('time') };
}

/** Click the date filter clear button. Desktop has a visible toolbar button; mobile uses the chip-clear span. */
async function clearDateFilter(page: Page) {
  const desktopBtn = page.locator('[data-testid="date-clear"]');
  if (await desktopBtn.isVisible()) {
    await desktopBtn.click();
  } else {
    // Mobile: chip-clear span is hidden by CSS until hover — dispatch a click event directly
    await page.locator('.filter-chip.active .chip-clear').first().dispatchEvent('click');
  }
  await page.waitForTimeout(600);
}

test('clearing date filter removes from/to from URL and sessionStorage', async ({ page }) => {
  await page.goto('/movies?from=2026-03-19&to=2026-03-21');
  await waitForBlazor(page);

  const urlBefore = urlFilterParams(page.url());
  console.log(`\n  After load: from=${urlBefore.from}, to=${urlBefore.to}`);

  await clearDateFilter(page);

  const urlAfter = urlFilterParams(page.url());
  const ssAfter = await page.evaluate(() => sessionStorage.getItem('vkine-movies-filters') ?? '');
  console.log(`  After clear: from=${urlAfter.from}, to=${urlAfter.to}, ss="${ssAfter}"`);

  expect(urlAfter.from, 'URL should not have "from" after clearing').toBeNull();
  expect(urlAfter.to,   'URL should not have "to" after clearing').toBeNull();
  expect(ssAfter.includes('from='), 'sessionStorage should not have "from"').toBe(false);
  expect(ssAfter.includes('to='),   'sessionStorage should not have "to"').toBe(false);
});

test('clearing date filter does not affect unrelated params (time stays)', async ({ page }) => {
  await page.goto('/movies?from=2026-03-19&to=2026-03-21&time=720');
  await waitForBlazor(page);

  console.log(`\n  URL on load: ${new URL(page.url()).search}`);

  await clearDateFilter(page);

  const params = new URL(page.url()).searchParams;
  console.log(`  URL after clearing date: ${new URL(page.url()).search}`);

  expect(params.get('from'), '"from" should be gone').toBeNull();
  expect(params.get('to'),   '"to" should be gone').toBeNull();
  expect(params.get('time'), '"time" should still be present').toBe('720');
});

test('full flow: set filter → open detail → back → clear → open detail → back → no filter', async ({ page, isMobile }) => {
  test.skip(!isMobile, 'Full-page navigation only on mobile');

  // 1. Load with date filter
  await page.goto('/movies?from=2026-03-19&to=2026-03-21');
  await waitForBlazor(page);
  console.log(`\n  Step 1 URL: ${new URL(page.url()).search}`);

  // 2. Open movie detail
  const card = page.locator('.movie-card').first();
  await card.waitFor({ timeout: 10_000 });
  await card.click();
  await waitForBlazor(page);
  console.log(`  Step 2 detail URL: ${new URL(page.url()).search}`);

  // 3. Navigate back
  await page.goBack();
  await waitForBlazor(page);
  console.log(`  Step 3 back URL: ${new URL(page.url()).search}`);

  // 4. Clear filters
  await clearDateFilter(page);
  const urlAfterClear = urlFilterParams(page.url());
  const ssAfterClear = await page.evaluate(() => sessionStorage.getItem('vkine-movies-filters') ?? '');
  console.log(`  Step 4 cleared: from=${urlAfterClear.from}, ss="${ssAfterClear}"`);

  // 5. Open detail again
  await card.waitFor({ timeout: 10_000 });
  await card.click();
  await waitForBlazor(page);
  console.log(`  Step 5 detail URL (2nd): ${new URL(page.url()).search}`);

  // 6. Navigate back
  await page.goBack();
  await waitForBlazor(page);
  const urlBack2 = urlFilterParams(page.url());
  const ssBack2 = await page.evaluate(() => sessionStorage.getItem('vkine-movies-filters') ?? '');
  console.log(`  Step 6 back URL (2nd): from=${urlBack2.from}, ss="${ssBack2}"`);

  expect(urlBack2.from, 'Filter should not be restored after clearing').toBeNull();
  expect(urlBack2.to,   'Filter should not be restored after clearing').toBeNull();
  expect(ssBack2.includes('from='), 'sessionStorage should be clean').toBe(false);
});
