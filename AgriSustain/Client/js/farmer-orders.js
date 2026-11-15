let currentView = 'list';
let ordersList = [];
let selectedOrder = null;
let totalRecords = 0;
let currentPage = 1;
let actionBusy = false;

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
  Completed: 'Hoàn tất'
};

function displayStatus(s) {
  return statusMap[s] || s || 'Chờ xác nhận';
}

function isBeforeShipping(status) {
  return ['Pending', 'Confirmed', 'Preparing', 'ReadyToShip', 'Ready', 'Chờ xác nhận', 'Đang chuẩn bị', 'Sẵn sàng giao'].includes(status);
}

document.addEventListener('DOMContentLoaded', async () => {
  await loadOrders();
});

function setFeedback(msg, bad = false) {
  const fb = document.getElementById('orders-feedback');
  if (msg) {
    fb.textContent = msg;
    fb.className = `feedback ${bad ? 'bad' : ''}`;
    fb.style.display = 'block';
    document.getElementById('orders-feedback-section').style.display = 'block';

    Utils.showToast(msg, bad ? 'error' : 'success');
  } else {
    fb.style.display = 'none';
    document.getElementById('orders-feedback-section').style.display = 'none';
  }
}

function switchView(viewName) {
  currentView = viewName;
  ['list', 'detail', 'reject'].forEach(v => {
    document.getElementById(`view-${v}`).style.display = (v === viewName) ? 'block' : 'none';
    const btn = document.getElementById(`view-btn-${v}`);
    if (btn) btn.className = (v === viewName) ? 'active' : '';
  });

  if ((viewName === 'detail' || viewName === 'reject') && selectedOrder) {
    populateDetailView(selectedOrder);
  }
}

let orderListGeneration = 0;

async function loadOrders(page = 1) {
  const generation = ++orderListGeneration;
  currentPage = page;
  const keyword = document.getElementById('orderKeyword').value.trim();
  const statusFilter = document.getElementById('orderStatus').value;
  const fromDate = document.getElementById('fromDate').value;
  const toDate = document.getElementById('toDate').value;

  let apiStatus = statusFilter;
  if (statusFilter === 'Chờ xác nhận') apiStatus = 'Pending';
  else if (statusFilter === 'Đang chuẩn bị') apiStatus = 'Confirmed';
  else if (statusFilter === 'Sẵn sàng giao') apiStatus = 'ReadyToShip';
  else if (statusFilter === 'Đang giao') apiStatus = 'Shipping';
  else if (statusFilter === 'Đã hủy') apiStatus = 'Cancelled';

  const tbody = document.getElementById('orders-tbody');
  tbody.innerHTML = '<tr><td colspan="5" style="text-align: center; padding: 24px; color: #62766a;">Đang tải dữ liệu đơn hàng…</td></tr>';

  if (fromDate && toDate && fromDate > toDate) {
    setFeedback(Messages.order.invalidDates, true);
    return;
  }
  const params = { page: currentPage, pageSize: 10 };
  if (keyword) params.keyword = keyword;
  if (apiStatus) params.status = apiStatus;
  if (fromDate) params.from = `${fromDate}T00:00:00+07:00`;
  if (toDate) {
    const exclusiveTo = new Date(`${toDate}T00:00:00+07:00`);
    exclusiveTo.setTime(exclusiveTo.getTime() + 86400000);
    params.to = exclusiveTo.toISOString();
  }

  try {
    const res = await API.getFarmerOrders(params);
    if (generation !== orderListGeneration) return;

    if (res.success && res.data) {
      const payload = res.data.data || res.data;
      const items = payload.items || (Array.isArray(payload) ? payload : []);
      ordersList = items;
      totalRecords = payload.total || items.length;
      renderOrdersTable(items);
      if (items.length > 0) {
        if (!selectedOrder || !items.some(o => (o.sellerOrderId || o.id) === (selectedOrder.sellerOrderId || selectedOrder.id))) {
          selectedOrder = items[0];
        } else {
          const fresh = items.find(o => (o.sellerOrderId || o.id) === (selectedOrder.sellerOrderId || selectedOrder.id));
          if (fresh) selectedOrder = fresh;
        }
      } else {
        selectedOrder = null;
      }
    } else {
      ordersList = [];
      totalRecords = 0;
      selectedOrder = null;
      renderOrdersTable([]);
      setFeedback(res.error || Messages.order.loadFailed, true);
    }
  } catch (err) {
    ordersList = [];
    totalRecords = 0;
    selectedOrder = null;
    renderOrdersTable([]);
    setFeedback(Messages.order.connectionFailed, true);
  }
}

