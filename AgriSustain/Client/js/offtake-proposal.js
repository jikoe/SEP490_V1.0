import { esc, money, request } from './page-ui.js';
import { byId, date, dateRange, detailMarkup, facts, hasBuyerParty, loadBuyerParty, notice, number, termsMarkup, thumbnail } from './offtake-ui.js';
import { contractArticles, readContractTerms, writeContractTerms } from './offtake-terms.js';

const form = byId('proposal-form');
const message = byId('proposal-notice');
const farmer = byId('proposal-farmer');
const product = byId('proposal-product');
const unit = byId('proposal-unit');
const quantity = form.elements.namedItem('quantity');
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
let buyerParty;
let catalog = [];
let selected;
let step = 0;
let submittedBody;
let requestId;

function renderArticleInputs() {
  const extraFields = {
    deliveryTerms: '<label>Ngày giao hàng dự kiến <input name="deliveryDate" type="date" required></label>',
    paymentTerms: '<label>Tiền cọc dự kiến (VND) <input name="depositAmount" type="number" min="0" step="0.01" value="0" required></label>',
    article7: '<div class="offtake-fields"><label>Hiệu lực từ ngày <input name="validFrom" type="date" required></label>'
      + '<label>Hiệu lực đến ngày <input name="validUntil" type="date" required></label></div>',
  };
  byId('proposal-articles').innerHTML = contractArticles.map(([title, field, hint]) => `
    <fieldset class="offtake-article-input">
      <legend>${esc(title)}</legend>
      ${extraFields[field] ?? ''}
      <label>Nội dung <textarea name="${field}" rows="3" maxlength="${field === 'deliveryTerms' ? 2000 : 1000}"
        placeholder="${esc(hint)}" required></textarea></label>
    </fieldset>`).join('');
}

async function showBuyerParty() {
  const note = byId('proposal-buyer-note');
  try {
    buyerParty = await loadBuyerParty();
    byId('proposal-buyer').innerHTML = facts([
      ['Tên doanh nghiệp', buyerParty.name], ['Người đại diện', buyerParty.representative],
      ['Địa chỉ', buyerParty.address], ['Mã số thuế', buyerParty.taxCode],
    ]);
    notice(note, hasBuyerParty(buyerParty) ? '' : 'Vui lòng bổ sung đủ thông tin Bên B trong hồ sơ doanh nghiệp.', true);
  } catch (error) {
    notice(note, 'Không thể tải hồ sơ Bên B. ' + error.message, true);
  }
}

