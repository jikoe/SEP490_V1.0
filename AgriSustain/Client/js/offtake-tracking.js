import { esc, money, request } from './page-ui.js';
import { badge, byId, date, dateRange, detailMarkup, facts, hasBuyerParty, loadBuyerParty, notice, number, statusName, thumbnail } from './offtake-ui.js';

const params = new URLSearchParams(location.search);
const path = '/b2b/contracts';
const filters = byId('tracking-filters');
const results = byId('tracking-result');
const retry = byId('tracking-retry');
const exportButton = byId('export-list');
const pageSize = byId('tracking-page-size');
const dialog = byId('tracking-detail');
const detailContent = byId('tracking-detail-content');
const downloadButton = byId('download-offtake');
const pdfNotice = byId('tracking-pdf-notice');
const editRevisionLink = byId('edit-offtake-revision');
const depositButton = byId('pay-offtake-deposit');
const depositPanel = byId('tracking-deposit');
const list = byId('tracking-list');
const pagination = byId('tracking-pagination');
const cancelDialog = byId('contract-cancel');
const cancelConfirm = byId('contract-cancel-confirm');
const cancelBack = byId('contract-cancel-back');
const cancelNotice = byId('contract-cancel-notice');
let query = {};
let page = 1;
let pending;
let pendingDetail;
let contractDetail;
let detailId;
let cancellation;

function url(pageNumber, size, activeFilters = query) {
  return path + '?' + API.toQueryString({ ...activeFilters, page: pageNumber, pageSize: size });
}

function renderStats(counts) {
  const count = (...statuses) => counts.filter(item => statuses.includes(item.status))
    .reduce((sum, item) => sum + item.count, 0);
  const groups = [
    ['Tổng số hợp đồng', counts.reduce((sum, item) => sum + item.count, 0), 'green'],
    ['Đang thương thảo', count('UnderNegotiation', 'Negotiating'), 'blue'],
    ['Chờ xác nhận', count('PendingApproval', 'AwaitingConfirmation', 'Pending'), 'warm'],
    ['Đã ký kết / xác nhận', count('Signed', 'Executed', 'AwaitingDeposit', 'Active',
      'InProgress', 'Fulfilling', 'Completed'), 'green'],
  ];
  byId('tracking-stats').innerHTML = groups.map(([label, value, color], i) =>
    '<div class="panel offtake-stat"><span aria-hidden="true" class="offtake-badge ' + color + '">'
    + ['▣', '↔', '◷', '✓'][i] + '</span><div>' + esc(label) + '<strong>'
    + number(value) + '</strong></div></div>').join('');
  const select = byId('tracking-status');
  select.replaceChildren(new Option('Tất cả', ''));
  counts.sort((a, b) => statusName(a.status).localeCompare(statusName(b.status), 'vi'))
    .forEach(item => select.add(new Option(statusName(item.status), item.status)));
  if (query.status && !counts.some(item => item.status === query.status)) {
    select.add(new Option(statusName(query.status), query.status));
  }
  select.value = query.status || '';
}

function pageButton(label, target, active = false, disabled = false) {
  const button = document.createElement('button');
  button.type = 'button';
  button.textContent = label;
  button.disabled = disabled;
  button.setAttribute('aria-label', target < page && label === '‹' ? 'Trang trước'
    : label === '›' ? 'Trang sau' : 'Trang ' + target);
  if (active) {
    button.className = 'primary';
    button.setAttribute('aria-current', 'page');
  }
  button.addEventListener('click', () => load(target));
  return button;
}

function rowActions(row) {
  const id = Number(row.id);
  const draft = row.status === 'Draft';
  const canCancel = draft || ['PendingApproval', 'UnderNegotiation', 'Negotiating'].includes(row.status);
  const action = (type, title, icon, enabled = true) =>
    '<button type="button" data-record="' + id + '" data-action="' + type + '"'
    + (enabled ? '' : ' disabled') + '><span aria-hidden="true">' + icon + '</span>'
    + title + '</button>';
  return '<details class="offtake-row-actions"><summary aria-label="Thao tác cho '
    + esc(row.code) + '">⋯</summary><div>'
    + action('view', 'Xem chi tiết', '◉')
    + action('edit', 'Chỉnh sửa', '✎', draft || row.canEditRevision)
    + action('submit', 'Gửi phê duyệt', '➤', draft)
    + action('pdf', 'Tải xuống PDF', '↓')
    + action('cancel', 'Hủy hợp đồng', '×', canCancel)
    + (canCancel ? '' : '<small>Trạng thái hiện tại không cho phép hủy trực tiếp.</small>')
    + '</div></details>';
}

