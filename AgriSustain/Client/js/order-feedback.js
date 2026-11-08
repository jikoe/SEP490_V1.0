import { esc, icon, productImage, money, localTime, request, showNotice, cleanMessage } from './page-ui.js';
import { bindImageFallback } from './marketplace-ui.js';

const params = new URLSearchParams(location.search);
const orderId = params.get('orderId');
const complaint = params.get('mode') === 'complaint';
const content = document.getElementById('feedback-content');
const notice = document.getElementById('feedback-notice');
const ratings = ['Rất không hài lòng', 'Không hài lòng', 'Bình thường', 'Hài lòng', 'Rất hài lòng'];
const reasons = [['NotAsDescribed', 'Sản phẩm không đúng mô tả'], ['Damaged', 'Sản phẩm bị hư hỏng'],
  ['MissingItems', 'Giao thiếu hàng'], ['Other', 'Khác']];
let context;
let eligible = [];
let selectedId;
let files = [];
let busy = false;
let previews = [];
const isVideo = file => /\.mp4$/i.test(file.name);
const hasRequiredEvidence = () => files.some(isVideo) && files.some(file => !isVideo(file));

function title(value) {
  document.title = `${value} - AgriSustain`;
  document.getElementById('feedback-title').textContent = value;
  document.getElementById('feedback-breadcrumb-title').textContent = value;
}
function selectedItem() { return eligible.find(item => String(item.id) === selectedId); }
function section(heading, body, name = 'shield') {
  return `<section class="feedback-panel"><h2 class="feedback-section-title">${icon(name)}${heading}</h2>${body}</section>`;
}
function productSummary(item) {
  return `<div class="feedback-product">${productImage(item.imageUrl, item.name)}<div class="feedback-product-copy">
    <h2>${esc(item.name)}</h2><p class="feedback-muted">${esc(item.category)}</p>
    <p class="feedback-farm">${icon('shield')}${esc(item.farmName)}${item.isFarmVerified ? '<span aria-label="Nhà cung cấp đã xác thực">✓</span>' : ''}</p>
    <dl class="feedback-metadata"><dt>Mã đơn hàng</dt><dd>#${esc(context.orderCode)}</dd>
    <dt>Trạng thái đơn hàng</dt><dd><span class="feedback-badge">Hoàn thành</span></dd>
    <dt>Ngày mua</dt><dd>${esc(localTime(context.orderDate))}</dd><dt>Số lượng</dt><dd>${esc(item.quantity)} ${esc(item.unit)}</dd></dl>
    </div></div>`;
}
function sidebar(item) {
  const order = `<dl class="feedback-metadata"><dt>Mã đơn hàng</dt><dd>#${esc(context.orderCode)}</dd>
    <dt>Ngày đặt</dt><dd>${esc(localTime(context.orderDate))}</dd><dt>Trạng thái</dt><dd><span class="feedback-badge">${context.status === 'Completed' ? 'Hoàn thành' : 'Đã giao hàng'}</span></dd>
    <dt>Tổng tiền</dt><dd class="feedback-price">${money(context.total)}</dd></dl>`;
  const tips = `<ul class="feedback-tips"><li>${icon('shield')}Chỉ đánh giá sản phẩm trong đơn hàng đã hoàn tất và đã nhận.</li>
    <li>${icon('card')}Mỗi dòng hàng có một đánh giá. Bạn có thể sửa đánh giá của mình.</li>
    <li>${icon('check')}Nội dung trung thực, lịch sự và liên quan đến sản phẩm.</li></ul>`;
  const receiver = `<div class="feedback-recipient"><strong>${esc(context.recipientName)}</strong><p>${esc(context.recipientPhone)}</p><p>${esc(context.address)}</p></div>`;
  const mini = `<p class="feedback-muted">Mã đơn hàng</p><strong>#${esc(context.orderCode)}</strong><p><span class="feedback-badge">Hoàn thành</span></p>
    ${item ? `<div class="feedback-mini-product">${productImage(item.imageUrl, item.name)}<div><strong>${esc(item.name)}</strong><p>${esc(item.quantity)} ${esc(item.unit)}</p><span>${money(item.total)}</span></div></div>` : ''}`;
  return `<aside class="feedback-sidebar">${section(complaint ? 'Thông tin đơn hàng' : 'Lưu ý khi đánh giá', complaint ? order : tips)}
    ${section(complaint ? 'Thông tin người nhận' : 'Đơn hàng của bạn', complaint ? receiver : mini, complaint ? 'user' : 'cart')}
    ${complaint ? section('Hỗ trợ nhanh', '<p class="feedback-muted">Cần trợ giúp? Hãy liên hệ và cung cấp mã đơn hàng.</p><a class="feedback-support" href="mailto:support@agrisustain.vn">Liên hệ hỗ trợ</a>', 'user') : ''}
    <div class="feedback-assurance">${icon('shield')}<p>${complaint ? 'Vui lòng gửi yêu cầu trong vòng 48 giờ sau khi giao và đính kèm cả ảnh lẫn video thực tế.'
      : 'Đánh giá của bạn giúp người mua lựa chọn tốt hơn và góp phần xây dựng cộng đồng nông sản minh bạch.'}</p></div></aside>`;
}
function actions(label) {
  return `<div class="feedback-actions"><a class="feedback-cancel" href="orders.html">Hủy</a><button id="feedback-submit" class="feedback-primary" type="submit">${label}</button></div>`;
}
function render() {
  const item = selectedItem();
  const editing = Boolean(item?.canEditReview);
  title(complaint ? 'Gửi khiếu nại đơn hàng' : editing ? 'Sửa đánh giá sản phẩm' : 'Đánh giá sản phẩm');
  document.getElementById('feedback-subtitle').textContent = complaint
    ? 'Nếu bạn gặp vấn đề với sản phẩm hoặc đơn hàng, vui lòng gửi khiếu nại để được hỗ trợ.'
    : 'Chia sẻ trải nghiệm của bạn để giúp người mua khác lựa chọn tốt hơn.';
  if (!eligible.length) {
    content.innerHTML = `<section class="feedback-panel feedback-empty"><h2>${complaint ? 'Đơn hàng chưa đủ điều kiện gửi khiếu nại' : 'Không còn sản phẩm đủ điều kiện đánh giá'}</h2>
      <p>${complaint ? 'Chỉ có thể gửi khiếu nại khi đơn ở trạng thái Đã giao hoặc Hoàn thành, trong vòng 48 giờ kể từ lúc giao hàng và chưa có khiếu nại đang xử lý.' : 'Bạn có thể gửi hoặc sửa đánh giá của mình cho sản phẩm trong đơn đã hoàn thành và đã nhận.'}</p>
      <a href="orders.html">Trở về đơn hàng</a></section>`;
    return;
  }
  const productSelect = eligible.length > 1 ? `<label class="feedback-field">Sản phẩm đánh giá<select id="feedback-item">${eligible.map(p => `<option value="${p.id}" ${p.id === item?.id ? 'selected' : ''}>${esc(p.name)} · ${esc(p.sellerOrderCode)}${p.canEditReview ? ' · Đã đánh giá' : ''}</option>`).join('')}</select></label>` : '';
  const review = !complaint ? `${productSelect}${productSummary(item)}<div class="feedback-review-fields">
    <fieldset class="feedback-rating"><legend>★ Mức độ hài lòng của bạn <span>*</span></legend><div class="feedback-stars">${ratings.map((label, index) => `<label>
      <input type="radio" name="rating" value="${index + 1}" ${item.review?.rating === index + 1 ? 'checked' : ''} required aria-label="${index + 1} sao – ${label}"><span aria-hidden="true">☆</span></label>`).join('')}</div><p id="feedback-rating-caption" class="feedback-muted"></p></fieldset>
    <label class="feedback-field">Nhận xét của bạn <span>*</span><textarea name="comment" required maxlength="1000" rows="5" placeholder="Chia sẻ trải nghiệm thực tế về sản phẩm…">${esc(item.review?.comment || '')}</textarea></label>
    <p class="feedback-counter"><span id="feedback-length">${(item.review?.comment || '').length}</span>/1.000 ký tự</p>${actions(editing ? 'Lưu thay đổi' : '★ Gửi đánh giá')}</div>` : '';
  const products = complaint ? `<p class="feedback-order-caption">Đặt ngày ${esc(localTime(context.orderDate))}<span class="feedback-badge">${context.status === 'Completed' ? 'Hoàn thành' : 'Đã giao hàng'}</span></p>
    <fieldset class="feedback-products"><legend>Chọn sản phẩm cần khiếu nại <span>*</span></legend><div class="feedback-product-selection"><p>Bạn có thể chọn nhiều sản phẩm để gửi trong một lần.</p><button id="feedback-select-all" type="button">Chọn tất cả</button></div>
    <div class="feedback-product-options">${eligible.map((p, index) => `<label class="feedback-product-option ${index === 0 ? 'selected' : ''}"><input type="checkbox" name="orderItemIds" value="${p.id}" ${index === 0 ? 'checked' : ''} aria-label="Khiếu nại ${esc(p.name)} từ ${esc(p.farmName)}">${productImage(p.imageUrl, p.name)}<span><strong>${esc(p.name)}</strong><small>${esc(p.farmName)}</small><small>${money(p.price)} / ${esc(p.unit)}</small></span></label>`).join('')}</div><p id="feedback-selection-count" class="feedback-selection-count" role="status"></p></fieldset>
    <div class="feedback-total"><span>Tổng tiền đơn hàng</span><strong>${money(context.total)}</strong></div>` : '';
  const complaintForm = complaint ? section('Thông tin khiếu nại', `<fieldset class="feedback-reasons"><legend>Lý do khiếu nại <span>*</span></legend><div>${reasons.map(([code, label]) => `<label><input type="radio" name="reason" value="${code}" required>${label}</label>`).join('')}</div></fieldset>
    <label class="feedback-field">Mô tả chi tiết vấn đề <span>*</span><textarea name="description" required maxlength="500" rows="5" placeholder="Vui lòng mô tả rõ vấn đề bạn gặp phải: bao bì, chất lượng hoặc số lượng…"></textarea></label>
    <p class="feedback-counter"><span id="feedback-length">0</span>/500 ký tự</p><section class="feedback-attachments" aria-label="Ảnh và video đính kèm"><div class="feedback-upload-copy">${icon('grid')}<div><strong>Ảnh và video bắt buộc</strong><span id="feedback-evidence-hint">Chọn từ 1 đến 4 ảnh JPG/PNG (tối đa 5 MB mỗi ảnh) và 1 video MP4 (tối đa 20 MB).</span></div></div>
    <div id="feedback-previews" class="feedback-previews"></div><button class="feedback-upload-button" id="feedback-add-file" type="button" aria-describedby="feedback-evidence-hint">＋ Thêm ảnh và video</button><input type="file" id="feedback-files" accept=".jpg,.jpeg,.png,.mp4" multiple hidden></section>${actions('Gửi khiếu nại')}`) : '';
  content.innerHTML = `<div class="feedback-layout"><form id="feedback-form" class="feedback-main"><fieldset id="feedback-fields">${section(complaint ? `Đơn hàng #${esc(context.orderCode)}` : 'Thông tin sản phẩm', products + review, complaint ? 'card' : 'leaf')}${complaintForm}</fieldset></form>${sidebar(item)}</div>`;
  const form = document.getElementById('feedback-form');
  bindImageFallback(content);
  document.getElementById('feedback-item')?.addEventListener('change', event => {
    selectedId = event.target.value;
    showNotice(notice, '');
    render();
  });
  form.addEventListener('change', () => complaint ? updateSelection() : updateRating());
  form.addEventListener('input', event => {
    if (['description', 'comment'].includes(event.target.name)) document.getElementById('feedback-length').textContent = event.target.value.length;
  });
  document.getElementById('feedback-select-all')?.addEventListener('click', () => {
    const boxes = [...form.querySelectorAll('[name="orderItemIds"]')];
    const selected = boxes.every(box => box.checked);
    boxes.forEach(box => { box.checked = !selected; });
    updateSelection();
  });
  document.getElementById('feedback-add-file')?.addEventListener('click', () => document.getElementById('feedback-files').click());
  document.getElementById('feedback-files')?.addEventListener('change', event => {
    const added = [...event.target.files];
    event.target.value = '';
    const selection = [...files, ...added];
    const videoCount = selection.filter(isVideo).length;
    if (selection.length - videoCount > 4 || videoCount > 1
      || added.some(file => !/\.(jpe?g|png|mp4)$/i.test(file.name)
        || file.size <= 0 || file.size > (isVideo(file) ? 20 : 5) * 1024 * 1024)) {
      showNotice(notice, 'Vui lòng chọn tối đa 4 ảnh JPG/PNG, mỗi ảnh không quá 5 MB, và 1 video MP4 không quá 20 MB.');
      return;
    }
    files.push(...added);
    showNotice(notice, '');
    renderPreviews();
  });
  form.addEventListener('submit', submit);
  complaint ? updateSelection() : updateRating();
}
function updateRating() {
  const rating = Number(document.querySelector('[name="rating"]:checked')?.value || 0);
  document.querySelectorAll('.feedback-stars label').forEach((label, index) => {
    label.classList.toggle('selected', index < rating);
    label.querySelector('span').textContent = index < rating ? '★' : '☆';
  });
  document.getElementById('feedback-rating-caption').textContent = rating ? `${rating}/5 – ${ratings[rating - 1]}` : 'Chọn số sao để đánh giá';
}
function updateSelection() {
  const boxes = [...document.querySelectorAll('[name="orderItemIds"]')];
  boxes.forEach(box => box.closest('label').classList.toggle('selected', box.checked));
  const count = boxes.filter(box => box.checked).length;
  document.getElementById('feedback-selection-count').textContent = `Đã chọn ${count} / ${boxes.length} sản phẩm`;
  document.getElementById('feedback-select-all').textContent = count === boxes.length ? 'Bỏ chọn tất cả' : 'Chọn tất cả';
  document.getElementById('feedback-submit').disabled = !count || !hasRequiredEvidence();
}
function clearPreviews() { previews.forEach(url => URL.revokeObjectURL(url)); previews = []; }
function renderPreviews() {
  clearPreviews();
  const container = document.getElementById('feedback-previews');
  container.innerHTML = files.map((file, index) => {
    const url = URL.createObjectURL(file);
    previews.push(url);
    const name = esc(file.name);
    const media = isVideo(file)
      ? `<video src="${url}" controls playsinline preload="metadata" aria-label="${name}"></video>`
      : `<img src="${url}" alt="${name}">`;
    return `<div class="feedback-preview">${media}<button type="button" data-remove="${index}" aria-label="Xóa tệp ${name}">×</button></div>`;
  }).join('');
  container.querySelectorAll('[data-remove]').forEach(button => button.addEventListener('click', () => {
    files.splice(Number(button.dataset.remove), 1);
    renderPreviews();
  }));
  updateSelection();
}
async function submit(event) {
  event.preventDefault();
  if (busy) return;
  const form = event.currentTarget;
  const body = new FormData(form);
  const text = String(body.get(complaint ? 'description' : 'comment') || '').trim();
  if (!text || (complaint && !body.getAll('orderItemIds').length)) {
    showNotice(notice, complaint ? 'Vui lòng chọn ít nhất một sản phẩm và mô tả vấn đề.' : 'Vui lòng nhập nhận xét của bạn.');
    return;
  }
  body.set(complaint ? 'description' : 'comment', text);
  if (complaint) {
    if (!hasRequiredEvidence()) {
      showNotice(notice, 'Vui lòng đính kèm ít nhất một ảnh JPG/PNG và một video MP4.');
      return;
    }
    files.forEach(file => body.append('files', file));
  }
  const item = selectedItem();
  busy = true;
  document.getElementById('feedback-fields').disabled = true;
  document.getElementById('feedback-submit').textContent = 'Đang lưu…';
  showNotice(notice, '');
  try {
    const response = await request(complaint ? `/orders/${orderId}/complaints` : `/orders/${orderId}/reviews/${item.id}`, {
      method: !complaint && item.canEditReview ? 'PUT' : 'POST', body,
    });
    clearPreviews(); files = [];
    content.innerHTML = `<section class="feedback-panel feedback-success" role="status">${icon('shield')}<h2>${complaint ? 'Đã nhận khiếu nại' : 'Đã lưu đánh giá'}</h2><p>${esc(cleanMessage(response.message))}</p><a href="orders.html" class="feedback-support">Trở về đơn hàng</a></section>`;
  } catch (error) {
    showNotice(notice, Object.values(error.fields || {}).flat().join(' ') || error.message);
    if (error.status === 409) {
      showNotice(notice, `${error.message} Vui lòng tải lại để kiểm tra điều kiện mới nhất.`);
    }
  } finally {
    busy = false;
    const fields = document.getElementById('feedback-fields');
    if (fields) { fields.disabled = false; document.getElementById('feedback-submit').textContent = complaint ? 'Gửi khiếu nại' : item.canEditReview ? 'Lưu thay đổi' : '★ Gửi đánh giá'; }
  }
}
async function load() {
  if (!Auth.requireAuth('RetailCustomer')) return;
  try {
    if (!/^\d+$/.test(orderId || '')) throw new Error('Vui lòng chọn một đơn hàng để tiếp tục.');
    context = await request(`/orders/${orderId}/feedback`);
    eligible = context.items.filter(item => complaint ? item.canComplain : item.canReview || item.canEditReview);
    selectedId = String(eligible[0]?.id || '');
    render();
  } catch (error) {
    showNotice(notice, error.message);
    content.innerHTML = '<section class="feedback-panel feedback-empty"><a href="orders.html">Trở về đơn hàng</a><button id="feedback-retry" type="button">Thử lại</button></section>';
    document.getElementById('feedback-retry').addEventListener('click', load);
  }
}
window.addEventListener('pagehide', clearPreviews);
document.querySelector('.order-feedback').addEventListener('click', event => {
  if (busy && event.target.closest('a')) event.preventDefault();
});
load();
