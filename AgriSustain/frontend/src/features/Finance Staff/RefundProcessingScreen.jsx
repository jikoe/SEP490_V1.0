import React, { useEffect, useState } from 'react';
import { requestUseCase as request, queryString } from '../../api/client';
import { Field, Modal, Notice, Pager, localTime, money } from '../../components/ui/Common';

const listRoute = '/finance/refunds';
const statuses = { Pending: 'Chờ hoàn tiền', Completed: 'Đã hoàn tiền' };
const methods = { BankTransfer: 'Chuyển khoản ngân hàng', GatewayRefund: 'Hoàn qua cổng thanh toán (PayOS/VietQR)' };
const actions = {
  ResolveComplaint: 'Quyết định khiếu nại', CompleteRefund: 'Xác nhận đã hoàn tiền',
  RefundTransferFailed: 'Chuyển tiền thất bại',
};
const faultParties = { Seller: 'Nông dân / Người bán', Buyer: 'Người mua', NotDetermined: 'Chưa xác định' };

function StatusBadge({ status }) {
  return <span className={`badge ${status === 'Completed' ? 'Active' : ''}`}>{statuses[status] || status}</span>;
}

/** UC-FIN-03 Process Refund: refund requests from resolved complaints and their settlement. */
export default function RefundProcessingScreen({ route = listRoute, navigate }) {
  const [draft, setDraft] = useState({ status: 'Pending', search: '' });
  const [filters, setFilters] = useState({ ...draft, page: 1 });
  const [result, setResult] = useState({ items: [], total: 0 });
  const [detail, setDetail] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [refresh, setRefresh] = useState(0);
  const [completing, setCompleting] = useState(null);
  const [failing, setFailing] = useState(null);
  const [busy, setBusy] = useState(false);
  const refundId = /^\/finance\/refunds\/\d+$/.test(route) ? Number(route.split('/').at(-1)) : null;

  useEffect(() => {
    if (refundId) return undefined;
    const controller = new AbortController();
    setLoading(true); setError('');
    request(`/finance/refunds?${queryString(filters)}`, { signal: controller.signal })
      .then(setResult).catch((failure) => { if (failure.name !== 'AbortError') setError(failure.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [filters, refresh, refundId]);

  useEffect(() => {
    if (!refundId) { setDetail(null); return undefined; }
    const controller = new AbortController();
    setLoading(true); setError('');
    request(`/finance/refunds/${refundId}`, { signal: controller.signal })
      .then(setDetail).catch((failure) => { if (failure.name !== 'AbortError') setError(failure.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [refundId, refresh]);

  async function submit(path, body, done) {
    setBusy(true); setError('');
    try {
      const response = await request(`/finance/refunds/${refundId}/${path}`, { method: 'POST', body });
      setMessage(response.message);
      done();
      setRefresh((value) => value + 1);
    } catch (failure) {
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  }

  if (!refundId) {
    return <section>
      <div className="section-heading"><div><p className="eyebrow">TÀI CHÍNH & KẾ TOÁN</p>
        <h1>Xử lý hoàn tiền</h1>
        <p className="muted">Hoàn tiền cho các khiếu nại đã được bộ phận hỗ trợ chấp nhận.</p></div></div>
      <Notice error>{error}</Notice>
      <form className="panel filters" onSubmit={(event) => {
        event.preventDefault(); setFilters({ ...draft, page: 1 });
      }}>
        <Field label="Tìm kiếm"><input value={draft.search} maxLength={100}
          placeholder="Mã hoàn tiền hoặc mã đơn hàng"
          onChange={(event) => setDraft({ ...draft, search: event.target.value })} /></Field>
        <Field label="Trạng thái"><select value={draft.status}
          onChange={(event) => setDraft({ ...draft, status: event.target.value })}>
          <option value="">Tất cả trạng thái</option>
          {Object.entries(statuses).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select></Field>
        <Field label="Hình thức hoàn"><select value={draft.refundMethod || ''}
          onChange={(event) => setDraft({ ...draft, refundMethod: event.target.value })}>
          <option value="">Tất cả hình thức</option>
          {Object.entries(methods).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select></Field>
        <button className="primary" type="submit">Tìm kiếm</button>
      </form>
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>Mã hoàn tiền</th><th>Đơn hàng</th><th>Khách hàng</th><th>Số tiền</th>
          <th>Ngày yêu cầu</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
        <tbody>{result.items.map((item) => <tr key={item.refundId}>
          <td><strong>{item.refundCode}</strong>{item.ticketId && <><br />
            <small className="muted">Khiếu nại #{item.ticketId}</small></>}</td>
          <td>{item.sellerOrderCode || '—'}<br /><small className="muted">{item.orderCode}</small></td>
          <td>{item.buyerName || '—'}</td><td>{money(item.amount)}{item.refundMethod && <><br />
            <small className="muted">{methods[item.refundMethod] || item.refundMethod}</small></>}</td>
          <td>{localTime(item.requestedAt)}</td><td><StatusBadge status={item.refundStatus} /></td>
          <td><button type="button" onClick={() => { setMessage(''); navigate(`${listRoute}/${item.refundId}`); }}>
            Xem</button></td>
        </tr>)}</tbody>
      </table>
      {loading && <p className="empty" role="status">Đang tải yêu cầu hoàn tiền…</p>}
      {!loading && !result.items.length && <p className="empty">Không có yêu cầu hoàn tiền nào phù hợp.</p>}
      </div>
      <Pager total={result.total} page={filters.page} pageSize={result.pageSize || 10}
        onPage={(page) => setFilters({ ...filters, page })} /></div>
    </section>;
  }

  return <section>
    <div className="breadcrumbs"><button className="text-button" onClick={() => navigate(listRoute)}>
      Xử lý hoàn tiền</button><span>/</span><span>{detail?.refundCode || `#${refundId}`}</span></div>
    <Notice>{message}</Notice><Notice error>{!completing && !failing && error}</Notice>
    {loading && !detail && <p className="empty" role="status">Đang tải yêu cầu hoàn tiền…</p>}
    {detail && <>
      <div className="section-heading"><div><h1>{detail.refundCode} · {money(detail.amount)}</h1>
        <p className="muted">Khiếu nại #{detail.ticketId || '—'} · {detail.disputeReason || ''}</p></div>
        <StatusBadge status={detail.refundStatus} /></div>

      <article className="panel padded">
        <h2>Quyết định khiếu nại</h2>
        <p>Bên có lỗi: {faultParties[detail.faultParty] || '—'} · Quyết định lúc {localTime(detail.decidedAt)}
          {detail.decidedByName && ` bởi ${detail.decidedByName}`}</p>
        <p>Lý do: {detail.resolutionNotes || detail.reason || '—'}</p>
        <h3>Khách hàng nhận hoàn tiền</h3>
        <p>{detail.buyerName || '—'} · {detail.buyerEmail || '—'} · {detail.buyerPhone || '—'}</p>
        <h3>Giao dịch thanh toán gốc</h3>
        <p>{detail.paymentCode} · {detail.paymentProvider} / {detail.paymentMethod} · {money(detail.paymentAmount)}
          · {detail.paymentStatus} · thanh toán lúc {localTime(detail.paidAt)}</p>
        <p>Đơn người bán {detail.sellerOrderCode || '—'} ({detail.sellerOrderStatus || '—'}) · Đơn tổng{' '}
          {detail.orderCode || '—'} ({detail.orderStatus || '—'})</p>
        {detail.refundStatus === 'Completed' && <p>Đã hoàn lúc {localTime(detail.processedAt)} bởi{' '}
          {detail.processedByName} · {methods[detail.refundMethod] || detail.refundMethod || '—'}
          {' '}· Mã giao dịch: <strong>{detail.transferReference}</strong></p>}
      </article>

      {!!detail.history.length && <article className="panel padded"><h2>Lịch sử xử lý</h2>
        <ul>{detail.history.map((item, index) => <li key={index}>{localTime(item.occurredAt)} ·{' '}
          {actions[item.action] || item.action} · {item.performedByName || '—'}
          {item.reason && ` · ${item.reason}`}</li>)}</ul></article>}

      {detail.refundStatus === 'Pending' && <div className="modal-actions">
        <button type="button" disabled={busy} onClick={() => setFailing({ reason: '' })}>
          Ghi nhận chuyển tiền thất bại</button>
        <button className="primary" type="button" disabled={busy}
          onClick={() => setCompleting({ refundMethod: '', transferReference: '', note: '' })}>
          Xác nhận đã hoàn tiền</button>
      </div>}
    </>}

    {completing && <Modal title="Xác nhận đã hoàn tiền" onClose={() => { if (!busy) setCompleting(null); }}>
      <form onSubmit={(event) => {
        event.preventDefault();
        submit('complete', {
          refundMethod: completing.refundMethod,
          transferReference: completing.transferReference.trim(),
          note: completing.note.trim() || null,
        }, () => setCompleting(null));
      }}>
        <p>Hoàn <strong>{money(detail.amount)}</strong> cho {detail.buyerName}.</p>
        <Field label="Hình thức hoàn tiền *"><select required autoFocus value={completing.refundMethod}
          onChange={(event) => setCompleting({ ...completing, refundMethod: event.target.value })}>
          <option value="">Chọn hình thức</option>
          {Object.entries(methods).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select></Field>
        <Field label="Mã giao dịch chuyển tiền *"><input required maxLength={150}
          value={completing.transferReference}
          onChange={(event) => setCompleting({ ...completing, transferReference: event.target.value })} /></Field>
        <Field label="Ghi chú"><textarea maxLength={500} value={completing.note}
          onChange={(event) => setCompleting({ ...completing, note: event.target.value })} /></Field>
        <Notice error>{error}</Notice>
        <div className="modal-actions"><button type="button" disabled={busy} onClick={() => setCompleting(null)}>
          Hủy</button><button className="primary" disabled={busy}>{busy ? 'Đang lưu…' : 'Xác nhận'}</button></div>
      </form>
    </Modal>}

    {failing && <Modal title="Ghi nhận chuyển tiền thất bại" onClose={() => { if (!busy) setFailing(null); }}>
      <form onSubmit={(event) => {
        event.preventDefault();
        submit('transfer-failed', { reason: failing.reason.trim() }, () => setFailing(null));
      }}>
        <p className="muted">Yêu cầu hoàn tiền vẫn ở trạng thái chờ để thực hiện lại.</p>
        <Field label="Lý do thất bại *"><textarea required autoFocus maxLength={500} value={failing.reason}
          onChange={(event) => setFailing({ reason: event.target.value })} /></Field>
        <Notice error>{error}</Notice>
        <div className="modal-actions"><button type="button" disabled={busy} onClick={() => setFailing(null)}>
          Hủy</button><button className="danger" disabled={busy}>{busy ? 'Đang lưu…' : 'Ghi nhận'}</button></div>
      </form>
    </Modal>}
  </section>;
}