function renderOrdersTable(items) {
  const tbody = document.getElementById('orders-tbody');
  if (!items || items.length === 0) {
    tbody.innerHTML = '<tr><td colspan="5" style="text-align: center; padding: 24px; color: #718276;">Không có đơn hàng phù hợp với điều kiện đã chọn.</td></tr>';
    renderOrderPager();
    return;
  }

  tbody.innerHTML = items.map(ord => {
    const st = displayStatus(ord.status);
    const isCancelled = ['Cancelled', 'Rejected', 'Đã hủy'].includes(ord.status);
    const itemsDesc = (ord.items || [])
      .map(it => `${it.productName} · ${it.quantity} ${it.unit}`)
      .join(', ');

    return `
      <tr>
        <td>
          <strong>${Utils.escapeHtml(ord.sellerOrderCode)}</strong>
          <br />
          <span style="color: #718276;">${Utils.escapeHtml(ord.orderCode)}</span>
        </td>
        <td>
          <strong>${Utils.escapeHtml(ord.recipientName)}</strong>
          <br />
          <small style="color: #718276;">${Utils.formatDateTime(ord.orderDate)}</small>
        </td>
        <td>
          ${Utils.escapeHtml(itemsDesc || 'Hàng nông sản')}
          <br />
          <span style="color: #1a7545; font-weight: 600;">
            ${ord.paymentStatus === 'Paid' ? 'Đã xác minh thanh toán' : 'Chưa có thanh toán'}
          </span>
          <br />
          <strong>${Utils.formatVND(ord.finalAmount)}</strong>
        </td>
        <td>
          <span class="pill ${isCancelled ? 'red' : ''}">${st}</span>
          ${ord.isRefunded ? '<br><span class="pill" style="margin-top: 4px;">Đã hoàn tiền</span>' : ''}
        </td>
        <td>
          <button
            type="button"
            class="btn"
            onclick="openOrder(${ord.sellerOrderId || ord.id}, 'detail')"
          >
            Xem và xử lý
          </button>
        </td>
      </tr>
    `;
  }).join('');

  renderOrderPager();
}

function renderOrderPager() {
  const pages = Math.max(1, Math.ceil(totalRecords / 10));
  const first = totalRecords ? (currentPage - 1) * 10 + 1 : 0;
  document.getElementById('pagination-info').textContent = `Hiển thị ${first}–${Math.min(currentPage * 10, totalRecords)} trong ${totalRecords} bản ghi · 10 bản ghi/trang`;
  document.getElementById('pagination-controls').innerHTML = `
    <button type="button" ${currentPage <= 1 ? 'disabled' : ''} onclick="loadOrders(${currentPage - 1})">‹</button>
    <button type="button" class="active">${currentPage} / ${pages}</button>
    <button type="button" ${currentPage >= pages ? 'disabled' : ''} onclick="loadOrders(${currentPage + 1})">›</button>`;
}

async function openOrder(orderId, targetView = 'detail') {
  const res = await API.getFarmerOrderDetail(orderId);
  if (!res.success || !res.data) {
    setFeedback(res.error || Messages.order.loadFailed, true);
    return;
  }
  selectedOrder = res.data;
  switchView(targetView);
}

