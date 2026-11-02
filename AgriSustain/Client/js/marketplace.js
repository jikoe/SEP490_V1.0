import { request, cleanMessage } from './page-ui.js';
import { esc, icon, account, updateCartBadge, productCard, bindImageFallback, addProduct } from './marketplace-ui.js';

const params = new URLSearchParams(location.search);
if (params.has('search') && !params.has('keyword')) { params.set('keyword', params.get('search')); params.delete('search'); }
let filterOptions;
let catalogueController;
let searchTimer;
let composing = false;
let page = Math.max(1, Number(params.get('page')) || 1);
const search = document.getElementById('mkt-search-input');
const grid = document.getElementById('mkt-product-grid');
const pagination = document.getElementById('mkt-pagination-nav');
const priceNames = { under50: 'Dưới 50.000đ', '50to100': '50.000đ – 100.000đ', '100to200': '100.000đ – 200.000đ', over200: 'Trên 200.000đ' };
account(); updateCartBadge();
search.value = params.get('keyword') || '';
document.getElementById('mkt-sort-select').value = params.get('sort') || 'newest';
document.getElementById('mkt-page-size').value = ['12', '24', '48'].includes(params.get('pageSize')) ? params.get('pageSize') : '12';
document.querySelectorAll('[name="priceRange"]').forEach(box => { box.checked = params.getAll('priceRanges').includes(box.value); });
document.getElementById('filter-rating').value = params.get('minRating') || '';

