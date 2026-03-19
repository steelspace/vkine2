/**
 * Debug test: diagnose the SignalR disconnection caused by vkineMovie.saveMoviesCache /
 * loadMoviesCache JS interop calls introduced for the back-navigation cache feature.
 *
 * Focus: measure JSON payload sizes and capture any circuit errors.
 */

import { test, expect, Page } from '@playwright/test';

const MOBILE_VIEWPORT = { width: 390, height: 844 }; // iPhone 13

async function waitForCards(page: Page) {
  await page.waitForLoadState('networkidle');
  await page.waitForSelector('[data-testid="movie-card"]', { timeout: 15_000 });
  await page.waitForTimeout(1000);
}

test.describe('Cache JS interop debug', () => {
  test('measure saveMoviesCache payload size and detect circuit errors', async ({ browser }) => {
    // Use mobile viewport so OpenModal takes the navigation path (window.innerWidth <= 768)
    const context = await browser.newContext({ viewport: MOBILE_VIEWPORT });
    const page = await context.newPage();

    const errors: string[] = [];
    const consoleMessages: string[] = [];

    page.on('console', msg => {
      const text = msg.text();
      consoleMessages.push(`[${msg.type()}] ${text}`);
      if (msg.type() === 'error') errors.push(text);
    });
    page.on('pageerror', err => errors.push(`PAGE ERROR: ${err.message}`));

    // Intercept WebSocket to detect SignalR disconnects
    const wsCloseEvents: string[] = [];
    page.on('websocket', ws => {
      ws.on('close', () => wsCloseEvents.push(`WS closed at ${new Date().toISOString()}`));
      ws.on('socketerror', e => wsCloseEvents.push(`WS error: ${e}`));
    });

    // Patch vkineMovie.saveMoviesCache BEFORE navigation to measure payload size
    await page.goto('/movies');
    await waitForCards(page);

    const patchResult = await page.evaluate(() => {
      const orig = (window as any).vkineMovie?.saveMoviesCache;
      if (!orig) return 'vkineMovie.saveMoviesCache NOT FOUND';
      (window as any).__cacheSaveLog = [];
      (window as any).vkineMovie.saveMoviesCache = function(json: string) {
        (window as any).__cacheSaveLog.push({
          byteLength: new Blob([json]).size,
          itemCount: (() => { try { return JSON.parse(json).length; } catch { return -1; } })(),
          preview: json.slice(0, 200),
        });
        return orig.call(this, json);
      };
      return 'patched';
    });
    console.log(`\n  saveMoviesCache patch: ${patchResult}`);

    // Count loaded movie cards
    const cardCount = await page.locator('[data-testid="movie-card"]').count();
    console.log(`  Movie cards visible on page: ${cardCount}`);

    // Report _loadedMovies count via checking the cache key before click
    const preClickCache = await page.evaluate(() => {
      const v = sessionStorage.getItem('vkine-movies-cache');
      return v ? `exists (${new Blob([v]).size} bytes)` : 'empty';
    });
    console.log(`  sessionStorage cache before click: ${preClickCache}`);

    // Click first movie card — on mobile this triggers saveMoviesCache then navigates
    const firstCard = page.locator('[data-testid="movie-card"]').first();
    await firstCard.click();

    // Wait a moment for the cache save + navigation to happen
    await page.waitForTimeout(2000);

    // Read the save log (may have navigated away — read before that)
    const saveLog: any[] = await page.evaluate(() => (window as any).__cacheSaveLog ?? []).catch(() => []);
    console.log(`\n  saveMoviesCache calls: ${saveLog.length}`);
    for (const entry of saveLog) {
      console.log(`    items: ${entry.itemCount}, bytes: ${entry.byteLength}`);
      console.log(`    preview: ${entry.preview}`);
    }

    // Check if we navigated to a movie detail page
    const url = page.url();
    console.log(`\n  Current URL after click: ${url}`);

    // Check sessionStorage on the detail page
    const detailCache = await page.evaluate(() => {
      const v = sessionStorage.getItem('vkine-movies-cache');
      return v ? `exists (${new Blob([v]).size} bytes)` : 'not found';
    });
    console.log(`  sessionStorage cache on detail page: ${detailCache}`);

    // Check for any errors so far
    console.log(`\n  Console errors so far (${errors.length}):`);
    for (const e of errors) console.log(`    ${e}`);
    console.log(`  WS close events so far (${wsCloseEvents.length}):`);
    for (const e of wsCloseEvents) console.log(`    ${e}`);

    // Now navigate back and see what happens with loadMoviesCache
    const backResult = await page.evaluate(() => {
      const orig = (window as any).vkineMovie?.loadMoviesCache;
      if (!orig) return 'vkineMovie.loadMoviesCache NOT FOUND on detail page';
      (window as any).__cacheLoadLog = [];
      (window as any).vkineMovie.loadMoviesCache = function() {
        const result = orig.call(this);
        (window as any).__cacheLoadLog.push({
          returned: result ? `${new Blob([result]).size} bytes` : 'null',
        });
        return result;
      };
      return 'patched';
    });
    console.log(`\n  loadMoviesCache patch on detail page: ${backResult}`);

    await page.goBack();
    await page.waitForTimeout(3000);

    const loadLog: any[] = await page.evaluate(() => (window as any).__cacheLoadLog ?? []).catch(() => []);
    console.log(`\n  loadMoviesCache calls: ${loadLog.length}`);
    for (const entry of loadLog) {
      console.log(`    returned: ${entry.returned}`);
    }

    const afterBackUrl = page.url();
    console.log(`  URL after back navigation: ${afterBackUrl}`);

    // Final error report
    console.log(`\n  All console errors (${errors.length}):`);
    for (const e of errors) console.log(`    ${e}`);

    console.log(`\n  WS close events (${wsCloseEvents.length}):`);
    for (const e of wsCloseEvents) console.log(`    ${e}`);

    // Report all Blazor-related console messages
    const blazorMessages = consoleMessages.filter(m =>
      m.includes('Blazor') || m.includes('blazor') || m.includes('SignalR') ||
      m.includes('disconnected') || m.includes('circuit') || m.includes('Connection')
    );
    console.log(`\n  Blazor/SignalR messages (${blazorMessages.length}):`);
    for (const m of blazorMessages) console.log(`    ${m}`);

    await context.close();
  });

  test('check if vkineMovie is defined before OnAfterRenderAsync fires', async ({ browser }) => {
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();

    // Intercept the sessionStorage.getItem call that happens in !_sessionStorageChecked block
    // to check if vkineMovie is defined at that same moment
    await page.addInitScript(() => {
      const origGetItem = Storage.prototype.getItem;
      Storage.prototype.getItem = function(key: string) {
        if (key === 'vkine-movies-filters') {
          const defined = typeof (window as any).vkineMovie !== 'undefined';
          const hasSaveCache = typeof (window as any).vkineMovie?.saveMoviesCache === 'function';
          const hasLoadCache = typeof (window as any).vkineMovie?.loadMoviesCache === 'function';
          console.log(`[INTERCEPT] sessionStorage.getItem('vkine-movies-filters') called — vkineMovie defined: ${defined}, saveMoviesCache: ${hasSaveCache}, loadMoviesCache: ${hasLoadCache}`);
        }
        return origGetItem.call(this, key);
      };
    });

    const logs: string[] = [];
    page.on('console', msg => {
      if (msg.text().includes('[INTERCEPT]') || msg.type() === 'error') {
        logs.push(`[${msg.type()}] ${msg.text()}`);
      }
    });

    await page.goto('/movies');
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(3000);

    console.log('\n  Intercepted logs:');
    for (const l of logs) console.log(`    ${l}`);

    // Also check directly
    const state = await page.evaluate(() => ({
      vkineMovieDefined: typeof (window as any).vkineMovie !== 'undefined',
      saveMoviesCacheDefined: typeof (window as any).vkineMovie?.saveMoviesCache === 'function',
      loadMoviesCacheDefined: typeof (window as any).vkineMovie?.loadMoviesCache === 'function',
    }));
    console.log('\n  vkineMovie state after load:', JSON.stringify(state, null, 2));

    await context.close();
  });
});
