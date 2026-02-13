import { test, expect } from '@playwright/test';
import path from 'path';

// Accessibility smoke using axe-core
test('a11y: no critical axe violations on Movies page', async ({ page }) => {
  await page.goto('/');

  // Inject axe-core from node_modules
  await page.addScriptTag({ path: require.resolve('axe-core/axe.min.js') });

  // Run axe
  const result = await page.evaluate(async () => {
    // @ts-ignore
    return await (window as any).axe.run(document, {
      runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa'] }
    });
  });

  const violations = result.violations || [];
  const critical = violations.filter((v: any) => v.impact === 'critical' || v.impact === 'serious');
  // Fail if any serious/critical violations
  expect(critical.length, `axe critical/serious violations: ${JSON.stringify(critical, null, 2)}`).toBe(0);
});
