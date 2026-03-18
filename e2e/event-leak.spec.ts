/**
 * Diagnostic test: do calendar / language-selector stop responding after
 * repeated modal open→close cycles?
 *
 * Hypotheses being tested:
 *  A) setupPageSwipeToClose() adds document-level touch listeners every time
 *     the modal opens (new backdrop element → pageSwipeBound check passes again)
 *  B) flatpickrInstance gets attached to a detached/replaced DOM element after
 *     a Blazor re-render, so openDatePicker() opens an invisible calendar
 *  C) The sticky toolbar keeps pointer-events:none after a scroll, blocking
 *     calendar / language-selector clicks
 */

import { test, expect, Page, CDPSession } from '@playwright/test';

// ── helpers ──────────────────────────────────────────────────────────────────

async function waitForBlazor(page: Page) {
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(800);
}

/** Install a patch on the page that counts addEventListener calls per target. */
async function installListenerCounter(page: Page) {
  await page.evaluate(() => {
    (window as any).__listenerLog = [];
    const orig = EventTarget.prototype.addEventListener;
    EventTarget.prototype.addEventListener = function (type, listener, options) {
      const isDoc = this === document;
      const isWin = this === window;
      if (isDoc || isWin) {
        (window as any).__listenerLog.push({
          target: isDoc ? 'document' : 'window',
          type,
          time: Date.now(),
        });
      }
      return orig.call(this, type, listener, options);
    };
  });
}

async function getListenerLog(page: Page): Promise<{ target: string; type: string; time: number }[]> {
  return page.evaluate(() => (window as any).__listenerLog ?? []);
}

async function countListeners(page: Page, target: 'document' | 'window', type?: string) {
  const log = await getListenerLog(page);
  return log.filter(e => e.target === target && (!type || e.type === type)).length;
}

async function openAndCloseModal(page: Page) {
  const card = page.locator('.movie-card').first();
  await card.waitFor({ timeout: 10_000 });
  await card.click();
  await page.waitForTimeout(800);

  // Close via close button (if present)
  const closeBtn = page.locator('[data-testid="modal-close"]');
  if (await closeBtn.isVisible({ timeout: 1_000 }).catch(() => false)) {
    await closeBtn.click();
  } else {
    // Fallback: Escape
    await page.keyboard.press('Escape');
  }
  await page.waitForTimeout(600);
}

async function calendarOpens(page: Page): Promise<boolean> {
  // Close any open calendar first
  await page.keyboard.press('Escape');
  await page.waitForTimeout(200);

  const dateInput = page.locator('[data-testid="date-range-input-visible"]').first();
  if (!await dateInput.isVisible({ timeout: 2_000 }).catch(() => false)) return false;

  await dateInput.click();
  await page.waitForTimeout(500);
  const cal = page.locator('.flatpickr-calendar.open');
  const visible = await cal.isVisible({ timeout: 1_000 }).catch(() => false);

  // Close it again
  await page.keyboard.press('Escape');
  await page.waitForTimeout(300);
  return visible;
}

async function languageButtonPointerEvents(page: Page): Promise<string> {
  return page.evaluate(() => {
    const btn = document.querySelector<HTMLElement>('.lang-toggle');
    if (!btn) return 'not-found';
    return getComputedStyle(btn).pointerEvents;
  });
}

async function toolbarPointerEvents(page: Page): Promise<string> {
  return page.evaluate(() => {
    const tb = document.querySelector<HTMLElement>('.sticky-toolbar');
    if (!tb) return 'not-found';
    return getComputedStyle(tb).pointerEvents;
  });
}

// ── Test A: document touch-listener leak across modal cycles ─────────────────

test.describe('Hypothesis A – document touch-listener accumulation', () => {
  test('touchmove listener count on document should not grow with modal cycles', async ({ page }) => {
    await page.goto('/movies');
    await waitForBlazor(page);
    await installListenerCounter(page);

    const CYCLES = 4;
    const touchmoveCounts: number[] = [];

    for (let i = 0; i < CYCLES; i++) {
      const before = await countListeners(page, 'document', 'touchmove');
      await openAndCloseModal(page);
      const after = await countListeners(page, 'document', 'touchmove');
      touchmoveCounts.push(after - before);
      console.log(`  Cycle ${i + 1}: +${after - before} document 'touchmove' listeners added`);
    }

    const log = await getListenerLog(page);
    const docTouchListeners = log.filter(e => e.target === 'document' && ['touchstart','touchmove','touchend','touchcancel'].includes(e.type));
    console.log('\n  All document touch listeners added during test:');
    for (const e of docTouchListeners) console.log(`    [${e.target}] ${e.type}`);

    const totalAdded = touchmoveCounts.reduce((a, b) => a + b, 0);
    console.log(`\n  Total document touchmove listeners added across ${CYCLES} cycles: ${totalAdded}`);

    // If leaking, each cycle adds 1 more. 0 is ideal; 1 total acceptable (first open).
    expect(
      totalAdded,
      `document 'touchmove' listeners grew by ${totalAdded} across ${CYCLES} cycles — listener leak detected`
    ).toBeLessThanOrEqual(1);
  });
});

