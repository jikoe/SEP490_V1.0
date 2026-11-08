import { esc, money, request } from './page-ui.js';
import { byId, date, dateRange, detailMarkup, facts, notice, thumbnail } from './offtake-ui.js';

const form = byId('proposal-form');
const message = byId('proposal-notice');
const farmer = byId('proposal-farmer');
const product = byId('proposal-product');
const next = byId('proposal-next');
const back = byId('proposal-back');
const save = byId('proposal-save');
const search = byId('catalog-search');
const panels = [...form.querySelectorAll('[data-step]')];
const steps = [...document.querySelectorAll('.offtake-steps li')];
const params = new URLSearchParams(location.search);
const revision = params.get('revision');
const edit = params.get('edit');
let draft;
let catalog = [];
let selected;
let step = 0;
let submittedBody;
let requestId;

async function loadCatalog() {
  search.disabled = true;
  notice(message, 'Đang tải nhà cung cấp và sản phẩm…');
  try {
    const keyword = byId('catalog-keyword').value.trim();
    const data = await request('/b2b/contracts/catalog?' + new URLSearchParams({ keyword }));
    const previous = selected;
    catalog = previous && !data.products.some(item => item.id === previous.id)
      ? [previous, ...data.products] : data.products;
    const suppliers = [...new Map(catalog.map(item => [item.farmerId, item.farmer]))];
    farmer.replaceChildren(new Option('Chọn nhà cung cấp', ''));
    suppliers.forEach(([id, name]) => farmer.add(new Option(name, id)));
    if (previous) farmer.value = previous.farmerId;
    updateProducts(previous?.id);
    if (data.supportEmail && /^[^\s@?]+@[^\s@?]+\.[^\s@?]+$/.test(data.supportEmail)) {
      byId('proposal-support-link').href = 'mailto:' + data.supportEmail;
      byId('proposal-support').hidden = false;
    }
    form.hidden = false;
    notice(message, data.products.length ? '' : 'Không tìm thấy nhà cung cấp hoặc sản phẩm phù hợp.');
    return true;
  } catch (error) {
    notice(message, error.message, true);
    if (form.hidden && error.status !== 403 && !byId('catalog-retry')) {
      const retry = document.createElement('button');
      retry.id = 'catalog-retry';
      retry.textContent = 'Thử lại';
      retry.addEventListener('click', () => { retry.remove(); loadCatalog(); });
      message.after(retry);
    }
  } finally {
    search.disabled = false;
  }
}

function updateProducts(previousId) {
  product.replaceChildren(new Option('Chọn sản phẩm', ''));
  catalog.filter(item => String(item.farmerId) === farmer.value)
    .forEach(item => product.add(new Option(item.name, item.id)));
  if (previousId) product.value = previousId;
  updateSummary();
}

function updateSummary() {
  selected = catalog.find(item => String(item.id) === product.value);
  const card = byId('proposal-product-card');
  card.hidden = !selected;
  card.innerHTML = selected ? thumbnail(selected.imageUrl) + '<div><strong>' + esc(selected.name)
    + '</strong><small>' + esc(selected.category) + ' · Đơn vị ' + esc(selected.unit) + '</small></div>' : '';
  byId('proposal-unit').textContent = selected ? '(' + selected.unit + ')' : '';
  byId('proposal-price-unit').textContent = selected ? '(VND/' + selected.unit + ')' : '(VND)';
  byId('proposal-summary').innerHTML = facts([
    ['Mã hợp đồng', byId('proposal-code').value], ['Loại', 'Hợp đồng Offtake'],
    ['Nhà cung cấp', farmer.value ? farmer.selectedOptions[0].text : 'Chưa chọn'],
    ['Sản phẩm', selected?.name || 'Chưa chọn'], ['Trạng thái', 'Bản nháp'],
  ]);
}

function showStep(index, focus = true) {
  step = index;
  panels.forEach((panel, i) => { panel.hidden = i !== step; });
  steps.forEach((item, i) => {
    if (i === step) item.setAttribute('aria-current', 'step');
    else item.removeAttribute('aria-current');
  });
  byId('proposal-progress').innerHTML = steps.map((item, i) => '<li><b>'
    + esc(item.querySelector('b').textContent) + '</b><small>'
    + (i === step ? 'Đang thực hiện' : i < step ? 'Đã hoàn thành' : 'Chưa hoàn thành')
    + '</small></li>').join('');
  next.textContent = step === 2 ? 'Xác nhận và gửi' : 'Tiếp theo →';
  save.hidden = step !== 2;
  back.hidden = step === 0;
  if (focus) panels[step].querySelector('h2').focus();
}

function readProposal() {
  const values = Object.fromEntries(new FormData(form));
  return {
    farmerId: selected.farmerId, productId: selected.id,
    quantity: Number(values.quantity), unitPrice: Number(values.unitPrice),
    validFrom: values.validFrom, validUntil: values.validUntil, deliveryDate: values.deliveryDate,
    deliveryTerms: values.deliveryTerms.trim(), paymentTerms: values.paymentTerms.trim(),
    depositAmount: Number(values.depositAmount),
  };
}

async function advance(event) {
  event.preventDefault();
  notice(message, '');
  const invalid = [...panels[step].querySelectorAll('input, select, textarea')]
    .find(field => !field.checkValidity());
  if (invalid) {
    invalid.reportValidity();
    return;
  }
  if (step === 0) {
    showStep(1);
    return;
  }
  const body = readProposal();
  if (step === 1) {
    if (!body.deliveryTerms || body.deliveryDate < body.validFrom || body.deliveryDate > body.validUntil
      || body.depositAmount > body.quantity * body.unitPrice) {
      notice(message, 'Kiểm tra điều khoản, ngày giao trong thời hạn hiệu lực và số tiền cọc.', true);
      return;
    }
    showReview(body);
    return;
  }

  await saveProposal(body, false);
}

