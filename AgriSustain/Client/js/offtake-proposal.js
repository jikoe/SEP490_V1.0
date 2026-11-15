import { esc, money, request } from './page-ui.js';
import { badge, byId, date, dateRange, facts, hasBuyerParty, loadBuyerParty, notice, number, termsMarkup, thumbnail } from './offtake-ui.js';
import { readContractTerms, writeContractTerms } from './offtake-terms.js';

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
const articles = byId('proposal-articles');
const addArticle = byId('proposal-add-article');
const panels = [...form.querySelectorAll('[data-step]')];
const steps = [...document.querySelectorAll('.offtake-steps li')];
const params = new URLSearchParams(location.search);
const edit = params.get('edit') || params.get('revision');
let draft;
let editingRevision = false;
let buyerParty;
let catalog = [];
let selected;
let step = 0;
let submittedBody;
let requestId;

function appendArticle({ title = '', body = '' } = {}) {
  articles.insertAdjacentHTML('beforeend', `
    <fieldset class="offtake-article-input">
      <legend>Điều ${articles.childElementCount + 1}</legend>
      <label>Tiêu đề <input name="articleTitle" type="text" maxlength="10000" required></label>
      <label>Nội dung <textarea name="articleBody" rows="3" maxlength="10000" required></textarea></label>
      <button type="button" data-remove-article>Xóa điều khoản</button>
    </fieldset>`);
  const article = articles.lastElementChild;
  article.querySelector('input').value = title;
  article.querySelector('textarea').value = body;
  return article;
}

function numberArticles() {
  [...articles.children].forEach((article, index) => {
    article.querySelector('legend').textContent = 'Điều ' + (index + 1);
    const remove = article.querySelector('[data-remove-article]');
    remove.hidden = articles.childElementCount === 1;
    remove.setAttribute('aria-label', 'Xóa điều ' + (index + 1));
  });
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
  next.textContent = step === 2
    ? (editingRevision ? 'Xác nhận và gửi lại' : 'Xác nhận và gửi') : 'Tiếp theo →';
  save.hidden = editingRevision || step !== 2;
  back.hidden = step === 0;
  if (focus) panels[step].querySelector('h2').focus();
}

function readProposal() {
  const fields = new FormData(form);
  const values = Object.fromEntries(fields);
  const bodies = fields.getAll('articleBody');
  return {
    farmerId: selected.farmerId, productId: selected.id,
    quantity: Number((Number(values.quantity) * quantityFactor()).toFixed(3)),
    ...(unit.value !== selected.unit && { quantityUnit: unit.value }),
    unitPrice: Number(values.unitPrice),
    validFrom: values.validFrom, validUntil: values.validUntil, deliveryDate: values.deliveryDate,
    deliveryTerms: writeContractTerms(fields.getAll('articleTitle').map((title, index) => ({ title, body: bodies[index] }))),
    paymentTerms: null,
    depositAmount: Number(values.depositAmount),
  };
}

function missingArticle() {
  return [...articles.querySelectorAll('input, textarea')].find(field => !field.value.trim());
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
      notice(message, 'Vui lòng nhập tiêu đề và nội dung cho từng điều khoản.', true);
      empty.focus();
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
    ['Ngày giao dự kiến', date(body.deliveryDate)], ['Tiền cọc', money(body.depositAmount)],
    ['Hiệu lực', dateRange(body.validFrom, body.validUntil)],
  ]);
  byId('proposal-review-terms').innerHTML = termsMarkup(body.deliveryTerms, body.paymentTerms);
  showStep(2);
}

async function saveProposal(body, asDraft) {
  if (!hasBuyerParty(buyerParty) || missingArticle()) {
    notice(message, 'Vui lòng kiểm tra thông tin Bên B và điền đầy đủ các điều khoản.', true);
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
    const outcome = editingRevision ? 'resent' : asDraft ? 'saved' : 'submitted';
    location.href = 'contracts.html?' + outcome + '=' + encodeURIComponent(result.id);
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
  notice(message, 'Đang tải đề xuất…');
  try {
    draft = await request('/b2b/contracts/' + edit + '/draft');
    editingRevision = draft.revisionChanges != null;
    byId('proposal-title').textContent = 'Chỉnh sửa đề xuất hợp đồng Offtake';
    byId('proposal-code').value = draft.code;
    byId('proposal-today').value = date(draft.createdAt);
    selected = draft.product;
    updateUnits(draft.proposal.quantityUnit);
    articles.replaceChildren();
    for (const article of readContractTerms(draft.proposal.deliveryTerms, draft.proposal.paymentTerms)) appendArticle(article);
    if (!articles.childElementCount) appendArticle({ title: 'Nội dung hợp đồng' });
    numberArticles();
    for (const [name, value] of Object.entries(draft.proposal)) {
      const field = form.elements.namedItem(name);
      if (field) field.value = value ?? '';
    }
    quantity.value = Number((draft.proposal.quantity / quantityFactor()).toPrecision(15));
    if (editingRevision) {
      byId('proposal-title').textContent = 'Chỉnh sửa và gửi lại đề xuất Offtake';
      byId('proposal-description').textContent = 'Cập nhật điều khoản theo yêu cầu của nông dân rồi gửi lại để nông dân xác nhận.';
      byId('proposal-status').innerHTML = 'Trạng thái hợp đồng ' + badge('Negotiating');
      byId('proposal-amendment-details').innerHTML = facts([
        ...(draft.revisionReason ? [['Lý do', draft.revisionReason]] : []),
        ['Nội dung đề nghị', draft.revisionChanges],
      ]);
      byId('proposal-amendment').hidden = false;
      byId('proposal-cancel').href = 'contracts.html?record=' + edit;
      byId('proposal-send-note').textContent = 'Nông dân sẽ nhận phiên bản mới để xem và xác nhận.';
      byId('catalog-picker').hidden = true;
      catalog = [draft.product];
      product.add(new Option(draft.product.name + ' — ' + draft.product.farmer, draft.product.id));
      product.disabled = true;
      updateSummary(draft.proposal.quantityUnit);
      form.hidden = false;
      notice(message, '');
      return;
    }
    if (await loadCatalog()) {
      if (!selected) {
        notice(message, 'Sản phẩm cũ không còn khả dụng. Vui lòng chọn lại sản phẩm.', true);
      } else if (params.has('review')) {
        byId('proposal-title').textContent = 'Gửi phê duyệt đề xuất Offtake';
        if (missingArticle() || [...panels[1].querySelectorAll('input, textarea')].some(field => !field.checkValidity())
          || readProposal().deliveryTerms.length > 10000) {
          showStep(1);
          notice(message, 'Vui lòng kiểm tra điều khoản và thông tin giao hàng, hiệu lực, đặt cọc trước khi gửi.', true);
        } else {
          showReview(readProposal());
        }
      }
    }
  } catch (error) {
    notice(message, error.message, true);
  }
}

if (Auth.requireAuth('WholesaleBuyer')) {
  appendArticle({ title: 'Nội dung hợp đồng' });
  numberArticles();
  addArticle.addEventListener('click', () => {
    const article = appendArticle();
    numberArticles();
    article.querySelector('input').focus();
  });
  articles.addEventListener('click', event => {
    const button = event.target.closest('[data-remove-article]');
    if (!button || articles.childElementCount === 1) return;
    const article = button.closest('fieldset');
    const nearby = article.nextElementSibling || article.previousElementSibling;
    article.remove();
    numberArticles();
    nearby.querySelector('input').focus();
  });
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
