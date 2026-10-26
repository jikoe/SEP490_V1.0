import { test, expect } from '@playwright/test';

async function openFilters(page, { supplierCount = 2, query = '', failSupplier = '' } = {}) {
  const scopes = [];
  const suppliers = Array.from({ length: supplierCount }, (_, index) => ({
    id: index + 1, name: `Nông trại ${index + 1}`, count: 1,
  }));
  const seasons = [
    { id: 11, name: 'Vụ thu · 2026 · Cam Vinh – Nông trại 1', category: '1', supplier: '1' },
    { id: 12, name: 'Đông Xuân · 2026 · Cam Vinh – Nông trại 2', category: '1', supplier: '2' },
    { id: 21, name: 'Vụ hè · 2026 · Gạo – Nông trại 1', category: '2', supplier: '1' },
  ];
  await page.route(url => url.pathname.startsWith('/api/'), async route => {
    const url = new URL(route.request().url());
    if (url.pathname === '/api/marketplace/filters') {
      const category = url.searchParams.get('categoryId') || '';
      const supplier = url.searchParams.get('supplierId') || '';
      scopes.push({ category, supplier });
      if (supplier && supplier === failSupplier) return route.fulfill({
        status: 500, json: { detail: 'Không tải được bộ lọc. Vui lòng thử lại.' },
      });
      return route.fulfill({ json: { data: {
        categories: [{ id: 1, name: 'Trái cây', count: 2 }, { id: 2, name: 'Gạo', count: 1 }],
        suppliers, seasons: seasons.filter(item => (!category || item.category === category)
          && (!supplier || item.supplier === supplier)),
        regions: ['Hưng Yên'], certifications: ['VietGAP'],
      } } });
    }
    if (url.pathname === '/api/marketplace/products') return route.fulfill({ json: {
      data: { items: [], total: 0, page: 1, pageSize: 12 },
    } });
    return route.fulfill({ status: 404, json: { detail: 'Unexpected request' } });
  });
  await page.goto(`/#/marketplace${query}`);
  await expect(page.getByRole('combobox', { name: 'Mùa vụ', exact: true })).toBeEnabled();
  return scopes;
}

test('supplier search handles long lists and keeps a single selection', async ({ page }, testInfo) => {
  await openFilters(page, { supplierCount: 13 });
  const picker = page.getByRole('button', { name: 'Chọn nhà cung cấp', exact: true });
  await picker.click();
  await page.getByRole('searchbox', { name: 'Tìm nhà cung cấp', exact: true }).fill('nong trai 12');
  const list = page.getByRole('listbox', { name: 'Nhà cung cấp', exact: true });
  await expect(list.locator('option:not([hidden])')).toHaveText(['Tất cả nhà cung cấp', 'Nông trại 12']);
  await list.selectOption('12');
  await expect(page).toHaveURL(/supplierId=12/);
  await expect(picker).toContainText('Nông trại 12');
  await expect(list).not.toBeVisible();
  await picker.click();
  await list.selectOption('');
  await expect(page).not.toHaveURL(/supplierId=/);
  await page.setViewportSize({ width: 320, height: 800 });
  await picker.click();
  const search = page.getByRole('searchbox', { name: 'Tìm nhà cung cấp', exact: true });
  await search.fill('khong co nha cung cap nay');
  await expect(page.getByRole('status').filter({ hasText: 'Không tìm thấy nhà cung cấp.' })).toBeVisible();
  await search.press('Escape');
  await expect(picker).toBeFocused();
  await expect(list).not.toBeVisible();
  await picker.click();
  await expect(search).toHaveValue('');
  await expect(page.locator('body')).toHaveJSProperty('scrollWidth', 320);
  await page.screenshot({ path: testInfo.outputPath('supplier-picker-mobile.png'), fullPage: true });
});

test('seasons follow category and supplier and clear only incompatible selection', async ({ page }) => {
  const scopes = await openFilters(page, {
    query: '?categoryId=1&supplierId=1&cropSeasonId=11&region=H%C6%B0ng+Y%C3%AAn&page=3',
  });
  const season = page.getByRole('combobox', { name: 'Mùa vụ', exact: true });
  await expect(season).toHaveValue('11');
  await page.getByRole('combobox', { name: 'Nhà cung cấp', exact: true }).selectOption('2');
  await expect(season).toBeEnabled();
  await expect(page).not.toHaveURL(/cropSeasonId=/);
  await expect(page).not.toHaveURL(/page=/);
  await expect(page).toHaveURL(/region=/);
  await expect(season.locator('option')).toHaveText(['Tất cả mùa vụ', 'Đông Xuân · 2026 · Cam Vinh – Nông trại 2']);
  expect(scopes).toContainEqual({ category: '1', supplier: '2' });
  await season.selectOption('12');
  await page.getByRole('combobox', { name: 'Nhà cung cấp', exact: true }).selectOption('');
  await expect(season).toBeEnabled();
  await expect(season).toHaveValue('12');
  await page.getByRole('button', { name: 'Bỏ Địa phương: Hưng Yên', exact: true }).click();
  await expect(page).not.toHaveURL(/region=/);
  await expect(page).toHaveURL(/cropSeasonId=12/);
  await page.getByRole('button', { name: 'Bỏ Danh mục: Trái cây', exact: true }).click();
  await expect(page).not.toHaveURL(/categoryId=/);
  await expect(season).toBeEnabled();
  await expect(season).toHaveValue('12');
});

test('failed dependent options preserve season selection and offer retry', async ({ page }) => {
  await openFilters(page, { query: '?supplierId=1&cropSeasonId=11', failSupplier: '2' });
  await page.getByRole('combobox', { name: 'Nhà cung cấp', exact: true }).selectOption('2');
  await expect(page.getByRole('alert')).toContainText('Không tải được bộ lọc');
  await expect(page).toHaveURL(/cropSeasonId=11/);
  await expect(page.getByRole('combobox', { name: 'Mùa vụ', exact: true })).toBeDisabled();
  await expect(page.getByRole('button', { name: 'Thử lại', exact: true })).toBeVisible();
});
