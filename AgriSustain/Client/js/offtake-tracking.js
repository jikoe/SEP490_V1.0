import { esc, money, request } from './page-ui.js';
import { badge, byId, date, dateRange, detailMarkup, hasBuyerParty, loadBuyerParty, notice, number, statusName, thumbnail } from './offtake-ui.js';

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
const revisionLink = byId('revision-offtake');
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
    + action('edit', 'Chỉnh sửa', '✎', draft)
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
  downloadButton.hidden = true;
  notice(pdfNotice, '');
  revisionLink.hidden = true;
  detailContent.innerHTML = '<p role="status">Đang tải chi tiết…</p>';
  if (!dialog.open) dialog.showModal();
  try {
    const data = await request(path + '/' + id, { signal: controller.signal });
    if (controller.signal.aborted) return;
    detailContent.innerHTML = detailMarkup(data);
    contractDetail = data;
    downloadButton.hidden = false;
    if (data.canConfirmRevision) {
      revisionLink.href = 'offtake-proposal.html?revision=' + id;
      revisionLink.hidden = false;
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
  const id = params.get('submitted') || params.get('record');
  if (id && /^\d+$/.test(id)) {
    if (params.has('submitted')) notice(byId('tracking-notice'), 'Đã gửi đề xuất cho nhà cung cấp.');
    openDetail(id);
  }
}