// ── Test B: flatpickr instance validity after modal cycles ───────────────────

test.describe('Hypothesis B – calendar stops working after modal cycles', () => {
  test('calendar opens correctly before and after each modal cycle', async ({ page, isMobile }) => {
    test.skip(isMobile, 'Calendar input only visible on desktop');

    await page.goto('/movies');
    await waitForBlazor(page);

    const CYCLES = 3;

    for (let i = 0; i < CYCLES; i++) {
      // Check before opening modal
      const before = await calendarOpens(page);
      console.log(`  Cycle ${i + 1} before modal: calendar opens = ${before}`);

      await openAndCloseModal(page);

      // Check after closing modal
      const after = await calendarOpens(page);
      console.log(`  Cycle ${i + 1} after  modal: calendar opens = ${after}`);

      expect(after, `Calendar stopped opening after modal cycle ${i + 1}`).toBe(true);
    }
  });

  test('diagnose why calendar stops opening after modal close', async ({ page, isMobile }) => {
    test.skip(isMobile, 'Calendar input only visible on desktop');

    await page.goto('/movies');
    await waitForBlazor(page);

    // Install a MutationObserver on body to track when .flatpickr-calendar is added/removed
    await page.evaluate(() => {
      (window as any).__fpCalendarLog = [];
      const obs = new MutationObserver((mutations) => {
        for (const m of mutations) {
          for (const node of Array.from(m.addedNodes)) {
            if (node instanceof HTMLElement && node.classList.contains('flatpickr-calendar')) {
              (window as any).__fpCalendarLog.push({ event: 'added', stack: new Error().stack });
            }
          }
          for (const node of Array.from(m.removedNodes)) {
            if (node instanceof HTMLElement && node.classList.contains('flatpickr-calendar')) {
              (window as any).__fpCalendarLog.push({ event: 'removed', stack: new Error().stack });
            }
          }
        }
      });
      obs.observe(document.body, { childList: true, subtree: false });
      (window as any).__fpCalObs = obs;
    });

    // Tag the altInput so we can check if it's the same element after re-render
    // Also patch flatpickr destroy to capture a stack trace
    await page.evaluate(() => {
      const alt = document.querySelector<HTMLElement>('[data-testid="date-range-input-visible"]');
      if (alt) alt.setAttribute('data-sentinel', 'before-modal');

      // Patch Node.removeChild on body to capture when flatpickr-calendar is removed
      (window as any).__fpDestroyStacks = [];
      const origRemoveChild = Node.prototype.removeChild;
      Node.prototype.removeChild = function (child: Node) {
        if (child instanceof HTMLElement && child.classList.contains('flatpickr-calendar')) {
          (window as any).__fpDestroyStacks.push(new Error('flatpickr-calendar removed').stack);
        }
        return origRemoveChild.call(this, child);
      };
    });

    // Open calendar to confirm flatpickr is working and track the calendar element
    const baseline = await calendarOpens(page);
    console.log(`  Baseline calendar opens: ${baseline}`);

    // Open and close one modal
    await openAndCloseModal(page);
    await page.waitForTimeout(300);

    // Check the mutation log
    const fpLog = await page.evaluate(() => (window as any).__fpCalendarLog ?? []);
    console.log(`\n  flatpickr-calendar DOM mutations (${fpLog.length} events):`);
    for (const entry of fpLog) {
      // Extract meaningful lines from the stack
      const lines = (entry.stack as string)?.split('\n').slice(1, 4).map((l: string) => l.trim()) ?? [];
      console.log(`    [${entry.event}] at:\n      ${lines.join('\n      ')}`);
    }

    // Check if altInput is the SAME element or was replaced
    const altState = await page.evaluate(() => {
      const same = document.querySelector<HTMLElement>('[data-sentinel="before-modal"]');
      const any = document.querySelector<HTMLElement>('[data-testid="date-range-input-visible"]');
      return {
        sameElementStillInDOM: !!same,
        anyAltInputInDOM: !!any,
        isReplacedElement: !!any && !same,  // new element without sentinel = was replaced
      };
    });
    console.log(`\n  altInput identity after modal cycle:`);
    console.log(`    same element still in DOM: ${altState.sameElementStillInDOM}`);
    console.log(`    any altInput in DOM: ${altState.anyAltInputInDOM}`);
    console.log(`    element was replaced by Blazor re-render: ${altState.isReplacedElement}`);

    // Print destroy stack traces
    const destroyStacks: string[] = await page.evaluate(() => (window as any).__fpDestroyStacks ?? []);
    console.log(`\n  flatpickr-calendar removeChild call stacks (${destroyStacks.length}):`);
    for (const s of destroyStacks) {
      const lines = s.split('\n').slice(0, 8).join('\n    ');
      console.log(`    ${lines}\n`);
    }

    // Final calendar state
    const calState = await page.evaluate(() => {
      const cal = document.querySelector<HTMLElement>('.flatpickr-calendar');
      if (!cal) return 'not in DOM';
      return `in DOM, classes: [${cal.className}], display: ${getComputedStyle(cal).display}`;
    });
    console.log(`\n  .flatpickr-calendar state: ${calState}`);

    // Confirm calendar is broken
    const afterModal = await calendarOpens(page);
    console.log(`  Calendar opens after modal cycle: ${afterModal}`);
  });
});

