const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const SCREENSHOTS_DIR = '/tmp/vkine-screenshots';
const BASE_URL = 'https://vkine.duckdns.org';

async function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

async function main() {
  const browser = await chromium.launch({ headless: true });

  // Desktop context
  const desktopContext = await browser.newContext({
    viewport: { width: 1440, height: 900 },
    userAgent: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
  });

  const page = await desktopContext.newPage();

  console.log('=== Step 1: Home/Movies Page ===');
  await page.goto(BASE_URL, { waitUntil: 'networkidle', timeout: 30000 });
  await sleep(2000);
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '01-home-desktop.png'), fullPage: true });
  console.log('Screenshot 01 saved');

  // Log page title and basic structure
  const title = await page.title();
  console.log('Page title:', title);

  // Log navigation structure
  const navLinks = await page.$$eval('nav a, header a, [role="navigation"] a', links =>
    links.map(l => ({ text: l.textContent.trim(), href: l.href }))
  );
  console.log('Nav links:', JSON.stringify(navLinks, null, 2));

  // Log main headings
  const headings = await page.$$eval('h1, h2, h3', h => h.map(el => ({ tag: el.tagName, text: el.textContent.trim().slice(0, 80) })));
  console.log('Headings:', JSON.stringify(headings, null, 2));

  // Capture viewport screenshot
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '01-home-viewport.png'), fullPage: false });
  console.log('Screenshot 01-viewport saved');

  console.log('\n=== Step 2: Interact with filters ===');
  // Look for filter/search controls
  const inputs = await page.$$eval('input, select, [role="slider"]', els =>
    els.map(el => ({ tag: el.tagName, type: el.type, placeholder: el.placeholder, id: el.id, class: el.className.slice(0, 60) }))
  );
  console.log('Form controls:', JSON.stringify(inputs, null, 2));

  // Screenshot with filters visible
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '04-filters.png'), fullPage: false });
  console.log('Screenshot 04 saved');

  console.log('\n=== Step 3: Click on a movie card ===');
  // Find movie cards
  const movieCards = await page.$$('article, .card, [class*="card"], [class*="movie"]');
  console.log('Found movie card elements:', movieCards.length);

  // Try clicking on the first movie card
  const firstCard = movieCards[0];
  if (firstCard) {
    try {
      const cardInfo = await firstCard.evaluate(el => ({
        tag: el.tagName,
        class: el.className.slice(0, 80),
        text: el.textContent.trim().slice(0, 100)
      }));
      console.log('First card:', JSON.stringify(cardInfo));

      await firstCard.click();
      await sleep(2000);
      await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '02-movie-detail.png'), fullPage: true });
      console.log('Screenshot 02 saved');

      // Log modal or new page content
      const modalContent = await page.$$eval('[role="dialog"], .modal, [class*="modal"], [class*="detail"]', els =>
        els.map(el => ({ class: el.className.slice(0, 80), text: el.textContent.trim().slice(0, 200) }))
      );
      console.log('Modal/detail content:', JSON.stringify(modalContent, null, 2));

      // Viewport screenshot of modal
      await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '02-movie-detail-viewport.png'), fullPage: false });

      // Close modal if there's a close button
      const closeButton = await page.$('[aria-label="close"], [aria-label="Close"], button[class*="close"], .close, button:has-text("×"), button:has-text("✕")');
      if (closeButton) {
        await closeButton.click();
        await sleep(1000);
        console.log('Modal closed');
      } else {
        // Try pressing Escape
        await page.keyboard.press('Escape');
        await sleep(1000);
        console.log('Pressed Escape to close');
      }
    } catch(e) {
      console.log('Error clicking card:', e.message);
    }
  }

  console.log('\n=== Step 4: Premieres Page ===');
  // Look for premieres link
  const premieresLink = await page.$('a[href*="premiere"], a[href*="premiera"], a:has-text("Premiere"), a:has-text("Premiéry"), a:has-text("Premiery")');
  if (premieresLink) {
    const href = await premieresLink.getAttribute('href');
    console.log('Premieres link found:', href);
    await premieresLink.click();
    await sleep(2000);
    await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '03-premieres.png'), fullPage: true });
    console.log('Screenshot 03 saved');
  } else {
    // Try navigating directly
    const allLinks = await page.$$eval('a', links => links.map(l => ({ text: l.textContent.trim(), href: l.href })));
    console.log('All links on page:', JSON.stringify(allLinks.slice(0, 30), null, 2));

    // Try common premiere URL patterns
    for (const url of ['/premiery', '/premieres', '/premiere', '/uvadeni']) {
      try {
        await page.goto(BASE_URL + url, { waitUntil: 'networkidle', timeout: 10000 });
        await sleep(1000);
        const pageTitle = await page.title();
        console.log(`Tried ${url}, title: ${pageTitle}`);
        await page.screenshot({ path: path.join(SCREENSHOTS_DIR, `03-premieres-try${url.replace('/', '-')}.png`) });
        break;
      } catch(e) {
        console.log(`Failed ${url}: ${e.message}`);
      }
    }
  }

  console.log('\n=== Step 5: Back to home, look at page structure ===');
  await page.goto(BASE_URL, { waitUntil: 'networkidle', timeout: 30000 });
  await sleep(2000);

  // Get full DOM snapshot for analysis
  const bodyHTML = await page.evaluate(() => {
    // Get structural overview
    function getStructure(el, depth = 0) {
      if (depth > 4) return '';
      const tag = el.tagName ? el.tagName.toLowerCase() : '';
      const cls = el.className ? el.className.toString().slice(0, 60) : '';
      const id = el.id || '';
      let str = '  '.repeat(depth) + `<${tag}${id ? ' id="'+id+'"' : ''}${cls ? ' class="'+cls+'"' : ''}>\n`;
      for (const child of Array.from(el.children || []).slice(0, 10)) {
        str += getStructure(child, depth + 1);
      }
      return str;
    }
    return getStructure(document.body);
  });
  console.log('Page structure:\n', bodyHTML.slice(0, 5000));

  // Get computed styles of key elements
  const styleInfo = await page.evaluate(() => {
    const results = [];
    const selectors = ['body', 'nav', 'header', 'main', 'h1', 'h2', 'article', '.card', '[class*="card"]'];
    for (const sel of selectors) {
      const el = document.querySelector(sel);
      if (el) {
        const cs = window.getComputedStyle(el);
        results.push({
          selector: sel,
          backgroundColor: cs.backgroundColor,
          color: cs.color,
          fontFamily: cs.fontFamily,
          fontSize: cs.fontSize,
          fontWeight: cs.fontWeight,
          padding: cs.padding,
          margin: cs.margin,
          borderRadius: cs.borderRadius,
          display: cs.display,
          flexDirection: cs.flexDirection,
          gridTemplateColumns: cs.gridTemplateColumns
        });
      }
    }
    return results;
  });
  console.log('Style info:', JSON.stringify(styleInfo, null, 2));

  // CSS custom properties (design tokens)
  const cssVars = await page.evaluate(() => {
    const root = document.documentElement;
    const cs = window.getComputedStyle(root);
    const vars = {};
    // Common CSS variable names
    const varNames = ['--primary', '--secondary', '--background', '--foreground', '--accent', '--muted',
                      '--card', '--border', '--text', '--font-sans', '--radius',
                      '--color-primary', '--color-background', '--color-text'];
    for (const v of varNames) {
      const val = cs.getPropertyValue(v).trim();
      if (val) vars[v] = val;
    }
    // Also try to get all CSS vars
    const allRules = [];
    for (const sheet of document.styleSheets) {
      try {
        for (const rule of sheet.cssRules || []) {
          if (rule.selectorText === ':root' || rule.selectorText === 'html') {
            const text = rule.cssText;
            const matches = text.match(/--[\w-]+:\s*[^;]+/g) || [];
            allRules.push(...matches.slice(0, 30));
          }
        }
      } catch(e) {}
    }
    return { specificVars: vars, allRootVars: allRules.slice(0, 50) };
  });
  console.log('CSS Variables:', JSON.stringify(cssVars, null, 2));

  console.log('\n=== Step 6: Mobile viewport ===');
  const mobileContext = await browser.newContext({
    viewport: { width: 390, height: 844 },
    userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1'
  });
  const mobilePage = await mobileContext.newPage();
  await mobilePage.goto(BASE_URL, { waitUntil: 'networkidle', timeout: 30000 });
  await sleep(2000);
  await mobilePage.screenshot({ path: path.join(SCREENSHOTS_DIR, '05-mobile-home.png'), fullPage: true });
  console.log('Screenshot 05 saved');

  // Mobile nav
  const mobileNavLinks = await mobilePage.$$eval('nav a, header a', links =>
    links.map(l => ({ text: l.textContent.trim(), href: l.href }))
  );
  console.log('Mobile nav links:', JSON.stringify(mobileNavLinks, null, 2));

  await mobilePage.screenshot({ path: path.join(SCREENSHOTS_DIR, '05-mobile-viewport.png'), fullPage: false });

  // Try to click a movie card on mobile
  const mobileCards = await mobilePage.$$('article, .card, [class*="card"], [class*="movie"]');
  if (mobileCards.length > 0) {
    await mobileCards[0].click();
    await sleep(2000);
    await mobilePage.screenshot({ path: path.join(SCREENSHOTS_DIR, '06-mobile-detail.png'), fullPage: false });
    console.log('Screenshot 06 saved');
  }

  await mobileContext.close();

  // Back to desktop - scroll down to see more content
  console.log('\n=== Step 7: Scroll desktop page ===');
  await page.evaluate(() => window.scrollTo(0, 500));
  await sleep(500);
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '07-home-scrolled.png'), fullPage: false });

  await page.evaluate(() => window.scrollTo(0, 1500));
  await sleep(500);
  await page.screenshot({ path: path.join(SCREENSHOTS_DIR, '08-home-bottom.png'), fullPage: false });

  // Check for loading states - look for skeleton/spinner elements
  const loadingElements = await page.$$eval('[class*="skeleton"], [class*="loading"], [class*="spinner"], [role="progressbar"]', els =>
    els.map(el => ({ class: el.className.slice(0, 80), tag: el.tagName }))
  );
  console.log('Loading elements:', JSON.stringify(loadingElements, null, 2));

  await desktopContext.close();
  await browser.close();

  console.log('\n=== All screenshots saved to', SCREENSHOTS_DIR, '===');
  const files = fs.readdirSync(SCREENSHOTS_DIR);
  console.log('Files:', files);
}

main().catch(console.error);
