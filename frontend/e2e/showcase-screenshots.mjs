// Capture the real app UI with intercepted, fictional data. No backend is required.
import assert from 'node:assert/strict';
import { mkdir } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import * as demo from './showcase-fixtures.mjs';

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE_PATH || 'playwright');
const base = process.env.SHOWCASE_BASE_URL || 'http://127.0.0.1:4310';
const output = fileURLToPath(new URL('../../docs/screenshots/', import.meta.url));
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ headless: true, args: ['--no-sandbox'] });
const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, deviceScaleFactor: 1, locale: 'en-CA', colorScheme: 'dark', timezoneId: 'UTC', reducedMotion: 'reduce', serviceWorkers: 'block' });
const page = await context.newPage();
const errors = [];
const unexpected = [];
page.on('pageerror', error => errors.push(error.message));
await context.route('https://fonts.googleapis.com/**', route => route.abort());
await context.route('https://fonts.gstatic.com/**', route => route.abort());

await context.route('**/api/**', async route => {
  const request = route.request();
  const url = new URL(request.url());
  const path = url.pathname;
  if (request.method() === 'OPTIONS') return route.fulfill({ status: 204, headers: { 'access-control-allow-origin': new URL(base).origin, 'access-control-allow-methods': '*', 'access-control-allow-headers': '*' } });
  let json;
  if (path === '/api/settings/ai') json = { llms: demo.models };
  else if (path === '/api/settings/media-sources') json = { movieSources: [{ id: 1, path: '/mnt/movies' }], tvSources: [{ id: 2, path: '/mnt/tv' }] };
  else if (path === '/api/video/list') json = demo.movies;
  else if (path === '/api/video/genre') json = { items: demo.movies.filter(movie => movie.genre === url.searchParams.get('genre')), total: 6 };
  else if (path === '/api/video' || path === '/api/video/search') json = demo.movieGroups;
  else if (path === '/api/tv/list') json = demo.series;
  else if (/^\/api\/tv\/\d+\/episodes$/.test(path)) json = demo.episodes;
  else if (path === '/api/tv' || path === '/api/tv/search') json = Object.groupBy(demo.series, series => series.genre);
  else if (path === '/api/expenses') json = demo.dashboard;
  else if (path === '/api/expenses/items') json = demo.itemComparisons;
  else if (path === '/api/expenses/analyze-preview') json = demo.receiptPreview;
  else if (path === '/api/ai/chat/sessions') json = demo.chatSessions;
  else if (/^\/api\/ai\/chat\/sessions\/\d+$/.test(path)) json = demo.chatDetail;
  else if (path === '/api/commute/workspace') json = demo.workspace;
  else if (path === '/api/commute') json = demo.commuteSnapshot();
  else if (path === '/api/commute/maps') json = { directionsUrl: 'https://www.google.com/maps', embedUrl: null };
  else {
    unexpected.push(`${request.method()} ${path}`);
    return route.fulfill({ status: 501, json: { message: 'No showcase fixture for this endpoint.' } });
  }
  await route.fulfill({ json, headers: { 'access-control-allow-origin': new URL(base).origin } });
});
await context.route('**/assets/vessel-ports.private.json', route => route.fulfill({ json: [] }));
await context.route('**/showcase/**', route => {
  const path = new URL(route.request().url()).pathname;
  const poster = path.match(/poster-(\d+)\.svg/);
  const camera = path.match(/camera-(\d+)\.svg/);
  if (poster) return route.fulfill({ contentType: 'image/svg+xml', body: demo.posterSvg(demo.movies[Number(poster[1])], Number(poster[1])) });
  if (camera) return route.fulfill({ contentType: 'image/svg+xml', body: demo.cameraSvg(Number(camera[1])) });
  return route.fulfill({ status: 404, body: '' });
});
await context.route('https://tile.openstreetmap.org/**', route => route.fulfill({ contentType: 'image/svg+xml', body: demo.mapTile }));

async function capture(name, fullPage = false) {
  console.log(`Capturing ${name}...`);
  await page.waitForFunction(() => document.fonts.status === 'loaded', null, { timeout: 10000 });
  await page.waitForFunction(() => !document.querySelector('.p-toast-message'), null, { timeout: 15000 });
  await page.screenshot({ path: `${output}${name}.png`, fullPage, animations: 'disabled' });
  console.log(`Captured ${name}.png`);
}

async function frameAt(locator, offset = 20) {
  await locator.scrollIntoViewIfNeeded();
  await locator.evaluate((element, inset) => window.scrollTo({ top: window.scrollY + element.getBoundingClientRect().top - inset, behavior: 'instant' }), offset);
}

async function visit(path) {
  console.log(`Opening ${path}`);
  await page.goto(`${base}${path}`, { waitUntil: 'networkidle' });
  await page.getByRole('navigation', { name: 'Main navigation' }).waitFor();

}

