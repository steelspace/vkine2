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

  // Go to movies page
  await page.goto(BASE_URL + '/movies', { waitUntil: 'networkidle', timeout: 30000 });
  await sleep(1500);

  // Get all actual movie card clickable elements
  const cardStructure = await page.evaluate(() => {
    // Look for the movies grid and card elements
    const grid = document.querySelector('.movies-grid');
    if (grid) {
      const cards = grid.querySelectorAll('[class*="movie-card"], [class*="card"], article, li');
      return Array.from(cards).slice(0, 5).map(c => ({
        tag: c.tagName,
        class: c.className.slice(0, 100),
        outerHTML: c.outerHTML.slice(0, 300)
      }));
    }
    return null;
  });
  console.log('Card structure:', JSON.stringify(cardStructure, null, 2));

  // Get detailed HTML of movies grid
  const gridHTML = await page.evaluate(() => {
    const grid = document.querySelector('.movies-grid');
    return grid ? grid.innerHTML.slice(0, 3000) : 'grid not found';
  });
  console.log('Grid HTML:', gridHTML);

  // Try clicking on a movie title link directly
  const firstMovieTitle = await page.$('.movies-grid h3, .movies-grid a');
  if (firstMovieTitle) {
    const titleText = await firstMovieTitle.textContent();
    console.log('Found first movie title:', titleText);
    await firstMovieTitle.click();
    await sleep(2000);
    await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '02-movie-detail-click.png'), fullPage: false });

    // Log what happened after click
    const url = page.url();
    console.log('URL after click:', url);

    // Check for modal
    const modalVisible = await page.evaluate(() => {
      const modal = document.querySelector('[class*="modal"], [class*="detail"], [role="dialog"], .overlay');
      return modal ? {
        class: modal.className.slice(0, 100),
        visible: modal.offsetHeight > 0,
        html: modal.innerHTML.slice(0, 500)
      } : null;
    });
    console.log('Modal after click:', JSON.stringify(modalVisible, null, 2));

    // Full page screenshot
    await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '02-movie-detail-full.png'), fullPage: true });
  }

  // Go back and examine toolbar structure
  await page.goto(BASE_URL + '/movies', { waitUntil: 'networkidle', timeout: 30000 });
  await sleep(1500);

  const toolbarHTML = await page.evaluate(() => {
    const toolbar = document.querySelector('.sticky-toolbar');
    return toolbar ? toolbar.outerHTML.slice(0, 5000) : 'toolbar not found';
  });
  console.log('Toolbar HTML:', toolbarHTML);

  // Get styles on toolbar elements
  const toolbarStyles = await page.evaluate(() => {
    const elements = document.querySelectorAll('.sticky-toolbar, .sticky-toolbar-inner, nav, .search-input, .date-range-input, .time-slider, nav a');
    return Array.from(elements).map(el => {
      const cs = window.getComputedStyle(el);
      return {
        selector: el.className.slice(0, 50),
        tag: el.tagName,
        bg: cs.backgroundColor,
        color: cs.color,
        border: cs.border,
        borderRadius: cs.borderRadius,
        padding: cs.padding,
        backdropFilter: cs.backdropFilter,
        width: cs.width,
        height: cs.height,
        display: cs.display,
        gap: cs.gap,
        fontSize: cs.fontSize
      };
    });
  });
  console.log('Toolbar styles:', JSON.stringify(toolbarStyles, null, 2));

  // Examine a movie card's full structure
  const movieCardHTML = await page.evaluate(() => {
    // Find actual movie cards
    const grid = document.querySelector('.movies-grid');
    if (!grid) return 'no grid';
    const firstItem = grid.firstElementChild;
    return firstItem ? firstItem.outerHTML.slice(0, 2000) : 'no children';
  });
  console.log('Movie card HTML:', movieCardHTML);

  // Get styles of card
  const cardStyles = await page.evaluate(() => {
    const card = document.querySelector('.movies-grid > *');
    if (!card) return null;
    const cs = window.getComputedStyle(card);
    const img = card.querySelector('img');
    const imgCs = img ? window.getComputedStyle(img) : null;
    return {
      card: {
        bg: cs.backgroundColor,
        borderRadius: cs.borderRadius,
        shadow: cs.boxShadow,
        border: cs.border,
        padding: cs.padding,
        display: cs.display,
        width: cs.width,
        maxWidth: cs.maxWidth
      },
      img: imgCs ? {
        width: imgCs.width,
        height: imgCs.height,
        objectFit: imgCs.objectFit,
        borderRadius: imgCs.borderRadius
      } : null
    };
  });
  console.log('Card styles:', JSON.stringify(cardStyles, null, 2));

  // Check the premieres page structure in detail
  await page.goto(BASE_URL + '/premieres', { waitUntil: 'networkidle', timeout: 30000 });
  await sleep(1500);

  const premieresHTML = await page.evaluate(() => document.body.innerHTML.slice(0, 5000));
  console.log('Premieres page HTML:', premieresHTML);

  const premieresStyles = await page.evaluate(() => {
    const els = document.querySelectorAll('.premieres-container, .premiere-card, [class*="premiere"], table, .timeline');
    return Array.from(els).slice(0, 10).map(el => ({
      class: el.className.slice(0, 80),
      tag: el.tagName,
      html: el.outerHTML.slice(0, 400)
    }));
  });
  console.log('Premieres elements:', JSON.stringify(premieresStyles, null, 2));

  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '03-premieres-detail.png'), fullPage: false });

  // Scroll to see more of the premieres page
  await page.evaluate(() => window.scrollTo(0, 600));
  await sleep(500);
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '03-premieres-scrolled.png'), fullPage: false });

  // Get the full page
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '03-premieres-fullpage.png'), fullPage: true });

  await browser.close();
  console.log('\nDone!');
}

main().catch(console.error);