function populateDetailView(o) {
  if (!o) return;
  const st = displayStatus(o.status);
  const isCancelled = ['Cancelled', 'Rejected', 'Đã hủy'].includes(o.status);

  // Detail headers
  document.getElementById('detail-order-code').textContent = `Chi tiết đơn ${o.sellerOrderCode}`;
  const detailPill = document.getElementById('detail-order-pill');
  detailPill.textContent = st;
  detailPill.className = `pill ${isCancelled ? 'red' : ''}`;

  // Reject headers
  document.getElementById('reject-order-code').textContent = `Từ chối đơn ${o.sellerOrderCode}`;
  const rejectPill = document.getElementById('reject-order-pill');
  rejectPill.textContent = st;
  rejectPill.className = `pill ${isCancelled ? 'red' : ''}`;

  // dl keyvalue
  document.getElementById('dt-recipient-name').textContent = o.recipientName || '---';
  document.getElementById('dt-recipient-phone').textContent = o.recipientPhone || '—';
  document.getElementById('dt-shipping-address').textContent = o.shippingAddress || '---';
  document.getElementById('dt-order-notes').textContent = o.notes || 'Không có ghi chú';

  // Items
  const itemsContainer = document.getElementById('dt-items-list');
  itemsContainer.innerHTML = (o.items || []).map(it => `
    <div style="margin-bottom: 8px;">
      <strong>${Utils.escapeHtml(it.productName)}</strong> · ${it.quantity} ${it.unit}
      <br />
      Đơn giá: ${Utils.formatVND(it.unitPrice)}/${it.unit} · Tiền hàng: ${Utils.formatVND(it.lineAmount ?? it.subtotal ?? it.quantity * it.unitPrice)}
      <br />
      ${(it.allocations || []).map(a => `Lô: ${Utils.escapeHtml(a.batchCode || '—')} · Hạn sử dụng: ${Utils.escapeHtml(a.expirationDate || '—')}`).join('<br>') || 'Chưa có lô hàng được phân bổ'}
    </div>
  `).join('');

  document.getElementById('dt-delivery-fee').textContent = Utils.formatVND(o.deliveryFee || 0);
  document.getElementById('dt-discount').textContent = Utils.formatVND(o.discountShare || 0);
  document.getElementById('dt-final-amount').textContent = Utils.formatVND(o.finalAmount || 0);

  const statusDesc = ['Cancelled', 'Rejected'].includes(o.status)
    ? 'Đã giải phóng hàng giữ chỗ'
    : ['Shipping', 'InTransit'].includes(o.status)
      ? 'Đã xuất giao hàng'
      : o.canConfirm || o.canMarkReady || o.canDispatch ? 'Đã có phân bổ hàng hợp lệ' : 'Cần kiểm tra thanh toán và phân bổ hàng';
  document.getElementById('dt-payment-status').textContent = `${o.paymentStatus === 'Paid' ? 'Đã xác minh thanh toán' : 'Chưa có thanh toán'} · ${statusDesc}`;

  // Buttons active/disabled states
  document.getElementById('btn-confirm-order').disabled = (actionBusy || !o.canConfirm);
  document.getElementById('btn-ready-order').disabled = actionBusy || !o.canMarkReady;
  document.getElementById('btn-dispatch-order').disabled = actionBusy || !o.canDispatch;
  document.getElementById('btn-goto-reject').disabled = actionBusy || !o.canReject;

  // Timeline
  const timelineContainer = document.getElementById('dt-timeline');
  if (o.timeline && o.timeline.length > 0) {
    timelineContainer.innerHTML = o.timeline.map(e => `
      <div class="event">
        <strong>${displayStatus(e.newStatus)}</strong>
        <p>${Utils.formatDateTime(e.occurredAt)} · ${Utils.escapeHtml(e.reason || 'Cập nhật trạng thái đơn hàng.')}</p>
      </div>
    `).join('');
  } else {
    timelineContainer.innerHTML = `
      <div class="event">
        <strong>${displayStatus(o.status)}</strong>
        <p>${Utils.formatDateTime(o.orderDate)} · Đơn hàng đã được tạo.</p>
      </div>
    `;
  }

  // Populate Reject view data
  document.getElementById('btn-submit-reject').disabled = actionBusy || !o.canReject;
}

