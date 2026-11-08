import { request, localTime, safeUrl } from './page-ui.js';
import { esc, icon, productImage, account, updateCartBadge, price, rating, bindImageFallback, addProduct } from './marketplace-ui.js';

const container = document.getElementById('mkt-detail-container');
const id = new URLSearchParams(location.search).get('id');
let detail;
let imageIndex = 0;
account(); updateCartBadge();
const search = document.getElementById('mkt-search-input');
let searchTimer;
let composing = false;
function handleSearch(event) {
  event?.preventDefault();
  clearTimeout(searchTimer);
  location.href = `index.html?${new URLSearchParams({ keyword: search.value.trim() })}`;
}
window.handleSearch = handleSearch;
search.addEventListener('input', () => {
  document.getElementById('mkt-search-clear').hidden = !search.value;
  clearTimeout(searchTimer);
  if (!composing && search.value.trim()) searchTimer = setTimeout(handleSearch, 500);
});
search.addEventListener('compositionstart', () => { composing = true; clearTimeout(searchTimer); });
search.addEventListener('compositionend', () => { composing = false; search.dispatchEvent(new Event('input')); });
document.getElementById('mkt-search-clear').addEventListener('click', () => { search.value = ''; clearTimeout(searchTimer); search.focus(); });
const dateLabel = date => date ? date.split('-').reverse().join('/') : 'Chưa cập nhật';
function gallery() {
  const images = detail.images;
  const product = detail.product;
  const photo = images[imageIndex];
  document.getElementById('detail-gallery').innerHTML = `<div class="mkt-main-photo">${productImage(photo?.url || product.imageUrl, photo?.altText || product.name)}
    ${product.certifications[0] ? `<span class="mkt-cert-badge">${icon('shield')}${esc(product.certifications[0])}</span>` : ''}
    ${images.length > 1 ? '<button type="button" class="mkt-photo-prev" id="detail-photo-prev" aria-label="Ảnh trước">‹</button><button type="button" class="mkt-photo-next" id="detail-photo-next" aria-label="Ảnh sau">›</button>' : ''}</div>
    ${images.length > 1 ? `<div class="mkt-thumbnails">${images.map((image, index) => `<button type="button" data-image="${index}" aria-label="Xem ảnh ${index + 1}" aria-pressed="${index === imageIndex}">${productImage(image.url, image.altText || product.name)}</button>`).join('')}</div>` : ''}`;
  for (const [buttonId, direction] of [['detail-photo-prev', -1], ['detail-photo-next', 1]]) document.getElementById(buttonId)?.addEventListener('click', () => { imageIndex = (imageIndex + direction + images.length) % images.length; gallery(); });
  document.querySelectorAll('[data-image]').forEach(button => button.addEventListener('click', () => { imageIndex = Number(button.dataset.image); gallery(); }));
  bindImageFallback(document.getElementById('detail-gallery'));
}
function renderTab(tab) {
  const { product, description, certificates, batches, reviews } = detail;
  const target = document.getElementById('product-panel');
  document.querySelectorAll('[data-detail-tab]').forEach(button => {
    const selected = button.dataset.detailTab === tab;
    button.setAttribute('aria-selected', String(selected));
    button.tabIndex = selected ? 0 : -1;
  });
  target.setAttribute('aria-labelledby', `tab-${tab}`);
  if (tab === 'information') {
    const batch = batches[0];
    target.innerHTML = `<div class="mkt-description-grid"><div><h2>Mô tả sản phẩm</h2><p class="mkt-description">${esc(description || 'Nhà cung cấp chưa cập nhật mô tả sản phẩm.')}</p>
      ${certificates.length ? `<h2>Chứng nhận đã xác thực</h2><ul class="mkt-certificates">${certificates.map(cert => `<li>${icon('check')}<div><strong>${esc(cert.type)}</strong> · ${esc(cert.number)}<p>${esc(cert.issuedBy)}${cert.expiryDate ? ` · Hết hạn: ${dateLabel(cert.expiryDate)}` : ''}</p></div></li>`).join('')}</ul>` : ''}</div>
      <section class="mkt-specifications"><h2>${icon('grid')}Thông số sản phẩm</h2><dl><dt>Loại sản phẩm</dt><dd>${esc(product.category)}</dd><dt>Xuất xứ</dt><dd>${esc(product.region || 'Chưa cập nhật')}</dd><dt>Đơn vị</dt><dd>${esc(product.unit)}</dd><dt>Tồn kho</dt><dd>${product.availableQuantity} ${esc(product.unit)}</dd>
      ${batch?.variety ? `<dt>Giống</dt><dd>${esc(batch.variety)}</dd>` : ''}${batch?.harvestDate ? `<dt>Thu hoạch</dt><dd>${dateLabel(batch.harvestDate)}</dd>` : ''}${batch?.expirationDate ? `<dt>Hạn sử dụng</dt><dd>${dateLabel(batch.expirationDate)}</dd>` : ''}</dl></section></div>
      ${batches.length ? `<section class="mkt-batches"><h2>Lô hàng & truy xuất nguồn gốc</h2>${batches.map(batch => `<article><div><h3>${esc(batch.code)}</h3><p>${esc(batch.season)} · ${batch.availableQuantity} ${esc(product.unit)} khả dụng</p>${batch.qualityNotes ? `<p>${esc(batch.qualityNotes)}</p>` : ''}</div>${safeUrl(batch.traceabilityUrl) ? `<a href="${esc(safeUrl(batch.traceabilityUrl))}" target="_blank" rel="noopener noreferrer">Xem truy xuất →</a>` : ''}</article>`).join('')}</section>` : ''}`;
  } else if (tab === 'reviews') {
    target.innerHTML = `<section><h2>Đánh giá từ khách hàng</h2>${rating(product)}${!reviews.length ? '<p>Chưa có đánh giá từ khách hàng đã mua sản phẩm.</p>' : `<p>Hiển thị ${reviews.length} đánh giá mới nhất trong ${product.reviewCount} đánh giá.</p>${reviews.map(review => `<article class="mkt-review"><h3>${esc(review.author)}</h3><span class="mkt-review-stars" aria-label="${review.rating} trên 5 sao">${'★'.repeat(Math.max(0, Math.min(5, review.rating)))}</span><span> · Đã mua hàng · ${esc(localTime(review.date))}</span><p>${esc(review.comment || 'Khách hàng đã đánh giá sản phẩm.')}</p></article>`).join('')}`}</section>`;
  } else {
    target.innerHTML = '<section><h2>Chính sách giao hàng</h2><p>Phí vận chuyển và khả năng phục vụ phụ thuộc địa chỉ nhận hàng và được xác nhận khi thanh toán.</p><p>Kiểm tra thông tin lô hàng, hạn sử dụng và tổng tiền trước khi xác nhận đơn hàng.</p></section>';
  }
}
function render() {
  const { product, certificates, similarProducts } = detail;
  const maxQuantity = Math.max(0, Math.min(999999999, Math.floor(product.availableQuantity)));
  document.title = `${product.name} - Nông Sản Việt`;
  container.innerHTML = `<nav class="mkt-breadcrumb" aria-label="Đường dẫn"><a href="index.html">⌂ Trang chủ</a><span>›</span><a href="index.html">Nông sản</a><span>›</span><a href="index.html?categoryId=${product.categoryId}">${esc(product.category)}</a><span>›</span><span aria-current="page">${esc(product.name)}</span></nav>
    <div class="mkt-detail-grid"><section id="detail-gallery" class="mkt-gallery" aria-label="Ảnh sản phẩm"></section>
    <section class="mkt-product-information"><a class="mkt-category-pill" href="index.html?categoryId=${product.categoryId}">${esc(product.category)}</a><h1>${esc(product.name)}</h1><p class="mkt-short-description">${product.region ? `Nông sản ${esc(product.region)} – ` : ''}${esc(product.supplier)}</p>${rating(product)}<div class="mkt-detail-price">${price(product)}</div>
    <ul class="mkt-highlights"><li>${icon()}Nguồn gốc: ${esc(product.region || 'Chưa cập nhật')}</li><li>${icon('shield')}${certificates.length ? `Chứng nhận: ${esc(product.certifications.join(', '))}` : detail.isFarmVerified ? 'Nhà cung cấp đã xác thực' : 'Xem thông tin nhà cung cấp'}</li><li>${icon('grid')}Còn ${Number(product.availableQuantity).toLocaleString('vi-VN')} ${esc(product.unit)} khả dụng</li>
    ${detail.wholesalePrice != null && detail.wholesaleMinQuantity != null ? `<li>${icon('card')}Giá sỉ: ${Utils.formatVND(detail.wholesalePrice)} / ${esc(product.unit)} từ ${detail.wholesaleMinQuantity} ${esc(product.unit)}</li>` : ''}</ul>
    <form id="detail-buy"><fieldset id="detail-buy-fields" class="mkt-buy" ${maxQuantity < 1 ? 'disabled' : ''}>
      <div class="mkt-quantity"><button id="detail-quantity-minus" type="button" aria-label="Giảm số lượng" disabled>−</button>
      <input id="detail-quantity" name="quantity" aria-label="Số lượng" type="number" min="1" max="${maxQuantity || 1}" step="1" value="1" required>
      <button id="detail-quantity-plus" type="button" aria-label="Tăng số lượng">+</button></div>
      <label class="mkt-quantity-unit" for="detail-quantity">(${esc(product.unit)})</label>
      <button id="detail-add-cart" class="mkt-primary" type="submit">${icon('cart')}${maxQuantity < 1 ? 'Hết hàng' : 'Thêm vào giỏ hàng'}</button>
    </fieldset></form>
    <button id="detail-share" class="mkt-share" type="button">${icon('share')} Chia sẻ</button><p id="detail-share-message" class="mkt-share-message" role="status"></p></section>
    <aside class="mkt-detail-sidebar"><section class="mkt-farm-card"><div class="mkt-farm-heading"><span class="mkt-farm-avatar">${icon()}</span><div><h2>${esc(product.supplier)}</h2><p>${detail.isFarmVerified ? `${icon('shield')}Đã xác thực` : 'Nhà cung cấp'}</p></div></div><button type="button" id="detail-farm-toggle" aria-expanded="false">Xem gian hàng →</button><div id="detail-farm-extra" class="mkt-farm-extra" hidden><p>${esc(detail.farmAddress)}</p><a href="index.html?supplierId=${product.supplierId}">Xem sản phẩm của nhà cung cấp</a><div style="margin-top: 8px; border-top: 1px dashed #e2e8f0; padding-top: 8px;"><a href="../WholesaleBuyer/supplier-capacity.html?id=${product.supplierId}" class="b2b-capacity-link" style="color: #15803d; font-weight: 600; text-decoration: none; display: inline-flex; align-items: center; gap: 4px;">🏭 Hồ sơ năng lực B2B →</a></div></div></section>
    <section class="mkt-service-card" aria-label="Thông tin mua hàng">${[['truck', 'Vận chuyển', 'Phí và phạm vi giao hàng được xác nhận khi thanh toán.'], ['card', 'Thanh toán', 'Kiểm tra phương thức và tổng tiền tại bước thanh toán.'], ['shield', 'Thông tin minh bạch', 'Xem chứng nhận và truy xuất lô hàng bên dưới.']].map(([name, title, text]) => `<div>${icon(name)}<div><h2>${title}</h2><p>${text}</p></div></div>`).join('')}</section></aside>
    <section class="mkt-detail-content"><div class="mkt-tabs" role="tablist" aria-label="Thông tin chi tiết">${[['information', 'Thông tin sản phẩm'], ['reviews', `Đánh giá (${product.reviewCount})`], ['delivery', 'Chính sách giao hàng']].map(([tab, label]) => `<button type="button" role="tab" id="tab-${tab}" data-detail-tab="${tab}" aria-controls="product-panel">${label}</button>`).join('')}</div><div id="product-panel" role="tabpanel" tabindex="0"></div></section>
    ${similarProducts.length ? `<aside class="mkt-similar"><div class="mkt-similar-heading"><h2>Sản phẩm tương tự</h2><a href="index.html?categoryId=${product.categoryId}">Xem tất cả</a></div>${similarProducts.map(item => `<a class="mkt-similar-item" href="product-detail.html?id=${item.id}">${productImage(item.imageUrl, item.name)}<div><h3>${esc(item.name)}</h3>${price(item)}${rating(item)}</div><span>›</span></a>`).join('')}</aside>` : ''}</div>`;
  gallery(); renderTab('information'); bindImageFallback(container);
  const quantityInput = document.getElementById('detail-quantity');
  const decrease = document.getElementById('detail-quantity-minus');
  const increase = document.getElementById('detail-quantity-plus');
  const buyFields = document.getElementById('detail-buy-fields');
  let adding = false;
  function updateQuantityButtons() {
    const quantity = quantityInput.valueAsNumber;
    decrease.disabled = !(quantity > 1);
    increase.disabled = !Number.isFinite(quantity) || quantity >= maxQuantity;
  }
  quantityInput.addEventListener('input', updateQuantityButtons);
  for (const [button, direction] of [[decrease, -1], [increase, 1]]) {
    button.addEventListener('click', () => {
      const current = Number.isFinite(quantityInput.valueAsNumber) ? quantityInput.valueAsNumber : 1;
      quantityInput.value = Math.max(1, Math.min(maxQuantity, Math.round(current) + direction));
      updateQuantityButtons();
    });
  }
  updateQuantityButtons();
  document.getElementById('detail-buy').addEventListener('submit', async event => {
    event.preventDefault();
    if (adding || maxQuantity < 1 || !quantityInput.reportValidity()) return;
    adding = true;
    buyFields.disabled = true;
    const button = document.getElementById('detail-add-cart');
    button.textContent = 'Đang thêm…';
    try {
      await addProduct(button, product.id, quantityInput.valueAsNumber);
    } finally {
      adding = false;
      buyFields.disabled = false;
      button.innerHTML = `${icon('cart')}Thêm vào giỏ hàng`;
      updateQuantityButtons();
    }
  });
  document.getElementById('detail-farm-toggle').addEventListener('click', event => {
    const extra = document.getElementById('detail-farm-extra');
    extra.hidden = !extra.hidden;
    event.currentTarget.setAttribute('aria-expanded', String(!extra.hidden));
    event.currentTarget.textContent = extra.hidden ? 'Xem gian hàng →' : 'Ẩn thông tin gian hàng';
  });
  document.querySelectorAll('[data-detail-tab]').forEach(button => button.addEventListener('click', () => renderTab(button.dataset.detailTab)));
  document.querySelector('.mkt-tabs').addEventListener('keydown', event => {
    const tabs = [...event.currentTarget.querySelectorAll('button')];
    const index = tabs.indexOf(document.activeElement);
    const next = event.key === 'ArrowRight' ? (index + 1) % tabs.length : event.key === 'ArrowLeft' ? (index + tabs.length - 1) % tabs.length : event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1 : null;
    if (next != null) { event.preventDefault(); renderTab(tabs[next].dataset.detailTab); tabs[next].focus(); }
  });
  document.getElementById('detail-share').addEventListener('click', async () => {
    try {
      if (navigator.share) await navigator.share({ title: product.name, url: location.href });
      else { await navigator.clipboard.writeText(location.href); document.getElementById('detail-share-message').textContent = 'Đã sao chép liên kết sản phẩm.'; }
    } catch (error) { if (error.name !== 'AbortError') document.getElementById('detail-share-message').textContent = `Liên kết sản phẩm: ${location.href}`; }
  });
}
async function load() {
  try {
    if (!/^\d+$/.test(id || '')) throw new Error('Vui lòng chọn sản phẩm trong danh sách.');
    detail = await request(`/marketplace/products/${id}`);
    render();
  } catch (error) {
    container.innerHTML = `<div class="mkt-state" role="alert"><h1>Sản phẩm không khả dụng</h1><p>${esc(error.message)}</p><a href="index.html">Quay lại danh sách</a><button id="detail-retry" type="button">Thử lại</button></div>`;
    document.getElementById('detail-retry').onclick = load;
  }
}
load();
