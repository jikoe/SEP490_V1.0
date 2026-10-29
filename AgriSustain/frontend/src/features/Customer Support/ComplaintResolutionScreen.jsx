import React, { useEffect, useState } from 'react';
import { backendUrl, requestUseCase as request, queryString } from '../../api/client';
import { Field, Modal, Notice, Pager, localTime, money } from '../../components/ui/Common';
import { getSession } from '../../session/session';

const listRoute = '/support/complaints';
const statuses = {
  Open: 'Mới tiếp nhận', UnderReview: 'Đang xem xét', WaitingCustomerInfo: 'Chờ bổ sung thông tin',
  RefundPending: 'Đã xử lý – chờ hoàn tiền', Rejected: 'Đã từ chối', Closed: 'Đã đóng',
};
const resolutions = {
  FullRefund: 'Hoàn tiền toàn bộ', PartialRefund: 'Hoàn tiền một phần', RejectClaim: 'Từ chối khiếu nại',
};
const faultParties = {
  Seller: 'Nông dân / Người bán', Buyer: 'Người mua', NotDetermined: 'Chưa xác định',
};
const parties = { Buyer: 'Người mua', Seller: 'Nông dân / Người bán' };
const requestStatuses = {
  Pending: 'Đang chờ phản hồi', Responded: 'Đã phản hồi', Closed: 'Đã đóng',
  Expired: 'Quá hạn – hệ thống đã tự xử lý (Evidence Overdue)',
};
const awaitingDecision = ['Open', 'UnderReview', 'WaitingCustomerInfo'];
const emptyDecision = { resolution: '', faultParty: '', refundAmount: '', reason: '' };

function StatusBadge({ status }) {
  const tone = status === 'Rejected' ? 'Rejected' : status === 'Closed' ? 'Active' : '';
  return <span className={`badge ${tone}`}>{statuses[status] || status}</span>;
}

/** Evidence downloads need the bearer token, so they are fetched and opened as a blob. */
async function openEvidence(ticketId, evidenceId) {
  const response = await fetch(backendUrl(`/api/support/complaints/${ticketId}/evidence/${evidenceId}`), {
    headers: { Authorization: `Bearer ${getSession()?.accessToken}` },
  });
  if (!response.ok) throw new Error('Không thể mở tệp bằng chứng. Tệp có thể đã bị xóa.');
  const url = URL.createObjectURL(await response.blob());
  window.open(url, '_blank', 'noopener');
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}

