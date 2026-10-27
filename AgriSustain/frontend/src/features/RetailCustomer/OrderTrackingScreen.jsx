import React, { useCallback, useEffect, useState } from 'react';
import { request, queryString } from '../../api/client';
import { money, localTime, Notice, Modal, Pager } from '../../components/ui/Common';

/**
 * UC-RC-04: Track Retail Order (SCR-12 Order History, SCR-16 Order Detail).
 * Data comes from GET /api/orders (server-side filter, search and paging) and
 * GET /api/orders/{id}; both require the RetailCustomer JWT of the signed-in user.
 */

// Status tabs defined in SRS III.6.3.1 (1).
const TABS = [
  { key: 'All', label: 'Tất cả' },
  { key: 'Completed', label: 'Hoàn thành' },
  { key: 'Cancelled', label: 'Đã hủy' },
];

const STATUS = {
  PendingPayment: { label: 'Chờ thanh toán', color: '#b7791f', bg: '#fdf6e3' },
  Paid: { label: 'Đã thanh toán', color: '#1976d2', bg: '#e6f3fa' },
  Processing: { label: 'Đang xử lý', color: '#1976d2', bg: '#e6f3fa' },
  Confirmed: { label: 'Đã xác nhận', color: '#1976d2', bg: '#e6f3fa' },
  Preparing: { label: 'Đang chuẩn bị', color: '#1976d2', bg: '#e6f3fa' },
  ReadyToShip: { label: 'Chờ giao', color: '#1976d2', bg: '#e6f3fa' },
  Shipping: { label: 'Đang vận chuyển', color: '#1976d2', bg: '#e6f3fa' },
  Delivered: { label: 'Đã giao', color: '#1e6546', bg: '#eaf4e9' },
  Completed: { label: 'Hoàn thành', color: '#1e6546', bg: '#eaf4e9' },
  Cancelled: { label: 'Đã hủy', color: '#c2533c', bg: '#fbeee9' },
  DeliveryFailed: { label: 'Giao thất bại', color: '#c2533c', bg: '#fbeee9' },
};

function StatusBadge({ value }) {
  const s = STATUS[value] || { label: value, color: '#4d695a', bg: '#eef3ef' };
  return (
    <span style={{ background: s.bg, color: s.color, fontWeight: 700, fontSize: 12, padding: '4px 12px', borderRadius: 12 }}>
      {s.label}
    </span>
  );
}

function itemSummary(order) {
  const items = order.sellerOrders.flatMap((so) => so.items);
  if (items.length === 0) return '';
  const first = `${items[0].productName} × ${items[0].quantity} ${items[0].unit}`;
  return items.length > 1 ? `${first} và ${items.length - 1} sản phẩm khác` : first;
}

