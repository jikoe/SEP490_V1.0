import { test, expect } from '@playwright/test';

const product = {
  id: 101, name: 'Cam hữu cơ', categoryId: 1, category: 'Trái cây',
  supplierId: 10, supplier: 'Nông trại xanh', region: 'Hưng Yên', imageUrl: null,
  price: 55000, unit: 'kg', availableQuantity: 5, rating: 4, reviewCount: 1,
  certifications: ['VietGAP'],
};

async function openMarketplace(page, { detail = false, failure = null, hold = null,
  initialCartFailure = false } = {}) {
  const posts = [];
  const items = [];
  const cart = () => ({
    items, selectedSubtotal: items.reduce((sum, item) => sum + item.subtotal, 0),
    selectedCount: items.length, reviewToken: 'test-review', canProceedToCheckout: items.length > 0,
    messages: ['Đã thêm sản phẩm vào giỏ hàng.'],
  });
  await page.addInitScript(() => sessionStorage.setItem('agrisustain:session', JSON.stringify({
    accessToken: 'test-token', expiresAtUtc: '2099-01-01T00:00:00Z',
    user: { id: 1, fullName: 'Khách hàng kiểm thử', role: 'RetailCustomer' },
  })));
  await page.route(url => url.pathname.startsWith('/api/'), async route => {
    const path = new URL(route.request().url()).pathname;
    if (path === '/api/marketplace/filters') return route.fulfill({ json: { data: {
      categories: [{ id: 1, name: 'Trái cây', count: 1 }],
      suppliers: [{ id: 10, name: product.supplier, count: 1 }],
      seasons: [{ id: 20, name: 'Vụ thu', count: 1 }], regions: ['Hưng Yên'], certifications: ['VietGAP'],
    } } });
    if (path === '/api/marketplace/products') return route.fulfill({ json: {
      data: { items: [product], total: 1, page: 1, pageSize: 12 },
    } });
    if (path === '/api/marketplace/products/101') return route.fulfill({ json: { data: {
      product, description: 'Cam thu hoạch trong ngày.', farmAddress: 'Hưng Yên', isFarmVerified: true,
      wholesalePrice: null, wholesaleMinQuantity: null, images: [], certificates: [], reviews: [],
      similarProducts: [], batches: [{ id: 11, code: 'CAM-01', availableQuantity: 5,
        season: 'Vụ thu', variety: 'Cam Vinh', harvestDate: '2026-10-01',
        expirationDate: '2026-11-01', qualityNotes: null, traceabilityUrl: null }],
    } } });
    if (path === '/api/cart' && route.request().method() === 'GET') {
      if (initialCartFailure && !posts.length) return route.fulfill({ status: 500,
        json: { detail: 'Không thể tải giỏ hàng. Vui lòng thử lại.' } });
      return route.fulfill({ json: cart() });
    }
    if (path === '/api/cart/items' && route.request().method() === 'POST') {
      const body = route.request().postDataJSON();
      posts.push(body);
      if (hold) await hold;
      if (failure) return route.fulfill({ status: failure.status, json: { detail: failure.message } });
      const existing = items.find(item => item.productId === body.productId);
      if (existing) {
        existing.quantity += body.quantity;
        existing.subtotal = existing.quantity * existing.unitPrice;
      } else items.push({ cartItemId: items.length + 1, productId: product.id, harvestBatchId: 11,
        productName: product.name, farmName: product.supplier, farmerProfileId: 10, unit: product.unit,
        batchCode: 'CAM-01', imageUrl: null, quantity: body.quantity, retailPrice: product.price,
        unitPrice: product.price, wholesalePrice: null, wholesaleMinQuantity: null,
        subtotal: body.quantity * product.price, availableQuantity: 5, quantityStep: 1,
        isSelected: true, isAvailable: true, unavailableReason: null });
      return route.fulfill({ json: cart() });
    }
    return route.fulfill({ status: 404, json: { detail: `Unexpected API request: ${path}` } });
  });
  await page.goto(detail ? '/#/marketplace/products/101' : '/#/marketplace');
  await expect(page.getByRole('heading', { name: product.name, exact: true })).toBeVisible();
  return posts;
}