function showActionFeedback(message, order) {
  renderOrdersTable(ordersList);
  setFeedback(message + (order.notificationPending
    ? Messages.order.notificationPending : ''));
}

async function confirmCurrentOrder() {
  if (!selectedOrder || actionBusy) return;
  if (!confirm(Messages.order.confirmPacking)) return;
  actionBusy = true;
  populateDetailView(selectedOrder);
  try {
    const res = await API.confirmFarmerOrder(selectedOrder.sellerOrderId || selectedOrder.id, selectedOrder.version || '1');
    if (res.success && res.data) {
      selectedOrder = res.data;
      ordersList = ordersList.map(o => ((o.sellerOrderId || o.id) === (res.data.sellerOrderId || res.data.id) ? res.data : o));
      populateDetailView(selectedOrder);
      showActionFeedback(Messages.order.confirmed, selectedOrder);
    } else {
      setFeedback(res.error || Messages.order.confirmFailed, true);
    }
  } catch (err) {
    setFeedback(err.message || Messages.order.confirmApiFailed, true);
  } finally {
    actionBusy = false;
    populateDetailView(selectedOrder);
  }
}

async function readyCurrentOrder() {
  if (!selectedOrder || actionBusy) return;
  if (!confirm(Messages.order.readyToShip)) return;
  actionBusy = true;
  populateDetailView(selectedOrder);
  try {
    const res = await API.readyFarmerOrder(selectedOrder.sellerOrderId || selectedOrder.id, selectedOrder.version || '1');
    if (res.success && res.data) {
      selectedOrder = res.data;
      ordersList = ordersList.map(o => ((o.sellerOrderId || o.id) === (res.data.sellerOrderId || res.data.id) ? res.data : o));
      populateDetailView(selectedOrder);
      showActionFeedback(Messages.order.readySuccess, selectedOrder);
    } else {
      setFeedback(res.error || Messages.order.actionFailed, true);
    }
  } catch (err) {
    setFeedback(err.message || Messages.order.readyFailed, true);
  } finally {
    actionBusy = false;
    populateDetailView(selectedOrder);
  }
}

async function dispatchCurrentOrder() {
  if (!selectedOrder || actionBusy) return;
  if (!confirm(Messages.order.startShipping)) return;
  actionBusy = true;
  populateDetailView(selectedOrder);
  try {
    const res = await API.dispatchFarmerOrder(selectedOrder.sellerOrderId || selectedOrder.id, selectedOrder.version || '1');
    if (res.success && res.data) {
      selectedOrder = res.data;
      ordersList = ordersList.map(o => ((o.sellerOrderId || o.id) === (res.data.sellerOrderId || res.data.id) ? res.data : o));
      populateDetailView(selectedOrder);
      showActionFeedback(Messages.order.shippingSuccess, selectedOrder);
    } else {
      setFeedback(res.error || Messages.order.shippingFailed, true);
    }
  } catch (err) {
    setFeedback(err.message || Messages.order.shippingApiFailed, true);
  } finally {
    actionBusy = false;
    populateDetailView(selectedOrder);
  }
}

async function handleRejectSubmit(e) {
  e.preventDefault();
  if (!selectedOrder || actionBusy) return;
  const reason = document.getElementById('rejectionReasonInput').value.trim();
  if (!reason) {
    setFeedback(Messages.order.rejectReasonRequired, true);
    return;
  }

  if (!confirm(Messages.order.rejectConfirm(selectedOrder.sellerOrderCode))) return;

  const btn = document.getElementById('btn-submit-reject');
  btn.disabled = true;
  btn.textContent = 'Đang xử lý…';
  actionBusy = true;
  populateDetailView(selectedOrder);

  try {
    const res = await API.rejectFarmerOrder(selectedOrder.sellerOrderId || selectedOrder.id, reason, selectedOrder.version || '1');
    if (res.success && res.data) {
      selectedOrder = res.data;
      ordersList = ordersList.map(o => ((o.sellerOrderId || o.id) === (res.data.sellerOrderId || res.data.id) ? res.data : o));
      showActionFeedback(Messages.order.rejectedPending, selectedOrder);
      switchView('detail');
    } else {
      setFeedback(res.error || Messages.order.rejectFailed, true);
    }
  } catch (err) {
    setFeedback(err.message || Messages.order.rejectApiFailed, true);
  } finally {
    actionBusy = false;
    populateDetailView(selectedOrder);
    btn.textContent = 'Xác nhận từ chối';
  }
}

