import { request, showNotice } from './page-ui.js';
import { esc, cleanMessage, money, localTime, status, metrics, dateLabel, todayVietnam, paymentTable, panel, facts, match } from './finance-ui.js';

const page = document.body.dataset.financePage;
const heading = document.getElementById('finance-heading');
const filters = document.getElementById('finance-filters');
const content = document.getElementById('finance-content');
const notice = document.getElementById('finance-notice');
const dialog = document.getElementById('finance-dialog');
const params = new URLSearchParams(location.search);
let data;
let busy = false;
let sequence = 0;
let loadController;
let recordsSequence = 0;
let recordsPage = 1;
let operation;
const today = todayVietnam();
let period = { period: 'Month', date: `${today.slice(0, 7)}-01` };
const active = Object.fromEntries(['search', 'category', 'provider', 'status', 'from', 'to', 'needsAttention', 'page'].map(key => [key, params.get(key) || '']));
active.page = Math.max(1, Number(active.page) || 1);

function api(path, query = {}) {
  const search = API.toQueryString(query);
  return `/finance/${path}${search ? `?${search}` : ''}`;
}
function header(title, description, extra = '') {
  heading.innerHTML = `<div class="fin-heading"><div><h1>${esc(title)}</h1><p>${esc(description)}</p></div><div class="fin-heading-actions">${extra}<button id="finance-refresh" type="button">↻ Làm mới</button></div></div>`;
  document.getElementById('finance-refresh').addEventListener('click', load);
}
function setBusy(value) {
  busy = value;
  document.getElementById('finance-refresh').disabled = value;
  document.getElementById('finance-close-dialog').disabled = value;
  content.querySelectorAll('button').forEach(button => { button.disabled = value || button.dataset.disabled === 'true'; });
}
function renderPeriod() {
  filters.innerHTML = `<form id="finance-period" class="fin-panel fin-period"><div class="fin-period-fields"><label>Kỳ báo cáo<select id="period-type"><option value="Day">Theo ngày</option><option value="Month" selected>Theo tháng</option><option value="Quarter">Theo quý</option></select></label>
    <div id="period-inputs"></div><button class="fin-primary" type="submit">Áp dụng</button></div><div id="period-caption" class="fin-period-caption"></div></form>`;
  const draw = () => {
    const type = document.getElementById('period-type').value;
    document.getElementById('period-inputs').innerHTML = type === 'Quarter'
      ? `<div class="fin-quarter"><label>Quý<select name="quarter">${[1, 2, 3, 4].map(n => `<option value="${n}" ${Math.ceil(Number(today.slice(5, 7)) / 3) === n ? 'selected' : ''}>Quý ${n}</option>`).join('')}</select></label><label>Năm<input name="year" type="number" min="1900" max="9998" required value="${today.slice(0, 4)}"></label></div>`
      : `<label>${type === 'Day' ? 'Ngày' : 'Tháng'}<input name="date" type="${type === 'Day' ? 'date' : 'month'}" min="${type === 'Day' ? '1900-01-01' : '1900-01'}" max="${type === 'Day' ? '9998-12-31' : '9998-12'}" required value="${type === 'Day' ? today : today.slice(0, 7)}"></label>`;
  };
  document.getElementById('period-type').addEventListener('change', draw); draw();
  document.getElementById('finance-period').addEventListener('submit', event => {
    event.preventDefault();
    const form = event.currentTarget;
    const type = document.getElementById('period-type').value;
    period = { period: type, date: type === 'Quarter' ? `${form.elements.year.value}-${String((Number(form.elements.quarter.value) - 1) * 3 + 1).padStart(2, '0')}-01` : form.elements.date.value + (type === 'Month' ? '-01' : '') };
    load();
  });
}
function renderDashboard() {
  header('Tổng quan tài chính', 'Theo dõi số liệu tài chính và các giao dịch cần xử lý.');
  document.getElementById('period-caption').innerHTML = `<span>Đang hiển thị: ${dateLabel(data.period.start)} – ${dateLabel(data.period.end)}</span><span>Cập nhật: ${esc(localTime(data.refreshedAt))}</span>`;
  const all = `payments.html?${API.toQueryString({ needsAttention: true, from: data.period.start, to: data.period.end })}`;
  content.innerHTML = `<div class="fin-metrics">${metrics.map(([key, label, caption]) => `<button class="fin-metric ${key === 'Gmv' ? 'featured' : ''}" data-metric="${key}" type="button" ${data.metrics[key] == null ? 'disabled' : ''} aria-label="${label}"><span>${label}<span>↗</span></span><strong>${money(data.metrics[key])}</strong><small>${caption}</small>${key === 'PendingPayouts' ? `<small>Đã chi trả: ${money(data.metrics.CompletedPayouts)}</small>` : ''}</button>`).join('')}
    <a class="fin-metric fin-metric-link" href="${all}"><span>Giao dịch cần xử lý<span>◷</span></span><strong>${data.transactionsToProcess ?? 'Không khả dụng'} <small>giao dịch</small></strong><small>Thanh toán chưa xác minh hoặc có sai lệch</small></a></div>
    <button class="fin-text" data-metric="CompletedPayouts" type="button">Xem các khoản chi trả nông hộ đã hoàn tất →</button>
    ${data.unavailable.length ? '<div class="fin-warning" role="alert">Một số số liệu chưa tải được. Giá trị không khả dụng chưa được tính là 0.</div>' : ''}
    ${panel('Giao dịch cần xử lý', data.pendingTransactions == null ? '<p class="fin-empty">Chưa tải được danh sách giao dịch.</p>' : data.pendingTransactions.length
      ? paymentTable(data.pendingTransactions, true) : '<p class="fin-empty">Không có giao dịch cần xử lý trong kỳ này.</p>', `<a href="${all}">Xem tất cả →</a>`)}
    <div class="fin-table-caption"><span>Hiển thị ${data.pendingTransactions?.length ?? '—'} / ${data.transactionsToProcess ?? '—'} giao dịch cần xử lý trong kỳ</span><span>Chỉ xem dữ liệu</span></div>
    <p class="fin-calculation">GMV loại trừ đơn đã hủy và khoản hoàn tất hoàn tiền. Khoản thu, hóa đơn và tiền cọc được áp dụng chỉ tính một lần.</p>`;
  content.querySelectorAll('[data-metric]').forEach(button => button.addEventListener('click', () => openRecords(button.dataset.metric)));
}
async function openRecords(metric, pageNumber = 1) {
  const current = ++recordsSequence;
  recordsPage = pageNumber;
  document.getElementById('finance-dialog-title').textContent = `Chứng từ · ${metrics.find(([key]) => key === metric)?.[1] || 'Chi trả đã hoàn tất'}`;
  const body = document.getElementById('finance-dialog-content');
  body.textContent = 'Đang tải chứng từ…';
  if (!dialog.open) dialog.showModal();
  try {
    const result = await request(api('dashboard/records', { metric, period: data.period.type, date: data.period.start, page: recordsPage, pageSize: 10 }));
    if (current !== recordsSequence) return;
    body.innerHTML = `<p class="muted">${dateLabel(data.period.start)} – ${dateLabel(data.period.end)} · Cơ sở dữ liệu</p><div class="fin-table-wrap"><table class="fin-table"><thead><tr><th>Mã chứng từ</th><th>Liên quan</th><th>Số tiền</th><th>Trạng thái</th><th>Thời gian</th><th>Thao tác</th></tr></thead><tbody>${result.items.map(row => `<tr><td>${esc(row.reference)}</td><td>${esc(row.relatedReference || '—')}</td><td>${money(row.amount)}</td><td>${status(row.status, metric === 'PendingPayouts' && row.status === 'Pending' ? 'Chờ chi trả' : metric === 'CompletedPayouts' && row.status === 'Paid' ? 'Đã chi trả' : undefined)}</td><td>${esc(localTime(row.at))}</td><td>${row.paymentId ? `<a href="payment-detail.html?id=${row.paymentId}">Xem giao dịch</a>` : '—'}</td></tr>`).join('')}</tbody></table></div>${!result.total ? '<p class="fin-empty">Không có chứng từ trong kỳ này.</p>' : ''}<div class="fin-records-pager"><button id="records-prev" type="button" ${recordsPage <= 1 ? 'disabled' : ''}>‹ Trước</button><span>Trang ${recordsPage} / ${Math.max(1, Math.ceil(result.total / 10))}</span><button id="records-next" type="button" ${recordsPage * 10 >= result.total ? 'disabled' : ''}>Sau ›</button></div>`;
    document.getElementById('records-prev').onclick = () => openRecords(metric, recordsPage - 1);
    document.getElementById('records-next').onclick = () => openRecords(metric, recordsPage + 1);
  } catch (error) { if (current === recordsSequence) body.textContent = cleanMessage(error.message); }
}
function renderFilters() {
  const options = (name, rows) => `<select name="${name}"><option value="">Tất cả</option>${rows.map(([key, label]) => `<option value="${key}" ${active[name] === key ? 'selected' : ''}>${label}</option>`).join('')}</select>`;
  filters.innerHTML = `<form id="finance-search" class="fin-panel fin-filters"><label class="fin-filter-search">Tìm kiếm<input name="search" maxlength="150" value="${esc(active.search)}" placeholder="Mã giao dịch, tham chiếu hoặc nghĩa vụ"></label>
    <label>Loại thanh toán${options('category', [['Order', 'Đơn hàng'], ['Subscription', 'Gói thành viên']])}</label>
    <label>Cổng thanh toán${options('provider', ['SePay', 'VietQR', 'PayOS', 'VNPay', 'MoMo'].map(value => [value, value]))}</label>
    <label>Trạng thái${options('status', [['Pending', 'Chờ xác minh'], ['Succeeded', 'Thành công'], ['Failed', 'Thất bại'], ['Expired', 'Hết hạn'], ['AmountMismatch', 'Sai lệch số tiền'], ['Unmatched', 'Chưa khớp đơn hàng']])}</label>
    <label class="fin-filter-date">Từ ngày tạo<input name="from" type="date" min="1900-01-01" max="9998-12-31" value="${esc(active.from)}"></label>
    <label class="fin-filter-date">Đến ngày tạo<input name="to" type="date" min="1900-01-01" max="9998-12-31" value="${esc(active.to)}"></label>
    <div class="fin-filter-actions"><button class="fin-primary" type="submit">Tìm kiếm</button><a class="btn" href="payments.html">Xóa bộ lọc</a></div>
    ${active.needsAttention === 'true' ? '<p class="fin-filter-notice">Đang lọc giao dịch cần xử lý. <a href="payments.html">Bỏ bộ lọc</a></p>' : ''}</form>`;
  document.getElementById('finance-search').addEventListener('submit', event => {
    event.preventDefault();
    const values = Object.fromEntries(new FormData(event.currentTarget));
    if (values.from && values.to && values.from > values.to) { showNotice(notice, 'Ngày bắt đầu phải không sau ngày kết thúc.'); return; }
    Object.assign(active, values, { search: values.search.trim(), page: 1 });
    history.replaceState(null, '', `payments.html?${API.toQueryString(active)}`);
    load();
  });
}
function renderList() {
  header('Đối soát thanh toán', 'Xem giao dịch đơn hàng, gói thành viên và kết quả đối soát đã ghi nhận.');
  const pages = Math.max(1, Math.ceil(data.total / 10));
  content.innerHTML = panel(`Danh sách giao dịch · ${data.total} kết quả`, data.total ? paymentTable(data.items) : '<p class="fin-empty">Không tìm thấy giao dịch phù hợp với bộ lọc.</p>', 'Đơn vị: VND')
    + `<div class="fin-records-pager"><span>Hiển thị ${data.total ? (data.page - 1) * 10 + 1 : 0}–${Math.min(data.page * 10, data.total)} / ${data.total} giao dịch · 10 dòng / trang</span><button id="payments-prev" type="button" ${data.page <= 1 ? 'disabled' : ''}>‹</button><span>Trang ${data.page} / ${pages}</span><button id="payments-next" type="button" ${data.page >= pages ? 'disabled' : ''}>›</button></div>
    <p class="fin-calculation">Chọn một giao dịch để xem nghĩa vụ liên kết, bằng chứng từ cổng và lịch sử đối soát. Chỉ ghi nhận thành công khi tham chiếu, số tiền và kết quả được xác minh.</p>`;
  for (const [id, delta] of [['payments-prev', -1], ['payments-next', 1]]) document.getElementById(id).addEventListener('click', () => {
    active.page = data.page + delta;
    history.replaceState(null, '', `payments.html?${API.toQueryString(active)}`);
    load();
  });
}
function renderDetail() {
  const row = data.payment;
  const receipt = data.lastReceipt;
  const expectedAmount = data.expectedAmount ?? row.amount;
  const bankTransfer = receipt?.status === 'FundsReceived';
  const normalizeStatus = value => ['Success', 'Succeeded', 'Paid', 'Completed'].includes(value) ? 'FundsReceived' : value;
  const backParams = new URLSearchParams(location.search); backParams.delete('id');
  header(row.code, `Đối chiếu nghĩa vụ thanh toán và kết quả từ ${row.provider}.`, status(row.status));
  filters.innerHTML = `<a class="fin-back" href="payments.html${backParams.size ? `?${esc(backParams)}` : ''}">← Danh sách giao dịch</a>`;
  const payment = facts([['Mã giao dịch', esc(row.code)], ['Cổng / Hình thức', `${esc(row.provider)} / ${esc(['BankTransfer', 'Transfer'].includes(data.method) ? 'Chuyển khoản' : data.method === 'OnlineBanking' ? 'Thanh toán trực tuyến' : data.method)}`],
    ['Tham chiếu cổng', esc(row.reference || 'Chưa có tham chiếu')], [bankTransfer ? 'Số tiền theo đơn' : 'Số tiền yêu cầu', money(expectedAmount)], ['Thời gian tạo', esc(localTime(row.requestedAt))],
    [bankTransfer ? 'Thời điểm nhận tiền theo cổng' : 'Thời gian thanh toán đã xác minh', row.paidAt ? esc(localTime(row.paidAt)) : 'Chưa xác minh'], ['Đối soát gần nhất', data.reconciledAt ? esc(localTime(data.reconciledAt)) : 'Chưa đối soát'], ['Trạng thái thanh toán', status(row.status)]]);
  const obligation = data.obligation ? facts([['Loại nghĩa vụ', data.obligation.category === 'Order' ? 'Thanh toán đơn hàng' : 'Gói thành viên'], ['Mã / Định danh', esc(data.obligation.reference)], ['Giá trị nghĩa vụ', money(data.obligation.amount)], ['Còn phải thanh toán', money(data.obligation.unpaid)], ['Tình trạng nghĩa vụ', status(data.obligation.status)]]) : '<p class="fin-empty">Chưa xác định được nghĩa vụ của giao dịch.</p>';
  const comparison = `<div class="fin-table-wrap"><table class="fin-table fin-comparison"><thead><tr><th>Tiêu chí</th><th>Dữ liệu hệ thống</th><th>Kết quả từ ${esc(row.provider)}</th><th>Đối chiếu</th></tr></thead><tbody>
    <tr><td>Tham chiếu</td><td>${esc(row.reference || 'Chưa có')}</td><td>${esc(receipt?.reference || 'Chưa có kết quả')}</td><td>${match(row.reference, receipt?.reference)}</td></tr>
    <tr><td>Số tiền</td><td>${money(expectedAmount)}</td><td>${receipt ? money(receipt.amount) : 'Chưa có kết quả'}</td><td>${match(expectedAmount, receipt?.amount, true)}</td></tr>
    <tr><td>Kết quả thanh toán</td><td>${status(row.status)}</td><td>${receipt ? status(receipt.status) : 'Chưa xác minh'}</td><td>${match(normalizeStatus(row.status), normalizeStatus(receipt?.status))}</td></tr></tbody></table></div>
    <div class="fin-reconcile-actions"><div><p>Kiểm tra lại trạng thái và quyền truy cập trước khi ghi nhận.</p><p>Mỗi khoản thu chỉ được áp dụng một lần.</p>${!data.canReconcile ? `<p>${esc(cleanMessage(data.unavailableReason) || 'Khoản thu đã được ghi nhận. Xem kết quả trong lịch sử đối soát.')}</p>` : ''}</div>
    <button id="finance-reconcile" class="fin-primary" type="button" data-disabled="${!data.canReconcile}" ${!data.canReconcile ? 'disabled' : ''}>Đối soát giao dịch</button></div>`;
  const history = `<ol class="fin-history">${data.history.map(entry => `<li><div><strong>${esc(cleanMessage(entry.message))}</strong><time>${esc(localTime(entry.at))}</time></div><p>${status(entry.before)} → ${status(entry.after)} · ${esc(entry.staff)} · ${esc(row.provider)}</p>${entry.receipt ? `<p>Bằng chứng: ${esc(entry.receipt.eventId)} · ${money(entry.receipt.amount)} · ${esc(entry.receipt.reference)}</p>` : ''}</li>`).join('')}<li><div><strong>${bankTransfer ? 'Ghi nhận giao dịch chuyển khoản' : 'Khởi tạo yêu cầu thanh toán'}</strong><time>${esc(localTime(row.requestedAt))}</time></div><p>${bankTransfer ? 'Số tiền nhận' : 'Số tiền yêu cầu'}: ${money(row.amount)} · ${esc(row.obligation)}</p></li></ol>`;
  content.innerHTML = `${row.needsReview ? `<div class="fin-warning" role="alert"><strong>Giao dịch cần kiểm tra</strong><p>${esc(cleanMessage(row.note) || 'Kết quả thanh toán cần được kiểm tra.')}</p></div>` : ''}<div class="fin-detail-grid">${panel('Thông tin thanh toán', payment, 'Chỉ đọc')}${panel('Nghĩa vụ liên kết', obligation)}</div>
    ${panel('Đối chiếu kết quả từ cổng thanh toán', comparison, receipt ? `Đã ghi nhận · ${esc(localTime(receipt.checkedAt))}` : 'Chưa có kết quả từ cổng')}${panel('Lịch sử đối soát', history, 'Kết quả · Trạng thái · Nhân viên · Thời gian')}`;
  document.getElementById('finance-reconcile').addEventListener('click', confirmReconcile);
}
function confirmReconcile() {
  if (busy || !data.canReconcile) return;
  document.getElementById('finance-dialog-title').textContent = 'Xác nhận đối soát';
  document.getElementById('finance-dialog-content').innerHTML = `<p>Đối soát giao dịch <strong>${esc(data.payment.code)}</strong> qua <strong>${esc(data.payment.provider)}</strong>?</p><p>Số tiền yêu cầu: <strong>${money(data.payment.amount)}</strong></p><div class="modal-actions"><button id="finance-cancel" type="button">Hủy</button><button id="finance-confirm" class="primary" type="button">Xác nhận đối soát</button></div>`;
  document.getElementById('finance-cancel').onclick = () => dialog.close();
  document.getElementById('finance-confirm').onclick = reconcile;
  dialog.showModal();
}
async function reconcile() {
  if (busy || !data.canReconcile) return;
  if (operation?.version !== data.version) operation = { version: data.version, requestKey: crypto.randomUUID() };
  setBusy(true);
  dialog.querySelectorAll('button').forEach(button => { button.disabled = true; });
  try {
    const response = await request(api(`payments/${data.payment.id}/reconcile`), { method: 'POST', body: operation });
    data = response.detail;
    renderDetail();
    showNotice(notice, response.message, !['Succeeded', 'AlreadyApplied', 'Pending'].includes(response.outcome));
  } catch (error) {
    showNotice(notice, error.message);
    if (error.status === 409) await load(true);
  } finally {
    dialog.close();
    dialog.querySelectorAll('button').forEach(button => { button.disabled = false; });
    setBusy(false);
  }
}
async function load(keepNotice = false) {
  if (busy && !keepNotice) return;
  loadController?.abort();
  const controller = new AbortController();
  loadController = controller;
  const current = ++sequence;
  if (!keepNotice) showNotice(notice, '');
  if (page === 'dashboard') header('Tổng quan tài chính', 'Theo dõi số liệu tài chính và các giao dịch cần xử lý.');
  if (page === 'payments') header('Đối soát thanh toán', 'Xem giao dịch đơn hàng, gói thành viên và kết quả đối soát đã ghi nhận.');
  if (page === 'detail') header('Chi tiết giao dịch', 'Đang tải thông tin thanh toán.');
  document.getElementById('finance-refresh').disabled = true;
  content.setAttribute('aria-busy', 'true');
  if (!data) content.innerHTML = '<p class="fin-loading" role="status">Đang tải dữ liệu tài chính…</p>';
  try {
    const id = params.get('id');
    if (page === 'detail' && !/^\d+$/.test(id || '')) throw new Error('Vui lòng chọn một giao dịch trong danh sách.');
    const result = await request(page === 'dashboard' ? api('dashboard', period) : page === 'payments' ? api('payments', { ...active, pageSize: 10 }) : api(`payments/${id}`), { signal: controller.signal });
    if (current !== sequence) return;
    data = result;
    if (page === 'dashboard') renderDashboard();
    else if (page === 'payments') renderList();
    else renderDetail();
  } catch (error) {
    if (current !== sequence || error.name === 'AbortError') return;
    showNotice(notice, error.message);
    if (!data) content.innerHTML = '<p class="fin-empty">Chưa tải được dữ liệu. Hãy bấm Làm mới để thử lại.</p>';
  } finally {
    if (current === sequence) {
      document.getElementById('finance-refresh').disabled = busy;
      content.removeAttribute('aria-busy');
    }
  }
}
document.getElementById('finance-close-dialog').addEventListener('click', () => { if (!busy) dialog.close(); });
dialog.addEventListener('cancel', event => { if (busy) event.preventDefault(); });
dialog.addEventListener('close', () => { recordsSequence++; });
if (Auth.requireAuth() && /finance\s*staff/i.test(Auth.getUserRole() || '')) {
  document.getElementById('finance-source').innerHTML = '<div class="fin-footer">Đơn vị: VND · Thời gian Việt Nam (UTC+7)</div>';
  const updateActivity = () => {
    const activity = document.querySelector('.sidebar-bottom p');
    if (activity) activity.textContent = '● Đang hoạt động';
  };
  if (document.readyState !== 'complete') document.addEventListener('DOMContentLoaded', updateActivity, { once: true });
  else updateActivity();
  if (page === 'dashboard') renderPeriod();
  if (page === 'payments') renderFilters();
  load();
} else if (Auth.isAuthenticated()) {
  content.textContent = 'Bạn không có quyền truy cập dữ liệu tài chính.';
}
