import { test } from '@playwright/test';

interface Violation { text: string; file: string; line: number; step: string; }

test('find all non-passive listener violations', async ({ page }) => {
  const violations: Violation[] = [];
  let step = 'init';

  page.on('console', msg => {
    const text = msg.text();
    if (text.includes('non-passive') || text.includes('passive') || text.includes('Violation') || text.includes('cancelable=false')) {
      const loc = msg.location();
      violations.push({ text, file: loc.url, line: loc.lineNumber, step });
    }
  });

  // ── /movies page load ──────────────────────────────────────────────────────
  step = 'page load /movies';
  await page.goto('/movies');
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(1000);

  // ── scroll the page ────────────────────────────────────────────────────────
  step = 'page scroll';
  await page.evaluate(() => window.scrollBy(0, 400));
  await page.waitForTimeout(300);
  await page.evaluate(() => window.scrollBy(0, -400));
  await page.waitForTimeout(300);

  // ── open date picker (flatpickr) ───────────────────────────────────────────
  step = 'flatpickr open';
  const dateInput = page.locator('[data-testid="date-range-input-visible"], .flatpickr-input.flatpickr-alt-input').first();
  if (await dateInput.isVisible()) {
    await dateInput.click();
    await page.waitForTimeout(500);
    await page.keyboard.press('Escape');
    await page.waitForTimeout(300);
  }

  // ── open movie modal ───────────────────────────────────────────────────────
  step = 'modal open';
  const card = page.locator('.movie-card').first();
  await card.waitFor({ timeout: 10_000 });
  await card.click();
  await page.waitForTimeout(1000);

  // ── scroll inside modal ────────────────────────────────────────────────────
  step = 'modal scroll';
  const modalContent = page.locator('[data-testid="movie-modal-content"]');
  if (await modalContent.isVisible()) {
    await modalContent.evaluate(el => el.scrollBy(0, 300));
    await page.waitForTimeout(300);
    await modalContent.evaluate(el => el.scrollBy(0, -300));
    await page.waitForTimeout(300);
  }

  // ── touch tap on modal ─────────────────────────────────────────────────────
  step = 'touch tap modal';
  if (await modalContent.isVisible()) {
    const box = await modalContent.boundingBox();
    if (box) {
      const hasTouchscreen = await page.evaluate(() => navigator.maxTouchPoints > 0);
      if (hasTouchscreen) {
        await page.touchscreen.tap(box.x + box.width / 2, box.y + 100);
      } else {
        await page.mouse.click(box.x + box.width / 2, box.y + 100);
      }
      await page.waitForTimeout(300);
    }
  }

  // ── close modal ────────────────────────────────────────────────────────────
  step = 'modal close';
  const closeBtn = page.locator('[data-testid="modal-close"]');
  if (await closeBtn.isVisible()) await closeBtn.click();
  await page.waitForTimeout(400);

  // ── /premieres page ────────────────────────────────────────────────────────
  step = '/premieres load';
  await page.goto('/premieres');
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(1000);

  step = '/premieres scroll';
  await page.evaluate(() => window.scrollBy(0, 400));
  await page.waitForTimeout(300);

  // ── report ─────────────────────────────────────────────────────────────────
  console.log('\n' + '═'.repeat(70));
  if (violations.length === 0) {
    console.log('✅ No violations found.');
  } else {
    console.log(`⚠️  ${violations.length} violation(s):\n`);
    const seen = new Set<string>();
    for (const v of violations) {
      const key = `${v.file}:${v.line}`;
      const shortFile = v.file
        .replace(/^https?:\/\/[^/]+/, '')
        .replace(/\?.*$/, '');
      const entry = `[${v.step}] ${shortFile}:${v.line}\n  → ${v.text}`;
      if (!seen.has(key)) {
        seen.add(key);
        console.log(entry);
        console.log();
      }
    }
    console.log(`(${violations.length} total, ${seen.size} unique sources)`);
  }
  console.log('═'.repeat(70));
});
