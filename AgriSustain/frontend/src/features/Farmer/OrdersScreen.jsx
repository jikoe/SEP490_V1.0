import React, { useState } from 'react';
import { request, queryString } from '../../api/client';
import { Field, Notice, Pager, ProductThumbnail, localTime, money } from '../../components/ui/Common';
import { Confirm, Empty, Errors, Status, Uploads, orderStatus, useFeatureData } from '../Commerce/Shared';

const initial = { keyword: '', status: '', orderType: '', paymentStatus: '', from: '', to: '', page: 1 };
const actionNames = { confirm: 'Xác nhận đơn', ready: 'Đánh dấu sẵn sàng giao',
  dispatch: 'Bắt đầu giao mô phỏng', reject: 'Xác nhận từ chối',
  'delivery-report': 'Gửi báo cáo giao hàng', evidence: 'Gửi phản hồi' };

const orderViews = [['list', 'Đơn hàng của nông trại'], ['prepare', 'Chuẩn bị / giao mô phỏng'],
  ['reject', 'Từ chối / giao thất bại'], ['complaint', 'Khiếu nại và tài chính']];

export default function OrdersScreen({ route, navigate }) {
  const suffix = route.slice('/farmer/orders'.length);
  const match = suffix.match(/^\/(\d+)(?:\/(prepare|reject|complaint))?$/);
  const id = match ? Number(match[1]) : null;
  const view = match ? match[2] || 'prepare'
    : orderViews.some(([key]) => suffix === '/' + key) ? suffix.slice(1) : 'list';
  function openView(next) {
    navigate(next === 'list' ? '/farmer/orders' : '/farmer/orders/' + (id ? id + '/' : '') + next);
  }
  return <div className="commerce">
    <nav className="commerce-tabs" aria-label="Các màn hình xử lý đơn hàng">
      {orderViews.map(([key, label]) => <button type="button" key={key}
        className={view === key ? 'active' : ''} aria-current={view === key ? 'page' : undefined}
        onClick={() => openView(key)}>{label}</button>)}
    </nav>
    {id ? <OrderDetail id={id} navigate={navigate} tab={view} />
      : <OrderList navigate={navigate} selectedView={view} />}
  </div>;
}