function renderPage(data) {
  page = data.page;
  const start = (page - 1) * data.pageSize;
  byId('tracking-rows').innerHTML = data.items.map((row, i) => '<tr><td>' + (start + i + 1)
    + '</td><td class="offtake-code">' + esc(row.code) + '</td><td>' + esc(row.partner || '—')
    + '</td><td><span class="offtake-cell-product">' + thumbnail(row.imageUrl) + esc(row.product || '—')
    + '</span></td><td>' + esc(row.quantity == null ? '—' : number(row.quantity) + ' ' + (row.unit || ''))
    + '</td><td>' + esc(money(row.value))
    + '</td><td>' + date(row.createdAt)
    + '</td><td>' + dateRange(row.deliveryFrom, row.deliveryTo) + '</td><td>' + badge(row.status)
    + '</td><td class="offtake-action-cell">' + rowActions(row) + '</td></tr>').join('');
  list.hidden = !data.items.length;
  pagination.hidden = !data.total;
  results.textContent = data.total
    ? 'Hiển thị ' + (start + 1) + '–' + (start + data.items.length) + ' trong ' + data.total + ' hồ sơ.'
    : Object.values(query).some(Boolean) ? 'Không tìm thấy hồ sơ phù hợp với bộ lọc.'
      : 'Bạn chưa có hợp đồng Offtake.';
  exportButton.disabled = !data.total;
  const last = Math.max(1, Math.ceil(data.total / data.pageSize));
  const pages = byId('tracking-pages');
  pages.replaceChildren(pageButton('‹', page - 1, false, page === 1));
  for (let index = Math.max(1, page - 2); index <= Math.min(last, page + 2); index++) {
    pages.append(pageButton(String(index), index, page === index));
  }
  pages.append(pageButton('›', page + 1, false, page === last));
}

async function load(target = page) {
  pending?.abort();
  const controller = new AbortController();
  pending = controller;
  exportButton.disabled = true;
  list.hidden = true;
  pagination.hidden = true;
  retry.hidden = true;
  results.textContent = 'Đang tải hồ sơ…';
  try {
    const data = await request(url(target, pageSize.value), { signal: controller.signal });
    if (controller.signal.aborted) return;
    renderStats(data.statusCounts);
    renderPage(data);
  } catch (error) {
    if (controller.signal.aborted) return;
    results.textContent = 'Không thể tải thông tin theo dõi. Vui lòng thử lại.';
    retry.hidden = false;
  }
}

async function openDetail(id, download = false) {
  pendingDetail?.abort();
  const controller = new AbortController();
  pendingDetail = controller;
  contractDetail = null;
  detailId = id;
  depositButton.hidden = true;
  depositButton.disabled = false;
  depositPanel.hidden = true;
  byId('tracking-deposit-qr').removeAttribute('src');
  downloadButton.hidden = true;
  notice(pdfNotice, '');
  editRevisionLink.hidden = true;
  detailContent.innerHTML = '<p role="status">Đang tải chi tiết…</p>';
  if (!dialog.open) dialog.showModal();
  try {
    const data = await request(path + '/' + id, { signal: controller.signal });
    if (controller.signal.aborted) return;
    detailContent.innerHTML = detailMarkup(data);
    renderFulfillment(data);
    contractDetail = data;
    depositButton.hidden = data.status !== 'AwaitingDeposit' || !(data.deposit > 0);
    downloadButton.hidden = false;
    if (data.canEditRevision) {
      editRevisionLink.href = 'offtake-proposal.html?edit=' + id;
      editRevisionLink.hidden = false;
    }
    if (download) await downloadPdf();
  } catch (error) {
    if (controller.signal.aborted) return;
    if (error.status === 404) {
      dialog.close();
      notice(byId('tracking-notice'), 'Hồ sơ không còn khả dụng.', true);
      load();
      return;
    }
    detailContent.innerHTML = '<p role="alert">Không thể tải chi tiết. Vui lòng thử lại.</p>'
      + '<button type="button" id="detail-retry">Thử lại</button>';
    byId('detail-retry').addEventListener('click', () => openDetail(id, download));
  }
}

