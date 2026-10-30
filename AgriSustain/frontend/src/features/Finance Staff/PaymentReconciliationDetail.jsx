import React, { useRef, useState } from 'react';
import { request } from '../../api/client';
import { Modal, Notice } from '../../components/ui/Common';
import {
  financeApi, financeMessage, FinanceHeader, FinanceSource, LoadState, localTime, money,
  Status, useFinanceData,
} from './FinanceUi';

function Match({ expected, actual, amount = false }) {
  if (actual == null) return <span className="fin-muted">Chưa có kết quả</span>;
  const matches = expected === actual;
  return <span className={`fin-status ${matches ? 'success' : 'pending'}`}>
    {matches ? 'Khớp' : amount ? `Lệch ${money(Math.abs(expected - actual))}` : 'Cần kiểm tra'}</span>;
}

export default function PaymentReconciliationDetail({ id, route, sample, setSample, navigate }) {
  const result = useFinanceData(financeApi(`payments/${id}`, sample));
  const [confirm, setConfirm] = useState(false);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState(null);
  const operation = useRef(null);
  const data = result.data;
  const row = data?.payment;
  const receipt = data?.lastReceipt;
  const latest = data?.history[0];
  const back = () => navigate(`/finance/payments${route.includes('?') ? `?${route.split('?')[1]}` : ''}`);

  async function reconcile() {
    if (busy || !data?.canReconcile) return;
    if (operation.current?.version !== data.version) {
      operation.current = { version: data.version, requestKey: crypto.randomUUID() };
    }
    setBusy(true); setNotice(null);
    try {
      const response = await request(financeApi(`payments/${id}/reconcile`, sample), {
        method: 'POST', body: operation.current,
      });
      result.setData(response.detail);
      setNotice({ message: financeMessage(response.message), error: !['Succeeded', 'AlreadyApplied', 'Pending'].includes(response.outcome) });
      setConfirm(false);
    } catch (failure) {
      setNotice({ message: financeMessage(failure.message), error: true });
      if (failure.status === 409) result.refresh();
      setConfirm(false);
    } finally { setBusy(false); }
  }

  return <section className="finance-page">
    <button type="button" className="fin-back" onClick={back} disabled={busy}>← Danh sách giao dịch</button>
    <FinanceHeader sample={sample} title={row?.code || 'Chi tiết giao dịch'}
      description={row ? `Đối chiếu nghĩa vụ thanh toán và kết quả từ ${row.provider}.` : 'Đang tải thông tin thanh toán.'}
      onRefresh={result.refresh} busy={busy || result.loading}>
      {row && <Status value={row.status} />}</FinanceHeader>
    <LoadState {...result} retry={result.refresh} hasData={Boolean(data)} />
    <Notice error={notice?.error}>{notice?.message}</Notice>
    {data && <>
      {row.needsReview && !notice && <div className="fin-warning" role="alert">
        <strong>{latest?.outcome === 'LatePayment' ? 'Thanh toán cần kiểm tra' : 'Giao dịch có sai lệch'}</strong>
        <p>{financeMessage(row.note) || 'Vui lòng đối chiếu lại tham chiếu, số tiền và kết quả thanh toán.'}</p></div>}
      <div className="fin-detail-grid">
        <section className="fin-panel"><div className="fin-panel-heading"><h2>Thông tin thanh toán</h2>
          <span>Chỉ đọc</span></div><dl className="fin-facts">
          <div><dt>Mã giao dịch</dt><dd>{row.code}</dd></div>
          <div><dt>Cổng / Hình thức</dt><dd>{row.provider} / {
            data.method === 'BankTransfer' ? 'Chuyển khoản' : data.method === 'OnlineBanking' ? 'Thanh toán trực tuyến' : data.method}</dd></div>
          <div><dt>Tham chiếu cổng</dt><dd>{row.reference || 'Chưa có tham chiếu'}</dd></div>
          <div><dt>Số tiền yêu cầu</dt><dd>{money(row.amount)}</dd></div>
          <div><dt>Thời gian tạo</dt><dd>{localTime(row.requestedAt)}</dd></div>
          <div><dt>Thời gian thanh toán đã xác minh</dt><dd>{row.paidAt ? localTime(row.paidAt) : 'Chưa xác minh'}</dd></div>
          <div><dt>Đối soát gần nhất</dt><dd>{data.reconciledAt ? localTime(data.reconciledAt) : 'Chưa đối soát'}</dd></div>
          <div><dt>Trạng thái thanh toán</dt><dd><Status value={row.status} /></dd></div>
        </dl></section>
        <section className="fin-panel"><div className="fin-panel-heading"><h2>Nghĩa vụ liên kết</h2></div>
          {data.obligation ? <dl className="fin-facts">
            <div><dt>Loại nghĩa vụ</dt><dd>{data.obligation.category === 'Order' ? 'Thanh toán đơn hàng' : 'Gói thành viên'}</dd></div>
            <div><dt>Mã / Định danh</dt><dd>{data.obligation.reference}</dd></div>
            <div><dt>Giá trị nghĩa vụ</dt><dd>{money(data.obligation.amount)}</dd></div>
            <div><dt>Còn phải thanh toán</dt><dd>{money(data.obligation.unpaid)}</dd></div>
            <div><dt>Tình trạng nghĩa vụ</dt><dd><Status value={data.obligation.status} /></dd></div>
          </dl> : <p className="fin-empty">Chưa xác định được nghĩa vụ của giao dịch.</p>}
        </section>
      </div>
      <section className="fin-panel"><div className="fin-panel-heading">
        <h2>Đối chiếu kết quả từ cổng thanh toán</h2>
        <span>{receipt ? `Đã truy vấn · ${localTime(receipt.checkedAt)}` : 'Chưa có kết quả từ cổng'}</span></div>
        <div className="fin-table-wrap"><table className="fin-table fin-comparison"><thead><tr>
          <th>Tiêu chí</th><th>Dữ liệu hệ thống</th><th>Kết quả từ {row.provider}</th><th>Đối chiếu</th>
        </tr></thead><tbody><tr><td>Tham chiếu</td><td>{row.reference || 'Chưa có'}</td>
          <td>{receipt?.reference || 'Chưa có kết quả'}</td><td><Match expected={row.reference} actual={receipt?.reference} /></td></tr>
          <tr><td>Số tiền</td><td>{money(row.amount)}</td><td>{receipt ? money(receipt.amount) : 'Chưa có kết quả'}</td>
            <td><Match expected={row.amount} actual={receipt?.amount} amount /></td></tr>
          <tr><td>Kết quả thanh toán</td><td><Status value={row.status} /></td><td>{receipt
            ? <Status value={receipt.status} /> : 'Chưa xác minh'}</td><td>{receipt
              ? <Match expected={row.status === 'Paid' ? 'Succeeded' : row.status} actual={receipt.status} />
              : <span className="fin-muted">Chờ kiểm tra</span>}</td></tr></tbody></table></div>
        <div className="fin-reconcile-actions"><div><p>Kiểm tra lại trạng thái và quyền truy cập trước khi ghi nhận.</p>
          <p>Mỗi khoản thu chỉ được áp dụng một lần.</p>{!data.canReconcile && <p>{data.unavailableReason
            || 'Khoản thu đã được ghi nhận. Xem kết quả trong lịch sử đối soát.'}</p>}</div>
          <button className="fin-primary" type="button" disabled={busy || result.loading || !data.canReconcile}
            onClick={() => setConfirm(true)}>{busy ? 'Đang đối soát…' : 'Đối soát giao dịch'}</button></div>
      </section>
      <section className="fin-panel"><div className="fin-panel-heading"><h2>Lịch sử đối soát</h2>
        <span>Kết quả · Trạng thái · Nhân viên · Thời gian</span></div>
        <ol className="fin-history">{data.history.map(entry => <li key={entry.id}>
          <div><strong>{financeMessage(entry.message)}</strong><time>{localTime(entry.at)}</time></div>
          <p><Status value={entry.before} /> → <Status value={entry.after} /> · {entry.staff} · {row.provider}</p>
          {entry.receipt && <p>Bằng chứng: {entry.receipt.eventId} · {money(entry.receipt.amount)} · {
            entry.receipt.reference}</p>}</li>)}
          <li><div><strong>Khởi tạo yêu cầu thanh toán</strong><time>{localTime(row.requestedAt)}</time></div>
            <p>Số tiền yêu cầu: {money(row.amount)} · {row.obligation}</p></li></ol>
      </section>
    </>}
    <FinanceSource sample={sample} onChange={setSample} disabled={busy} />
    {confirm && data && <Modal title="Xác nhận đối soát" onClose={() => { if (!busy) setConfirm(false); }}>
      <p>Đối soát giao dịch <strong>{row.code}</strong> qua <strong>{row.provider}</strong>?</p>
      <p>Số tiền yêu cầu: <strong>{money(row.amount)}</strong></p>
      {sample && <p className="fin-calculation">Đây là giao dịch mẫu. Kết quả không ghi vào dữ liệu tài chính thật.</p>}
      <div className="modal-actions"><button type="button" disabled={busy} onClick={() => setConfirm(false)}>Hủy</button>
        <button type="button" className="primary" disabled={busy} onClick={reconcile}>
          {busy ? 'Đang đối soát…' : 'Xác nhận đối soát'}</button></div>
    </Modal>}
  </section>;
}
