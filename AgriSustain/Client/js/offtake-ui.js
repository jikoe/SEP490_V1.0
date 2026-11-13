import { esc, icon, localTime, money, request, safeUrl } from './page-ui.js';
import { contractArticles, readContractTerms } from './offtake-terms.js';

export const byId = id => document.getElementById(id);
export const number = value => value == null ? '—' : Number(value).toLocaleString('vi-VN');
export function date(value) {
  if (!value) return '—';
  return value.length === 10 ? value.split('-').reverse().join('/')
    : localTime(value).match(/\d{2}\/\d{2}\/\d{4}/)?.[0] || '—';
}
export const dateRange = (from, to) => from === to || !to ? date(from) : date(from) + ' – ' + date(to);
export const facts = entries => entries.map(([label, value]) =>
  '<div><dt>' + esc(label) + '</dt><dd>' + esc(value ?? '—') + '</dd></div>').join('');

export async function loadBuyerParty() {
  const profile = await request('/profile/business');
  return { name: profile.companyName, representative: profile.representativeName,
    address: profile.businessAddress, taxCode: profile.taxCode };
}

export const hasBuyerParty = party =>
  [party?.name, party?.representative, party?.address, party?.taxCode].every(value => value?.trim());

const statuses = {
  Draft: ['Bản nháp', 'warm'],
  Pending: ['Đang chờ', 'warm'],
  UnderNegotiation: ['Đang thương thảo', 'blue'],
  Negotiating: ['Đang thương thảo', 'blue'],
  PendingApproval: ['Chờ nhà cung cấp xác nhận', 'warm'],
  AwaitingConfirmation: ['Chờ xác nhận', 'warm'],
  AwaitingDeposit: ['Chờ đặt cọc', 'warm'],
  Signed: ['Đã ký kết', 'green'],
  Executed: ['Đã xác nhận', 'green'],
  Active: ['Đang thực hiện', 'green'],
  InProgress: ['Đang thực hiện', 'green'],
  Fulfilling: ['Đang thực hiện', 'green'],
  Rejected: ['Đã từ chối', 'red'],
  Cancelled: ['Đã hủy', 'red'],
  Completed: ['Hoàn thành', 'green'],
  Terminated: ['Đã chấm dứt', 'red'],
  Confirmed: ['Đã xác nhận', 'green'],
  Unpaid: ['Chưa thanh toán', 'warm'],
  Paid: ['Đã thanh toán', 'green'],
  Delivered: ['Đã giao', 'green'],
  Received: ['Đã nhận', 'green'],
  Shipped: ['Đã gửi hàng', 'blue'],
  PendingPayment: ['Chờ thanh toán', 'warm'],
  Shipping: ['Đang giao hàng', 'blue'],
};
export const statusName = status => Object.hasOwn(statuses, status) ? statuses[status][0] : status;
export const badge = status => '<span class="offtake-badge '
  + (Object.hasOwn(statuses, status) ? statuses[status][1] : '') + '">'
  + esc(statusName(status)) + '</span>';

export function thumbnail(url) {
  const source = safeUrl(url);
  return '<span class="offtake-thumb">' + (source
    ? '<img src="' + esc(source) + '" alt="" loading="lazy">' : icon('leaf')) + '</span>';
}

export function notice(element, message, isError = false) {
  element.textContent = message || '';
  element.hidden = !message;
  element.setAttribute('role', isError ? 'alert' : 'status');
}

export function termsMarkup(terms, paymentTerms, showEmpty = false, articleFacts = {}) {
  const values = readContractTerms(terms, paymentTerms);
  const articles = contractArticles.filter(([, field]) => showEmpty || values[field])
    .map(([title, field]) => '<h4>' + esc(title) + '</h4>'
      + (articleFacts[field] ? '<dl class="offtake-facts">' + facts(articleFacts[field]) + '</dl>' : '')
      + '<p>' + esc(values[field] || 'Chưa nhập nội dung bổ sung') + '</p>').join('');
  const revision = values.revisionTerms ? '<p>' + esc(values.revisionTerms) + '</p>' : '';
  return articles || revision ? '<section class="offtake-terms"><h3>Điều khoản hợp đồng</h3>'
    + articles + revision + '</section>' : '';
}

export function detailMarkup(detail) {
  const info = [['Đối tác', detail.partner], ['Trạng thái', statusName(detail.status)]];
  if (detail.version != null) {
    info.push(['Phiên bản', detail.version], ['Hiệu lực', dateRange(detail.validFrom, detail.validUntil)],
      ['Tiền cọc', money(detail.deposit)]);
  }
  return '<h3>' + esc(detail.code) + '</h3><dl class="offtake-facts">' + facts(info) + '</dl>'
    + detailTable('Sản phẩm', ['Sản phẩm', 'Số lượng', 'Giá / cơ chế giá'], detail.lines.map(line =>
      [line.product, number(line.quantity) + ' ' + line.unit, line.priceFormula || money(line.unitPrice)]))
    + (detail.version == null ? '' : termsMarkup(detail.terms, detail.paymentTerms))
    + (detail.version == null ? '' : detailTable('Xác nhận điều khoản', ['Bên xác nhận', 'Thời điểm', 'Trạng thái'],
      detail.confirmations.map(item => [
        ({ Buyer: 'Bên mua', WholesaleBuyer: 'Bên mua', Farmer: 'Nhà cung cấp' })[item.role] || item.role,
        localTime(item.confirmedAt), statusName(item.status),
      ])))
    + (detail.version == null ? '' : detailTable('Mốc giao hàng',
      ['Mốc', 'Ngày dự kiến', 'Cam kết', 'Đã giao', 'Đã chấp nhận', 'Tiến độ', 'Thanh toán'],
      detail.milestones.map(item => [item.title, date(item.dueDate), number(item.quantity),
        number(item.delivered), number(item.accepted), statusName(item.status), statusName(item.paymentStatus)])))
    + detailTable('Thông tin vận chuyển', ['Mã vận đơn', 'Dự kiến', 'Đã gửi', 'Đã giao', 'Trạng thái'],
      detail.shipments.map(item => [item.trackingNumber, localTime(item.plannedAt),
        localTime(item.shippedAt), localTime(item.deliveredAt), statusName(item.status)]))
    + detailTable('Hóa đơn', ['Số hóa đơn', 'Ngày lập', 'Hạn thanh toán', 'Giá trị', 'Trạng thái'],
      detail.invoices.map(item => [item.number, localTime(item.issuedAt), localTime(item.dueAt),
        money(item.amount), statusName(item.status)]));
}

function detailTable(title, headings, rows) {
  return '<section><h3>' + esc(title) + '</h3>' + (!rows.length
    ? '<p class="muted">Chưa có dữ liệu.</p>'
    : '<table class="offtake-detail-table" role="table"><thead role="rowgroup"><tr role="row">'
      + headings.map(heading => '<th scope="col" role="columnheader">' + esc(heading) + '</th>').join('')
      + '</tr></thead><tbody role="rowgroup">' + rows.map(row => '<tr role="row">'
        + row.map((value, index) => '<td role="cell"><span class="offtake-mobile-label" aria-hidden="true">'
          + esc(headings[index]) + '</span><span>' + esc(value ?? '—')
          + '</span></td>').join('') + '</tr>').join('')
      + '</tbody></table>') + '</section>';
}
