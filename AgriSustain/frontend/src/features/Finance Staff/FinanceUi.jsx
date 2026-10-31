import React, { useEffect, useState } from 'react';
import { request, queryString } from '../../api/client';
import { localTime, money, Notice, Pager } from '../../components/ui/Common';
import { displayMessage } from '../../utils/displayMessage';
import './finance.css';

export { localTime, money, Pager };
export function financeMessage(value) {
  return displayMessage(value).replace(/\b(?:MSG\d+|UC-[A-Z]+-\d+)\b/gi, '')
    .replace(/\(\s*\)/g, '').replace(/^\s*[-:·–]+\s*/, '').trim();
}
export const statuses = {
  Pending: 'Chờ xác minh', AwaitingPayment: 'Chờ xác minh', Processing: 'Đang xử lý',
  Succeeded: 'Thành công', Success: 'Thành công', Paid: 'Đã thanh toán', Completed: 'Hoàn thành',
  Failed: 'Thất bại', Expired: 'Hết hạn', Cancelled: 'Đã hủy', Active: 'Đang hoạt động',
  PendingPayment: 'Chờ thanh toán', Confirmed: 'Đã xác nhận', Issued: 'Đã phát hành',
  Approved: 'Đã duyệt', PartiallyRefunded: 'Hoàn một phần', Refunded: 'Đã hoàn tiền',
  Delivered: 'Đã giao hàng', Received: 'Đã nhận hàng', Shipping: 'Đang giao hàng',
  Preparing: 'Đang chuẩn bị', PendingConfirmation: 'Chờ xác nhận', Draft: 'Bản nháp',
  Requested: 'Khởi tạo yêu cầu', Mismatch: 'Có sai lệch', LatePayment: 'Thanh toán sau khi hủy',
  NeedsReview: 'Cần kiểm tra', VerificationFailed: 'Chưa xác minh được', AlreadyApplied: 'Đã ghi nhận',
};

export function Status({ value, label }) {
  const success = ['Succeeded', 'Success', 'Paid', 'Completed', 'Active'].includes(value);
  const error = ['Failed', 'Cancelled', 'Expired'].includes(value);
  return <span className={`fin-status ${success ? 'success' : error ? 'danger' : 'pending'}`}>
    {label || statuses[value] || 'Cần kiểm tra'}</span>;
}

export function todayVietnam() {
  const parts = Object.fromEntries(new Intl.DateTimeFormat('en', {
    timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit',
  }).formatToParts(new Date()).map(part => [part.type, part.value]));
  return `${parts.year}-${parts.month}-${parts.day}`;
}

export const dateLabel = date => date ? date.split('-').reverse().join('/') : '—';
export const financeUrl = (path, values = {}) => `${path}${queryString(values) ? `?${queryString(values)}` : ''}`;
export const financeApi = (path, sample, values = {}) => financeUrl(`/finance/${path}`, { ...values, sample });

export function useFinanceSource() {
  const [sample, setValue] = useState(() => {
    try { return sessionStorage.getItem('agrisustain:finance-source') !== 'database'; }
    catch { return true; }
  });
  function setSample(value) {
    setValue(value);
    try { sessionStorage.setItem('agrisustain:finance-source', value ? 'sample' : 'database'); }
    catch { /* Storage may be disabled; the current selection still works. */ }
  }
  return [sample, setSample];
}

export function useFinanceData(path) {
  const [data, setData] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(Boolean(path));
  const [reload, setReload] = useState(0);
  useEffect(() => {
    if (!path) return;
    const controller = new AbortController();
    setLoading(true); setError('');
    request(path, { signal: controller.signal }).then(result => {
      if (!controller.signal.aborted) setData(result);
    }).catch(failure => {
      if (failure.name !== 'AbortError') setError(financeMessage(failure.message));
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [path, reload]);
  return { data, setData, error, loading, refresh: () => setReload(value => value + 1) };
}

export function FinanceHeader({ title, description, children, onRefresh, busy, sample }) {
  return <div className="fin-heading"><div><h1>{title}</h1><p>{description}</p></div>
    <div className="fin-heading-actions">{sample && <span className="fin-mode-badge">Dữ liệu mẫu</span>}{children}
      {onRefresh && <button type="button" disabled={busy} onClick={onRefresh}>
        <span aria-hidden="true">↻</span> {busy ? 'Đang tải…' : 'Làm mới'}</button>}</div></div>;
}

export function FinanceSource({ sample, onChange, disabled = false }) {
  return <><div className="fin-source-note" role="note">{sample
    ? 'Đang xem dữ liệu mẫu. Thao tác không thay đổi dữ liệu tài chính thật. Kết quả mẫu chỉ được lưu tạm trong phiên sử dụng.'
    : 'Đang xem dữ liệu từ cơ sở dữ liệu. Kết nối xác minh cổng thanh toán sẽ được tích hợp sau.'}</div>
    <div className="fin-footer"><span>Đơn vị: VND · Thời gian Việt Nam (UTC+7)</span>
      <label>Nguồn dữ liệu <select value={sample ? 'sample' : 'database'} disabled={disabled}
        onChange={event => onChange(event.target.value === 'sample')}>
        <option value="sample">Dữ liệu mẫu</option><option value="database">Cơ sở dữ liệu</option>
      </select></label></div></>;
}

export function LoadState({ loading, error, retry, hasData = false }) {
  return <>{error && <Notice error>{error}{' '}
    <button type="button" disabled={loading} onClick={retry}>Thử lại</button>
    {hasData && <span> Đang giữ kết quả của lần tải trước.</span>}</Notice>}
    {loading && <p className="fin-loading" role="status">Đang tải dữ liệu tài chính…</p>}</>;
}

export function PaymentTable({ items, onOpen, compact = false }) {
  return <div className="fin-table-wrap"><table className="fin-table">
    <thead><tr><th>Mã giao dịch / {compact ? 'Nghĩa vụ liên kết' : 'Tham chiếu cổng'}</th>
      {!compact && <th>Loại / Nghĩa vụ liên kết</th>}<th>Cổng thanh toán</th><th className="amount">Số tiền</th>
      {!compact && <th>Tạo lúc / Thanh toán lúc</th>}<th>Trạng thái</th><th>Thao tác</th></tr></thead>
    <tbody>{items.map(row => <tr key={row.id}><td><strong>{row.code}</strong>
      <small>{compact ? row.obligation : row.reference || 'Chưa có tham chiếu'}</small></td>
      {!compact && <td>{row.category === 'Order' ? 'Đơn hàng' : 'Gói thành viên'}<small>{row.obligation}</small></td>}
      <td>{row.provider}</td><td className="amount">{money(row.amount)}</td>
      {!compact && <td>{localTime(row.requestedAt)}<small>{row.paidAt
        ? `Đã trả: ${localTime(row.paidAt)}` : 'Chưa xác minh thời gian trả'}</small></td>}
      <td><Status value={row.status} />{row.needsReview && <small className="fin-discrepancy">
        {financeMessage(row.note) || 'Có sai lệch cần kiểm tra'}</small>}{row.cancelledOrder && <small>Đơn hàng đã hủy</small>}</td>
      <td><button className="fin-view" type="button" onClick={() => onOpen(row.id)}>Xem chi tiết</button></td></tr>)}</tbody>
  </table></div>;
}
