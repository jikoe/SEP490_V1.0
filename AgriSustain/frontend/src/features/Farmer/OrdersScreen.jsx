import React, { useEffect, useState } from 'react';
import { request, queryString } from '../../api/client';
import './orders-uc06.css';

const money = (n) => new Intl.NumberFormat('vi-VN').format(Number(n) || 0) + ' ₫';

const localDate = (d) => {
  if (!d) return '—';
  try {
    const dt = new Date(d);
    if (isNaN(dt.getTime())) return String(d);
    const dateStr = dt.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
    const timeStr = dt.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
    return `${dateStr} ${timeStr}`;
  } catch {
    return String(d);
  }
};

const statusMap = {
  Pending: 'Chờ xác nhận',
  Confirmed: 'Đang chuẩn bị',
  Preparing: 'Đang chuẩn bị',
  ReadyToShip: 'Sẵn sàng giao',
  Ready: 'Sẵn sàng giao',
  Shipping: 'Đang giao',
  InTransit: 'Đang giao',
  Cancelled: 'Đã hủy',
  Rejected: 'Đã hủy',
  Delivered: 'Đã giao',
  Completed: 'Hoàn tất',
};

const displayStatus = (s) => statusMap[s] || s || 'Chờ xác nhận';

const isBeforeShipping = (status) =>
  ['Pending', 'Confirmed', 'Preparing', 'ReadyToShip', 'Ready', 'Chờ xác nhận', 'Đang chuẩn bị', 'Sẵn sàng giao'].includes(status);

