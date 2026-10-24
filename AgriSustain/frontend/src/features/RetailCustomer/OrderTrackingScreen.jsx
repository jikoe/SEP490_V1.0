import React, { useEffect, useState } from 'react';
import { request, queryString } from '../../api/client';
import { money, localTime, Notice, Modal, Pager, ProductThumbnail } from '../../components/ui/Common';

const pageSize = 10;
const tabs = [
  ['All', 'Tất cả đơn'], ['PendingPayment', 'Chờ thanh toán'], ['Processing', 'Đang xử lý'],
  ['Shipping', 'Đang vận chuyển'], ['Delivered', 'Đã giao'], ['Cancelled', 'Đã hủy'],
];
const statusLabels = {
  PendingPayment: 'Chờ thanh toán', Processing: 'Đang xử lý', Shipping: '🚚 Đang vận chuyển',
  Delivered: '✓ Đã giao', Completed: '✓ Đã hoàn thành', DeliveryFailed: 'Giao thất bại',
  Cancelled: '✕ Đã hủy', Created: 'Đã đặt hàng',
};
const statusColors = {
  Delivered: ['#eaf4e9', '#1e6546'], Completed: ['#eaf4e9', '#1e6546'],
  Shipping: ['#e6f3fa', '#1976d2'], Processing: ['#e6f3fa', '#1976d2'],
  PendingPayment: ['#fbf3dc', '#8b6b26'],
};
const paymentLabels = { Paid: 'Đã thanh toán', Unpaid: 'Chưa thanh toán' };
const deliveryLabels = { Standard: 'Giao tiêu chuẩn', FreshExpress: 'Giao nhanh nông sản' };
const safeUrl = (value) => typeof value === 'string' && /^(https?:\/\/|\/(?!\/))/.test(value) ? value : '';
const dateLabel = (value) => value ? new Date(`${value}T00:00:00`).toLocaleDateString('vi-VN') : '—';

function StatusPill({ status }) {
  const [background, color] = statusColors[status] || ['#fbeee9', '#c2533c'];
  return <span style={{ background, color, fontWeight: 700, fontSize: 12, padding: '4px 12px', borderRadius: 12 }}>
    {statusLabels[status] || status}</span>;
}

/**
 * UC-RC-04: Track Retail Order Screen (SCR-16). Lists the signed-in RetailCustomer's orders from
 * /api/orders, including the harvest batches allocated at checkout for traceability.
 */