try {
  await visit('/movie-player');
  await page.locator('.movie-card').first().waitFor();
  await capture('media-library');
  await page.locator('.movie-card').first().click();
  await page.getByRole('button', { name: 'Play on TV', exact: true }).waitFor();
  await capture('media-details');

  await visit('/tv-table');
  await page.getByText('Harbor Days', { exact: true }).waitFor();
  await page.getByRole('button', { name: 'Open', exact: true }).first().click();
  await page.getByRole('button', { name: 'Expand All', exact: true }).click();
  await page.getByText('A New Shore', { exact: true }).waitFor();
  await capture('tv-series');

  await visit('/expenses');
  await page.getByRole('heading', { name: 'Less typing. Every receipt accounted for.' }).waitFor();
  const receiptPage = await context.newPage();
  await receiptPage.setViewportSize({ width: 480, height: 640 });
  await receiptPage.setContent(`<html><body style="margin:0">${demo.receiptSvg}</body></html>`);
  const receipt = await receiptPage.screenshot();
  await receiptPage.close();
  await page.locator('input[type=file]:not([capture])').first().setInputFiles({ name: 'maple-market-demo.png', mimeType: 'image/png', buffer: receipt });
  await page.getByRole('button', { name: 'Save receipt', exact: true }).waitFor();
  await capture('receipt-review', true);
  await page.setViewportSize({ width: 390, height: 844 });
  assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Receipt page must fit mobile viewport');
  await frameAt(page.getByRole('heading', { name: 'Check your receipt details' }), 100);
  await capture('receipt-mobile');

  await page.setViewportSize({ width: 1440, height: 1100 });
  await visit('/expenses?section=report');
  await page.getByRole('button', { name: 'Monthly Report', exact: true }).click();
  await page.getByRole('heading', { name: 'Monthly Report', exact: true }).waitFor();
  await page.locator('.report-month-picker select').first().selectOption('09');
  await page.getByRole('button', { name: 'Refresh Report', exact: true }).click();
  await page.getByRole('heading', { name: 'Daily spend trend' }).waitFor();
  await capture('spending-report', true);
  await page.getByRole('button', { name: 'Price Finder', exact: true }).click();
  await page.getByRole('heading', { name: 'Item Price Finder' }).waitFor();
  await page.getByRole('button', { name: 'Compare Prices', exact: true }).click();
  await page.locator('.item-comparison-card').first().waitFor();
  await page.locator('.item-comparison-card summary').first().click();
  await capture('price-finder');
  await page.getByRole('button', { name: 'Expenses', exact: true }).click();
  await page.getByRole('heading', { name: 'Table, Search, And Totals' }).waitFor();
  await capture('expense-history');

  await page.setViewportSize({ width: 1440, height: 1000 });
  await visit('/ai-chat');
  await page.getByRole('heading', { name: 'Make room for the good stuff' }).waitFor();
  await capture('ai-chat');

  await visit('/commute');
  await page.getByRole('heading', { name: 'Riverside → Harbor district', exact: true }).waitFor();
  await capture('commute-dashboard', true);
  await page.getByRole('tab', { name: 'Map', exact: true }).click();
  await page.getByRole('heading', { name: 'Route map', exact: true }).waitFor();
  await page.locator('.leaflet-tile-loaded').first().waitFor();
  await capture('commute-map', true);
  await page.setViewportSize({ width: 390, height: 844 });
  assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'Commute page must fit mobile viewport');
  await frameAt(page.getByRole('heading', { name: 'Route map', exact: true }));
  await capture('commute-mobile');
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.getByRole('tab', { name: 'Build my dashboard', exact: true }).click();
  await page.getByRole('button', { name: 'Edit', exact: true }).nth(1).click();
  await page.getByRole('heading', { name: 'Edit dashboard', exact: true }).waitFor();
  await page.evaluate(() => window.scrollTo({ top: 0, behavior: 'instant' }));
  await capture('commute-builder', true);

  await visit('/settings');
  await page.getByRole('heading', { name: 'LLMs', exact: true }).waitFor();
  await capture('model-settings');
  assert.deepEqual(unexpected, [], 'Every API request must be intercepted with a fixture');
  assert.deepEqual(errors, [], 'Showcase navigation must not raise browser errors');
  console.log('Showcase complete: media, TV, receipt review, reports, prices, history, chat, commute and settings. All API requests intercepted.');
} catch (error) {
  console.error((await page.locator('body').innerText()).slice(-3500));
  console.error({ errors, unexpected });
  await page.screenshot({ path: '/tmp/homeapp-showcase-failure.png', fullPage: true });
  throw error;
} finally {
  await browser.close();
}