test('catalogue adds directly, updates cart quantity on repeat, and removes duplicate navigation', async ({ page }) => {
  const posts = await openMarketplace(page);
  await expect(page.getByText('Khám phá ngay', { exact: false })).toHaveCount(0);
  await expect(page.getByRole('navigation', { name: 'Danh mục nhanh' })).toHaveCount(0);
  await expect(page.getByRole('complementary', { name: 'Bộ lọc sản phẩm' })).toBeVisible();
  const add = page.getByRole('button', { name: 'Thêm vào giỏ hàng', exact: true });
  await add.click();
  await expect(page.getByRole('button', { name: /Giỏ hàng/ })).toContainText('1');
  await expect(page).toHaveURL(/#\/marketplace$/);
  await add.click();
  await expect(page.getByRole('button', { name: /Giỏ hàng/ })).toContainText('2');
  expect(posts).toEqual([{ productId: 101, quantity: 1 }, { productId: 101, quantity: 1 }]);
  await page.getByRole('heading', { name: product.name, exact: true }).getByRole('link').click();
  await expect(page.getByRole('heading', { name: product.name, exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: /Giỏ hàng/ })).toContainText('2');
  await page.reload();
  await expect(page.getByRole('heading', { name: product.name, exact: true })).toBeVisible();
  await expect(page.getByRole('button', { name: /Giỏ hàng/ })).toContainText('2');
});

for (const detail of [false, true]) {
  test(`cart confirmation stays in the top-right for four seconds (${detail ? 'detail' : 'catalogue'})`, async ({ page }) => {
    await openMarketplace(page, { detail });
    await page.clock.install();
    const add = page.getByRole('button', { name: 'Thêm vào giỏ hàng', exact: true });
    const toast = page.getByRole('status').filter({ hasText: /Đã thêm/ });
    await add.click();
    await expect(toast).toHaveCSS('position', 'fixed');
    const bounds = await toast.boundingBox();
    expect(bounds.y).toBeLessThanOrEqual(32);
    expect(page.viewportSize().width - bounds.x - bounds.width).toBeLessThanOrEqual(32);
    await page.clock.fastForward(3500);
    await expect(toast).toBeVisible();
    await add.click();
    await page.clock.fastForward(3500);
    await expect(toast).toBeVisible();
    await page.clock.fastForward(500);
    await expect(toast).toHaveCount(0);
    await expect(page.getByRole('button', { name: /Giỏ hàng/ })).toContainText('2');
  });
}

test('detail removes batch selection and uses a positive quantity stepper', async ({ page }) => {
  const posts = await openMarketplace(page, { detail: true });
  await expect(page.getByRole('combobox', { name: /Lô hàng/ })).toHaveCount(0);
  const quantity = page.getByRole('spinbutton', { name: /Số lượng/ });
  const minus = page.getByRole('button', { name: 'Giảm số lượng', exact: true });
  const plus = page.getByRole('button', { name: 'Tăng số lượng', exact: true });
  await expect(quantity).toHaveAttribute('min', '1');
  await expect(quantity).toHaveValue('1');
  await minus.evaluate(button => button.click());
  await expect(quantity).toHaveValue('1');
  await plus.click();
  await expect(quantity).toHaveValue('2');
  await minus.click();
  await expect(quantity).toHaveValue('1');
  await quantity.fill('3');
  await page.getByRole('button', { name: 'Thêm vào giỏ hàng', exact: true }).click();
  await expect(page.getByRole('button', { name: /Giỏ hàng/ })).toContainText('3');
  await expect(page.getByRole('status').filter({ hasText: /Đã thêm/ })).toBeVisible();
  expect(posts).toEqual([{ productId: 101, quantity: 3 }]);
});

test('zero and negative quantities never reach the cart API', async ({ page }) => {
  const posts = await openMarketplace(page, { detail: true });
  const quantity = page.getByRole('spinbutton', { name: /Số lượng/ });
  const add = page.getByRole('button', { name: 'Thêm vào giỏ hàng', exact: true });
  for (const value of ['0', '-2']) {
    await quantity.fill(value);
    await add.click();
    expect(posts.every(item => item.quantity >= 1)).toBe(true);
  }
});

for (const failure of [
  { status: 400, message: 'Sản phẩm chỉ còn 2 kg. Vui lòng giảm số lượng.' },
  { status: 500, message: 'Không thể thêm vào giỏ hàng. Vui lòng thử lại.' },
]) {
  test(`cart failure ${failure.status} shows an error without a success badge`, async ({ page }) => {
    const posts = await openMarketplace(page, { detail: true, failure });
    await page.getByRole('spinbutton', { name: /Số lượng/ }).fill('3');
    await page.getByRole('button', { name: 'Thêm vào giỏ hàng', exact: true }).click();
    await expect(page.getByRole('alert')).toContainText(failure.message);
    await expect(page.getByRole('status').filter({ hasText: /Đã thêm/ })).toHaveCount(0);
    await expect(page.getByRole('button', { name: /Giỏ hàng/ })).not.toContainText('3');
    expect(posts).toHaveLength(1);
  });
}

test('an in-flight add disables repeat submission', async ({ page }) => {
  let release;
  const hold = new Promise(resolve => { release = resolve; });
  const posts = await openMarketplace(page, { hold });
  const add = page.getByRole('article').filter({ hasText: product.name }).getByRole('button');
  try {
    await add.click();
    await expect.poll(() => posts.length).toBe(1);
    await expect(add).toBeDisabled();
    await add.evaluate(button => { button.click(); button.click(); });
    expect(posts).toHaveLength(1);
  } finally {
    release();
  }
  await expect(page.getByRole('button', { name: /Giỏ hàng/ })).toContainText('1');
});

test('cart reload cannot replace a pending add with stale quantities', async ({ page }) => {
  let release;
  const hold = new Promise(resolve => { release = resolve; });
  const posts = await openMarketplace(page, { hold, initialCartFailure: true });
  const retry = page.getByRole('button', { name: 'Tải lại giỏ hàng', exact: true });
  await expect(retry).toBeVisible();
  try {
    await page.getByRole('button', { name: 'Thêm vào giỏ hàng', exact: true }).click();
    await expect.poll(() => posts.length).toBe(1);
    await expect(retry).toBeDisabled();
  } finally {
    release();
  }
  await expect(page.getByRole('button', { name: /Giỏ hàng/ })).toContainText('1');
  await expect(retry).toHaveCount(0);
});

test('catalogue and detail keep cart visible without horizontal overflow at supported widths', async ({ page }, testInfo) => {
  test.setTimeout(60000);
  await openMarketplace(page);
  await page.getByRole('button', { name: 'Thêm vào giỏ hàng', exact: true }).click();
  await expect(page.getByRole('button', { name: /Giỏ hàng/ })).toContainText('1');

  for (const width of [320, 768, 1024, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto('/#/marketplace');
    await expect(page.getByRole('heading', { name: product.name, exact: true })).toBeVisible();
    for (const view of ['catalogue', 'detail']) {
      if (view === 'detail') {
        await page.getByRole('heading', { name: product.name, exact: true }).getByRole('link').click();
        await expect(page.getByRole('spinbutton', { name: /Số lượng/ })).toBeVisible();
      }
      await page.evaluate(() => window.scrollTo(0, 0));
      const cart = page.getByRole('button', { name: /Giỏ hàng/ });
      await expect(cart).toContainText('1');
      await expect(cart).toBeInViewport();
      const layout = await page.evaluate(() => ({
        width: window.innerWidth, contentWidth: document.documentElement.scrollWidth,
      }));
      await page.screenshot({ path: testInfo.outputPath(`${view}-${width}.png`), fullPage: true });
      expect.soft(layout.contentWidth, `${view} at ${width}px must fit the viewport`)
        .toBeLessThanOrEqual(layout.width + 1);
    }
  }
});