function renderFulfillment(data) {
  const blocks = [];
  if (data.production?.length || data.deliveryEvents?.length) {
    blocks.push('<section><h3>Tiến độ sản xuất</h3>' + (data.production ?? []).map(season =>
      '<h4>' + esc(season.product + ' · ' + season.season) + '</h4><dl class="offtake-facts">' + facts([
        ['Trạng thái', statusName(season.status)], ['Dự kiến thu hoạch', date(season.estimatedHarvest)],
        ['Thu hoạch thực tế', date(season.actualHarvest)], ['Sản lượng đã thu hoạch', number(season.harvested) + ' ' + season.unit],
      ]) + '</dl>' + (season.behindSchedule ? '<p class="offtake-note">Mùa vụ đã quá ngày dự kiến thu hoạch.</p>' : '')
      + season.logs.map(log => '<p>' + esc(date(log.date) + ' · ' + log.activity + (log.description ? ' · ' + log.description : '')) + '</p>').join('')
    ).join('') + (data.deliveryEvents ?? []).map(event => '<p class="offtake-note">' + esc(
      'Báo chậm hoặc thiếu hàng · ' + date(event.at) + ' · ' + (event.reason ?? '')
      + (event.proposedDate ? ' · Đề xuất giao ' + number(event.quantity) + ' ngày ' + date(event.proposedDate) : '')
    ) + '</p>').join('') + '</section>');
  }
  for (const shipment of data.shipments ?? []) {
    if (!shipment.items?.length) continue;
    blocks.push('<section><h3>Lô giao #' + esc(shipment.id) + '</h3>'
      + shipment.items.map(item => '<dl class="offtake-facts">' + facts([
        ['Sản phẩm', item.product], ['Mã lô', item.batch], ['Đã giao', number(item.shipped) + ' ' + item.unit],
        ['Thực nhận', item.received == null ? '—' : number(item.received) + ' ' + item.unit],
        ['Chấp nhận', item.accepted == null ? '—' : number(item.accepted) + ' ' + item.unit],
        ['Phẩm cấp', item.grade], ['Kiểm nhận', statusName(item.status)],
      ]) + '</dl>').join('')
      + (shipment.canInspect ? '<button type="button" data-inspect="' + esc(shipment.id) + '">Kiểm nhận lô giao</button>' : '')
      + '</section>');
  }
  if (data.discrepancies?.length) {
    blocks.push('<section><h3>Phản hồi chênh lệch</h3>' + data.discrepancies.map(ticket =>
      '<h4>Hồ sơ #' + esc(ticket.id) + '</h4><dl class="offtake-facts">' + facts([
        ['Trạng thái', statusName(ticket.status)], ['Lý do ghi nhận', ticket.description], ['Kết quả xử lý', ticket.resolution],
      ]) + '</dl>' + ticket.replies.map(reply => '<dl class="offtake-facts">' + facts([
        ['Nông dân phản hồi', date(reply.at)], ['Lô số', reply.shipmentItemId], ['Giải trình', reply.reason],
        ['Phương án đề xuất', reply.resolution], ['Số lượng đề xuất', reply.quantity == null ? '—' : number(reply.quantity)],
      ]) + '</dl>').join('') + ticket.evidence.map(evidence => '<button type="button" data-evidence="'
        + esc(evidence.id) + '">Xem bằng chứng #' + esc(evidence.id)
        + (evidence.fromBuyer ? ' của bên mua' : ' của nông dân') + '</button>').join(' ')
      + (ticket.status === 'Open' && data.status === 'Active'
        ? '<form class="offtake-actions" data-discrepancy="' + esc(ticket.id) + '">'
          + (ticket.replies.length ? '<button type="submit" value="accept">Chốt theo phản hồi của nông dân</button>' : '')
          + '<label>Lý do chuyển xem xét <input name="reason" maxlength="500"></label>'
          + '<button type="submit" value="review">Chuyển hỗ trợ xem xét</button></form>' : '')
    ).join('') + '</section>');
  }
  for (const invoice of (data.invoices ?? []).filter(item => item.canPay)) {
    blocks.push('<p><button type="button" data-pay-invoice="' + esc(invoice.milestoneId)
      + '">Thanh toán hóa đơn ' + esc(invoice.number) + '</button></p>');
  }
  detailContent.insertAdjacentHTML('beforeend', blocks.join(''));
}

