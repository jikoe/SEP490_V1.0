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

test('moderation screen shows SQL pending products when reviewed-list request fails', async ({ page }) => {
  let listRequests = 0;
  await page.route('**/api/auth/login', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      accessToken: 'moderator-test-token',
      expiresAtUtc: '2026-10-25T18:00:00Z',
      user: {
        id: 2,
        username: 'moderator-test',
        email: 'moderator-test@example.com',
        fullName: 'Moderator Test',
        role: 'ContentModerator',
      },
    }),
  }));
  await page.route('**/api/moderation/products?**', (route) => {
    listRequests += 1;
    const url = new URL(route.request().url());
    if (url.searchParams.get('status') === 'Pending') {
      return route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [{
            id: 81,
            name: 'Cải ngọt hữu cơ từ SQL',
            status: 'Pending',
            category: 'Rau củ',
            farmerName: 'Nông trại kiểm thử',
            farmName: 'Nông trại xanh',
            submittedAt: '2026-10-25T10:00:00Z',
            imageUrl: null,
          }],
          total: 1,
          page: 1,
          pageSize: 10,
        }),
      });
    }
    return route.fulfill({
      status: 500,
      contentType: 'application/json',
      body: JSON.stringify({ title: 'Không tải được danh sách đã xử lý.' }),
    });
  });
  await page.route('**/api/moderation/products/81', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 81,
      submittedAt: '2026-10-25T10:00:00Z',
      moderationStatus: 'Pending',
      name: 'Cải ngọt hữu cơ từ SQL',
      category: 'Rau củ',
      images: [],
      retailPrice: 25000,
      wholesalePrice: 20000,
      wholesaleMinQuantity: 30,
      unit: 'kg',
      quantity: 12,
      farmer: { fullName: 'Moderator Test', email: 'farmer@example.com' },
      farm: { farmName: 'Nông trại xanh', verificationStatus: 'PendingVerification' },
      harvest: [{
        batchCode: 'BATCH-81',
        estimatedHarvestDate: '2026-12-10',
        actualHarvestDate: null,
        plantingDate: '2026-09-01',
        season: 'Vụ Đông 2026',
        cropVariety: 'Cải ngọt',
        status: 'Growing',
        growingPractices: 'Bón phân hữu cơ: sử dụng phân compost',
        qualityNotes: 'Canh tác không thuốc trừ sâu',
      }],
      traceability: [{
        batchCode: 'BATCH-81',
        targetUrl: 'https://example.test/trace/BATCH-81',
        isRevoked: true,
      }],
      certificates: [
        {
          certificateType: 'VietGAP còn hạn',
          expiryDate: '2099-12-31',
          verificationStatus: 'Approved',
        },
        {
          certificateType: 'Chứng nhận chờ xác minh',
          expiryDate: null,
          verificationStatus: 'Pending',
        },
        {
          certificateType: 'Chứng nhận hết hạn',
          expiryDate: '2000-01-01',
          verificationStatus: 'Approved',
        },
      ],
    }),
  }));

  await page.goto('/#/login');
  await page.locator('.login-form input').nth(0).fill('moderator-test');
  await page.locator('.login-form input').nth(1).fill('password');
  await page.locator('.login-submit').click();

  await expect(page.getByRole('heading', { name: 'Cải ngọt hữu cơ từ SQL' }).first()).toBeVisible();
  await expect(page.getByRole('alert')).toContainText('Không tải được danh sách đã xử lý.');
  await expect(page.getByRole('button', { name: 'Duyệt sản phẩm' })).toBeDisabled();
  await expect(page.getByText('Trạng thái xác minh:').locator('..'))
    .toContainText('Đang chờ xác minh');
  await expect(page.getByText('Giá bán sỉ')).toBeVisible();
  await expect(page.getByText(/20\.000.*\/ kg · Tối thiểu 30 kg/)).toBeVisible();
  await expect(page.getByText('Ngày gieo trồng').locator('..'))
    .toContainText(/1 thg 9, 2026/);
  await expect(page.getByText('Trạng thái lô').locator('..')).toContainText('Growing');
  await expect(page.getByText('Quy trình canh tác').locator('..'))
    .toContainText('Bón phân hữu cơ: sử dụng phân compost');
  await expect(page.getByText('Trạng thái mã truy xuất').locator('..'))
    .toContainText('Đã thu hồi');
  await expect(page.locator('.moderation-certificates .verified'))
    .toContainText('VietGAP còn hạn · Đã xác minh');
  await expect(page.locator('.moderation-certificates .unverified'))
    .toContainText(['Chứng nhận chờ xác minh · Đang chờ xác minh',
      'Chứng nhận hết hạn · Đã xác minh · Đã hết hạn']);

  await page.getByRole('button', { name: /Cải ngọt hữu cơ từ SQL/ }).click();
  await expect(page).toHaveURL(/#\/moderation\/products\/81$/);
  expect(listRequests).toBe(2);
});