export default function OrderTrackingScreen({ navigate }) {
  const [activeTab, setActiveTab] = useState('All');
  const [keywordDraft, setKeywordDraft] = useState('');
  const [keyword, setKeyword] = useState('');
  const [page, setPage] = useState(1);
  const [result, setResult] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [detail, setDetail] = useState(null);
  const [detailError, setDetailError] = useState('');
  const [trace, setTrace] = useState(null);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError('');
    request(`/orders?${queryString({ status: activeTab === 'All' ? '' : activeTab, keyword, page, pageSize })}`,
      { signal: controller.signal })
      .then(setResult)
      .catch((failure) => { if (failure.name !== 'AbortError') setError(failure.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [activeTab, keyword, page]);

  function selectTab(tab) {
    setActiveTab(tab);
    setPage(1);
  }

  function search(event) {
    event.preventDefault();
    setKeyword(keywordDraft.trim());
    setPage(1);
  }

  async function openDetail(order) {
    setDetail({ ...order, loading: true });
    setDetailError('');
    try {
      setDetail(await request(`/orders/${order.orderId}`));
    } catch (failure) {
      setDetailError(failure.message);
    }
  }

  const orders = result?.items || [];

  return (
    <div style={{ maxWidth: 1120, margin: '0 auto' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end', marginBottom: 20, gap: 16, flexWrap: 'wrap' }}>
        <div>
          <span style={{ fontSize: 11, fontWeight: 700, letterSpacing: 1.2, color: '#1e6546' }}>
            THEO DÕI ĐƠN HÀNG (SCR-16 / UC-RC-04)
          </span>
          <h1 style={{ fontSize: 26, margin: '4px 0 6px', color: '#1b4332', fontWeight: 800 }}>
            Lịch sử & Theo dõi chi tiết đơn hàng
          </h1>
          <p className="muted" style={{ margin: 0, fontSize: 13 }}>
            Tra cứu trạng thái đơn, vận chuyển và mã lô thu hoạch của nông sản đã mua.
          </p>
        </div>
        <form onSubmit={search} style={{ display: 'flex', gap: 8, width: 380 }}>
          <input type="search" placeholder="Tìm theo mã đơn hoặc tên nông sản..." maxLength={100}
            value={keywordDraft} onChange={(e) => setKeywordDraft(e.target.value)}
            style={{ borderRadius: 20, padding: '9px 16px', background: '#ffffff', border: '1px solid #cce0d2' }} />
          <button type="submit">Tìm</button>
        </form>
      </div>

      <div role="tablist" aria-label="Trạng thái đơn hàng"
        style={{ display: 'flex', gap: 10, borderBottom: '2px solid #e2eae4', marginBottom: 24, paddingBottom: 2, flexWrap: 'wrap' }}>
        {tabs.map(([key, label]) => {
          const isSelected = activeTab === key;
          return <button key={key} type="button" role="tab" aria-selected={isSelected} onClick={() => selectTab(key)}
            style={{ border: 'none', background: isSelected ? '#1e6546' : 'transparent', color: isSelected ? '#ffffff' : '#4d695a',
              fontWeight: isSelected ? 700 : 500, padding: '9px 18px', borderRadius: '8px 8px 0 0', cursor: 'pointer' }}>
            {label}{isSelected && result ? ` (${result.total})` : ''}
          </button>;
        })}
      </div>

      <Notice error>{error}</Notice>

      {loading && !result ? <p role="status">Đang tải đơn hàng…</p> : orders.length === 0 ? (
        <div className="panel padded empty" style={{ textAlign: 'center', padding: '60px 20px' }}>
          <span style={{ fontSize: 48, display: 'block', marginBottom: 12 }}>📦</span>
          <strong style={{ fontSize: 16, color: '#2d4d3c' }}>
            {keyword || activeTab !== 'All' ? 'Không tìm thấy đơn hàng phù hợp' : 'Bạn chưa có đơn hàng nào'}
          </strong>
          <p className="muted" style={{ marginTop: 6 }}>
            {keyword || activeTab !== 'All' ? 'Thử từ khóa khác hoặc chuyển tab trạng thái.'
              : 'Hãy khám phá nông sản và đặt đơn đầu tiên của bạn.'}
          </p>
          {!keyword && activeTab === 'All' && <button type="button" className="primary"
            onClick={() => navigate('/marketplace')}>Khám phá nông sản</button>}
        </div>
      ) : (
        <div style={{ display: 'grid', gap: 20 }} aria-busy={loading}>
          {orders.map((order) => (
            <div key={order.orderId} className="panel"
              style={{ borderRadius: 14, border: '1px solid #dce8e0', overflow: 'hidden' }}>
              <div style={{ padding: '16px 24px', background: '#f9fbf9', borderBottom: '1px solid #eef3ef',
                display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
                  <span style={{ fontWeight: 800, color: '#1b4332', fontSize: 15 }}>Mã đơn: {order.orderCode}</span>
                  <span style={{ fontSize: 12, color: '#6a8274' }}>Đặt lúc: {localTime(order.orderDate)}</span>
                </div>
                <StatusPill status={order.orderStatus} />
              </div>

              <div style={{ padding: '20px 24px' }}>
                {order.sellerOrders.map((sellerOrder) => (
                  <div key={sellerOrder.sellerOrderId}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 14 }}>
                      <span style={{ fontSize: 16 }}>🏡</span>
                      <strong style={{ fontSize: 14, color: '#1e4834' }}>{sellerOrder.farmName}</strong>
                      {sellerOrder.farmLocation && <span style={{ fontSize: 12, color: '#7a9184' }}>({sellerOrder.farmLocation})</span>}
                    </div>
                    {sellerOrder.items.map((item) => (
                      <div key={item.orderItemId} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                        padding: '12px 16px', background: '#f8faf8', borderRadius: 10, marginBottom: 10,
                        border: '1px solid #edf3ef', flexWrap: 'wrap', gap: 12 }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
                          <ProductThumbnail src={item.imageUrl} name={item.productName} />
                          <div>
                            <div style={{ fontWeight: 700, fontSize: 14, color: '#1f3e2e' }}>{item.productName}</div>
                            <div style={{ fontSize: 12, color: '#688273', marginTop: 4 }}>
                              Số lượng: <strong>{item.quantity} {item.unit}</strong> × {money(item.unitPrice)}
                            </div>
                          </div>
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
                          {item.batches.map((batch) => (
                            <button key={batch.batchCode} type="button"
                              onClick={() => setTrace({ ...batch, productName: item.productName, unit: item.unit, farmName: sellerOrder.farmName })}
                              title="Xem thông tin truy xuất nguồn gốc lô này"
                              style={{ border: '1px solid #1e6546', background: '#edf7f1', color: '#1e6546', padding: '6px 12px',
                                borderRadius: 20, fontSize: 12, fontWeight: 700 }}>
                              🔍 Mã lô: {batch.batchCode}
                            </button>
                          ))}
                          <strong style={{ fontSize: 15, color: '#1e6546', minWidth: 100, textAlign: 'right' }}>
                            {money(item.subTotal)}
                          </strong>
                        </div>
                      </div>
                    ))}
                  </div>
                ))}
              </div>

              <div style={{ padding: '16px 24px', borderTop: '1px solid #edf2ed', display: 'flex',
                justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 14 }}>
                <span style={{ fontSize: 12, color: '#6f8578' }}>
                  Mã vận đơn: <strong>{order.trackingNumber || 'Chưa có'}</strong>
                </span>
                <div style={{ display: 'flex', alignItems: 'center', gap: 16, flexWrap: 'wrap' }}>
                  <span>
                    <span style={{ fontSize: 12, color: '#6f8578', marginRight: 6 }}>Tổng thanh toán:</span>
                    <strong style={{ fontSize: 18, color: '#1e6546' }}>{money(order.finalAmount)}</strong>
                  </span>
                  <button type="button" onClick={() => openDetail(order)}>Xem chi tiết</button>
                  {['Delivered', 'Completed'].includes(order.orderStatus) && <button type="button" disabled
                    title="Chức năng đánh giá (UC-RC-05) chưa được triển khai.">⭐ Đánh giá</button>}
                  <button type="button" disabled title="Chức năng khiếu nại (UC-RC-06) chưa được triển khai.">
                    Khiếu nại</button>
                </div>
              </div>
            </div>
          ))}
          <Pager total={result.total} page={page} pageSize={pageSize} onPage={setPage} />
        </div>
      )}

      {detail && (
        <Modal onClose={() => setDetail(null)} title={`Chi tiết đơn hàng #${detail.orderCode}`}>
          <Notice error>{detailError}</Notice>
          {detail.loading && !detailError ? <p role="status">Đang tải chi tiết đơn…</p> : !detail.loading && <>
            <div style={{ background: '#f5faf7', borderRadius: 12, padding: '16px 20px', marginBottom: 20 }}>
              <h4 style={{ margin: '0 0 12px', fontSize: 13, color: '#1e6546' }}>HÀNH TRÌNH ĐƠN HÀNG</h4>
              <ol style={{ margin: 0, paddingLeft: 18, display: 'grid', gap: 8, fontSize: 13 }}>
                {detail.timeline.map((entry, index) => <li key={index}>
                  <strong>{statusLabels[entry.status] || entry.status}</strong>
                  <span className="muted"> · {localTime(entry.timestamp)}</span>
                  <div style={{ color: '#557262' }}>{entry.description}</div>
                </li>)}
              </ol>
            </div>
            <dl className="details">
              <dt>Trạng thái</dt><dd><StatusPill status={detail.orderStatus} /></dd>
              <dt>Người nhận</dt><dd>{detail.recipientName} · {detail.recipientPhone}</dd>
              <dt>Địa chỉ</dt><dd>{detail.shippingAddress}</dd>
              <dt>Vận chuyển</dt><dd>{deliveryLabels[detail.deliveryMethod] || detail.deliveryMethod || '—'}</dd>
              <dt>Ngày giao dự kiến</dt><dd>{detail.expectedDeliveryDate
                ? new Date(detail.expectedDeliveryDate).toLocaleDateString('vi-VN') : '—'}</dd>
              <dt>Mã vận đơn</dt><dd>{detail.trackingNumber || 'Chưa có'}</dd>
              <dt>Thanh toán</dt><dd>{detail.paymentMethod || '—'} · {paymentLabels[detail.paymentStatus] || detail.paymentStatus}</dd>
              <dt>Tiền sản phẩm</dt><dd>{money(detail.totalAmount)}</dd>
              <dt>Phí giao hàng</dt><dd>{money(detail.deliveryFee)}</dd>
              <dt>Giảm giá</dt><dd>{money(detail.discountAmount)}</dd>
              <dt><strong>Tổng thanh toán</strong></dt><dd><strong>{money(detail.finalAmount)}</strong></dd>
            </dl>
          </>}
          <div className="modal-actions">
            <button type="button" onClick={() => setDetail(null)}>Đóng cửa sổ</button>
          </div>
        </Modal>
      )}

      {trace && (
        <Modal onClose={() => setTrace(null)} title={`Truy xuất nguồn gốc lô: ${trace.batchCode}`}>
          <h3 style={{ margin: '0 0 12px', color: '#1b4332' }}>{trace.productName}</h3>
          <dl className="details">
            <dt>Nông trại / HTX</dt><dd>{trace.farmName}</dd>
            <dt>Mã lô</dt><dd>{trace.batchCode}</dd>
            <dt>Số lượng thuộc lô</dt><dd>{trace.quantity} {trace.unit}</dd>
            <dt>Ngày thu hoạch</dt><dd>{dateLabel(trace.harvestDate)}</dd>
            <dt>Mùa vụ</dt><dd>{trace.seasonName || '—'}</dd>
          </dl>
          <div className="modal-actions">
            <button type="button" onClick={() => setTrace(null)}>Đóng</button>
            {safeUrl(trace.traceabilityUrl) ? <a className="primary" href={safeUrl(trace.traceabilityUrl)}
              target="_blank" rel="noreferrer" style={{ padding: '9px 16px', borderRadius: 8 }}>
              Xem truy xuất đầy đủ →</a>
              : <span className="muted" style={{ alignSelf: 'center' }}>Lô này chưa có mã QR truy xuất.</span>}
          </div>
        </Modal>
      )}
    </div>
  );
}