function openInspection(shipment) {
  const previous = byId('delivery-inspection');
  previous?.remove();
  detailContent.insertAdjacentHTML('beforeend', '<form id="delivery-inspection">'
    + '<h3>Kiểm nhận lô giao #' + esc(shipment.id) + '</h3>'
    + shipment.items.map(item => '<fieldset class="offtake-article-input"><legend>' + esc(item.batch + ' · ' + item.product)
      + '</legend><div class="offtake-fields"><label>Thực nhận (' + esc(item.unit) + ') <input name="received" type="number" min="0" max="'
      + esc(item.shipped) + '" step="0.001" value="' + esc(item.shipped) + '" required></label>'
      + '<label>Chấp nhận (' + esc(item.unit) + ') <input name="accepted" type="number" min="0" max="'
      + esc(item.shipped) + '" step="0.001" value="' + esc(item.shipped) + '" required></label>'
      + '<label>Phẩm cấp thực tế <input name="grade" maxlength="255" required></label></div></fieldset>').join('')
    + '<label>Lý do chênh lệch <textarea name="reason" rows="3" maxlength="500"></textarea></label>'
    + '<label>Bằng chứng chênh lệch (JPG, PNG, PDF) <input name="evidence" type="file" multiple accept=".jpg,.jpeg,.png,.pdf"></label>'
    + '<p id="delivery-inspection-notice" role="status" class="offtake-notice" hidden></p><div class="offtake-actions">'
    + '<button type="button" data-cancel-inspection>Quay lại</button><button type="submit" class="primary">Lưu kiểm nhận</button></div></form>');
  const form = byId('delivery-inspection');
  form.querySelector('input').focus();
  form.addEventListener('submit', event => {
    event.preventDefault();
    saveInspection(form, shipment);
  });
}

async function saveInspection(form, shipment) {
  const submit = form.querySelector('[type=submit]');
  if (submit.disabled) return;
  const values = new FormData(form);
  const received = values.getAll('received');
  const accepted = values.getAll('accepted');
  const grades = values.getAll('grade');
  const items = shipment.items.map((item, i) => ({
    itemId: item.id, received: Number(received[i]), accepted: Number(accepted[i]), grade: grades[i].trim(),
  }));
  const reason = values.get('reason').trim();
  const files = [...form.elements.namedItem('evidence').files];
  const output = byId('delivery-inspection-notice');
  if (items.some(item => item.accepted > item.received || !item.grade)) {
    notice(output, 'Nhập phẩm cấp và số lượng chấp nhận không vượt quá số lượng thực nhận.', true);
    return;
  }
  if ((reason || items.some((item, i) => item.accepted < shipment.items[i].shipped)) && (!reason || !files.length)) {
    notice(output, 'Vui lòng nhập lý do và đính kèm bằng chứng chênh lệch.', true);
    return;
  }
  if (files.length > 5 || files.some(file => file.size > 5 * 1024 * 1024)) {
    notice(output, 'Chọn tối đa 5 tệp, mỗi tệp tối đa 5 MB.', true);
    return;
  }
  const id = detailId;
  const { signal } = pendingDetail;
  submit.disabled = true;
  notice(output, 'Đang lưu kiểm nhận…');
  try {
    const evidence = [];
    for (const file of files) {
      const body = new FormData();
      body.append('file', file);
      evidence.push((await request(path + '/' + id + '/evidence', { method: 'POST', body, signal })).url);
    }
    const result = await request(path + '/' + id + '/shipments/' + shipment.id + '/inspection', {
      method: 'POST', body: { items, reason, evidence }, signal,
    });
    if (signal.aborted) return;
    await openDetail(id);
    if (detailId === id && dialog.open) notice(pdfNotice, result.ticketId
      ? 'Đã gửi chênh lệch cho nông dân phản hồi.' : 'Đã lưu kết quả kiểm nhận.');
    load();
  } catch (error) {
    if (!signal.aborted) notice(output, error.message, true);
  } finally {
    submit.disabled = false;
  }
}

async function viewEvidence(id) {
  try {
    const response = await fetch(CONFIG.API_BASE_URL + path + '/' + detailId + '/evidence/' + id, {
      headers: { Authorization: 'Bearer ' + Auth.getAccessToken() }, signal: pendingDetail.signal,
    });
    if (!response.ok) throw new Error('Không thể mở bằng chứng.');
    const file = await response.blob();
    const url = URL.createObjectURL(file);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'bang-chung-' + id + ({ 'application/pdf': '.pdf', 'image/png': '.png', 'image/jpeg': '.jpg' }[file.type] ?? '');
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 60000);
  } catch (error) {
    if (error.name !== 'AbortError') notice(pdfNotice, error.message, true);
  }
}