function persist() {
  const values = { keyword: search.value.trim(), sort: document.getElementById('mkt-sort-select').value,
    page, pageSize: document.getElementById('mkt-page-size').value,
    region: filterOptions ? document.getElementById('filter-region').value : params.get('region') || '',
    minRating: document.getElementById('filter-rating').value };
  Object.entries(values).forEach(([key, value]) => { params.delete(key); if (value) params.set(key, value); });
  for (const [name, key] of [['priceRange', 'priceRanges'], ['cert', 'certifications']]) {
    if (name === 'cert' && !filterOptions) continue;
    params.delete(key);
    document.querySelectorAll(`[name="${name}"]:checked`).forEach(box => params.append(key, box.value));
  }
  history.replaceState(null, '', `index.html${params.size ? `?${params}` : ''}`);
  document.getElementById('mkt-search-clear').hidden = !search.value;
  renderActiveFilters();
}
function renderActiveFilters() {
  const labels = { keyword: 'Từ khóa', categoryId: 'Danh mục', supplierId: 'Nhà cung cấp', cropSeasonId: 'Mùa vụ',
    region: 'Địa phương', certifications: 'Chứng nhận', priceRanges: 'Khoảng giá', minRating: 'Đánh giá tối thiểu' };
  const options = { categoryId: filterOptions?.categories, supplierId: filterOptions?.suppliers, cropSeasonId: filterOptions?.seasons };
  document.getElementById('mkt-active-filters').innerHTML = [...params].filter(([key]) => labels[key]).map(([key, value]) => {
    const text = options[key]?.find(option => String(option.id) === value)?.name || (key === 'priceRanges' ? priceNames[value] : value);
    return `<button type="button" data-filter-key="${key}" data-filter-value="${esc(value)}">${labels[key]}: ${esc(text)} ×</button>`;
  }).join('');
}
function renderCategories() {
  const categories = filterOptions?.categories || [];
  const total = categories.reduce((sum, item) => sum + item.count, 0);
  document.getElementById('mkt-categories-list').innerHTML = `<button class="mkt-category ${!params.has('categoryId') ? 'active' : ''}" type="button" data-category="">${icon()}Tất cả sản phẩm <span>${total}</span></button>`
    + categories.map(item => `<button class="mkt-category ${params.get('categoryId') === String(item.id) ? 'active' : ''}" type="button" data-category="${item.id}">${icon()}${esc(item.name)}<span>${item.count}</span></button>`).join('');
  document.getElementById('mkt-categories-list').querySelectorAll('[data-category]').forEach(button => button.addEventListener('click', () => filterByCategory(button.dataset.category)));
}
async function loadOptions() {
  try {
    filterOptions = await request('/marketplace/filters');
    renderCategories();
    const region = document.getElementById('filter-region');
    region.innerHTML = '<option value="">Tất cả tỉnh/thành</option>' + filterOptions.regions.map(value => `<option value="${esc(value)}">${esc(value)}</option>`).join('');
    region.value = params.get('region') || '';
    const certs = document.getElementById('mkt-certifications');
    certs.innerHTML = '<legend>Chứng nhận</legend>' + filterOptions.certifications.map(value => `<label class="mkt-checkbox"><input type="checkbox" name="cert" value="${esc(value)}" ${params.getAll('certifications').includes(value) ? 'checked' : ''}>${esc(value)}</label>`).join('');
    certs.addEventListener('change', () => loadCatalogue());
    renderActiveFilters();
  } catch (error) {
    document.getElementById('mkt-categories-list').innerHTML = `<p role="alert">${esc(error.message)}</p><button id="mkt-retry-filters" type="button">Thử lại</button>`;
    document.getElementById('mkt-retry-filters').onclick = loadOptions;
  }
}
async function fetchCatalogue() {
  catalogueController?.abort();
  const controller = new AbortController(); catalogueController = controller;
  grid.setAttribute('aria-busy', 'true');
  persist();
  try {
    const result = await request(`/marketplace/products?${params}`, { signal: controller.signal });
    if (controller.signal.aborted) return;
    document.getElementById('mkt-result-count').textContent = `${result.total} sản phẩm`;
    grid.innerHTML = result.items.length ? result.items.map(productCard).join('') : '<div class="mkt-state"><h3>Không tìm thấy sản phẩm phù hợp</h3><p>Thử thay đổi từ khóa hoặc xóa bộ lọc để xem thêm sản phẩm.</p><button type="button" id="mkt-empty-clear">Xóa bộ lọc</button></div>';
    document.getElementById('mkt-empty-clear')?.addEventListener('click', clearFilters);
    bindImageFallback(grid);
    grid.querySelectorAll('[data-add-product]').forEach(button => button.addEventListener('click', () => addProduct(button, Number(button.dataset.addProduct))));
    const pages = Math.max(1, Math.ceil(result.total / result.pageSize));
    const numbers = [...new Set([1, ...Array.from({ length: 5 }, (_, i) => result.page + i - 2).filter(value => value > 1 && value < pages), pages])];
    pagination.innerHTML = `<button type="button" data-page="${result.page - 1}" aria-label="Trang trước" ${result.page <= 1 ? 'disabled' : ''}>‹</button>${numbers.map(value => `<button type="button" data-page="${value}" ${value === result.page ? 'aria-current="page"' : ''}>${value}</button>`).join('')}<button type="button" data-page="${result.page + 1}" aria-label="Trang sau" ${result.page >= pages ? 'disabled' : ''}>›</button>`;
    pagination.querySelectorAll('[data-page]').forEach(button => button.addEventListener('click', () => { page = Number(button.dataset.page); fetchCatalogue(); }));
  } catch (error) {
    if (error.name === 'AbortError') return;
    grid.innerHTML = `<div class="mkt-state" role="alert"><p>${esc(cleanMessage(error.message))}</p><button id="mkt-retry" type="button">Thử lại</button></div>`;
    document.getElementById('mkt-result-count').textContent = 'Chưa tải được danh sách sản phẩm';
    document.getElementById('mkt-retry').onclick = fetchCatalogue;
    pagination.innerHTML = '';
  } finally { if (!controller.signal.aborted) grid.removeAttribute('aria-busy'); }
}
function loadCatalogue() { page = 1; clearTimeout(searchTimer); fetchCatalogue(); }
function filterByCategory(id) { params.delete('categoryId'); if (id) params.set('categoryId', id); renderCategories(); loadCatalogue(); }
function clearFilters() { location.href = 'index.html'; }
function handleSearch(event) { event.preventDefault(); loadCatalogue(); }
Object.assign(window, { loadCatalogue, filterByCategory, clearFilters, handleSearch });
function scheduleSearch() {
  clearTimeout(searchTimer);
  document.getElementById('mkt-search-clear').hidden = !search.value;
  if (!composing) searchTimer = setTimeout(loadCatalogue, 300);
}
search.addEventListener('input', scheduleSearch);
search.addEventListener('compositionstart', () => { composing = true; clearTimeout(searchTimer); });
search.addEventListener('compositionend', () => { composing = false; scheduleSearch(); });
document.getElementById('mkt-search-clear').addEventListener('click', () => { search.value = ''; loadCatalogue(); search.focus(); });
document.getElementById('mkt-active-filters').addEventListener('click', event => {
  const button = event.target.closest('[data-filter-key]'); if (!button) return;
  const key = button.dataset.filterKey, value = button.dataset.filterValue;
  if (key === 'keyword') search.value = '';
  if (key === 'region') document.getElementById('filter-region').value = '';
  if (key === 'minRating') document.getElementById('filter-rating').value = '';
  if (['priceRanges', 'certifications'].includes(key)) document.querySelectorAll(`[name="${key === 'priceRanges' ? 'priceRange' : 'cert'}"]`).forEach(box => { if (box.value === value) box.checked = false; });
  params.delete(key, value); renderCategories(); loadCatalogue();
});
loadOptions().then(fetchCatalogue);