function showReview(body) {
  byId('proposal-review').innerHTML = facts([
    ['Nhà cung cấp', selected.farmer], ['Sản phẩm', selected.name],
    ['Số lượng', body.quantity + ' ' + selected.unit], ['Đơn giá', money(body.unitPrice)],
    ['Hiệu lực', dateRange(body.validFrom, body.validUntil)], ['Ngày giao dự kiến', date(body.deliveryDate)],
    ['Điều khoản giao hàng', body.deliveryTerms], ['Thanh toán', body.paymentTerms || '—'],
    ['Tiền cọc', money(body.depositAmount)],
  ]);
  showStep(2);
}

async function saveProposal(body, asDraft) {
  const serialized = JSON.stringify(body);
  if (submittedBody !== serialized) {
    requestId = draft?.proposal.requestId ?? crypto.randomUUID();
    submittedBody = serialized;
  }
  next.disabled = true;
  back.disabled = true;
  save.disabled = true;
  notice(message, asDraft ? 'Đang lưu bản nháp…' : 'Đang gửi đề xuất…');
  try {
    const newProposalUrl = asDraft ? '/b2b/contracts/drafts' : '/b2b/contracts';
    const url = draft
      ? '/b2b/contracts/' + edit + '/draft/' + draft.version + '?submit=' + !asDraft
      : newProposalUrl;
    const result = await request(url, { method: draft ? 'PUT' : 'POST', body: { ...body, requestId } });
    if (!Number.isSafeInteger(result?.id) || result.id <= 0) {
      notice(message, 'API chưa xác nhận đã lưu. Vui lòng kiểm tra lại danh sách hợp đồng.', true);
      return;
    }
    location.href = 'contracts.html?' + (asDraft ? 'saved' : 'submitted') + '=' + encodeURIComponent(result.id);
  } catch (error) {
    const draftRouteMissing = asDraft && !draft && [404, 405].includes(error.status);
    const errorMessage = draftRouteMissing
      ? 'API chưa có đường dẫn lưu bản nháp. Hãy khởi động lại API rồi thử lại.'
      : error.status >= 500 || !error.status
        ? 'Chưa thể lưu thay đổi. Vui lòng thử lại.' : error.message;
    notice(message, errorMessage, true);
  } finally {
    next.disabled = false;
    back.disabled = false;
    save.disabled = false;
  }
}

async function loadDraft() {
  notice(message, 'Đang tải bản nháp…');
  try {
    draft = await request('/b2b/contracts/' + edit + '/draft');
    byId('proposal-title').textContent = 'Chỉnh sửa đề xuất hợp đồng Offtake';
    byId('proposal-code').value = draft.code;
    byId('proposal-today').value = date(draft.createdAt);
    selected = draft.product;
    for (const [name, value] of Object.entries(draft.proposal)) {
      const field = form.elements.namedItem(name);
      if (field) field.value = value ?? '';
    }
    if (await loadCatalog()) {
      if (!selected) {
        notice(message, 'Sản phẩm cũ không còn khả dụng. Vui lòng chọn lại sản phẩm.', true);
      } else if (params.has('review')) {
        byId('proposal-title').textContent = 'Gửi phê duyệt đề xuất Offtake';
        showReview(readProposal());
      }
    }
  } catch (error) {
    notice(message, error.message, true);
  }
}

async function loadRevision(id) {
  byId('proposal-wizard').hidden = true;
  byId('proposal-title').textContent = 'Xem lại đề xuất sửa đổi';
  byId('proposal-description').textContent = 'Kiểm tra phiên bản hiện tại trước khi xác nhận.';
  notice(message, 'Đang tải điều khoản…');
  const panel = byId('revision-panel');
  try {
    const detail = await request('/b2b/contracts/' + id);
    panel.innerHTML = detailMarkup(detail);
    panel.hidden = false;
    notice(message, '');
    const action = document.createElement('div');
    action.className = 'offtake-actions';
    const leave = document.createElement('a');
    leave.href = 'contracts.html?record=' + id;
    leave.textContent = 'Quay lại theo dõi';
    action.append(leave);
    if (detail.canConfirmRevision) {
      const confirm = document.createElement('button');
      confirm.type = 'button';
      confirm.className = 'primary';
      confirm.textContent = 'Xác nhận phiên bản ' + detail.version;
      confirm.addEventListener('click', async () => {
        confirm.disabled = true;
        try {
          await request('/b2b/contracts/' + id + '/versions/' + detail.version + '/confirmation',
            { method: 'POST' });
          location.href = leave.href;
        } catch (error) {
          notice(message, error.message, true);
          confirm.disabled = false;
        }
      });
      action.append(confirm);
    }
    panel.append(action);
  } catch (error) {
    notice(message, error.message, true);
  }
}

if (Auth.requireAuth('WholesaleBuyer')) {
  if (revision && /^\d+$/.test(revision)) {
    loadRevision(revision);
  } else {
    byId('proposal-today').value = new Intl.DateTimeFormat('vi-VN', {
      dateStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh',
    }).format(new Date());
    farmer.addEventListener('change', () => updateProducts());
    product.addEventListener('change', updateSummary);
    search.addEventListener('click', loadCatalog);
    byId('catalog-keyword').addEventListener('keydown', event => {
      if (event.key === 'Enter') { event.preventDefault(); loadCatalog(); }
    });
    form.addEventListener('submit', advance);
    save.addEventListener('click', () => saveProposal(readProposal(), true));
    back.addEventListener('click', () => { notice(message, ''); showStep(step - 1); });
    showStep(0, false);
    if (edit && /^\d+$/.test(edit)) loadDraft();
    else loadCatalog();
  }
}