async function showTransfer(button, endpoint, deposit) {
  if (button.disabled) return;
  const controller = pendingDetail;
  button.disabled = true;
  depositPanel.hidden = true;
  notice(pdfNotice, 'Đang tải thông tin chuyển khoản…');
  try {
    const data = await request(path + '/' + detailId + endpoint, { method: deposit ? 'POST' : 'GET', signal: controller.signal });
    if (controller.signal.aborted) return;
    byId('tracking-deposit-title').textContent = deposit ? 'Chuyển khoản đặt cọc' : 'Chuyển khoản thanh toán hóa đơn';
    byId('tracking-deposit-bank').innerHTML = facts([
      [deposit ? 'Số tiền cọc còn lại' : 'Số tiền còn phải trả', money(data.amount)], ['Ngân hàng', data.bankName],
      ['Chủ tài khoản', data.accountName], ['Số tài khoản', data.accountNumber],
      ['Nội dung chuyển khoản', data.reference],
    ]);
    byId('tracking-deposit-qr').src = data.qrUrl;
    depositPanel.hidden = false;
    depositPanel.scrollIntoView({ block: 'nearest' });
    notice(pdfNotice, '');
  } catch (error) {
    if (!controller.signal.aborted) notice(pdfNotice, error.message, true);
  } finally {
    if (pendingDetail === controller) button.disabled = false;
  }
}

async function downloadPdf() {
  const detail = contractDetail;
  if (!detail) return;
  downloadButton.disabled = true;
  notice(pdfNotice, 'Đang tạo tệp PDF…');
  try {
    const buyer = hasBuyerParty(detail.document?.buyer) ? detail.document.buyer : await loadBuyerParty();
    if (!hasBuyerParty(buyer)) {
      notice(pdfNotice, 'Vui lòng bổ sung thông tin Bên B trong hồ sơ doanh nghiệp trước khi tải PDF.', true);
      return;
    }
    const { downloadOfftakePdf } = await import('./offtake-pdf.js');
    await downloadOfftakePdf({ ...detail, document: { ...detail.document, buyer } });
    notice(pdfNotice, '');
  } catch {
    notice(pdfNotice, 'Không thể tải tệp PDF. Vui lòng thử lại.', true);
  } finally {
    downloadButton.disabled = false;
  }
}

function openCancellation(id, code) {
  cancellation = { id, code };
  byId('contract-cancel-description').textContent = 'Hủy hợp đồng ' + code
    + ' sẽ dừng xử lý đề xuất.';
  notice(cancelNotice, '');
  cancelDialog.showModal();
}

async function cancelContract() {
  const { id, code } = cancellation;
  cancelConfirm.disabled = true;
  cancelBack.disabled = true;
  notice(cancelNotice, 'Đang hủy hợp đồng…');
  try {
    const receipt = await request(path + '/' + id + '/cancellation', { method: 'POST' });
    if (String(receipt?.id) !== id) {
      notice(cancelNotice, 'API chưa xác nhận đã hủy. Vui lòng kiểm tra lại danh sách.', true);
      return;
    }
    cancelDialog.close();
    notice(byId('tracking-notice'), 'Đã hủy hợp đồng ' + code + '.');
    await load();
  } catch (error) {
    notice(cancelNotice, error.status === 405
      ? 'API chưa nhận thao tác hủy. Hãy khởi động lại API rồi thử lại.' : error.message, true);
  } finally {
    cancelConfirm.disabled = false;
    cancelBack.disabled = false;
  }
}

function csvField(value) {
  const text = String(value ?? '');
  const safe = /^[\s]*[=+@-]/.test(text) || /^[\t\r\n]/.test(text) ? "'" + text : text;
  return '"' + safe.replaceAll('"', '""') + '"';
}

async function exportList() {
  exportButton.disabled = true;
  const activeFilters = { ...query };
  const lines = [['Mã hợp đồng', 'Đối tác', 'Sản phẩm',
    'Khối lượng', 'Đơn vị', 'Giá trị VND', 'Ngày tạo', 'Giao hàng', 'Trạng thái']];
  try {
    let pages = 1;
    for (let index = 1; index <= pages; index++) {
      const data = await request(url(index, 100, activeFilters));
      if (data.page !== index) break;
      pages = Math.ceil(data.total / 100);
      lines.push(...data.items.map(row => [row.code, row.partner, row.product, row.quantity,
        row.unit, row.value, date(row.createdAt), dateRange(row.deliveryFrom, row.deliveryTo),
        statusName(row.status)]));
    }
    const csv = '\uFEFF' + lines.map(line => line.map(csvField).join(',')).join('\r\n');
    const href = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = href;
    link.download = 'hop-dong-offtake.csv';
    link.click();
    setTimeout(() => URL.revokeObjectURL(href), 1000);
  } catch {
    notice(byId('tracking-notice'), 'Không thể xuất danh sách. Vui lòng thử lại.', true);
  } finally {
    exportButton.disabled = list.hidden;
  }
}