// ── Test C: toolbar pointer-events after scroll ──────────────────────────────

test.describe('Hypothesis C – toolbar pointer-events blocked after scroll', () => {
  test('toolbar is interactive when visible (no scroll)', async ({ page }) => {
    await page.goto('/movies');
    await waitForBlazor(page);

    const pe = await toolbarPointerEvents(page);
    console.log(`  Toolbar pointer-events at top: "${pe}"`);
    expect(pe, 'Toolbar should be interactive at top of page').not.toBe('none');
  });

  test('toolbar pointer-events and language-button state after scroll down then back up', async ({ page }) => {
    await page.goto('/movies');
    await waitForBlazor(page);

    // Scroll down to hide toolbar
    await page.evaluate(() => window.scrollBy(0, 400));
    await page.waitForTimeout(500);

    const peHidden = await toolbarPointerEvents(page);
    const langHidden = await languageButtonPointerEvents(page);
    console.log(`  After scroll down  — toolbar: "${peHidden}", lang-btn: "${langHidden}"`);

    // Scroll back to top
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.waitForTimeout(500);

    const peRestored = await toolbarPointerEvents(page);
    const langRestored = await languageButtonPointerEvents(page);
    console.log(`  After scroll back  — toolbar: "${peRestored}", lang-btn: "${langRestored}"`);

    expect(
      peRestored,
      'Toolbar should restore pointer-events after scrolling back to top'
    ).not.toBe('none');
    expect(
      langRestored,
      'Language button should be clickable after toolbar is shown again'
    ).not.toBe('none');
  });

  test('language button is actually clickable (click registers) after scroll-hide/show cycle', async ({ page }) => {
    await page.goto('/movies');
    await waitForBlazor(page);

    // Scroll down and back
    await page.evaluate(() => window.scrollBy(0, 400));
    await page.waitForTimeout(400);
    await page.evaluate(() => window.scrollTo(0, 0));
    await page.waitForTimeout(400);

    const langBtn = page.locator('.lang-toggle').first();
    await expect(langBtn).toBeVisible({ timeout: 3_000 });

    let clicked = false;
    try {
      await langBtn.click({ timeout: 2_000 });
      clicked = true;
    } catch {
      clicked = false;
    }
    console.log(`  Language button click registered: ${clicked}`);
    expect(clicked, 'Language button should accept clicks after scroll cycle').toBe(true);
  });
});

// ── Combined stress: all three in sequence ───────────────────────────────────

test.describe('Combined stress: scroll + modal cycles + control checks', () => {
  test('calendar and language-selector both work after mixed scroll+modal stress', async ({ page, isMobile }) => {
    test.skip(isMobile, 'Calendar input only visible on desktop');

    await page.goto('/movies');
    await waitForBlazor(page);
    await installListenerCounter(page);

    const results: string[] = [];

    for (let i = 0; i < 3; i++) {
      // Scroll down and back
      await page.evaluate(() => window.scrollBy(0, 300));
      await page.waitForTimeout(300);
      await page.evaluate(() => window.scrollTo(0, 0));
      await page.waitForTimeout(300);

      // Open/close modal
      await openAndCloseModal(page);

      const cal = await calendarOpens(page);
      const langPe = await languageButtonPointerEvents(page);
      const docTouchTotal = await countListeners(page, 'document', 'touchmove');

      results.push(
        `Iter ${i + 1}: calendar=${cal}, lang-pe="${langPe}", doc-touchmove-total=${docTouchTotal}`
      );
    }

    console.log('\n  Results:');
    results.forEach(r => console.log('   ', r));

    // All calendars should have opened
    const allCalendarsOpened = results.every(r => r.includes('calendar=true'));
    expect(allCalendarsOpened, `Calendar stopped working:\n${results.join('\n')}`).toBe(true);

    // Language button should never have pointer-events:none
    const langNeverBlocked = results.every(r => !r.includes('lang-pe="none"'));
    expect(langNeverBlocked, `Language button got blocked:\n${results.join('\n')}`).toBe(true);
  });
});