/** UC-CS-01 Resolve Complaint: complaint list (SRS 4.3.1 fields 1–5) and resolution (6–10). */
export default function ComplaintResolutionScreen({ route = listRoute, navigate }) {
  const [draft, setDraft] = useState({ status: '', reason: '', search: '' });
  const [filters, setFilters] = useState({ ...draft, page: 1 });
  const [result, setResult] = useState({ items: [], total: 0, reasons: [] });
  const [detail, setDetail] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [refresh, setRefresh] = useState(0);
  const [decision, setDecision] = useState(emptyDecision);
  const [infoRequest, setInfoRequest] = useState(null);
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const ticketId = /^\/support\/complaints\/\d+$/.test(route) ? Number(route.split('/').at(-1)) : null;

  useEffect(() => {
    if (ticketId) return undefined;
    const controller = new AbortController();
    setLoading(true); setError('');
    request(`/support/complaints?${queryString(filters)}`, { signal: controller.signal })
      .then(setResult).catch((failure) => { if (failure.name !== 'AbortError') setError(failure.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [filters, refresh, ticketId]);

  useEffect(() => {
    if (!ticketId) { setDetail(null); return undefined; }
    const controller = new AbortController();
    setLoading(true); setError('');
    request(`/support/complaints/${ticketId}`, { signal: controller.signal })
      .then(setDetail).catch((failure) => { if (failure.name !== 'AbortError') setError(failure.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [ticketId, refresh]);

  function openTicket(id) {
    setDecision(emptyDecision); setMessage(''); setError('');
    navigate(`${listRoute}/${id}`);
  }

  async function submit(path, body, done) {
    setBusy(true); setError('');
    try {
      const response = await request(`/support/complaints/${ticketId}/${path}`, { method: 'POST', body });
      setMessage(response.message);
      done();
      setRefresh((value) => value + 1);
    } catch (failure) {
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  }

  function prepareResolution(event) {
    event.preventDefault(); setError('');
    if (decision.resolution === 'PartialRefund') {
      const amount = Number(decision.refundAmount);
      if (!(amount > 0 && amount <= detail.maxRefundAmount)) {
        setError(`Số tiền hoàn phải lớn hơn 0 và không vượt quá ${money(detail.maxRefundAmount)}.`);
        return;
      }
    }
    setConfirming(true);
  }

  function confirmResolution() {
    submit('resolve', {
      resolution: decision.resolution,
      faultParty: decision.faultParty,
      refundAmount: decision.resolution === 'PartialRefund' ? Number(decision.refundAmount) : null,
      reason: decision.reason.trim(),
    }, () => { setConfirming(false); setDecision(emptyDecision); });
  }

  if (!ticketId) {
    return <section>
      <div className="section-heading"><div><p className="eyebrow">CHĂM SÓC KHÁCH HÀNG & HỖ TRỢ</p>
        <h1>Xử lý khiếu nại</h1>
        <p className="muted">Xem xét bằng chứng, ghi nhận quyết định và chuyển yêu cầu hoàn tiền sang Tài chính.</p>
      </div></div>
      <Notice error>{error}</Notice>
      <form className="panel filters" onSubmit={(event) => {
        event.preventDefault(); setFilters({ ...draft, page: 1 });
      }}>
        <Field label="Tìm kiếm"><input value={draft.search} maxLength={100}
          placeholder="Mã khiếu nại hoặc mã đơn hàng"
          onChange={(event) => setDraft({ ...draft, search: event.target.value })} /></Field>
        <Field label="Trạng thái"><select value={draft.status}
          onChange={(event) => setDraft({ ...draft, status: event.target.value })}>
          <option value="">Tất cả trạng thái</option>
          {Object.entries(statuses).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select></Field>
        <Field label="Loại khiếu nại"><select value={draft.reason}
          onChange={(event) => setDraft({ ...draft, reason: event.target.value })}>
          <option value="">Tất cả loại</option>
          {result.reasons.map((reason) => <option key={reason} value={reason}>{reason}</option>)}
        </select></Field>
        <button className="primary" type="submit">Tìm kiếm</button>
      </form>
      <div className="panel"><div className="table-wrap"><table>
        <thead><tr><th>Mã khiếu nại</th><th>Đơn hàng</th><th>Người khiếu nại</th><th>Loại</th>
          <th>Ngày tạo</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
        <tbody>{result.items.map((item) => <tr key={item.ticketId}>
          <td><strong>#{item.ticketId}</strong></td>
          <td>{item.sellerOrderCode || '—'}<br /><small className="muted">{item.orderCode}</small></td>
          <td>{item.complainantName}</td><td>{item.disputeReason}</td>
          <td>{localTime(item.createdAt)}</td><td><StatusBadge status={item.ticketStatus} /></td>
          <td><button type="button" onClick={() => openTicket(item.ticketId)}>Xem</button></td>
        </tr>)}</tbody>
      </table>
      {loading && <p className="empty" role="status">Đang tải danh sách khiếu nại…</p>}
      {!loading && !result.items.length && <p className="empty">
        Không có khiếu nại nào khớp với điều kiện tìm kiếm hoặc bộ lọc đã chọn.</p>}
      </div>
      <Pager total={result.total} page={filters.page} pageSize={result.pageSize || 10}
        onPage={(page) => setFilters({ ...filters, page })} /></div>
    </section>;
  }

  const canDecide = detail && awaitingDecision.includes(detail.ticketStatus) && detail.sellerOrderId;
  return <section>
    <div className="breadcrumbs"><button className="text-button" onClick={() => navigate(listRoute)}>
      Xử lý khiếu nại</button><span>/</span><span>Khiếu nại #{ticketId}</span></div>
    <Notice>{message}</Notice><Notice error>{!confirming && !infoRequest && error}</Notice>
    {loading && !detail && <p className="empty" role="status">Đang tải khiếu nại…</p>}
    {detail && <>
      <div className="section-heading"><div><h1>Khiếu nại #{detail.ticketId}</h1>
        <p className="muted">{detail.disputeReason} · tạo lúc {localTime(detail.createdAt)}</p></div>
        <StatusBadge status={detail.ticketStatus} /></div>

      <article className="panel padded">
        <h2>Nội dung khiếu nại</h2>
        <p>{detail.description || 'Không có mô tả.'}</p>
        <p className="muted">Người khiếu nại: {detail.complainant.fullName}
          {detail.complainant.phoneNumber && ` · ${detail.complainant.phoneNumber}`}</p>
        <h3>Đơn hàng liên quan</h3>
        <p>Đơn người bán <strong>{detail.sellerOrderCode || '—'}</strong> (đơn tổng {detail.orderCode || '—'})
          · Trạng thái: {detail.sellerOrderStatus || '—'} · Giá trị: {money(detail.sellerOrderAmount || 0)}
          · Giao lúc: {localTime(detail.deliveredAt)}</p>
        <p>Người mua: {detail.buyer?.fullName || '—'} · Người bán: {detail.seller?.fullName || '—'}</p>
        <p>Người mua trả cho đơn (sau voucher/điểm): {money(detail.orderFinalAmount || 0)}
          · Đã thanh toán: {money(detail.paidAmount)} · Đã dành cho hoàn tiền: {money(detail.reservedRefundAmount)}
          · Có thể hoàn tối đa: <strong>{money(detail.maxRefundAmount)}</strong></p>
      </article>

      <article className="panel padded">
        <h2>Bằng chứng ({detail.evidences.length})</h2>
        {!detail.evidences.length && <p className="muted">Chưa có bằng chứng.</p>}
        <ul>{detail.evidences.map((evidence) => <li key={evidence.evidenceId}>
          {parties[evidence.submittedByParty]} {evidence.submittedByName} · {localTime(evidence.submittedAt)}
          {evidence.description && ` · ${evidence.description}`}{' '}
          <button type="button" className="text-button" onClick={() =>
            openEvidence(detail.ticketId, evidence.evidenceId).catch((failure) => setError(failure.message))}>
            {evidence.isDocument ? 'Mở tài liệu' : 'Xem ảnh'}</button>
        </li>)}</ul>
        {!!detail.evidenceRequests.length && <><h3>Yêu cầu bổ sung thông tin</h3>
          <ul>{detail.evidenceRequests.map((item) => <li key={item.evidenceRequestId}>
            {parties[item.party]}: {item.instructions} · hạn {localTime(item.dueAt)} ·{' '}
            {item.respondedAt ? `đã phản hồi ${localTime(item.respondedAt)}`
              : item.isOverdue ? <strong>Quá hạn – chưa phản hồi</strong>
                : requestStatuses[item.requestStatus] || item.requestStatus}
          </li>)}</ul></>}
      </article>

      {!canDecide && <article className="panel padded"><h2>Kết quả xử lý</h2>
        <p>Bên có lỗi: {faultParties[detail.faultParty] || '—'} · Số tiền hoàn:{' '}
          {detail.refundAmount ? money(detail.refundAmount) : '—'} · Xử lý lúc {localTime(detail.resolvedAt)}
          {detail.assignedStaffName && ` bởi ${detail.assignedStaffName}`}</p>
        <p>Lý do: {detail.resolutionNotes || '—'}</p>
        {detail.refunds.map((refund) => <p key={refund.refundCode}>Yêu cầu hoàn {refund.refundCode}:{' '}
          {money(refund.amount)} · {refund.refundStatus}</p>)}
      </article>}

      {canDecide && <form className="panel padded" onSubmit={prepareResolution}>
        <h2>Quyết định xử lý</h2>
        <Field label="Phương án xử lý *"><select required value={decision.resolution}
          onChange={(event) => setDecision({ ...decision, resolution: event.target.value })}>
          <option value="">Chọn phương án</option>
          {Object.entries(resolutions).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select></Field>
        <Field label="Bên có lỗi *"><select required value={decision.faultParty}
          onChange={(event) => setDecision({ ...decision, faultParty: event.target.value })}>
          <option value="">Chọn bên có lỗi</option>
          {Object.entries(faultParties).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select></Field>
        {decision.resolution === 'PartialRefund' && <Field label="Số tiền hoàn (₫) *"
          hint={`Tối đa ${money(detail.maxRefundAmount)}`}><input type="number" required min="1"
          step="1" max={detail.maxRefundAmount} value={decision.refundAmount}
          onChange={(event) => setDecision({ ...decision, refundAmount: event.target.value })} /></Field>}
        <Field label="Lý do quyết định *"><textarea required maxLength={1000} value={decision.reason}
          onChange={(event) => setDecision({ ...decision, reason: event.target.value })} /></Field>
        <div className="modal-actions">
          <button type="button" disabled={busy} onClick={() => setInfoRequest({ party: '', instructions: '' })}>
            Yêu cầu bổ sung thông tin</button>
          <button type="button" disabled={busy} onClick={() => { setDecision(emptyDecision); setError(''); }}>
            Hủy</button>
          <button className="primary" type="submit" disabled={busy}>Xác nhận quyết định</button>
        </div>
      </form>}
    </>}

    {confirming && <Modal title="Xác nhận quyết định" onClose={() => { if (!busy) setConfirming(false); }}>
      <p>{resolutions[decision.resolution]} cho khiếu nại #{ticketId}
        {decision.resolution === 'FullRefund' && ` – ${money(detail.maxRefundAmount)}`}
        {decision.resolution === 'PartialRefund' && ` – ${money(Number(decision.refundAmount))}`}.</p>
      {decision.resolution !== 'RejectClaim'
        ? <p className="muted">Một yêu cầu hoàn tiền sẽ được chuyển sang bộ phận Tài chính; đơn hàng chuyển sang
          trạng thái chờ hoàn tiền.</p>
        : <p className="muted">Đơn hàng giữ nguyên trạng thái đã giao.</p>}
      <Notice error>{error}</Notice>
      <div className="modal-actions"><button type="button" disabled={busy} onClick={() => setConfirming(false)}>
        Quay lại</button><button className="primary" type="button" disabled={busy} onClick={confirmResolution}>
        {busy ? 'Đang xử lý…' : 'Xác nhận'}</button></div>
    </Modal>}

    {infoRequest && <Modal title="Yêu cầu bổ sung thông tin" onClose={() => { if (!busy) setInfoRequest(null); }}>
      <form onSubmit={(event) => {
        event.preventDefault();
        submit('request-info', { party: infoRequest.party, instructions: infoRequest.instructions.trim() },
          () => setInfoRequest(null));
      }}>
        <Field label="Bên cần bổ sung *"><select required autoFocus value={infoRequest.party}
          onChange={(event) => setInfoRequest({ ...infoRequest, party: event.target.value })}>
          <option value="">Chọn bên</option>
          {Object.entries(parties).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select></Field>
        <Field label="Thông tin cần bổ sung *" hint="Bên liên quan có 48 giờ để phản hồi.">
          <textarea required maxLength={1000} value={infoRequest.instructions}
            onChange={(event) => setInfoRequest({ ...infoRequest, instructions: event.target.value })} /></Field>
        <Notice error>{error}</Notice>
        <div className="modal-actions"><button type="button" disabled={busy} onClick={() => setInfoRequest(null)}>
          Hủy</button><button className="primary" disabled={busy}>{busy ? 'Đang gửi…' : 'Gửi yêu cầu'}</button></div>
      </form>
    </Modal>}
  </section>;
}