function OrderList({ navigate, selectedView }) {
  const [filters, setFilters] = useState(initial);
  const [draft, setDraft] = useState(initial);
  const state = useFeatureData(signal => request('/farmer/orders?' + queryString(filters), { signal }),
    [JSON.stringify(filters)]);
  function apply(event) {
    event.preventDefault();
    if (draft.from && draft.to && draft.from > draft.to) {
      state.setError(new Error('Ngày bắt đầu không được sau ngày kết thúc.')); return;
    }
    setFilters({ ...draft, from: draft.from ? new Date(draft.from + 'T00:00:00+07:00').toISOString() : '',
      to: draft.to ? new Date(Date.parse(draft.to + 'T00:00:00+07:00') + 86400000).toISOString() : '', page: 1 });
  }
  return <div className="commerce"><div className="commerce-top"><div>
    <p className="commerce-subtitle">UC-FAR-06 · Đơn hàng của nông trại</p><h1>Xử lý đơn hàng</h1>
    <p className="muted">Chuẩn bị hàng, giao mô phỏng và theo dõi phản hồi của người mua.</p></div>
    <button type="button" onClick={() => navigate('/farmer/products')}>← Quản lý sản phẩm</button></div>
    <Errors error={state.error} hideMessageCodes />
    {state.error && <button type="button" onClick={state.reload}>Tải lại danh sách</button>}
    {selectedView !== 'list' && <Notice>Chọn đơn hàng bên dưới để mở màn {orderViews.find(([key]) => key === selectedView)?.[1]}.</Notice>}
    <section className="panel"><form className="commerce-filters padded" onSubmit={apply}>
      <Field label="Tìm kiếm"><input value={draft.keyword} maxLength={255} placeholder="Mã đơn / người nhận…"
        onChange={event => setDraft({ ...draft, keyword: event.target.value })} /></Field>
      <Field label="Trạng thái"><select value={draft.status} onChange={event => setDraft({ ...draft, status: event.target.value })}>
        <option value="">Tất cả trạng thái</option>{Object.entries(orderStatus).filter(([value]) =>
          !['Ready', 'InTransit'].includes(value)).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></Field>
      <Field label="Loại đơn"><select value={draft.orderType} onChange={event => setDraft({ ...draft, orderType: event.target.value })}>
        <option value="">Tất cả</option><option value="Retail">Bán lẻ</option><option value="Wholesale">Bán buôn</option>
        <option value="PreOrder">Đặt trước</option></select></Field>
      <Field label="Thanh toán"><select value={draft.paymentStatus} onChange={event => setDraft({ ...draft, paymentStatus: event.target.value })}>
        <option value="">Tất cả</option><option value="Paid">Đã có thanh toán xác minh</option>
        <option value="AwaitingPayment">Chưa có thanh toán xác minh</option></select></Field>
      <Field label="Từ ngày"><input type="date" value={draft.from} onChange={event => setDraft({ ...draft, from: event.target.value })} /></Field>
      <Field label="Đến ngày"><input type="date" value={draft.to} min={draft.from || undefined}
        onChange={event => setDraft({ ...draft, to: event.target.value })} /></Field>
      <button className="primary">Áp dụng</button><button type="button" onClick={() => {
        setDraft(initial); setFilters(initial);
      }}>Xóa bộ lọc</button></form>
      {state.loading ? <p className="padded" role="status">Đang tải đơn hàng…</p>
        : state.data?.items.length === 0 ? <Empty>Không có đơn hàng phù hợp</Empty>
        : <div className="table-wrap"><table><thead><tr><th>Đơn con / Đơn tổng</th><th>Người mua / Ngày</th>
          <th>Hàng hóa / Thanh toán</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>
          {state.data?.items.map(order => <tr key={order.sellerOrderId}>
            <td><strong>{order.sellerOrderCode}</strong><small>{order.orderCode} · {order.orderType}</small></td>
            <td>{order.recipientName}<small>{localTime(order.orderDate)}</small></td>
            <td>{order.items.map(item => item.productName + ' · ' + item.quantity + ' ' + item.unit).join(', ')}
              <small>{order.paymentStatus === 'Paid' ? 'Đã đủ thanh toán/cọc xác minh' : 'Chưa đủ thanh toán/cọc'}</small></td>
            <td><Status value={order.status} /></td><td><button type="button" onClick={() =>
              navigate('/farmer/orders/' + order.sellerOrderId + '/' + (selectedView === 'list' ? 'prepare' : selectedView))}>
              {selectedView === 'complaint' ? 'Theo dõi / khiếu nại' : 'Xem và xử lý'}</button>
              {selectedView === 'list' && <button type="button" onClick={() =>
                navigate('/farmer/orders/' + order.sellerOrderId + '/complaint')}>Theo dõi / khiếu nại</button>}</td></tr>)}
        </tbody></table></div>}
      {state.data && <Pager total={state.data.total} page={filters.page} onPage={page => setFilters({ ...filters, page })} />}
    </section></div>;
}

function OrderDetail({ id, navigate, tab }) {
  const state = useFeatureData(signal => request('/farmer/orders/' + id, { signal }), [id]);
  const [pending, setPending] = useState(null);
  const [busy, setBusy] = useState(false);
  const [reason, setReason] = useState('');
  const [explanation, setExplanation] = useState('');
  const [report, setReport] = useState('');
  const [reportType, setReportType] = useState('DeliveryFailure');
  const [reportFiles, setReportFiles] = useState([]);
  const [uploading, setUploading] = useState(false);
  const [response, setResponse] = useState('');
  const [responseFiles, setResponseFiles] = useState([]);
  const [requestId, setRequestId] = useState('');
  const [message, setMessage] = useState('');
  const order = state.data;
  function prepareAction(action) {
    state.setError(null);
    if (action === 'reject' && (!reason || explanation.trim().length < 15)) {
      state.setError(new Error('Chọn lý do và giải thích ít nhất 15 ký tự.')); return;
    }
    if (action === 'delivery-report' && (report.trim().length < 15 || !reportFiles.length)) {
      state.setError(new Error('Nhập báo cáo ít nhất 15 ký tự và tải bằng chứng.')); return;
    }
    if (action === 'evidence' && (!requestId || response.trim().length < 15 || !responseFiles.length)) {
      state.setError(new Error('Chọn yêu cầu, nhập giải trình ít nhất 15 ký tự và tải bằng chứng.')); return;
    }
    setPending({ action, requestKey: crypto.randomUUID() });
  }
  async function perform() {
    setBusy(true); state.setError(null);
    try {
      const body = { version: order.version, requestKey: pending.requestKey,
        reason: pending.action === 'reject' ? reason : pending.action === 'delivery-report' ? reportType : null,
        explanation: pending.action === 'reject' ? explanation : pending.action === 'delivery-report' ? report : response,
        evidenceUrls: pending.action === 'evidence' ? responseFiles.map(file => file.fileUrl)
          : pending.action === 'delivery-report' ? reportFiles.map(file => file.fileUrl) : [],
        evidenceRequestId: pending.action === 'evidence' ? Number(requestId) : null };
      const updated = await request('/farmer/orders/' + id + '/' + pending.action, { method: 'POST', body });
      state.setData(updated);
      const messages = {
        confirm: 'Xác nhận đơn hàng thành công! Vui lòng đóng gói sản phẩm theo quy cách.',
        ready: 'Đơn hàng ' + updated.sellerOrderCode + ' đã sẵn sàng giao mô phỏng.',
        dispatch: 'Bắt đầu giao mô phỏng thành công. Đơn hàng đang được giao.',
        reject: 'Từ chối đơn hàng thành công. Người mua đã được thông báo lý do từ chối.',
        'delivery-report': 'Đã ghi nhận giao hàng thất bại hoặc hàng trả. Hàng chỉ được nhập lại kho bán sau khi kiểm tra và điều chỉnh kho có thẩm quyền.',
        evidence: 'Đã gửi giải trình và bằng chứng để xem xét. Việc gửi phản hồi không quyết định kết quả khiếu nại.'
      };
      setMessage(messages[pending.action]
        + (pending.action === 'reject' && updated.verifiedPayment > 0
          ? ' Khoản hoàn tiền đang chờ bộ phận tài chính xác minh và xử lý; chưa xác nhận đã chuyển tiền.' : ''));
      if (pending.action === 'evidence') { setResponse(''); setResponseFiles([]); setRequestId(''); }
      if (pending.action === 'delivery-report') { setReport(''); setReportFiles([]); }
      setPending(null);
    } catch (error) { state.setError(error); setPending(null); }
    finally { setBusy(false); }
  }
  function printPackingSlip() {
    const escape = value => String(value ?? '').replace(/[&<>"']/g, char =>
      ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
    const rows = order.items.map(item => '<tr><td>' + escape(item.productName) + '</td><td>'
      + escape(item.quantity + ' ' + item.unit) + '</td><td>'
      + escape(money(item.unitPrice)) + '/' + escape(item.unit) + '</td><td>'
      + escape(money(item.subtotal)) + '</td><td>'
      + item.allocations.map(a => escape(a.batchCode + ' · ' + a.allocatedQuantity)).join('<br>') + '</td></tr>').join('');
    const html = '<!doctype html><html lang="vi"><meta charset="utf-8"><title>Phiếu đóng gói '
      + escape(order.sellerOrderCode) + '</title><style>body{font:15px Arial;padding:30px}td,th{padding:10px;border:1px solid #ccc}table{border-collapse:collapse;width:100%}</style>'
      + '<h1>AgriSustain · Phiếu đóng gói</h1><h2>' + escape(order.sellerOrderCode) + '</h2><p>Đơn tổng: '
      + escape(order.orderCode) + '</p><p>Người nhận: ' + escape(order.recipientName) + ' · '
      + escape(order.recipientPhone) + '</p><p>' + escape(order.shippingAddress)
      + '</p><table><tr><th>Sản phẩm</th><th>Số lượng</th><th>Đơn giá</th><th>Thành tiền</th><th>Lô</th></tr>' + rows
      + '</table><h3>Thanh toán đơn của nông trại</h3>'
      + '<p>Tiền hàng: ' + escape(money(order.subtotal)) + '</p>'
      + '<p>Phí giao hàng: ' + escape(money(order.deliveryFee)) + '</p>'
      + (order.voucherCode ? '<p>Mã giảm giá của đơn tổng: ' + escape(order.voucherCode) + '</p>' : '')
      + '<p>Giảm giá được phân bổ (ưu đãi / điểm): −' + escape(money(order.discountShare)) + '</p>'
      + '<p><strong>Tổng thanh toán: ' + escape(money(order.finalAmount)) + '</strong></p>'
      + '<p>Ghi chú: ' + escape(order.notes) + '</p><p>Người đóng gói: __________ Người giao: __________</p></html>';
    const popup = window.open('', '_blank', 'width=1000,height=700');
    if (!popup) { state.setError(new Error('Cho phép mở cửa sổ in phiếu.')); return; }
    popup.document.write(html); popup.document.close(); popup.focus(); popup.print();
    setMessage('Phiếu đóng gói đã sẵn sàng để in.');
  }
  if (state.loading) return <p role="status">Đang tải chi tiết đơn…</p>;
  const evidenceRequests = order?.complaints.flatMap(complaint => complaint.evidenceRequests) || [];
  return <div className="commerce"><div className="commerce-top"><div>
    <p className="commerce-subtitle">UC-FAR-06 · {orderViews.find(([key]) => key === tab)?.[1]}</p>
    <h1>Chi tiết đơn {order?.sellerOrderCode}</h1></div><button type="button" onClick={() => navigate('/farmer/orders')}>
      ← Đơn hàng của nông trại</button></div><Errors error={state.error} hideMessageCodes />{state.error && <button type="button" onClick={state.reload}>Tải lại chi tiết đơn</button>}<Notice>{message}</Notice>
    {order && <><div className="commerce-actions"><Status value={order.status} />
      <span>Đơn tổng {order.orderCode} · {orderStatus[order.parentStatus] || order.parentStatus}</span></div>
      <div className="commerce-grid"><div>
        <section className="commerce-section"><h2>Người nhận & thông tin đơn hàng</h2>
          <dl className="commerce-details"><div><dt>Họ tên</dt><dd>{order.recipientName}</dd></div>
            <div><dt>Điện thoại</dt><dd>{order.recipientPhone}</dd></div><div><dt>Địa chỉ</dt><dd>{order.shippingAddress}</dd></div>
            <div><dt>Ngày / Loại đơn</dt><dd>{localTime(order.orderDate)} · {order.orderType}</dd></div>
            <div><dt>Ghi chú</dt><dd>{order.notes || '—'}</dd></div>
            <div><dt>Thanh toán đơn tổng</dt><dd>{money(order.verifiedPayment)} đã xác minh / {money(order.requiredPayment)} yêu cầu</dd></div></dl>
          <div className="table-wrap"><table style={{ minWidth: 580 }}><thead><tr><th>Hàng hóa</th><th>Số lượng / Giá</th><th>Lô đã phân bổ</th></tr></thead>
            <tbody>{order.items.map(item => <tr key={item.orderItemId}><td><div className="commerce-product-cell">
              <ProductThumbnail src={item.imageUrl} name={item.productName} /><strong>{item.productName}</strong></div></td>
              <td>{item.quantity} {item.unit}<small>{money(item.unitPrice)}/{item.unit}</small></td>
              <td>{item.allocations.length ? item.allocations.map(a => <div key={a.batchCode}>{a.batchCode}
                <small>{a.allocatedQuantity} {item.unit} · Hạn {a.expirationDate || '—'} · {a.status}</small></div>)
                : <span>Chưa có lô thực tế</span>}</td></tr>)}</tbody></table></div></section>
        {tab === 'prepare' && <section className="commerce-section"><h2>Chuẩn bị và giao hàng</h2>
          <div className="commerce-actions" style={{ marginTop: 18 }}>
            <button type="button" disabled={busy || uploading || !order.canConfirm} onClick={() => prepareAction('confirm')}>Xác nhận đơn</button>
            <button type="button" disabled={busy || uploading || !order.canMarkReady} onClick={() => prepareAction('ready')}>Đánh dấu sẵn sàng giao</button>
            <button type="button" className="primary" disabled={busy || uploading || !order.canDispatch} onClick={() => prepareAction('dispatch')}>Bắt đầu giao mô phỏng</button>
            <button type="button" disabled={busy || uploading || !order.canReject}
              onClick={() => navigate('/farmer/orders/' + id + '/reject')}>Từ chối trước khi giao</button>
          </div><hr /><div className="commerce-actions">
            <button type="button" onClick={printPackingSlip}>In phiếu</button></div>
          {order.shipments.map(shipment => <p key={shipment.shipmentId} style={{ marginTop: 18 }}>Chuyến giao #{shipment.shipmentId}
            {' · ' + shipment.status + ' · Xuất: ' + localTime(shipment.shippedAt)}</p>)}
        </section>}
        {tab === 'reject' && <><section className="commerce-section"><h2>Từ chối trước khi giao</h2>
          <Field label="Lý do từ chối *"><select disabled={busy || uploading || !order.canReject} value={reason} onChange={event => setReason(event.target.value)}>
            <option value="">Chọn lý do</option><option value="Quality">Chất lượng không đạt</option>
            <option value="Shortfall">Thiếu sản lượng</option><option value="ForceMajeure">Bất khả kháng</option></select></Field>
          <Field label="Giải thích chi tiết * (ít nhất 15 ký tự)"><textarea minLength={15} maxLength={450}
            disabled={busy || uploading || !order.canReject} value={explanation} onChange={event => setExplanation(event.target.value)} /></Field>
          <p>Chỉ giải phóng lượng giữ chỗ chưa tiêu thụ một lần. Khoản đã thu được chuyển Finance xử lý hoàn tiền.</p>
          <button type="button" disabled={busy || uploading || !order.canReject} onClick={() => prepareAction('reject')}>Xác nhận từ chối</button></section>
          <section className="commerce-section"><h2>Ghi nhận giao thất bại / hàng trả</h2>
            <Field label="Loại báo cáo *"><select value={reportType} onChange={event => setReportType(event.target.value)}>
              <option value="DeliveryFailure">Giao hàng thất bại</option><option value="Return">Hàng bị trả lại</option></select></Field>
            <Field label="Nguyên nhân, lượng hàng và tình trạng thực tế *"><textarea value={report} minLength={15} maxLength={2000}
              onChange={event => setReport(event.target.value)} /></Field>
            <Uploads evidence onBusyChange={setUploading} files={reportFiles} onChange={setReportFiles} onError={state.setError} disabled={busy} />
            <p>Hàng trả chỉ được nhập lại kho bán sau kiểm tra và điều chỉnh có thẩm quyền.</p>
            <button type="button" disabled={busy || uploading || !['Shipping', 'InTransit', 'Delivered', 'DeliveryFailed'].includes(order.status)}
              onClick={() => prepareAction('delivery-report')}>Gửi báo cáo giao hàng</button></section></>}
        {tab === 'complaint' && <><section className="commerce-section"><h2>Khiếu nại và tài chính</h2>
          <p>Giao lúc: {localTime(order.deliveredAt)} · Hạn gửi khiếu nại: {localTime(order.complaintDeadline)}</p>
          {order.complaints.length === 0 && <p>Chưa có khiếu nại liên quan.</p>}
          {order.complaints.map(complaint => <article key={complaint.disputeTicketId}>
            <h3>CS-{complaint.disputeTicketId} · {complaint.status}</h3><p>{complaint.reason} · {complaint.description}</p>
            <p>{complaint.resolutionNotes || 'Đang chờ xử lý theo thẩm quyền.'}</p>
            {complaint.refundAmount != null && <p>Số tiền theo quyết định: {money(complaint.refundAmount)}</p>}
            {complaint.evidenceRequests.map(item => <p key={item.evidenceRequestId}>Yêu cầu #{item.evidenceRequestId}
              {' · Gửi: ' + localTime(item.requestedAt) + ' · Hạn: ' + localTime(item.dueAt)}<br />
              {item.instructions} · {item.status}</p>)}</article>)}</section>
          <section className="commerce-section"><h2>Giải trình của nông trại</h2>
            <Field label="Yêu cầu bổ sung bằng chứng *"><select value={requestId} onChange={event => setRequestId(event.target.value)}>
              <option value="">Chọn yêu cầu</option>{evidenceRequests.filter(item => !item.respondedAt).map(item =>
                <option key={item.evidenceRequestId} value={item.evidenceRequestId}>#{item.evidenceRequestId} · Hạn {localTime(item.dueAt)}</option>)}</select></Field>
            <Field label="Giải trình *"><textarea value={response} minLength={15} maxLength={2000}
              onChange={event => setResponse(event.target.value)} /></Field>
            <Uploads evidence onBusyChange={setUploading} files={responseFiles} onChange={setResponseFiles} onError={state.setError} disabled={busy} />
            <p>Nông dân cung cấp giải trình và bằng chứng; kết quả khiếu nại, hoàn tiền và chi trả do bộ phận có thẩm quyền quyết định.</p>
            <button type="button" className="primary" disabled={busy || uploading || !requestId} onClick={() => prepareAction('evidence')}>Gửi phản hồi</button>
          </section></>}
      </div><aside><section className="commerce-section"><h2>Giá trị đơn của nông trại</h2><dl className="commerce-summary">
        <div className="commerce-details"><dt>Tiền hàng</dt><dd>{money(order.subtotal)}</dd>
          <dt>Giảm giá phân bổ</dt><dd>{money(order.discountShare)}</dd><dt>Phí giao hàng</dt><dd>{money(order.deliveryFee)}</dd>
          <dt>Thành tiền</dt><dd className="commerce-subtotal">{money(order.finalAmount)}</dd></div></dl></section>
        <section className="commerce-section"><h2>Lịch sử xử lý</h2><div className="commerce-timeline">
          {order.timeline.length ? order.timeline.map((entry, index) => <div key={index}>
            <strong>{orderStatus[entry.newStatus] || entry.newStatus}</strong><small>{localTime(entry.occurredAt)}</small>
            <p>{entry.reason}</p></div>) : <p>Chưa có thay đổi trạng thái.</p>}</div></section></aside></div>
    </>}
    {pending && <Confirm title={actionNames[pending.action]} busy={busy} onClose={() => setPending(null)} onConfirm={perform}>
      Xác nhận thao tác cho đơn {order.sellerOrderCode}? Trạng thái đơn sẽ được kiểm tra lại trước khi lưu.</Confirm>}
  </div>;
}