if (Auth.requireAuth('WholesaleBuyer')) {
  depositButton.addEventListener('click', () => showTransfer(depositButton, '/deposit', true));
  detailContent.addEventListener('click', event => {
    const button = event.target.closest('button');
    if (button?.dataset.inspect) {
      const shipment = contractDetail?.shipments.find(item => String(item.id) === button.dataset.inspect && item.canInspect);
      if (shipment) openInspection(shipment);
    }
    if (button?.dataset.evidence) viewEvidence(button.dataset.evidence);
    if (button?.dataset.payInvoice) showTransfer(button, '/milestones/' + button.dataset.payInvoice + '/payment', false);
    if (button?.hasAttribute('data-cancel-inspection')) byId('delivery-inspection')?.remove();
  });
  detailContent.addEventListener('submit', async event => {
    const form = event.target;
    if (!form.dataset.discrepancy) return;
    event.preventDefault();
    const ticket = contractDetail.discrepancies.find(item => String(item.id) === form.dataset.discrepancy);
    const shipment = contractDetail.shipments.find(item => item.sellerOrderId === ticket?.sellerOrderId
      && item.items.some(line => line.status === 'Disputed'));
    const accept = event.submitter?.value === 'accept';
    const reason = new FormData(form).get('reason').trim();
    if (!shipment || (!accept && !reason)) {
      notice(pdfNotice, 'Vui lòng nhập lý do chuyển xem xét.', true);
      return;
    }
    const buttons = form.querySelectorAll('button');
    buttons.forEach(button => { button.disabled = true; });
    const id = detailId;
    const { signal } = pendingDetail;
    try {
      await request(path + '/' + id + '/shipments/' + shipment.id + '/discrepancies/' + ticket.id + '/decision', {
        method: 'POST', body: { accept, reason, replyAt: ticket.replies[0]?.at }, signal,
      });
      if (!signal.aborted) await openDetail(id);
    } catch (error) {
      if (!signal.aborted) notice(pdfNotice, error.message, true);
    } finally {
      buttons.forEach(button => { button.disabled = false; });
    }
  });
  filters.addEventListener('submit', event => {
    event.preventDefault();
    const nextFilters = Object.fromEntries(new FormData(filters));
    notice(byId('tracking-notice'), '');
    if (nextFilters.from && nextFilters.to && nextFilters.from > nextFilters.to) {
      notice(byId('tracking-notice'), 'Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.', true);
      return;
    }
    query = nextFilters;
    load(1);
  });
  pageSize.addEventListener('change', () => load(1));
  retry.addEventListener('click', () => load());
  exportButton.addEventListener('click', exportList);
  downloadButton.addEventListener('click', downloadPdf);
  cancelConfirm.addEventListener('click', cancelContract);
  cancelDialog.addEventListener('cancel', event => {
    if (cancelConfirm.disabled) event.preventDefault();
  });
  dialog.addEventListener('close', () => pendingDetail?.abort());
  list.addEventListener('click', event => {
    const button = event.target.closest('[data-record]');
    if (!button || button.disabled) return;
    const actions = button.closest('details');
    const { action, record } = button.dataset;
    actions.open = false;
    actions.querySelector('summary').focus();
    if (action === 'edit' || action === 'submit') {
      location.href = 'offtake-proposal.html?edit=' + record + (action === 'submit' ? '&review=1' : '');
    } else if (action === 'cancel') {
      openCancellation(record, button.closest('tr').querySelector('.offtake-code').textContent);
    } else {
      openDetail(record, action === 'pdf');
    }
  });
  load(1);
  if (params.has('saved')) notice(byId('tracking-notice'), 'Đã lưu bản nháp hợp đồng.');
  const id = params.get('resent') || params.get('submitted') || params.get('record');
  if (id && /^\d+$/.test(id)) {
    if (params.has('resent')) notice(byId('tracking-notice'), 'Đã gửi lại đề xuất để nông dân xác nhận.');
    if (params.has('submitted')) notice(byId('tracking-notice'), 'Đã gửi đề xuất cho nhà cung cấp.');
    openDetail(id);
  }
}
