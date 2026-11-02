import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

// Full retail purchase through the UI, paid by the SePay bank-transfer webhook (Authorization: Apikey).
// Set PAYMENT_WEBHOOK_BASE_URL to the ngrok URL to send the webhook through the public tunnel.
const callbackBase = (process.env.PAYMENT_WEBHOOK_BASE_URL || 'http://localhost:5179').replace(/\/+$/, '');
const buyer = { username: process.env.E2E_BUYER || 'retail1', password: process.env.E2E_PASSWORD || '123456' };
const productName = process.env.E2E_PRODUCT || 'Cam Vinh Hưng Yên Canh Tác Hữu Cơ';

const readText = (relative) => readFileSync(fileURLToPath(new URL(relative, import.meta.url)), 'utf8').replace(/^\uFEFF/, '');
const sePaySettings = JSON.parse(readText('../../AgriSustain/appsettings.json')).Payment.SePay;
const sePayApiKey = process.env.Payment__SePay__WebhookApiKey
  || readText('../../AgriSustain/.env').match(/^Payment__SePay__WebhookApiKey=(.+)$/m)?.[1].trim();
const transferReference = (orderCode) => orderCode.replace(/[^a-z0-9]/gi, '').toUpperCase().slice(0, 18);

async function login(page) {
  await page.goto('/#/login');
  await page.getByPlaceholder('Nhập email hoặc tên đăng nhập').fill(buyer.username);
  await page.getByPlaceholder('Nhập mật khẩu').fill(buyer.password);
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await expect(page).not.toHaveURL(/#\/login/, { timeout: 15_000 });
}

async function placeOrder(page) {
  await page.goto('/#/marketplace');
  const card = page.locator('.mkt-product-card').filter({ has: page.getByRole('heading', { name: productName }) });
  await card.getByRole('button', { name: 'Thêm vào giỏ hàng' }).click();
  await expect(page.getByText('Đã thêm vào giỏ hàng.')).toBeVisible();

  await page.goto('/#/cart');
  await page.getByRole('button', { name: /Tiến hành thanh toán/ }).click();
  await expect(page).toHaveURL(/#\/checkout/);

  // Enabled once the Goship delivery quote has been priced.
  const placeOrderBtn = page.getByRole('button', { name: /^Đặt hàng · / });
  await expect(placeOrderBtn).toBeEnabled({ timeout: 30_000 });
  await expect(page.getByRole('button', { name: /SePay/ })).toHaveAttribute('aria-pressed', 'true');
  await placeOrderBtn.click();
  await page.getByRole('button', { name: 'Xác nhận đặt hàng' }).click();

  await expect(page).toHaveURL(/#\/payment\?/, { timeout: 30_000 });
  const params = new URLSearchParams(page.url().split('?')[1]);
  const order = { orderCode: params.get('orderCode'), amount: Number(params.get('amount')) };
  expect(order.orderCode).toMatch(/^RC-[0-9a-f]{32}$/);
  expect(order.amount).toBeGreaterThan(0);
  return order;
}

async function sendSePayWebhook(request, { orderCode, amount }) {
  expect(sePayApiKey, 'Payment__SePay__WebhookApiKey missing in AgriSustain/.env').toBeTruthy();
  const now = new Date(Date.now() + 7 * 3600_000).toISOString();
  const response = await request.post(`${callbackBase}/api/payment/sepay-webhook`, {
    headers: { 'ngrok-skip-browser-warning': 'true', Authorization: `Apikey ${sePayApiKey}` },
    data: {
      id: Date.now(), gateway: 'MBBank', transactionDate: `${now.slice(0, 10)} ${now.slice(11, 19)}`,
      accountNumber: sePaySettings.AccountNumber, subAccount: '', code: null,
      content: `${transferReference(orderCode)} FT26306123456789`, transferType: 'in',
      description: `BankAPINotify ${transferReference(orderCode)}`, transferAmount: amount, accumulated: 0,
      referenceCode: `FT${Date.now()}`,
    },
  });
  expect(response.ok(), await response.text()).toBeTruthy();
  expect(await response.json()).toEqual({ success: true });
}

const nextStatusPoll = (page, orderCode) =>
  page.waitForResponse((response) => response.url().includes(`/api/orders/by-code/${orderCode}`));

test('sepay webhook marks the order paid and the payment page exits automatically', async ({ page, request }) => {
  await login(page);
  const order = await placeOrder(page);

  await expect(page.getByText(transferReference(order.orderCode), { exact: true })).toBeVisible();
  await expect(page.getByText(`Chờ thanh toán · Mã đơn #${order.orderCode}`)).toBeVisible();

  await sendSePayWebhook(request, order);

  await expect(page.getByRole('heading', { name: 'Đã thanh toán thành công!' })).toBeVisible({ timeout: 10_000 });
  await expect(page).toHaveURL(/#\/retail\/orders/, { timeout: 10_000 });

  const row = page.locator('div', { has: page.getByText(`#${order.orderCode}`, { exact: true }) })
    .filter({ hasText: 'Đã xác nhận' }).last();
  await expect(row).toBeVisible();
});

test('an underpaid sepay transfer keeps the customer on the payment page', async ({ page, request }) => {
  await login(page);
  const order = await placeOrder(page);

  await sendSePayWebhook(request, { ...order, amount: order.amount - 1000 });
  await nextStatusPoll(page, order.orderCode);
  await nextStatusPoll(page, order.orderCode);

  await expect(page.getByRole('heading', { name: 'Đã thanh toán thành công!' })).toHaveCount(0);
  await expect(page).toHaveURL(/#\/payment\?/);
  await expect(page.getByText(`Chờ thanh toán · Mã đơn #${order.orderCode}`)).toBeVisible();
});