async function printOrderSlip() {
  if (!selectedOrder) return;
  const popup = window.open('', '_blank', 'width=1000,height=760');
  if (!popup) {
    alert(Messages.order.popupBlockedNotice);
    return;
  }

  const res = await API.getPackingSlip(selectedOrder.sellerOrderId);
  if (!res.success || !res.data) {
    popup.close();
    setFeedback(res.error || Messages.order.printFailed, true);
    return;
  }
  const o = res.data;
  const itemRows = (o.items || []).map(it => `
    <tr>
      <td>${Utils.escapeHtml(it.productName)}</td>
      <td>${Utils.escapeHtml(it.unit)}</td>
      <td>${it.quantity}</td>
      <td>${Utils.formatVND(it.unitPrice)}</td>
      <td>${Utils.formatVND(it.lineAmount ?? it.subtotal ?? it.quantity * it.unitPrice)}</td>
      <td>${Utils.escapeHtml(it.batchCode || '—')}</td>
      <td>${Utils.escapeHtml(it.expirationDate || '—')}</td>
    </tr>
  `).join('');

  const html = `<!doctype html>
  <html lang="vi">
  <head>
    <meta charset="utf-8">
    <title>Phiếu đóng gói ${Utils.escapeHtml(o.sellerOrderCode)}</title>
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
    <h2>${Utils.escapeHtml(o.sellerOrderCode)}</h2>
    <p><strong>Đơn tổng:</strong> ${Utils.escapeHtml(o.masterOrderCode)} · <strong>Ngày đặt:</strong> ${Utils.formatDateTime(o.orderDate)}</p>
    <p><strong>Người nhận:</strong> ${Utils.escapeHtml(o.recipientName)} · <strong>Điện thoại:</strong> ${Utils.escapeHtml(o.recipientPhone)}</p>
    <p><strong>Địa chỉ giao:</strong> ${Utils.escapeHtml(o.shippingAddress)}</p>
    <table>
      <thead>
        <tr>
          <th>Sản phẩm</th><th>Đơn vị</th><th>Số lượng</th><th>Đơn giá</th><th>Thành tiền</th><th>Mã lô</th><th>Hạn sử dụng</th>
        </tr>
      </thead>
      <tbody>
        ${itemRows || '<tr><td colspan="7">Không có dữ liệu hàng hóa</td></tr>'}
      </tbody>
    </table>
    <div class="right">
      <p>Tiền hàng: ${Utils.formatVND(o.subtotal || 0)}</p>
      <p>Phí giao hàng: ${Utils.formatVND(o.deliveryFee || 0)}</p>
      <p>Mã giảm giá: ${Utils.escapeHtml(o.voucherCode || 'Không áp dụng')}</p>
      <p>Giảm giá phân bổ: −${Utils.formatVND(o.discountShare || 0)}</p>
      <h3>Tổng thanh toán: ${Utils.formatVND(o.finalAmount || 0)}</h3>
    </div>
    <p><strong>Ghi chú:</strong> ${Utils.escapeHtml(o.notes || 'Không có ghi chú')}</p>
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
  setFeedback(Messages.order.printReady);
}

function handleApplyFilters(e) {
  e.preventDefault();
  loadOrders();
}

function handleClearFilters() {
  document.getElementById('orderKeyword').value = '';
  document.getElementById('orderStatus').value = '';
  document.getElementById('fromDate').value = '';
  document.getElementById('toDate').value = '';
  loadOrders();
}