test('moderation pagination stops at the last page and resets after approving its last product', async ({ page }) => {
  let approved = false;
  let pendingTotal = 10;
  const pendingPageOne = Array.from({ length: 10 }, (_, index) => ({
    id: index + 1,
    name: `Sản phẩm chờ duyệt ${index + 1}`,
    status: 'Pending',
    category: 'Rau củ',
    farmerName: 'Nông trại kiểm thử',
    submittedAt: '2026-10-25T10:00:00Z',
    imageUrl: null,
  }));
  const finalPendingProduct = {
    id: 11,
    name: 'Sản phẩm cuối trang 2',
    status: 'Pending',
    category: 'Rau củ',
    farmerName: 'Nông trại kiểm thử',
    submittedAt: '2026-10-25T10:00:00Z',
    imageUrl: null,
  };

  await page.route('**/api/auth/login', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      accessToken: 'moderator-test-token',
      expiresAtUtc: '2026-10-25T18:00:00Z',
      user: {
        id: 2,
        username: 'moderator-test',
        email: 'moderator-test@example.com',
        fullName: 'Moderator Test',
        role: 'ContentModerator',
      },
    }),
  }));
  await page.route('**/api/moderation/products?**', (route) => {
    const url = new URL(route.request().url());
    const status = url.searchParams.get('status');
    const currentPage = Number(url.searchParams.get('page'));
    const items = status === 'Reviewed'
      ? (approved ? [{
        ...finalPendingProduct,
        status: 'Approved',
        reviewedAt: '2026-10-26T10:00:00Z',
        reviewedBy: 'Moderator Test',
      }] : [])
      : currentPage === 1 ? pendingPageOne : [finalPendingProduct];
    const total = status === 'Reviewed' ? (approved ? 1 : 0) : pendingTotal;
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items, total, page: currentPage, pageSize: 10 }),
    });
  });
  await page.route(/\/api\/moderation\/products\/\d+$/, (route) => {
    const id = Number(new URL(route.request().url()).pathname.split('/').at(-1));
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id,
        submittedAt: '2026-10-25T10:00:00Z',
        moderationStatus: approved ? 'Approved' : 'Pending',
        name: id === 11 ? 'Sản phẩm cuối trang 2' : `Sản phẩm chờ duyệt ${id}`,
        category: 'Rau củ',
        images: [],
        retailPrice: 25000,
        unit: 'kg',
        quantity: 12,
        farmer: { fullName: 'Moderator Test', email: 'farmer@example.com' },
        farm: { farmName: 'Nông trại xanh', verificationStatus: 'Approved' },
        harvest: [],
        traceability: [],
        certificates: [],
      }),
    });
  });
  await page.route('**/api/moderation/products/11/approve', (route) => {
    approved = true;
    return route.fulfill({ status: 204, body: '' });
  });

  await page.goto('/#/login');
  await page.locator('.login-form input').nth(0).fill('moderator-test');
  await page.locator('.login-form input').nth(1).fill('password');
  await page.locator('.login-submit').click();

  await expect(page.getByRole('button', { name: 'Trang sau' })).toBeDisabled();
  pendingTotal = 11;
  await page.reload();
  await expect(page.getByRole('button', { name: 'Trang sau' })).toBeEnabled();
  await page.getByRole('button', { name: 'Trang sau' }).click();
  await expect(page.getByText('Sản phẩm cuối trang 2', { exact: true }).first()).toBeVisible();
  await page.getByRole('button', { name: 'Trang trước' }).waitFor();
  await page.getByRole('button', { name: /Sản phẩm cuối trang 2/ }).click();
  await page.getByRole('button', { name: 'Duyệt sản phẩm' }).click();

  pendingTotal = 10;
  await expect(page.getByRole('tab', { name: /Đã xử lý/ })).toHaveAttribute('aria-selected', 'true');
  await expect(page.getByRole('button', { name: 'Trang trước' })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Trang sau' })).toBeDisabled();
  await expect(page.getByText('1 hồ sơ', { exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: /Sản phẩm cuối trang 2/ }).first()).toBeVisible();
  await expect(page.getByText('Người xử lý: Moderator Test')).toBeVisible();
});

