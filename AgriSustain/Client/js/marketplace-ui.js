import { esc, icon, productImage, money, cleanMessage } from './page-ui.js';
export { esc, icon, productImage, money };
export const rating = product => `<span class="mkt-rating"><span aria-hidden="true">★</span> <strong>${product.rating == null ? 'Chưa đánh giá' : Number(product.rating).toFixed(1)}</strong> <small>(${product.reviewCount || 0} đánh giá)</small></span>`;
export const price = product => `<span class="mkt-price">${money(product.price)} <small>/ ${esc(product.unit)}</small></span>`;
export function account() {
  const container = document.getElementById('mkt-account-container');
  const user = Auth.getUserInfo();
  if (Auth.isAuthenticated() && user) {
    container.innerHTML = `<details class="mkt-account"><summary>${icon('user')}<span>${esc(user.fullName || user.username)}</span><span>⌄</span></summary><div>
      <a href="${Auth.getLandingPageForRole(user.role)}">Khu vực làm việc</a><a href="orders.html">Đơn hàng của tôi</a><a href="profile.html">Hồ sơ cá nhân</a>
      <a href="../Guest/change-password.html">Đổi mật khẩu</a><button id="mkt-logout" type="button">Đăng xuất</button></div></details>`;
    document.getElementById('mkt-logout').addEventListener('click', () => API.logout());
  }
}
export async function updateCartBadge() {
  if (!Auth.isAuthenticated() || Auth.getUserRole() !== 'RetailCustomer') return;
  const result = await API.getCart({ suppressErrorToast: true });
  if (result.success) {
    const quantity = (result.data.items || []).reduce((sum, item) => sum + item.quantity, 0);
    const badge = document.getElementById('mkt-cart-badge');
    badge.textContent = quantity;
    badge.style.display = quantity ? 'inline-block' : 'none';
  }
}
export function productCard(product) {
  return `<article class="mkt-product-card"><a class="mkt-card-photo" href="product-detail.html?id=${product.id}">${productImage(product.imageUrl, product.name)}
    ${product.certifications?.length ? `<span class="mkt-cert-badge">${icon('shield')}${esc(product.certifications[0])}</span>` : ''}</a>
    <div class="mkt-card-content"><h3><a href="product-detail.html?id=${product.id}">${esc(product.name)}</a></h3><p class="mkt-supplier">${esc(product.supplier)}</p>
    ${rating(product)}${product.region ? `<p class="mkt-region">⌖ ${esc(product.region)}</p>` : ''}${price(product)}
    <button type="button" class="mkt-primary mkt-list-add" data-add-product="${product.id}" ${product.availableQuantity <= 0 ? 'disabled' : ''}>${icon('cart')}${product.availableQuantity <= 0 ? 'Hết hàng' : 'Thêm vào giỏ hàng'}</button></div></article>`;
}
export function bindImageFallback(container) {
  container.querySelectorAll('img').forEach(img => img.addEventListener('error', () => {
    const fallback = document.createElement('div');
    fallback.className = 'mkt-image-fallback';
    fallback.setAttribute('role', 'img');
    fallback.setAttribute('aria-label', img.alt);
    fallback.innerHTML = `${icon()}<span>Chưa có ảnh sản phẩm</span>`;
    img.replaceWith(fallback);
  }, { once: true }));
}
export async function addProduct(button, productId, quantity = 1) {
  if (!Auth.isAuthenticated()) { Utils.showToast('Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng.', 'warning'); return; }
  if (Auth.getUserRole() !== 'RetailCustomer') { Utils.showToast('Chỉ tài khoản khách lẻ có thể thêm sản phẩm vào giỏ hàng.', 'warning'); return; }
  button.disabled = true;
  try {
    const result = await API.addToCart(productId, quantity, { suppressErrorToast: true });
    Utils.showToast(cleanMessage(result.success ? result.data.messages?.join(' ') || 'Đã thêm sản phẩm vào giỏ hàng.' : result.error || 'Không thể thêm sản phẩm vào giỏ hàng.'), result.success ? 'success' : 'error');
    if (result.success) await updateCartBadge();
  } finally { button.disabled = false; }
}
