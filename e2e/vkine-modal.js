const { chromium } = require('playwright');
const path = require('path');

const SCREENSHOTS_DIR = '/tmp/vkine-screenshots';
const BASE_URL = 'https://vkine.duckdns.org';

async function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
    userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
  });
  const page = await context.newPage();

  await page.goto(BASE_URL + '/movies', { waitUntil: 'networkidle', timeout: 30000 });
  await sleep(1500);

  // Click on the first movie card
  const firstCard = await page.$('[data-testid="movie-card"]');
  await firstCard.click();
  await sleep(2000);

  // Screenshot the modal
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '02-movie-modal.png'), fullPage: false });

  // Get full modal HTML
  const modalHTML = await page.evaluate(() => {
    const modal = document.querySelector('.modal-backdrop');
    return modal ? modal.innerHTML.slice(0, 8000) : 'no modal found';
  });
  console.log('Modal HTML:', modalHTML);

  // Get modal styles
  const modalStyles = await page.evaluate(() => {
    const backdrop = document.querySelector('.modal-backdrop');
    const wrapper = document.querySelector('.modal-wrapper');
    const content = document.querySelector('[data-testid="movie-modal-content"]');

    function getStyles(el) {
      if (!el) return null;
      const cs = window.getComputedStyle(el);
      return {
        class: el.className.slice(0, 80),
        bg: cs.backgroundColor,
        color: cs.color,
        borderRadius: cs.borderRadius,
        padding: cs.padding,
        width: cs.width,
        maxWidth: cs.maxWidth,
        maxHeight: cs.maxHeight,
        overflow: cs.overflow,
        display: cs.display,
        position: cs.position,
        shadow: cs.boxShadow
      };
    }

    return {
      backdrop: getStyles(backdrop),
      wrapper: getStyles(wrapper),
      content: getStyles(content)
    };
  });
  console.log('Modal styles:', JSON.stringify(modalStyles, null, 2));

  // Scroll modal to see more content
  await page.evaluate(() => {
    const wrapper = document.querySelector('.modal-wrapper');
    if (wrapper) wrapper.scrollTop = 300;
  });
  await sleep(500);
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '02-movie-modal-scrolled.png'), fullPage: false });

  // Get modal content details
  const modalContent = await page.evaluate(() => {
    const modal = document.querySelector('[data-testid="movie-modal-content"]');
    if (!modal) return null;

    // Get all text content and structure
    const sections = [];
    for (const child of modal.children) {
      sections.push({
        tag: child.tagName,
        class: child.className.slice(0, 80),
        text: child.textContent.trim().slice(0, 200),
        html: child.outerHTML.slice(0, 500)
      });
    }
    return sections;
  });
  console.log('Modal content sections:', JSON.stringify(modalContent, null, 2));

  // Dark mode - toggle theme
  await page.keyboard.press('Escape');
  await sleep(500);

  const themeToggle = await page.$('.theme-toggle');
  if (themeToggle) {
    await themeToggle.click();
    await sleep(1000);
    await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '09-dark-mode.png'), fullPage: false });
    console.log('Dark mode screenshot saved');

    // Dark mode CSS vars
    const darkVars = await page.evaluate(() => {
      const root = document.documentElement;
      const cs = window.getComputedStyle(root);
      return {
        accent: cs.getPropertyValue('--accent').trim(),
        textPrimary: cs.getPropertyValue('--text-primary').trim(),
        bgPrimary: cs.getPropertyValue('--bg-primary').trim(),
        bgSecondary: cs.getPropertyValue('--bg-secondary').trim(),
        scheme: document.documentElement.dataset.theme || document.documentElement.className
      };
    });
    console.log('Dark mode vars:', JSON.stringify(darkVars, null, 2));

    // Click a card in dark mode
    const card = await page.$('[data-testid="movie-card"]');
    await card.click();
    await sleep(1500);
    await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '09-dark-mode-modal.png'), fullPage: false });
    await page.keyboard.press('Escape');
    await sleep(500);

    // Reset to light mode
    await themeToggle.click();
    await sleep(500);
  }

  // Lang toggle
  const langToggle = await page.$('.lang-toggle');
  if (langToggle) {
    const langText = await langToggle.textContent();
    console.log('Lang toggle text:', langText);
    await langToggle.click();
    await sleep(1000);
    await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '10-lang-toggle.png'), fullPage: false });
    const langTextAfter = await langToggle.textContent();
    console.log('Lang toggle text after click:', langTextAfter);

    // Check if page content changed
    const firstTitle = await page.$eval('[data-testid="movie-title"]', el => el.textContent.trim());
    console.log('First movie title after lang change:', firstTitle);
  }

  // Open date picker
  const dateInput = await page.$('[data-testid="date-range-input-visible"]');
  if (dateInput) {
    await dateInput.click();
    await sleep(1000);
    await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '04-date-picker-open.png'), fullPage: false });
    console.log('Date picker screenshot saved');
    await page.keyboard.press('Escape');
    await sleep(500);
  }

  // Check movie cards grid layout
  const gridStyles = await page.evaluate(() => {
    const grid = document.querySelector('.movies-grid');
    if (!grid) return null;
    const cs = window.getComputedStyle(grid);
    return {
      display: cs.display,
      gridTemplateColumns: cs.gridTemplateColumns,
      gap: cs.gap,
      columnGap: cs.columnGap,
      rowGap: cs.rowGap,
      padding: cs.padding
    };
  });
  console.log('Grid layout:', JSON.stringify(gridStyles, null, 2));

  // Check rating badge styles
  const ratingStyles = await page.evaluate(() => {
    const badges = document.querySelectorAll('.rating-badge');
    return Array.from(badges).slice(0, 6).map(badge => {
      const cs = window.getComputedStyle(badge);
      return {
        class: badge.className.slice(0, 60),
        bg: cs.backgroundColor,
        color: cs.color,
        borderRadius: cs.borderRadius,
        padding: cs.padding,
        fontSize: cs.fontSize,
        border: cs.border
      };
    });
  });
  console.log('Rating badge styles:', JSON.stringify(ratingStyles, null, 2));

  await browser.close();
  console.log('\nDone!');
}

main().catch(console.error);
