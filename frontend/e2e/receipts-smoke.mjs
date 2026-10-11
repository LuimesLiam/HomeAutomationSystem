// Uses synthetic intercepted API responses; never writes actual finance data.
import assert from 'node:assert/strict';
const { chromium } = await import(process.env.PLAYWRIGHT_MODULE_PATH || 'playwright');
const browser = await chromium.launch({ headless: true, args: ['--no-sandbox'] });
const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
const errors = [];
page.on('pageerror', error => errors.push(error.message));
const base = process.env.RECEIPTS_BASE_URL || 'http://127.0.0.1:4200';
const dashboard = {
  expenses: [], groups: [{ id: 1, key: 'food', name: 'Food', color: '#16a34a', isEnabled: true }],
  settings: { modelKey: null, defaultCurrencyCode: 'USD', extractionPrompt: '' },
  models: [{ key: 'gpt-6-luna', name: 'GPT-6 Luna', modelName: 'gpt-6-luna', providerName: 'OpenAI', isDefault: true }],
  summary: { totalAmount: 0, expenseCount: 0, averageAmount: 0, latestExpenseDate: null, totalsByGroup: [] }
};
let scans = 0, saves = 0, failScan = false, failSave = true;
await page.route('**/api/expenses**', async route => {
  const request = route.request();
  if (request.url().endsWith('/analyze-preview')) {
    scans++;
    assert(!request.postData().includes('name="expenseDate"'), 'prefilled date must not override extraction');
    assert(!request.postData().includes('name="currencyCode"'), 'prefilled currency must not override extraction');
    if (failScan) return route.fulfill({ status: 502, json: { message: 'Please try again' } });
    return route.fulfill({ json: {
      reviewToken: 'synthetic-review', expenseGroupId: 1, expenseDate: '2026-09-28', totalAmount: 12.34,
      currencyCode: 'CAD', merchantName: 'Receipt Test Store', modelKeyUsed: 'gpt-6-luna', receiptText: 'Test receipt',
      extraction: { confidence: .94, items: [{ name: 'Apples', quantity: 2, totalPrice: 12.34 }] }
    }});
  }
  if (request.url().endsWith('/save-reviewed')) {
    saves++;
    assert(request.postData().includes('synthetic-review'));
    assert(request.postData().includes('15.25'), 'corrected total should be saved');
    if (failSave) return route.fulfill({ status: 500, json: { message: 'Retry save' } });
    return route.fulfill({ json: { id: 10, merchantName: 'Receipt Test Store' } });
  }
  return route.fulfill({ json: dashboard });
});
try {
  await page.goto(`${base}/expenses`);
  await page.getByRole('heading', { name: 'Less typing. Every receipt accounted for.' }).waitFor();
  assert.equal(await page.getByRole('button', { name: 'Save receipt', exact: true }).count(), 0);
  assert.equal(await page.locator('input[capture="environment"]').count(), 1);
  assert.equal(await page.locator('input[type=file]:not([capture])').first().getAttribute('capture'), null);
  const file = { name: 'receipt.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aWQAAAABJRU5ErkJggg==', 'base64') };
  await page.locator('input[type=file]:not([capture])').first().setInputFiles(file);
  await page.getByRole('button', { name: 'Save receipt', exact: true }).waitFor();
  assert.equal(scans, 1);
  assert.equal(saves, 0);
  assert.equal(await page.locator('input[type=date]').first().inputValue(), '2026-09-28');
  assert.equal(await page.locator('input[maxlength="3"]').first().inputValue(), 'CAD');
  await page.locator('input[type=number]').first().fill('15.25');
  await page.getByRole('button', { name: 'Save receipt', exact: true }).click();
  await page.getByRole('status').filter({ hasText: 'Retry save' }).waitFor();
  assert.equal(await page.locator('.receipt-image-card').count(), 1, 'failed save keeps photo');
  failSave = false;
  await page.getByRole('button', { name: 'Save receipt', exact: true }).click();
  await page.getByRole('status').filter({ hasText: 'Ready for the next one' }).waitFor();
  assert.equal(scans, 1, 'saving must not call AI again');
  assert.equal(saves, 2);
  assert.equal(await page.locator('.receipt-image-card').count(), 0);
  await page.setViewportSize({ width: 390, height: 844 });
  failScan = true;
  await page.locator('input[type=file]:not([capture])').first().setInputFiles(file);
  await page.getByRole('button', { name: 'Try reading receipt again' }).waitFor();
  failScan = false;
  await page.getByRole('button', { name: 'Try reading receipt again' }).click();
  await page.getByRole('button', { name: 'Save receipt', exact: true }).waitFor();
  assert.equal(scans, 3);
  assert(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), 'mobile should not overflow');
  await page.screenshot({ path: '/tmp/receipts-mobile.png', fullPage: true });
  await page.getByRole('button', { name: 'Remove receipt' }).click();
  await page.locator('input[type=file]:not([capture])').first().setInputFiles({ name: 'bad.pdf', mimeType: 'application/pdf', buffer: Buffer.from('bad') });
  await page.getByRole('status').filter({ hasText: 'Choose a JPG' }).waitFor();
  assert.equal(scans, 3, 'unsupported file must not be sent');
  await page.getByRole('button', { name: 'Enter manually', exact: true }).click();
  await page.getByRole('button', { name: 'Save expense', exact: true }).waitFor();
  assert.deepEqual(errors, []);
  console.log('Receipt smoke passed: upload, review, corrections, save retry, scan retry, validation, manual entry and mobile layout.');
} catch (error) { console.error((await page.locator('body').innerText()).slice(-6000)); throw error; } finally { await browser.close(); }