export default function OrdersScreen({ route, navigate }) {
  const [view, setView] = useState('list');
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);

  // Filters
  const [keyword, setKeyword] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');

  // Orders list and selected order
  const [orders, setOrders] = useState([]);
  const [selectedOrder, setSelectedOrder] = useState(null);
  const [totalCount, setTotalCount] = useState(0);

  // Rejection reason form
  const [rejectionReason, setRejectionReason] = useState('');

  // Feedback & Toast notification
  const [feedback, setFeedback] = useState({ text: '', bad: false });
  const [toast, setToast] = useState({ text: '', bad: false, show: false });

  // Confirmation dialog
  const [confirmModal, setConfirmModal] = useState({ open: false, text: '', onConfirm: null });

  const notify = (text, bad = false) => {
    setFeedback({ text, bad });
    setToast({ text, bad, show: true });
    window.clearTimeout(window.__toastTimer);
    window.__toastTimer = window.setTimeout(() => {
      setToast((prev) => ({ ...prev, show: false }));
    }, 5000);
  };

  const ask = (text, onConfirm) => {
    setConfirmModal({
      open: true,
      text,
      onConfirm: () => {
        setConfirmModal({ open: false, text: '', onConfirm: null });
        onConfirm();
      },
    });
  };

  const loadOrders = async (kw = keyword, st = statusFilter, from = fromDate, to = toDate) => {
    setLoading(true);
    try {
      // Map Vietnamese filter value to backend enum
      let apiStatus = st;
      if (st === 'Chờ xác nhận') apiStatus = 'Pending';
      else if (st === 'Đang chuẩn bị') apiStatus = 'Confirmed';
      else if (st === 'Sẵn sàng giao') apiStatus = 'ReadyToShip';
      else if (st === 'Đang giao') apiStatus = 'Shipping';
      else if (st === 'Đã hủy') apiStatus = 'Cancelled';

      const queryObj = {
        keyword: kw || undefined,
        status: apiStatus || undefined,
        from: from ? new Date(from + 'T00:00:00+07:00').toISOString() : undefined,
        to: to ? new Date(Date.parse(to + 'T00:00:00+07:00') + 86400000).toISOString() : undefined,
        page: 1,
        pageSize: 10,
      };

      const res = await request('/farmer/orders?' + queryString(queryObj));
      const items = res?.items || [];
      setOrders(items);
      setTotalCount(res?.total || items.length);

      if (items.length > 0) {
        if (!selectedOrder || !items.some((o) => o.sellerOrderId === selectedOrder.sellerOrderId)) {
          setSelectedOrder(items[0]);
        } else {
          const fresh = items.find((o) => o.sellerOrderId === selectedOrder.sellerOrderId);
          if (fresh) setSelectedOrder(fresh);
        }
      }
    } catch (err) {
      notify('Không thể tải dữ liệu đơn hàng. Vui lòng thử lại.', true);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadOrders();
  }, []);

  const handleApplyFilters = (e) => {
    e?.preventDefault();
    if (fromDate && toDate && fromDate > toDate) {
      notify('Vui lòng kiểm tra lại khoảng ngày: Ngày bắt đầu không được sau ngày kết thúc.', true);
      return;
    }
    loadOrders(keyword, statusFilter, fromDate, toDate);
  };

  const handleClearFilters = () => {
    setKeyword('');
    setStatusFilter('');
    setFromDate('');
    setToDate('');
    loadOrders('', '', '', '');
  };

  const openOrder = (order, targetView = 'detail') => {
    setSelectedOrder(order);
    setRejectionReason('');
    setView(targetView);
  };

  const performTransition = (expectedStatus, apiAction, nextStatus, confirmPrompt, successMsg) => {
    if (!selectedOrder) return;
    const currentStatus = selectedOrder.status;

    if (expectedStatus === 'Pending' && currentStatus !== 'Pending') {
      notify('Không thể chuyển trạng thái đơn hàng. Thao tác không phù hợp với trạng thái hiện tại.', true);
      return;
    }
    if (expectedStatus === 'Confirmed' && !['Confirmed', 'Preparing'].includes(currentStatus)) {
      notify('Không thể chuyển trạng thái đơn hàng. Thao tác không phù hợp với trạng thái hiện tại.', true);
      return;
    }
    if (expectedStatus === 'ReadyToShip' && !['ReadyToShip', 'Ready'].includes(currentStatus)) {
      notify('Không thể chuyển trạng thái đơn hàng. Thao tác không phù hợp với trạng thái hiện tại.', true);
      return;
    }

    ask(confirmPrompt, async () => {
      setBusy(true);
      try {
        const body = {
          version: selectedOrder.version,
          requestKey: crypto.randomUUID(),
        };
        const updated = await request(`/farmer/orders/${selectedOrder.sellerOrderId}/${apiAction}`, {
          method: 'POST',
          body,
        });

        setSelectedOrder(updated);
        setOrders((prev) => prev.map((o) => (o.sellerOrderId === updated.sellerOrderId ? updated : o)));
        notify(successMsg);
      } catch (err) {
        notify(err.message || 'Yêu cầu xung đột với trạng thái hiện tại hoặc đã được xử lý. Vui lòng làm mới để xem kết quả mới nhất.', true);
      } finally {
        setBusy(false);
      }
    });
  };

  const handleRejectSubmit = (e) => {
    e.preventDefault();
    if (!selectedOrder) return;

    if (!isBeforeShipping(selectedOrder.status)) {
      notify('Đơn hàng không còn ở trạng thái được phép từ chối.', true);
      return;
    }

    const trimmed = rejectionReason.trim();
    if (!trimmed) {
      notify('Vui lòng nhập lý do từ chối. Không được để trống hoặc chỉ nhập khoảng trắng.', true);
      return;
    }
    const refundAmount = selectedOrder.refundableAmount || 0;
    const bankAccount = selectedOrder.bankAccount || 'chưa có tài khoản nhận tiền';
    const recipient = selectedOrder.bankAccountName || 'khách hàng';

    ask(
      refundAmount > 0 ? `Từ chối đơn ${selectedOrder.sellerOrderCode} và tự động hoàn ${money(refundAmount)} vào tài khoản ${bankAccount} của ${recipient}?` : `Từ chối đơn ${selectedOrder.sellerOrderCode}? Đơn hàng này không có khoản cần hoàn tiền.`,
      async () => {
        setBusy(true);
        try {
          const body = {
            version: selectedOrder.version,
            requestKey: crypto.randomUUID(),
            reason: 'Quality',
            explanation: trimmed,
          };
          const updated = await request(`/farmer/orders/${selectedOrder.sellerOrderId}/reject`, {
            method: 'POST',
            body,
          });

          setSelectedOrder(updated);
          setOrders((prev) => prev.map((o) => (o.sellerOrderId === updated.sellerOrderId ? updated : o)));
          notify('Từ chối đơn hàng thành công.');
          setView('detail');
        } catch (err) {
          notify(err.message || 'Yêu cầu xung đột với trạng thái hiện tại hoặc đã được xử lý. Vui lòng làm mới để xem kết quả mới nhất.', true);
        } finally {
          setBusy(false);
        }
      }
    );
  };

  const printSlip = () => {
    if (!selectedOrder) return;
    const o = selectedOrder;
    const popup = window.open('', '_blank', 'width=1000,height=760');
    if (!popup) {
      notify('Vui lòng cho phép mở cửa sổ popup của trình duyệt để in phiếu đóng gói.', true);
      return;
    }

    const esc = (v) => String(v ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    const itemRows = (o.items || [])
      .map(
        (it) => `<tr>
          <td>${esc(it.productName)}</td>
          <td>${esc(it.unit)}</td>
          <td>${it.quantity}</td>
          <td>${money(it.unitPrice)}</td>
          <td>${money(it.subtotal || it.quantity * it.unitPrice)}</td>
          <td>${esc(it.allocations?.map(a => a.batchCode).filter(Boolean).join(', ') || '—')}</td>
        </tr>`
      )
      .join('');

    const html = `<!doctype html>
<html lang="vi">
<head>
  <meta charset="utf-8">
  <title>Phiếu đóng gói ${esc(o.sellerOrderCode)}</title>
  <style>
    body { font: 14px 'Segoe UI', Arial, sans-serif; padding: 30px; color: #183b2a; }
    h1 { margin-top: 0; color: #0b573b; font-size: 22px; }
    h2 { font-size: 18px; color: #284735; }
    table { border-collapse: collapse; width: 100%; margin: 15px 0; }
    td, th { padding: 9px 12px; border: 1px solid #cfdcd3; text-align: left; }
    th { background: #f4f7f4; font-weight: 650; }
    .right { text-align: right; margin-top: 15px; }
    .sign { margin-top: 40px; display: flex; justify-content: space-between; }
    @media print { button { display: none; } }
  </style>
</head>
<body>
  <h1>AgriSustain · Phiếu đóng gói / Giao nhận hàng</h1>
  <h2>${esc(o.sellerOrderCode)}</h2>
  <p><strong>Đơn tổng:</strong> ${esc(o.orderCode)} · <strong>Ngày đặt:</strong> ${localDate(o.orderDate)}</p>
  <p><strong>Người nhận:</strong> ${esc(o.recipientName)} · <strong>Điện thoại:</strong> ${esc(o.recipientPhone)}</p>
  <p><strong>Địa chỉ giao:</strong> ${esc(o.shippingAddress)}</p>
  <table>
    <thead>
      <tr>
        <th>Sản phẩm</th><th>Đơn vị</th><th>Số lượng</th><th>Đơn giá</th><th>Thành tiền</th><th>Mã lô</th>
      </tr>
    </thead>
    <tbody>
      ${itemRows || '<tr><td colspan="6">Không có dữ liệu hàng hóa</td></tr>'}
    </tbody>
  </table>
  <div class="right">
    <p>Tiền hàng: ${money(o.subtotal)}</p>
    <p>Phí giao hàng: ${money(o.deliveryFee)}</p>
    ${o.voucherCode ? `<p>Mã ưu đãi: ${esc(o.voucherCode)}</p>` : ''}
    <p>Giảm giá phân bổ: −${money(o.discountShare)}</p>
    <h3>Tổng thanh toán: ${money(o.isRefunded ? o.refundedAmount : o.refundableAmount)}</h3>
  </div>
  <p><strong>Ghi chú:</strong> ${esc(o.notes || 'Không có ghi chú')}</p>
  <div class="sign">
    <div>Người đóng gói<br><br><br>____________________</div>
    <div>Người giao hàng<br><br><br>____________________</div>
    <div>Người nhận hàng<br><br><br>____________________</div>
  </div>
  <br><br>
  <button onclick="window.print()" style="padding: 8px 16px; background: #15814f; color: white; border: none; border-radius: 4px; cursor: pointer;">In phiếu ngay</button>
</body>
</html>`;

    popup.document.write(html);
    popup.document.close();
    popup.focus();
    popup.print();
    notify('Phiếu đóng gói đã sẵn sàng để in.');
  };

  const o = selectedOrder;

  return (
    <div className="farmer-orders-app">
      {/* Head */}
      <div className="head">
        <div>
          <h1>Xử lý đơn hàng</h1>
          <p>Quản lý hoạt động của nông trại</p>
        </div>
      </div>

      {/* Views Navigation */}
      <nav className="views" aria-label="Các chế độ xem đơn hàng">
        <button
          type="button"
          className={view === 'list' ? 'active' : ''}
          onClick={() => setView('list')}
        >
          Đơn hàng của nông trại
        </button>
        <button
          type="button"
          className={view === 'detail' ? 'active' : ''}
          onClick={() => {
            if (!selectedOrder && orders.length > 0) setSelectedOrder(orders[0]);
            setView('detail');
          }}
        >
          Xem và xử lý đơn
        </button>
        <button
          type="button"
          className={view === 'reject' ? 'active' : ''}
          onClick={() => {
            if (!selectedOrder && orders.length > 0) setSelectedOrder(orders[0]);
            setView('reject');
          }}
        >
          Từ chối đơn hàng
        </button>
      </nav>

      {/* VIEW 1: LIST */}
      {view === 'list' && (
        <div className="view">
          {feedback.text && (
            <div className={`feedback ${feedback.bad ? 'bad' : ''}`} role="status">
              {feedback.text}
            </div>
          )}

          <div className="field">
            <form className="filter-row order-filters" onSubmit={handleApplyFilters}>
              <div className="filter-field filter-keyword">
                <label htmlFor="orderKeyword">Tìm kiếm</label>
                <input
                  id="orderKeyword"
                  value={keyword}
                  onChange={(e) => setKeyword(e.target.value)}
                  placeholder="Mã đơn, khách hàng, mã lô..."
                />
              </div>
              <div className="filter-field">
                <label htmlFor="orderStatus">Trạng thái</label>
                <select
                  id="orderStatus"
                  value={statusFilter}
                  onChange={(e) => setStatusFilter(e.target.value)}
                >
                  <option value="">Tất cả trạng thái</option>
                  <option value="Chờ xác nhận">Chờ xác nhận</option>
                  <option value="Đang chuẩn bị">Đang chuẩn bị</option>
                  <option value="Sẵn sàng giao">Sẵn sàng giao</option>
                  <option value="Đang giao">Đang giao</option>
                  <option value="Đã hủy">Đã hủy</option>
                </select>
              </div>
              <div className="filter-field">
                <label htmlFor="fromDate">Từ ngày</label>
                <input
                  id="fromDate"
                  type="date"
                  value={fromDate}
                  onChange={(e) => setFromDate(e.target.value)}
                />
              </div>
              <div className="filter-field">
                <label htmlFor="toDate">Đến ngày</label>
                <input
                  id="toDate"
                  type="date"
                  value={toDate}
                  min={fromDate || undefined}
                  onChange={(e) => setToDate(e.target.value)}
                />
              </div>
              <button type="submit" className="btn primary" disabled={loading}>
                {loading ? 'Đang lọc…' : 'Áp dụng'}
              </button>
              <button type="button" className="btn" onClick={handleClearFilters}>
                Xóa bộ lọc
              </button>
            </form>
          </div>

          <div className="field">
            <label>Danh sách đơn hàng</label>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Đơn của nông trại / Đơn tổng</th>
                    <th>RetailCustomer / Ngày</th>
                    <th>Hàng hóa / Thanh toán</th>
                    <th>Trạng thái</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {loading ? (
                    <tr>
                      <td colSpan="5" style={{ textAlign: 'center', padding: 24, color: '#62766a' }}>
                        Đang tải dữ liệu đơn hàng…
                      </td>
                    </tr>
                  ) : orders.length === 0 ? (
                    <tr>
                      <td colSpan="5" style={{ textAlign: 'center', padding: 24, color: '#718276' }}>
                        Không có đơn hàng phù hợp với điều kiện đã chọn.
                      </td>
                    </tr>
                  ) : (
                    orders.map((ord) => {
                      const st = displayStatus(ord.status);
                      const isCancelled = ['Cancelled', 'Rejected', 'Đã hủy'].includes(ord.status);
                      const itemsDesc = (ord.items || [])
                        .map((it) => `${it.productName} · ${it.quantity} ${it.unit}`)
                        .join(', ');

                      return (
                        <tr key={ord.sellerOrderId}>
                          <td>
                            <strong>{ord.sellerOrderCode}</strong>
                            <br />
                            <span style={{ color: '#718276' }}>{ord.orderCode}</span>
                          </td>
                          <td>
                            <strong>{ord.recipientName}</strong>
                            <br />
                            <small style={{ color: '#718276' }}>{localDate(ord.orderDate)}</small>
                          </td>
                          <td>
                            {itemsDesc || 'Hàng nông sản'}
                            <br />
                            <span style={{ color: '#1a7545', fontWeight: 600 }}>
                              {ord.paymentStatus === 'Paid' ? 'Đã xác minh thanh toán' : 'Chưa có thanh toán'}
                            </span>
                            <br />
                            <strong>{money(ord.finalAmount)}</strong>
                          </td>
                          <td>
                            <span className={`pill ${isCancelled ? 'red' : ''}`}>{st}</span>
                            {ord.isRefunded && (
                              <>
                                <br />
                                <span className="pill" style={{ marginTop: 4 }}>
                                  Đã hoàn tiền
                                </span>
                              </>
                            )}
                          </td>
                          <td>
                            <button
                              type="button"
                              className="btn"
                              onClick={() => openOrder(ord, 'detail')}
                            >
                              Xem và xử lý
                            </button>
                          </td>
                        </tr>
                      );
                    })
                  )}
                </tbody>
              </table>
            </div>
          </div>

          <div className="pagination">
            <span>
              Hiển thị {orders.length ? `1–${orders.length}` : '0'} trong {totalCount} bản ghi · 10 bản ghi/trang
            </span>
            <span>
              <button type="button" disabled>‹</button>
              <button type="button" className="active">1</button>
              <button type="button" disabled>›</button>
            </span>
          </div>
        </div>
      )}

      {/* VIEW 2: DETAIL */}
      {view === 'detail' && o && (
        <div className="view">
          <div className="topline">
            <h2>Chi tiết đơn {o.sellerOrderCode}</h2>
            <span className={`pill ${['Cancelled', 'Rejected', 'Đã hủy'].includes(o.status) ? 'red' : ''}`}>
              {displayStatus(o.status)}
            </span>
          </div>

          {feedback.text && (
            <div className={`feedback ${feedback.bad ? 'bad' : ''}`} role="status">
              {feedback.text}
            </div>
          )}

          <section className="section">
            <h2>Thông tin đơn hàng</h2>
            <div className="inside">
              <div className="grid two">
                <div className="field">
                  <label>Người nhận và địa chỉ</label>
                  <dl className="keyvalue">
                    <dt>Họ tên</dt>
                    <dd>{o.recipientName}</dd>
                    <dt>Điện thoại</dt>
                    <dd>{o.recipientPhone ? '•••• ' + o.recipientPhone.slice(-4) : '—'}</dd>
                    <dt>Địa chỉ</dt>
                    <dd>{o.shippingAddress}</dd>
                    <dt>Ghi chú</dt>
                    <dd>{o.notes || '—'}</dd>
                  </dl>
                </div>

                <div className="field">
                  <label>Sản phẩm và điều kiện giao</label>
                  <div className="readonly">
                    {(o.items || []).map((it, idx) => (
                      <div key={idx} style={{ marginBottom: 8 }}>
                        <strong>{it.productName}</strong> · {it.quantity} {it.unit}
                        <br />
                        Đơn giá: {money(it.unitPrice)}/{it.unit} · Tiền hàng: {money(it.subtotal || it.quantity * it.unitPrice)}
                        <br />
                        Lô: {it.allocations?.[0]?.batchCode || 'Lô thu hoạch nông trại'} · Hạn sử dụng: {it.allocations?.[0]?.expirationDate || 'Đạt chuẩn'}
                      </div>
                    ))}
                    <div style={{ borderTop: '1px dashed #cfdcd3', paddingTop: 6, marginTop: 6 }}>
                      Phí giao hàng: {money(o.deliveryFee)} · Giảm giá: {money(o.discountShare)}
                      <br />
                      <strong>Tổng thanh toán: {money(o.finalAmount)}</strong>
                      <br />
                      <span style={{ color: '#176d40', fontWeight: 600 }}>
                        {o.paymentStatus === 'Paid' ? 'Đã xác minh thanh toán' : 'Chưa có thanh toán'} ·{' '}
                        {['Cancelled', 'Rejected'].includes(o.status)
                          ? 'Đã giải phóng hàng giữ chỗ'
                          : ['Shipping', 'InTransit'].includes(o.status)
                            ? 'Đã xuất giao hàng'
                            : 'Đã giữ đủ hàng'}
                      </span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </section>

          {o.notificationPending && <div role="status" className="notice">Thao tác đã hoàn tất. Thông báo đang chờ gửi và sẽ được thử lại; bạn không cần thực hiện lại thao tác.</div>}
          {/* Action buttons (Confirm, Ready, Ship) */}
          <div className="grid">
            <div className="field">
              <label>Xác nhận đơn</label>
              <button
                type="button"
                className="btn primary"
                disabled={busy || o.status !== 'Pending'}
                onClick={() =>
                  performTransition(
                    'Pending',
                    'confirm',
                    'Confirmed',
                    'Xác nhận đơn hàng này?',
                    'Xác nhận đơn hàng thành công! Vui lòng tiến hành đóng gói sản phẩm theo đúng quy cách.'
                  )
                }
              >
                Xác nhận đơn
              </button>
            </div>
            <div className="field">
              <label>Sẵn sàng giao</label>
              <button
                type="button"
                className="btn"
                disabled={busy || !['Confirmed', 'Preparing'].includes(o.status)}
                onClick={() =>
                  performTransition(
                    'Confirmed',
                    'ready',
                    'ReadyToShip',
                    'Đánh dấu đơn hàng sẵn sàng giao?',
                    `Đơn hàng ${o.sellerOrderCode} đã sẵn sàng để giao hàng mô phỏng.`
                  )
                }
              >
                Đánh dấu sẵn sàng giao
              </button>
            </div>
            <div className="field">
              <label>Giao mô phỏng</label>
              <button
                type="button"
                className="btn primary"
                disabled={busy || !['ReadyToShip', 'Ready'].includes(o.status)}
                onClick={() =>
                  performTransition(
                    'ReadyToShip',
                    'dispatch',
                    'Shipping',
                    'Bắt đầu giao hàng mô phỏng?',
                    'Bắt đầu giao hàng mô phỏng thành công. Đơn hàng hiện đang trong trạng thái Đang giao hàng.'
                  )
                }
              >
                Bắt đầu giao mô phỏng
              </button>
            </div>
          </div>

          {/* Timeline History */}
          <div className="field">
            <label>Lịch sử xử lý</label>
            <div className="timeline">
              {o.timeline && o.timeline.length > 0 ? (
                o.timeline.map((entry, idx) => (
                  <div className="event" key={idx}>
                    <strong>{displayStatus(entry.newStatus)}</strong>
                    <p>
                      {localDate(entry.occurredAt)} · {entry.reason || 'Cập nhật trạng thái đơn hàng.'}
                    </p>
                  </div>
                ))
              ) : (
                <div className="event">
                  <strong>{displayStatus(o.status)}</strong>
                  <p>{localDate(o.orderDate)} · Đơn hàng đã được tạo.</p>
                </div>
              )}
            </div>
          </div>

          {/* Bottom Actions: Print slip & Reject */}
          <div className="grid two">
            <div className="field">
              <label>In phiếu</label>
              <button type="button" className="btn" onClick={printSlip}>
                In phiếu
              </button>
            </div>
            <div className="field">
              <label>Từ chối trước khi giao</label>
              <button
                type="button"
                className="btn danger"
                disabled={busy || !isBeforeShipping(o.status)}
                onClick={() => openOrder(o, 'reject')}
              >
                Từ chối đơn hàng
              </button>
            </div>
          </div>
        </div>
      )}

      {/* VIEW 3: REJECT */}
      {view === 'reject' && o && (
        <div className="view">
          <div className="topline">
            <h2>Từ chối đơn {o.sellerOrderCode}</h2>
            <span className={`pill ${['Cancelled', 'Rejected', 'Đã hủy'].includes(o.status) ? 'red' : ''}`}>
              {displayStatus(o.status)}
            </span>
          </div>

          {feedback.text && (
            <div className={`feedback ${feedback.bad ? 'bad' : ''}`} role="status">
              {feedback.text}
            </div>
          )}

          <form onSubmit={handleRejectSubmit}>
            <div className="field">
              <label htmlFor="rejectionReason">
                Lý do từ chối *
              </label>
              <textarea
                id="rejectionReason"
                required
                disabled={busy || !isBeforeShipping(o.status)}
                value={rejectionReason}
                onChange={(e) => setRejectionReason(e.target.value)}
                placeholder="Nhập lý do từ chối đơn hàng..."
              />
              <p className="hint">Không được để trống hoặc chỉ nhập khoảng trắng.</p>
            </div>

            <div className="field">
              <label>Thông tin hoàn tiền</label>
              <div className="readonly">
                Người nhận hoàn tiền: <strong>{o.bankAccountName || 'Chưa có thông tin'}</strong>
                <br />
                Ngân hàng: <strong>{o.bankName || 'Chưa có thông tin'}</strong>
                <br />
                Tài khoản nhận tiền: <strong>{o.bankAccount || 'Chưa có thông tin'}</strong> · {o.bankAccountName || o.recipientName}
                <h3 style={{ margin: '8px 0', color: '#173b24' }}>Số tiền hoàn: {money(o.isRefunded ? o.refundedAmount : o.refundableAmount)}</h3>
                <span className="pill">{o.isRefunded ? 'Đã hoàn tiền' : o.refundableAmount > 0 ? 'Chờ hoàn tiền' : 'Không có khoản cần hoàn'}</span>
                <p style={{ margin: '8px 0 0', fontSize: 12, color: '#556c5e' }}>
                  Số tiền được tự động hoàn vào tài khoản nhận tiền đã xác thực. Thông báo hệ thống và email được gửi sau khi hoàn tiền hoàn tất.
                </p>
              </div>
            </div>

            <div className="field">
              <label>Xác nhận từ chối</label>
              <div className="actions">
                <button
                  type="submit"
                  className="btn danger"
                  disabled={busy || !isBeforeShipping(o.status)}
                >
                  {busy ? 'Đang xử lý…' : 'Xác nhận từ chối'}
                </button>
                <button
                  type="button"
                  className="btn"
                  onClick={() => setView('detail')}
                >
                  Quay lại đơn hàng
                </button>
              </div>
            </div>
          </form>
        </div>
      )}

      {/* Footer */}
      <footer className="foot">
        AgriSustain · Không gian quản lý đơn hàng của nông trại · Dữ liệu bảo mật theo tiêu chuẩn nông nghiệp bền vững
      </footer>

      {/* Modal Dialog */}
      {confirmModal.open && (
        <div className="farmer-modal-backdrop" role="dialog" aria-modal="true">
          <div className="farmer-modal-dialog">
            <h2>Xác nhận thao tác</h2>
            <p>{confirmModal.text}</p>
            <div className="actions end">
              <button
                type="button"
                className="btn"
                onClick={() => setConfirmModal({ open: false, text: '', onConfirm: null })}
              >
                Quay lại
              </button>
              <button
                type="button"
                className="btn primary"
                onClick={confirmModal.onConfirm}
              >
                Xác nhận
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Fixed Toast */}
      {toast.show && (
        <div className={`farmer-toast ${toast.bad ? 'bad' : ''}`} role="status">
          {toast.text}
        </div>
      )}
    </div>
  );
}