test('moderation rejection requires a separate confirmation with the reason', async ({ page }) => {
  let rejected = false;
  let rejectionReason = '';

  await page.route('**/api/auth/login', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      accessToken: 'moderator-test-token',
      expiresAtUtc: '2026-10-25T18:00:00Z',
      user: {
        id: 2,
        username: 'moderator-test',
        email: 'moderator-test@example.com',
        fullName: 'Moderator Test',
        role: 'ContentModerator',
      },
    }),
  }));
  await page.route('**/api/moderation/products?**', (route) => {
    const url = new URL(route.request().url());
    const status = url.searchParams.get('status');
    const product = {
      id: 21,
      name: 'Sản phẩm cần từ chối',
      status: rejected ? 'Rejected' : 'Pending',
      category: 'Rau củ',
      farmerName: 'Farmer Test',
      submittedAt: '2026-10-25T10:00:00Z',
      imageUrl: null,
      rejectionReason,
    };
    const items = status === 'Pending' && !rejected
      ? [product] : status === 'Reviewed' && rejected ? [product] : [];
    return route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ items, total: items.length, page: 1, pageSize: 10 }),
    });
  });
  await page.route('**/api/moderation/products/21', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      id: 21,
      submittedAt: '2026-10-25T10:00:00Z',
      moderationStatus: rejected ? 'Rejected' : 'Pending',
      rejectionReason,
      name: 'Sản phẩm cần từ chối',
      category: 'Rau củ',
      images: [],
      retailPrice: 25000,
      unit: 'kg',
      quantity: 12,
      farmer: { fullName: 'Farmer Test', email: 'farmer@example.com' },
      farm: { farmName: 'Nông trại xanh', verificationStatus: 'Approved' },
      harvest: [],
      traceability: [],
      certificates: [],
    }),
  }));
  await page.route('**/api/moderation/products/21/reject', async (route) => {
    rejectionReason = JSON.parse(route.request().postData()).reason;
    rejected = true;
    return route.fulfill({ status: 204, body: '' });
  });

  await page.goto('/#/login');
  await page.locator('.login-form input').nth(0).fill('moderator-test');
  await page.locator('.login-form input').nth(1).fill('password');
  await page.locator('.login-submit').click();
  await page.getByRole('button', { name: /Sản phẩm cần từ chối/ }).click();

  await page.getByRole('button', { name: 'Từ chối sản phẩm' }).click();
  await expect(page.getByLabel(/Lý do từ chối/)).toBeVisible();
  await page.getByRole('button', { name: 'Xem lại và xác nhận' }).click();
  await expect(page.getByRole('alert')).toContainText('Vui lòng nhập lý do từ chối');
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await page.getByLabel(/Lý do từ chối/).fill('Thiếu thông tin nguồn gốc.');
  await page.getByRole('button', { name: 'Xem lại và xác nhận' }).click();
  const confirmation = page.getByRole('dialog');
  await expect(confirmation).toContainText('Thiếu thông tin nguồn gốc.');
  await confirmation.getByRole('button', { name: 'Hủy' }).click();
  await expect(confirmation).toHaveCount(0);
  expect(rejected).toBe(false);

  await page.getByRole('button', { name: 'Xem lại và xác nhận' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Xác nhận từ chối' }).click();
  await expect(page.locator('.moderation-notification')).toContainText('Đã từ chối');
  await expect(page.getByRole('tab', { name: /Đã xử lý/ })).toHaveAttribute('aria-selected', 'true');
  expect(rejectionReason).toBe('Thiếu thông tin nguồn gốc.');
});

test('empty moderation queue keeps search available without a separate dashboard page', async ({ page }) => {
  await page.route('**/api/auth/login', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      accessToken: 'moderator-test-token',
      expiresAtUtc: '2026-10-25T18:00:00Z',
      user: {
        id: 2,
        username: 'moderator-test',
        email: 'moderator-test@example.com',
        fullName: 'Moderator Test',
        role: 'ContentModerator',
      },
    }),
  }));
  await page.route('**/api/moderation/products?**', (route) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ items: [], total: 0, page: 1, pageSize: 10 }),
  }));

  await page.goto('/#/login');
  await page.locator('.login-form input').nth(0).fill('moderator-test');
  await page.locator('.login-form input').nth(1).fill('password');
  await page.locator('.login-submit').click();
  await expect(page.getByRole('heading', { name: 'Không có tin đăng cần kiểm duyệt.' })).toBeVisible();
  await expect(page.getByLabel('Tìm tin đăng')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Quay lại bảng điều khiển' })).toHaveCount(0);
  await expect(page).toHaveURL(/#\/moderation\/products$/);
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
