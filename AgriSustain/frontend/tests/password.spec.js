import { test, expect } from '@playwright/test';

const successMessage = 'Đổi mật khẩu thành công. Vui lòng sử dụng mật khẩu mới cho những lần đăng nhập sau.';

async function openPassword(page) {
  await page.addInitScript(() => sessionStorage.setItem('agrisustain:session', JSON.stringify({
    accessToken: 'browser-test-token', expiresAtUtc: '2099-01-01T00:00:00Z',
    user: { userId: 1, email: 'test@example.test', fullName: 'Test User', role: 'RetailCustomer' },
  })));
  await page.route('**/health', (route) => route.fulfill({ json: { persistence: 'Test' } }));
  await page.goto('/#/account/password');
  await expect(page.getByRole('heading', { name: 'Đổi mật khẩu', exact: true })).toBeVisible();
  await page.getByLabel('Mật khẩu hiện tại *', { exact: true }).fill('Old12345');
  await page.getByLabel('Mật khẩu mới *', { exact: true }).fill('New12345');
  await page.getByLabel('Xác nhận mật khẩu mới *', { exact: true }).fill('New12345');
}

for (const contentType of ['application/json', 'text/plain']) {
  test(`password success shows the MSG11 toast and retains the form (${contentType})`, async ({ page }) => {
    const crashes = [];
    page.on('pageerror', (error) => crashes.push(error.message));
    await openPassword(page);
    await page.clock.install();
    await page.route('**/api/auth/change-password', async (route) => {
      expect(route.request().postDataJSON()).toEqual({
        currentPassword: 'Old12345', newPassword: 'New12345', confirmPassword: 'New12345',
      });
      await route.fulfill({ contentType, body: contentType === 'application/json'
        ? JSON.stringify(successMessage) : successMessage });
    });
    await page.getByRole('button', { name: 'Đổi mật khẩu', exact: true }).last().click();
    await expect(page.getByRole('status')).toContainText(successMessage);
    await expect(page.getByLabel('Mật khẩu hiện tại *', { exact: true })).toHaveValue('');
    await expect(page.getByLabel('Mật khẩu mới *', { exact: true })).toHaveValue('');
    await expect(page.getByLabel('Xác nhận mật khẩu mới *', { exact: true })).toHaveValue('');
    await expect(page).toHaveURL(/#\/account\/password$/);
    if (contentType === 'application/json') {
      await page.getByRole('button', { name: 'Đóng thông báo', exact: true }).click();
    } else await page.clock.fastForward(8000);
    await expect(page.getByRole('status')).toHaveCount(0);
    expect(crashes).toEqual([]);
  });
}

test('password rejection stays visible and does not show a success toast', async ({ page }) => {
  await openPassword(page);
  await page.route('**/api/auth/change-password', (route) => route.fulfill({
    status: 400, json: { title: 'Mật khẩu hiện tại không chính xác.', code: 'INVALID_CURRENT_PASSWORD' },
  }));
  await page.getByRole('button', { name: 'Đổi mật khẩu', exact: true }).last().click();
  await expect(page.getByRole('alert')).toHaveText('Mật khẩu hiện tại không chính xác.');
  await expect(page.getByRole('status')).toHaveCount(0);
  await expect(page.getByLabel('Mật khẩu hiện tại *', { exact: true })).toHaveValue('Old12345');
});
