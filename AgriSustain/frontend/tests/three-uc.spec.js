import { test, expect } from '@playwright/test';

async function openDemo(page) {
  await page.goto('/');
  await page.getByRole('button', { name: 'Tiếp tục với bản demo' }).click();
}

test('authenticated user can log out to the home page', async ({ page }) => {
  let logoutRequestSeen = false;
  await page.route('**/api/auth/login', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      accessToken: 'test-token',
      expiresAtUtc: '2026-10-25T18:00:00Z',
      user: {
        id: 1,
        username: 'logout-test',
        email: 'logout-test@example.com',
        fullName: 'Logout Test',
        role: 'RetailCustomer',
      },
    }),
  }));
  await page.route('**/api/auth/logout', (route) => {
    logoutRequestSeen = route.request().method() === 'POST'
      && route.request().headers().authorization === 'Bearer test-token';
    return route.fulfill({ status: 204, body: '' });
  });

  await page.goto('/#/login');
  await page.locator('.login-form input').nth(0).fill('logout-test');
  await page.locator('.login-form input').nth(1).fill('password');
  await page.locator('.login-submit').click();
  await page.locator('.mkt-account summary').click();
  await page.locator('.mkt-account button').click();

  await expect(page.locator('.marketplace')).toBeVisible();
  await expect(page.getByRole('status').filter({ hasText: 'Đăng xuất thành công.' })).toBeVisible();
  expect(logoutRequestSeen).toBe(true);
});

test('UC-ADM-03: cancel and confirm a status change', async ({ page }) => {
  await openDemo(page);
  const row = page.getByRole('row').filter({ hasText: 'Quản trị viên dự phòng' });
  await expect(row.getByText('Hoạt động', { exact: true })).toBeVisible();
  await expect(row).toContainText(/\d{2}\/\d{2}\/\d{4} \d{2}:\d{2}/);
  await row.getByRole('button', { name: 'Tạm ngưng' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Hủy' }).click();
  await expect(row.getByText('Hoạt động', { exact: true })).toBeVisible();
  await row.getByRole('button', { name: 'Tạm ngưng' }).click();
  await page.getByLabel('Lý do *', { exact: true }).selectOption('Khác');
  await page.getByLabel('Ghi rõ lý do *', { exact: true }).fill('Kiểm tra chức năng theo SRS');
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  await expect(row.getByText('Tạm ngưng', { exact: true })).toBeVisible();
  await row.getByRole('button', { name: 'Kích hoạt lại' }).click();
  await page.getByLabel('Lý do *', { exact: true }).selectOption('Tạm ngưng nhầm');
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  await expect(row.getByText('Hoạt động', { exact: true })).toBeVisible();
});

test('UC-ADM-05: create, edit and delete an unused category', async ({ page }) => {
  await openDemo(page);
  await page.getByRole('button', { name: 'Danh mục sản phẩm', exact: false }).click();
  await page.getByRole('button', { name: 'Thêm danh mục' }).click();
  const name = `Hạt giống E2E ${Date.now()}`;
  await page.getByLabel('Tên danh mục *', { exact: true }).fill(name);
  await page.getByRole('button', { name: 'Lưu danh mục' }).click();
  const row = page.getByRole('row').filter({ hasText: name });
  await expect(row).toBeVisible();
  await row.getByRole('button', { name: 'Sửa', exact: true }).click();
  await page.getByLabel('Mô tả', { exact: true }).fill('Danh mục kiểm thử, không gắn sản phẩm.');
  await page.getByRole('button', { name: 'Lưu danh mục' }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Đã cập nhật danh mục' })).toBeVisible();
  await row.getByRole('button', { name: 'Xóa', exact: true }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận', exact: true }).click();
  await expect(row).toHaveCount(0);
  await expect(page.getByLabel('Ảnh danh mục')).toHaveCount(0);
});

test('UC-RC-02: voucher feedback and successful checkout', async ({ page }) => {
  await openDemo(page);
  await page.getByLabel('Phiên giả lập').selectOption('retail-customer');
  await expect(page.getByText('Cải ngọt hữu cơ', { exact: true })).toBeVisible();
  await page.getByLabel('Mã voucher', { exact: true }).fill('INVALID');
  await expect(page.getByRole('button', { name: 'Đặt hàng', exact: true })).toBeDisabled();
  await page.getByRole('button', { name: 'Tính lại tổng tiền' }).click();
  await expect(page.getByRole('status')).toContainText('Voucher không hợp lệ');
  await expect(page.getByLabel('Mã voucher', { exact: true })).toHaveValue('');
  await page.getByLabel('Mã voucher', { exact: true }).fill('WELCOME10');
  await page.getByLabel(/Điểm sử dụng/).fill('100');
  await page.getByRole('button', { name: 'Tính lại tổng tiền' }).click();
  await expect(page.getByRole('button', { name: 'Đặt hàng', exact: true })).toBeEnabled();
  await page.getByRole('button', { name: 'Đặt hàng', exact: true }).click();
  await page.getByRole('button', { name: 'Xác nhận đặt hàng', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Đã tạo đơn hàng' })).toBeVisible();
  await expect(page.getByText('Chờ thanh toán', { exact: true })).toBeVisible();
  await expect(page.locator('.success-panel')).toContainText('214.500');
  await expect(page.locator('.success-panel')).toContainText('Module QR/thanh toán chưa được tích hợp');
});

test('UC-RC-02: unsupported address preserves input and blocks placement', async ({ page }) => {
  await openDemo(page);
  await page.getByLabel('Phiên giả lập').selectOption('other-retail-customer');
  await expect(page.getByText('Cải ngọt hữu cơ', { exact: true })).toBeVisible();
  await page.getByLabel('Tỉnh / thành phố *', { exact: true }).selectOption('13');
  await page.getByLabel('Phường / xã *', { exact: true }).selectOption('269');
  await page.getByRole('button', { name: 'Tính lại tổng tiền' }).click();
  await expect(page.getByRole('alert')).toContainText('chưa thuộc vùng giao hàng');
  await expect(page.getByLabel('Phường / xã *', { exact: true })).toHaveValue('269');
  await expect(page.getByRole('button', { name: 'Đặt hàng', exact: true })).toBeDisabled();
});