export default function OrderTrackingScreen({ navigate }) {
  const [tab, setTab] = useState('All');
  const [keywordInput, setKeywordInput] = useState('');
  const [keyword, setKeyword] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [data, setData] = useState({ items: [], totalCount: 0 });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState(null);

  const [detail, setDetail] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [batch, setBatch] = useState(null);

  // Order history: every filter change is a new server query (SRS UC-RC-04 AF Step 3).
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);
    const qs = queryString({ status: tab === 'All' ? '' : tab, keyword: keyword.trim(), page, pageSize });
    request(`/orders?${qs}`, { signal: controller.signal })
      .then((result) => setData({ items: result.items || [], totalCount: result.total ?? 0 }))
      .catch((err) => {
        if (err.name !== 'AbortError') setError(err.message);
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [tab, keyword, page, pageSize]);

  const openDetail = useCallback(async (orderId) => {
    setDetailLoading(true);
    setNotice(null);
    try {
      setDetail(await request(`/orders/${orderId}`));
    } catch (err) {
      setError(err.message);
    } finally {
      setDetailLoading(false);
    }
  }, []);

  function changeTab(key) {
    setTab(key);
    setPage(1);
  }

  function search(e) {
    e.preventDefault();
    setPage(1);
    setKeyword(keywordInput);
  }

  function goCatalog() {
    if (navigate) navigate('/marketplace');
    else window.location.hash = '#/marketplace';
  }

  const actions = detail?.allowedActions || [];
  const hasFilter = tab !== 'All' || keyword.trim() !== '';

  return (
    <div style={{ maxWidth: 1120, margin: '0 auto' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end', marginBottom: 20, flexWrap: 'wrap', gap: 12 }}>
        <div>
          <span style={{ fontSize: 11, fontWeight: 700, letterSpacing: 1.2, color: '#1e6546' }}>THEO DÕI ĐƠN HÀNG</span>
          <h1 style={{ fontSize: 26, margin: '4px 0 6px', color: '#1b4332', fontWeight: 800 }}>Lịch sử mua hàng</h1>
          <p className="muted" style={{ margin: 0, fontSize: 13 }}>
            Theo dõi trạng thái giao hàng và tra cứu mã lô truy xuất nguồn gốc.
          </p>
        </div>

        <form onSubmit={search} style={{ display: 'flex', gap: 8, width: 420, maxWidth: '100%' }}>
          <input
            type="search"
            aria-label="Tìm kiếm đơn hàng"
            placeholder="Mã đơn (#ORD-...) hoặc tên sản phẩm"
            value={keywordInput}
            maxLength={100}
            onChange={(e) => setKeywordInput(e.target.value)}
            style={{ flex: 1, borderRadius: 20, padding: '9px 16px', border: '1px solid #cce0d2' }}
          />
          <button type="submit" className="primary" style={{ borderRadius: 20, background: '#1e6546' }}>Tìm kiếm</button>
        </form>
      </div>

      <Notice error>{error}</Notice>
      <Notice>{notice}</Notice>

      <div role="tablist" style={{ display: 'flex', gap: 10, borderBottom: '2px solid #e2eae4', marginBottom: 24 }}>
        {TABS.map((t) => (
          <button
            key={t.key}
            type="button"
            role="tab"
            aria-selected={tab === t.key}
            onClick={() => changeTab(t.key)}
            style={{
              border: 'none',
              background: tab === t.key ? '#1e6546' : 'transparent',
              color: tab === t.key ? '#ffffff' : '#4d695a',
              fontWeight: tab === t.key ? 700 : 500,
              padding: '9px 18px',
              borderRadius: '8px 8px 0 0',
              cursor: 'pointer',
            }}
          >
            {t.label}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="panel padded" style={{ textAlign: 'center', padding: 40 }}>Đang tải đơn hàng...</div>
      ) : data.items.length === 0 ? (
        <div className="panel padded empty" style={{ textAlign: 'center', padding: '60px 20px', background: '#ffffff', borderRadius: 14 }}>
          <span style={{ fontSize: 52, display: 'block', marginBottom: 14 }}>📦</span>
          {hasFilter ? (
            <>
              <strong style={{ fontSize: 16, color: '#2d4d3c' }}>Không tìm thấy đơn hàng phù hợp</strong>
              <p className="muted" style={{ marginTop: 6 }}>Thử từ khóa khác hoặc chọn tab trạng thái khác.</p>
            </>
          ) : (
            <>
              {/* MSG23 */}
              <strong style={{ fontSize: 18, color: '#1e6546', display: 'block', marginBottom: 12 }}>
                Bạn chưa có đơn hàng nào.
              </strong>
              <button type="button" className="primary" style={{ background: '#1e6546' }} onClick={goCatalog}>
                Khám phá nông sản
              </button>
            </>
          )}
        </div>
      ) : (
        <>
          <div style={{ display: 'grid', gap: 16 }}>
            {data.items.map((order) => (
              <div key={order.orderId} className="panel" style={{ borderRadius: 14, border: '1px solid #dce8e0', overflow: 'hidden' }}>
                <div style={{ padding: '14px 24px', background: '#f9fbf9', borderBottom: '1px solid #eef3ef', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 14, flexWrap: 'wrap' }}>
                    <strong style={{ color: '#1b4332', fontSize: 15 }}>#{order.orderCode}</strong>
                    <span style={{ fontSize: 12, color: '#6a8274' }}>Ngày đặt: {localTime(order.orderDate)}</span>
                  </div>
                  <StatusBadge value={order.orderStatus} />
                </div>
                <div style={{ padding: '14px 24px', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 12 }}>
                  <div style={{ fontSize: 13, color: '#2d4d3c' }}>
                    <div>{itemSummary(order)}</div>
                    <div className="muted" style={{ fontSize: 12, marginTop: 4 }}>
                      {order.sellerOrders.map((so) => so.farmName).join(', ')}
                    </div>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
                    <div style={{ textAlign: 'right' }}>
                      <span style={{ fontSize: 12, color: '#6f8578', marginRight: 6 }}>Tổng tiền:</span>
                      <strong style={{ fontSize: 17, color: '#1e6546' }}>{money(order.finalAmount)}</strong>
                    </div>
                    <button type="button" disabled={detailLoading} onClick={() => openDetail(order.orderId)}>
                      Xem chi tiết
                    </button>
                  </div>
                </div>
              </div>
            ))}
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 16, flexWrap: 'wrap', gap: 12 }}>
            <label style={{ fontSize: 13 }}>
              Hiển thị{' '}
              <select value={pageSize} onChange={(e) => { setPageSize(Number(e.target.value)); setPage(1); }}>
                {[10, 20, 50].map((n) => <option key={n} value={n}>{n} / trang</option>)}
              </select>
            </label>
            <Pager total={data.totalCount} page={page} pageSize={pageSize} onPage={setPage} />
          </div>
        </>
      )}

      {detail && (
        <Modal title={`Chi tiết đơn hàng #${detail.orderCode}`} onClose={() => setDetail(null)} hideCloseButton>
          <section style={{ marginBottom: 16 }}>
            <h4 style={{ margin: '0 0 8px', fontSize: 13, color: '#2d4d3c' }}>THÔNG TIN ĐƠN HÀNG</h4>
            <div style={{ display: 'grid', gridTemplateColumns: '160px 1fr', gap: 6, fontSize: 13 }}>
              <span className="muted">Mã đơn hàng</span><strong>#{detail.orderCode}</strong>
              <span className="muted">Ngày đặt</span><span>{localTime(detail.orderDate)}</span>
              <span className="muted">Trạng thái</span><span><StatusBadge value={detail.orderStatus} /></span>
              <span className="muted">Thanh toán</span>
              <span>{detail.paymentMethod || '—'} · {detail.paymentStatus === 'Paid' ? 'Đã thanh toán' : 'Chưa thanh toán'}</span>
              <span className="muted">Mã vận đơn</span><span>{detail.trackingNumber || '—'}</span>
              {detail.expectedDeliveryDate && (<><span className="muted">Dự kiến giao</span><span>{localTime(detail.expectedDeliveryDate)}</span></>)}
            </div>
          </section>

          <section style={{ marginBottom: 16 }}>
            <h4 style={{ margin: '0 0 8px', fontSize: 13, color: '#2d4d3c' }}>THÔNG TIN GIAO HÀNG</h4>
            <div style={{ border: '1px solid #e0eae2', borderRadius: 8, padding: 12, fontSize: 13 }}>
              <strong>{detail.recipientName}</strong> · {detail.recipientPhone}
              <div className="muted" style={{ marginTop: 4 }}>{detail.shippingAddress}</div>
            </div>
          </section>

          <section style={{ marginBottom: 16 }}>
            <h4 style={{ margin: '0 0 8px', fontSize: 13, color: '#2d4d3c' }}>SẢN PHẨM</h4>
            {detail.sellerOrders.map((so) => (
              <div key={so.sellerOrderId} style={{ marginBottom: 10 }}>
                <div style={{ fontSize: 13, marginBottom: 6 }}>
                  🏡 <strong>{so.farmName}</strong> <StatusBadge value={so.status} />
                </div>
                <table style={{ width: '100%', fontSize: 13 }}>
                  <thead>
                    <tr><th align="left">Sản phẩm</th><th align="left">Mã lô</th><th align="right">SL</th><th align="right">Đơn giá</th><th align="right">Thành tiền</th></tr>
                  </thead>
                  <tbody>
                    {so.items.map((item) => (
                      <tr key={item.orderItemId}>
                        <td>{item.productName}</td>
                        <td>
                          {item.batches.length === 0 ? '—' : item.batches.map((b) => (
                            <button key={b.batchCode} type="button" className="text-button" onClick={() => setBatch({ ...b, productName: item.productName, farmName: so.farmName })}>
                              {b.batchCode}
                            </button>
                          ))}
                        </td>
                        <td align="right">{item.quantity} {item.unit}</td>
                        <td align="right">{money(item.unitPrice)}</td>
                        <td align="right">{money(item.subTotal)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ))}
          </section>

          <section style={{ marginBottom: 16 }}>
            <h4 style={{ margin: '0 0 8px', fontSize: 13, color: '#2d4d3c' }}>TỔNG QUAN THANH TOÁN</h4>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr auto', gap: 4, fontSize: 13 }}>
              <span className="muted">Tạm tính</span><span>{money(detail.totalAmount)}</span>
              <span className="muted">Phí giao hàng</span><span>{money(detail.deliveryFee)}</span>
              <span className="muted">Giảm giá</span><span>-{money(detail.discountAmount)}</span>
              <strong>Tổng thanh toán</strong><strong style={{ color: '#1e6546' }}>{money(detail.finalAmount)}</strong>
            </div>
          </section>

          {detail.timeline?.length > 0 && (
            <section style={{ marginBottom: 16 }}>
              <h4 style={{ margin: '0 0 8px', fontSize: 13, color: '#2d4d3c' }}>HÀNH TRÌNH ĐƠN HÀNG</h4>
              <ol style={{ margin: 0, paddingLeft: 18, fontSize: 13 }}>
                {detail.timeline.map((t, i) => (
                  <li key={i}>{localTime(t.timestamp)} — {t.description}</li>
                ))}
              </ol>
            </section>
          )}

          <div className="modal-actions" style={{ display: 'flex', justifyContent: 'space-between', gap: 8, flexWrap: 'wrap' }}>
            <div style={{ display: 'flex', gap: 8 }}>
              {actions.includes('PayNow') && (
                <button type="button" className="primary" onClick={() => setNotice('Màn hình thanh toán QR (UC-RC-03) sẽ được tích hợp ở iteration sau.')}>
                  Thanh toán ngay
                </button>
              )}
              {actions.includes('Review') && (
                <button type="button" onClick={() => setNotice('Chức năng đánh giá sản phẩm (UC-RC-05) thuộc Iteration 2.')}>
                  ⭐ Đánh giá
                </button>
              )}
              {actions.includes('SubmitComplaint') && (
                <button type="button" style={{ color: '#b23b3b' }} onClick={() => setNotice('Chức năng khiếu nại đơn hàng (UC-RC-06) thuộc Iteration 2.')}>
                  Gửi khiếu nại
                </button>
              )}
            </div>
            <button type="button" className="primary" style={{ background: '#1e6546' }} onClick={() => setDetail(null)}>
              ← Quay lại danh sách
            </button>
          </div>
        </Modal>
      )}

      {batch && (
        <Modal title={`Truy xuất lô ${batch.batchCode}`} onClose={() => setBatch(null)}>
          <div style={{ display: 'grid', gridTemplateColumns: '140px 1fr', gap: 8, fontSize: 13 }}>
            <strong>Sản phẩm</strong><span>{batch.productName}</span>
            <strong>Nông trại</strong><span>{batch.farmName}</span>
            <strong>Mùa vụ</strong><span>{batch.seasonName || '—'}</span>
            <strong>Ngày thu hoạch</strong><span>{batch.harvestDate || '—'}</span>
            <strong>Số lượng phân bổ</strong><span>{batch.quantity}</span>
          </div>
          <div className="modal-actions">
            {batch.traceabilityUrl && (
              <a href={batch.traceabilityUrl} target="_blank" rel="noopener noreferrer">Mở trang truy xuất</a>
            )}
            <button type="button" onClick={() => setBatch(null)}>Đóng</button>
          </div>
        </Modal>
      )}
    </div>
  );
}