async function loadCatalog() {
  if (search.disabled) return;
  search.disabled = true;
  notice(message, 'Đang tải sản phẩm…');
  try {
    const keyword = byId('catalog-keyword').value.trim();
    const data = await request('/b2b/contracts/catalog?' + new URLSearchParams({ keyword }));
    const previous = selected;
    const previousUnit = unit.value;
    catalog = previous && !data.products.some(item => item.id === previous.id)
      ? [previous, ...data.products] : data.products;
    product.replaceChildren(new Option('Chọn sản phẩm và nhà cung cấp', ''));
    catalog.forEach(item => product.add(new Option(item.name + ' — ' + item.farmer, item.id)));
    if (previous) product.value = previous.id;
    updateSummary(previousUnit);
    if (data.supportEmail && /^[^\s@?]+@[^\s@?]+\.[^\s@?]+$/.test(data.supportEmail)) {
      byId('proposal-support-link').href = 'mailto:' + data.supportEmail;
      byId('proposal-support').hidden = false;
    }
    form.hidden = false;
    notice(message, data.products.length ? '' : 'Không tìm thấy sản phẩm phù hợp.');
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

function quantityFactor() {
  return Number(unit.selectedOptions[0]?.dataset.factor || 1);
}

function updateQuantityStep() {
  // The API stores quantities to three decimal places in the product unit.
  quantity.min = quantity.step = Number((0.001 / quantityFactor()).toPrecision(1));
}

function updateUnits(preferred) {
  unit.replaceChildren();
  const options = selected ? selected.quantityUnits ?? [{ unit: selected.unit, factor: 1 }] : [];
  options.forEach(choice => {
    const option = new Option(choice.unit, choice.unit);
    option.dataset.factor = choice.factor;
    unit.add(option);
  });
  unit.disabled = !selected;
  quantity.disabled = !selected;
  unit.value = options.some(choice => choice.unit === preferred) ? preferred : selected?.unit ?? '';
  updateQuantityStep();
}

function updateSummary(preferredUnit) {
  const current = catalog.find(item => String(item.id) === product.value);
  if (current?.id !== selected?.id || current?.unit !== selected?.unit) {
    quantity.value = '';
    form.elements.namedItem('unitPrice').value = '';
  }
  selected = current;
  updateUnits(preferredUnit);
  farmer.value = selected?.farmer ?? '';
  const card = byId('proposal-product-card');
  card.hidden = !selected;
  card.innerHTML = selected ? thumbnail(selected.imageUrl) + '<div><strong>' + esc(selected.name)
    + '</strong><small>' + esc(selected.category) + ' · Đơn vị ' + esc(selected.unit) + '</small></div>' : '';
  byId('proposal-price-unit').textContent = selected ? '(VND/' + selected.unit + ')' : '(VND)';
  byId('proposal-summary').innerHTML = facts([
    ['Mã hợp đồng', byId('proposal-code').value], ['Loại', 'Hợp đồng Offtake'],
    ['Nhà cung cấp', selected?.farmer || 'Chưa chọn'],
    ['Sản phẩm', selected?.name || 'Chưa chọn'],
  ]);
}

function showStep(index, focus = true) {
  step = index;
  panels.forEach((panel, i) => { panel.hidden = i !== step; });
  steps.forEach((item, i) => {
    if (i === step) item.setAttribute('aria-current', 'step');
    else item.removeAttribute('aria-current');
  });
  next.textContent = step === 2 ? 'Xác nhận và gửi' : 'Tiếp theo →';
  save.hidden = step !== 2;
  back.hidden = step === 0;
  if (focus) panels[step].querySelector('h2').focus();
}

function readProposal() {
  const values = Object.fromEntries(new FormData(form));
  return {
    farmerId: selected.farmerId, productId: selected.id,
    quantity: Number((Number(values.quantity) * quantityFactor()).toFixed(3)),
    ...(unit.value !== selected.unit && { quantityUnit: unit.value }),
    unitPrice: Number(values.unitPrice),
    validFrom: values.validFrom, validUntil: values.validUntil, deliveryDate: values.deliveryDate,
    deliveryTerms: values.deliveryTerms.trim() ? writeContractTerms(values) : '',
    paymentTerms: values.paymentTerms.trim(),
    depositAmount: Number(values.depositAmount),
  };
}

function missingArticle() {
  return contractArticles.find(([, field]) => !form.elements.namedItem(field).value.trim());
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
    if (!hasBuyerParty(buyerParty)) {
      notice(message, 'Cần có đầy đủ thông tin Bên B trong hồ sơ doanh nghiệp.', true);
      return;
    }
    showStep(1);
    return;
  }
  const body = readProposal();
  if (step === 1) {
    const empty = missingArticle();
    if (empty) {
      notice(message, 'Vui lòng nhập nội dung cho ' + empty[0] + '.', true);
      form.elements.namedItem(empty[1]).focus();
      return;
    }
    if (body.deliveryTerms.length > 10000) {
      notice(message, 'Tổng nội dung các điều khoản vượt quá 10.000 ký tự. Vui lòng rút gọn.', true);
      return;
    }
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
  const baseQuantity = number(body.quantity) + ' ' + selected.unit;
  byId('proposal-review').innerHTML = facts([
    ['Bên mua', buyerParty?.name], ['Nhà cung cấp', selected.farmer], ['Sản phẩm', selected.name],
    ['Số lượng', body.quantityUnit ? quantity.value + ' ' + body.quantityUnit + ' (' + baseQuantity + ')' : baseQuantity],
    ['Đơn giá', money(body.unitPrice) + '/' + selected.unit],
  ]);
  byId('proposal-review-terms').innerHTML = termsMarkup(body.deliveryTerms, body.paymentTerms, true, {
    deliveryTerms: [['Ngày giao dự kiến', date(body.deliveryDate)]],
    paymentTerms: [['Tiền cọc', money(body.depositAmount)]],
    article7: [['Hiệu lực', dateRange(body.validFrom, body.validUntil)]],
  });
  showStep(2);
}

async function saveProposal(body, asDraft) {
  if (!hasBuyerParty(buyerParty) || missingArticle()) {
    notice(message, 'Vui lòng kiểm tra thông tin Bên B và nhập đủ cả 8 điều khoản.', true);
    return;
  }
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
    updateUnits(draft.proposal.quantityUnit);
    const terms = readContractTerms(draft.proposal.deliveryTerms, draft.proposal.paymentTerms);
    for (const [name, value] of Object.entries({ ...draft.proposal, ...terms })) {
      const field = form.elements.namedItem(name);
      if (field) field.value = value ?? '';
    }
    quantity.value = Number((draft.proposal.quantity / quantityFactor()).toPrecision(15));
    if (await loadCatalog()) {
      if (!selected) {
        notice(message, 'Sản phẩm cũ không còn khả dụng. Vui lòng chọn lại sản phẩm.', true);
      } else if (params.has('review')) {
        byId('proposal-title').textContent = 'Gửi phê duyệt đề xuất Offtake';
        if (missingArticle()) {
          showStep(1);
          notice(message, 'Bản nháp chưa có đủ 8 điều khoản. Vui lòng bổ sung trước khi gửi.', true);
        } else {
          showReview(readProposal());
        }
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
    renderArticleInputs();
    showBuyerParty();
    byId('proposal-today').value = new Intl.DateTimeFormat('vi-VN', {
      dateStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh',
    }).format(new Date());
    product.addEventListener('change', () => updateSummary());
    unit.addEventListener('change', () => { quantity.value = ''; updateQuantityStep(); });
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
