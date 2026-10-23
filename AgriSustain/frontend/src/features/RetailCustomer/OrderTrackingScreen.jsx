import React, { useState } from 'react';
import { money, localTime, Notice, Modal, Field } from '../../components/ui/Common';

/**
 * UC-RC-04: Track Retail Order Screen (SCR-16)
 * Styled with e-commerce agricultural design language matching Figma SCR-TRC-01
 */
// Sample demonstration orders for UC-RC-04 presentation
const SAMPLE_ORDERS = [
  {
    orderId: 101,
    orderCode: 'ORD-2026-9812',
    orderDate: '2026-10-18T09:30:00Z',
    finalAmount: 325000,
    orderStatus: 'Shipping', // Đang vận chuyển
    paymentStatus: 'Paid',
    recipientName: 'Nguyễn Văn C',
    recipientPhone: '0987223344',
    shippingAddress: 'Số 18, Phố Duy Tân, Phường Dịch Vọng Hậu, Quận Cầu Giấy, Hà Nội',
    trackingNumber: 'AGRI-EXPRESS-VN8823',
    sellerOrders: [
      {
        sellerOrderId: 201,
        farmName: 'HTX Nông Nghiệp Lâm Đồng',
        farmLocation: 'Lâm Hà, Lâm Đồng',
        items: [
          {
            orderItemId: 301,
            productName: 'Bơ Sáp 034 Hữu Cơ (Chứng chỉ VietGAP)',
            harvestBatchCode: 'LOT-BO-2026-08',
            farmPlot: 'Vườn Đồi A3 - Cây đầu dòng',
            harvestDate: '15/10/2026',
            quantity: 5,
            unit: 'kg',
            unitPriceSnapshot: 65000,
            isReviewed: false,
            imageEmoji: '🥑',
          },
        ],
      },
    ],
  },
  {
    orderId: 102,
    orderCode: 'ORD-2026-8940',
    orderDate: '2026-10-12T14:15:00Z',
    finalAmount: 450000,
    orderStatus: 'Completed', // Đã hoàn thành
    paymentStatus: 'Paid',
    recipientName: 'Nguyễn Văn C',
    recipientPhone: '0987223344',
    shippingAddress: 'Số 18, Phố Duy Tân, Phường Dịch Vọng Hậu, Quận Cầu Giấy, Hà Nội',
    trackingNumber: 'AGRI-EXPRESS-VN7719',
    sellerOrders: [
      {
        sellerOrderId: 202,
        farmName: 'Nông trại Xanh Đồng Tháp',
        farmLocation: 'Cao Lãnh, Đồng Tháp',
        items: [
          {
            orderItemId: 302,
            productName: 'Xoài Cát Chu Cao Lãnh Chuẩn OCOP 4 Sao',
            harvestBatchCode: 'LOT-XOAI-09',
            farmPlot: 'Khu A Cù Lao Tây',
            harvestDate: '09/10/2026',
            quantity: 10,
            unit: 'kg',
            unitPriceSnapshot: 45000,
            isReviewed: false,
            imageEmoji: '🥭',
          },
        ],
      },
    ],
  },
  {
    orderId: 103,
    orderCode: 'ORD-2026-7731',
    orderDate: '2026-10-05T08:00:00Z',
    finalAmount: 180000,
    orderStatus: 'Cancelled', // Đã hủy
    paymentStatus: 'Refunded',
    recipientName: 'Nguyễn Văn C',
    recipientPhone: '0987223344',
    shippingAddress: 'Số 18, Phố Duy Tân, Phường Dịch Vọng Hậu, Quận Cầu Giấy, Hà Nội',
    trackingNumber: '—',
    sellerOrders: [
      {
        sellerOrderId: 203,
        farmName: 'Nông Hộ Rau Sạch An Phú',
        farmLocation: 'Thuận Thành, Bắc Ninh',
        items: [
          {
            orderItemId: 303,
            productName: 'Cải Thìa Sinh Thái Bắc Ninh',
            harvestBatchCode: 'BN-20260920-01',
            farmPlot: 'Nhà màng số 2',
            harvestDate: '03/10/2026',
            quantity: 4,
            unit: 'kg',
            unitPriceSnapshot: 45000,
            isReviewed: true,
            imageEmoji: '🥬',
          },
        ],
      },
    ],
  },
];

export default function OrderTrackingScreen({ fixture = 'retail-customer', navigate }) {
  const storedUser = (() => {
    try {
      const raw = sessionStorage.getItem('agrisustain:current-user');
      return raw ? JSON.parse(raw) : null;
    } catch { return null; }
  })();

  const authIdentifier = storedUser?.email || fixture;
  const [activeTab, setActiveTab] = useState('All');
  const [keyword, setKeyword] = useState('');
  const [selectedOrder, setSelectedOrder] = useState(null);
  const [reviewModalOpen, setReviewModalOpen] = useState(false);
  const [complaintModalOpen, setComplaintModalOpen] = useState(false);
  const [traceModalOpen, setTraceModalOpen] = useState(false);
  const [selectedBatch, setSelectedBatch] = useState(null);
  const [toastMessage, setToastMessage] = useState(null);
  const [isDemoView, setIsDemoView] = useState(false);
  const [orders, setOrders] = useState([]);
  const [loading, setLoading] = useState(true);

  React.useEffect(() => {
    let cancelled = false;
    async function loadOrders() {
      setLoading(true);
      try {
        const res = await fetch('/api/orders', {
          headers: { Authorization: `Demo ${authIdentifier}` },
        });
        if (res.ok) {
          const data = await res.json();
          if (!cancelled) {
            setOrders(data.items || []);
          }
        }
      } catch (err) {
        console.error('Failed to load orders', err);
      } finally {
        if (!cancelled) setLoading(false);
      }
    }
    loadOrders();
    return () => { cancelled = true; };
  }, [authIdentifier, fixture]);

  const tabs = [
    { key: 'All', label: 'Tất cả đơn', count: orders.length },
    { key: 'Shipping', label: 'Đang vận chuyển', count: orders.filter((o) => o.orderStatus === 'Shipping').length },
    { key: 'Completed', label: 'Đã hoàn thành', count: orders.filter((o) => o.orderStatus === 'Completed').length },
    { key: 'Cancelled', label: 'Đã hủy', count: orders.filter((o) => o.orderStatus === 'Cancelled').length },
  ];

  const filteredOrders = orders.filter((order) => {
    if (activeTab === 'Completed' && order.orderStatus !== 'Completed') return false;
    if (activeTab === 'Cancelled' && order.orderStatus !== 'Cancelled') return false;
    if (activeTab === 'Shipping' && order.orderStatus !== 'Shipping') return false;
    if (keyword.trim()) {
      const matchCode = order.orderCode.toLowerCase().includes(keyword.toLowerCase());
      const matchProduct = order.sellerOrders.some((so) =>
        so.items.some((item) => item.productName.toLowerCase().includes(keyword.toLowerCase()))
      );
      return matchCode || matchProduct;
    }
    return true;
  });

  function openTraceModal(item, farmName) {
    setSelectedBatch({ ...item, farmName });
    setTraceModalOpen(true);
  }

  return (
    <div style={{ maxWidth: 1120, margin: '0 auto' }}>
      {/* Page Heading matching e-commerce portal */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end', marginBottom: 20 }}>
        <div>
          <span style={{ fontSize: 11, fontWeight: 700, letterSpacing: 1.2, color: '#1e6546' }}>
            THEO DÕI ĐƠN HÀNG (SCR-16 / UC-RC-04)
          </span>
          <h1 style={{ fontSize: 26, margin: '4px 0 6px', color: '#1b4332', fontWeight: 800 }}>
            Lịch sử & Theo dõi chi tiết đơn hàng
          </h1>
          <p className="muted" style={{ margin: 0, fontSize: 13 }}>
            Tra cứu hành trình vận chuyển bưu kiện, truy xuất mã lô nhật ký nông sản số.
          </p>
        </div>

        {/* Thanh tìm kiếm đơn hàng */}
        <div style={{ width: 320 }}>
          <input
            type="text"
            placeholder="Tìm theo mã đơn (ORD-...) hoặc tên nông sản..."
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
            style={{ borderRadius: 20, padding: '9px 16px', background: '#ffffff', border: '1px solid #cce0d2' }}
          />
        </div>
      </div>

      {toastMessage && <Notice>{toastMessage}</Notice>}

      {isDemoView && (
        <div
          style={{
            background: '#f0fdf4',
            border: '1px solid #bbf7d0',
            borderRadius: 10,
            padding: '12px 18px',
            marginBottom: 20,
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: 10,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontSize: 18 }}>💡</span>
            <span style={{ fontSize: 13, color: '#166534' }}>
              Đang hiển thị <strong>3 đơn hàng mẫu</strong> phục vụ kiểm thử tính năng Tra cứu hành trình & Mã lô thu hoạch (UC-RC-04).
            </span>
          </div>
          <button
            type="button"
            className="text-button"
            style={{ color: '#15803d', fontWeight: 700, fontSize: 12 }}
            onClick={() => {
              setOrders([]);
              setIsDemoView(false);
            }}
          >
            Quay lại đơn thực tế của tôi ✕
          </button>
        </div>
      )}

      {/* Filter Tabs */}
      <div
        style={{
          display: 'flex',
          gap: 10,
          borderBottom: '2px solid #e2eae4',
          marginBottom: 24,
          paddingBottom: 2,
        }}
      >
        {tabs.map((tab) => {
          const isSelected = activeTab === tab.key;
          return (
            <button
              key={tab.key}
              type="button"
              onClick={() => setActiveTab(tab.key)}
              style={{
                border: 'none',
                background: isSelected ? '#1e6546' : 'transparent',
                color: isSelected ? '#ffffff' : '#4d695a',
                fontWeight: isSelected ? 700 : 500,
                padding: '9px 18px',
                borderRadius: '8px 8px 0 0',
                cursor: 'pointer',
                transition: 'all 0.15s',
                display: 'flex',
                alignItems: 'center',
                gap: 8,
              }}
            >
              <span>{tab.label}</span>
              <span
                style={{
                  background: isSelected ? 'rgba(255,255,255,0.25)' : '#e8f2eb',
                  color: isSelected ? '#ffffff' : '#2d6a4f',
                  fontSize: 11,
                  padding: '2px 7px',
                  borderRadius: 10,
                  fontWeight: 700,
                }}
              >
                {tab.count}
              </span>
            </button>
          );
        })}
      </div>

      {/* Orders List */}
      <div style={{ display: 'grid', gap: 20 }}>
        {orders.length === 0 && !isDemoView ? (
          <div className="panel padded empty" style={{ textAlign: 'center', padding: '60px 20px', background: '#ffffff', borderRadius: 14 }}>
            <span style={{ fontSize: 52, display: 'block', marginBottom: 14 }}>📦</span>
            <strong style={{ fontSize: 18, color: '#1e6546', display: 'block', marginBottom: 8 }}>
              Tài khoản {storedUser?.fullName || 'của bạn'} chưa có đơn hàng nào
            </strong>
            <p className="muted" style={{ maxWidth: 520, margin: '0 auto 20px', lineHeight: 1.6, fontSize: 13 }}>
              Tài khoản <strong>{storedUser?.fullName || 'Khách hàng'}</strong> ({storedUser?.email || ''}) vừa được tạo thành công trong cơ sở dữ liệu và hiện chưa phát sinh đơn mua hàng nông sản nào.
            </p>
            <button
              type="button"
              className="primary"
              style={{
                padding: '10px 24px',
                borderRadius: 8,
                background: '#1e6546',
                fontWeight: 600,
                fontSize: 14,
                cursor: 'pointer',
              }}
              onClick={() => {
                setOrders(SAMPLE_ORDERS);
                setIsDemoView(true);
              }}
            >
              🌱 Xem 3 đơn hàng mẫu để kiểm thử tính năng Tra cứu & Lô nông sản (UC-RC-04)
            </button>
          </div>
        ) : filteredOrders.length === 0 ? (
          <div className="panel padded empty" style={{ textAlign: 'center', padding: '60px 20px' }}>
            <span style={{ fontSize: 48, display: 'block', marginBottom: 12 }}>🔍</span>
            <strong style={{ fontSize: 16, color: '#2d4d3c' }}>Không tìm thấy đơn hàng phù hợp</strong>
            <p className="muted" style={{ marginTop: 6 }}>
              Thử tìm kiếm với từ khóa khác hoặc chuyển tab bộ lọc trạng thái.
            </p>
          </div>
        ) : (
          filteredOrders.map((order) => (
            <div
              key={order.orderId}
              className="panel"
              style={{
                borderRadius: 14,
                boxShadow: '0 3px 12px rgba(0,0,0,0.04)',
                border: '1px solid #dce8e0',
                overflow: 'hidden',
              }}
            >
              {/* Order Card Header */}
              <div
                style={{
                  padding: '16px 24px',
                  background: '#f9fbf9',
                  borderBottom: '1px solid #eef3ef',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  flexWrap: 'wrap',
                  gap: 12,
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
                  <span style={{ fontWeight: 800, color: '#1b4332', fontSize: 15 }}>
                    Mã đơn: {order.orderCode}
                  </span>
                  <span style={{ fontSize: 12, color: '#6a8274' }}>
                    Đặt lúc: {localTime(order.orderDate)}
                  </span>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                  <span
                    style={{
                      background:
                        order.orderStatus === 'Completed'
                          ? '#eaf4e9'
                          : order.orderStatus === 'Shipping'
                          ? '#e6f3fa'
                          : '#fbeee9',
                      color:
                        order.orderStatus === 'Completed'
                          ? '#1e6546'
                          : order.orderStatus === 'Shipping'
                          ? '#1976d2'
                          : '#c2533c',
                      fontWeight: 700,
                      fontSize: 12,
                      padding: '4px 12px',
                      borderRadius: 12,
                    }}
                  >
                    {order.orderStatus === 'Shipping'
                      ? '🚚 Đang vận chuyển'
                      : order.orderStatus === 'Completed'
                      ? '✓ Đã hoàn thành'
                      : '✕ Đã hủy'}
                  </span>
                </div>
              </div>

              {/* Order Items */}
              <div style={{ padding: '20px 24px' }}>
                {order.sellerOrders.map((sellerOrder) => (
                  <div key={sellerOrder.sellerOrderId}>
                    {/* Farm Shop Title */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 14 }}>
                      <span style={{ fontSize: 16 }}>🏡</span>
                      <strong style={{ fontSize: 14, color: '#1e4834' }}>{sellerOrder.farmName}</strong>
                      <span style={{ fontSize: 12, color: '#7a9184' }}>({sellerOrder.farmLocation})</span>
                    </div>

                    {sellerOrder.items.map((item) => (
                      <div
                        key={item.orderItemId}
                        style={{
                          display: 'flex',
                          alignItems: 'center',
                          justifyContent: 'space-between',
                          padding: '12px 16px',
                          background: '#f8faf8',
                          borderRadius: 10,
                          marginBottom: 10,
                          border: '1px solid #edf3ef',
                          flexWrap: 'wrap',
                          gap: 12,
                        }}
                      >
                        <div style={{ display: 'flex', alignItems: 'center', gap: 14 }}>
                          <div
                            style={{
                              width: 52,
                              height: 52,
                              borderRadius: 10,
                              background: '#e9f5ed',
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'center',
                              fontSize: 26,
                            }}
                          >
                            {item.imageEmoji}
                          </div>
                          <div>
                            <div style={{ fontWeight: 700, fontSize: 14, color: '#1f3e2e' }}>
                              {item.productName}
                            </div>
                            <div style={{ fontSize: 12, color: '#688273', marginTop: 4 }}>
                              Số lượng: <strong>{item.quantity} {item.unit}</strong> × {money(item.unitPriceSnapshot)}
                            </div>
                          </div>
                        </div>

                        {/* Traceability Badge pill */}
                        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                          <button
                            type="button"
                            onClick={() => openTraceModal(item, sellerOrder.farmName)}
                            style={{
                              border: '1px solid #1e6546',
                              background: '#edf7f1',
                              color: '#1e6546',
                              padding: '6px 12px',
                              borderRadius: 20,
                              fontSize: 12,
                              fontWeight: 700,
                              display: 'flex',
                              alignItems: 'center',
                              gap: 6,
                            }}
                            title="Bấm để xem thông tin truy xuất nguồn gốc mã lô này"
                          >
                            <span>🔍</span>
                            <span>Mã lô: {item.harvestBatchCode}</span>
                          </button>

                          <div style={{ textAlign: 'right', minWidth: 100 }}>
                            <strong style={{ fontSize: 15, color: '#1e6546' }}>
                              {money(item.quantity * item.unitPriceSnapshot)}
                            </strong>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                ))}
              </div>

              {/* Order Card Footer */}
              <div
                style={{
                  padding: '16px 24px',
                  background: '#ffffff',
                  borderTop: '1px solid #edf2ed',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  flexWrap: 'wrap',
                  gap: 14,
                }}
              >
                <div>
                  <span style={{ fontSize: 12, color: '#6f8578' }}>
                    Vận chuyển bởi: <strong>{order.trackingNumber}</strong>
                  </span>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
                  <div style={{ textAlign: 'right' }}>
                    <span style={{ fontSize: 12, color: '#6f8578', marginRight: 6 }}>Tổng thanh toán:</span>
                    <strong style={{ fontSize: 18, color: '#1e6546' }}>
                      {money(order.finalAmount)}
                    </strong>
                  </div>

                  <div style={{ display: 'flex', gap: 8 }}>
                    <button
                      type="button"
                      style={{ padding: '8px 14px', borderRadius: 8, fontSize: 12.5 }}
                      onClick={() => setSelectedOrder(order)}
                    >
                      Xem chi tiết
                    </button>

                    {order.orderStatus === 'Completed' && (
                      <button
                        type="button"
                        className="primary"
                        style={{ padding: '8px 14px', borderRadius: 8, fontSize: 12.5, background: '#1e6546' }}
                        onClick={() => setReviewModalOpen(true)}
                      >
                        ⭐ Đánh giá (UC-RC-05)
                      </button>
                    )}

                    <button
                      type="button"
                      style={{ padding: '8px 12px', borderRadius: 8, fontSize: 12, color: '#b23b3b', borderColor: '#ecd3cd' }}
                      onClick={() => setComplaintModalOpen(true)}
                    >
                      Khiếu nại (UC-RC-06)
                    </button>
                  </div>
                </div>
              </div>
            </div>
          ))
        )}
      </div>

      {/* 1. Modal Chi Tiết Đơn Hàng (UC-RC-04 / SCR-16) */}
      {selectedOrder && (
        <Modal
          open={Boolean(selectedOrder)}
          onClose={() => setSelectedOrder(null)}
          title={`Chi tiết đơn hàng #${selectedOrder.orderCode}`}
        >
          {/* Timeline vận chuyển */}
          <div style={{ background: '#f5faf7', borderRadius: 12, padding: '16px 20px', marginBottom: 20 }}>
            <h4 style={{ margin: '0 0 14px', fontSize: 13, color: '#1e6546' }}>HÀNH TRÌNH VẬN CHUYỂN</h4>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 10, textAlign: 'center' }}>
              <div>
                <span style={{ fontSize: 20 }}>📝</span>
                <div style={{ fontSize: 12, fontWeight: 700, color: '#1e6546', marginTop: 4 }}>Đã đặt hàng</div>
                <small style={{ color: '#7a8e82' }}>18/10 09:30</small>
              </div>
              <div>
                <span style={{ fontSize: 20 }}>🌾</span>
                <div style={{ fontSize: 12, fontWeight: 700, color: '#1e6546', marginTop: 4 }}>Nhà vườn đóng gói</div>
                <small style={{ color: '#7a8e82' }}>18/10 14:00</small>
              </div>
              <div>
                <span style={{ fontSize: 20 }}>🚚</span>
                <div style={{ fontSize: 12, fontWeight: 700, color: '#1976d2', marginTop: 4 }}>Đang giao hàng</div>
                <small style={{ color: '#7a8e82' }}>Hôm nay 08:00</small>
              </div>
              <div>
                <span style={{ fontSize: 20 }}>🏠</span>
                <div style={{ fontSize: 12, fontWeight: 600, color: '#97a89d', marginTop: 4 }}>Nhận hàng</div>
                <small style={{ color: '#97a89d' }}>Dự kiến hôm nay</small>
              </div>
            </div>
          </div>

          <div style={{ marginBottom: 16 }}>
            <h4 style={{ margin: '0 0 8px', fontSize: 13, color: '#2d4d3c' }}>ĐỊA CHỈ NHẬN HÀNG</h4>
            <div style={{ background: '#ffffff', border: '1px solid #e0eae2', borderRadius: 8, padding: 14 }}>
              <strong>{selectedOrder.recipientName}</strong> · {selectedOrder.recipientPhone}
              <p style={{ margin: '4px 0 0', color: '#557262', fontSize: 13 }}>
                {selectedOrder.shippingAddress}
              </p>
            </div>
          </div>

          <div>
            <h4 style={{ margin: '0 0 8px', fontSize: 13, color: '#2d4d3c' }}>THÔNG TIN THANH TOÁN</h4>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 13, padding: '6px 0' }}>
              <span className="muted">Hình thức thanh toán:</span>
              <strong>VNPay / Chuyển khoản QR (Đã thanh toán)</strong>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 14, padding: '8px 0', borderTop: '1px solid #e5ede7', marginTop: 8 }}>
              <span>Tổng số tiền:</span>
              <strong style={{ color: '#1e6546', fontSize: 18 }}>{money(selectedOrder.finalAmount)}</strong>
            </div>
          </div>

          <div className="modal-actions">
            <button type="button" onClick={() => setSelectedOrder(null)}>
              Đóng cửa sổ
            </button>
          </div>
        </Modal>
      )}

      {/* 2. Modal Xem Truy Xuất Nguồn Gốc Lô Thu Hoạch (SCR-TRC-01) */}
      {traceModalOpen && selectedBatch && (
        <Modal
          open={traceModalOpen}
          onClose={() => setTraceModalOpen(false)}
          title={`Truy xuất nguồn gốc lô: ${selectedBatch.harvestBatchCode}`}
        >
          <div style={{ textAlign: 'center', padding: '10px 0 16px' }}>
            <div style={{ fontSize: 40, marginBottom: 8 }}>{selectedBatch.imageEmoji}</div>
            <h3 style={{ margin: 0, color: '#1b4332' }}>{selectedBatch.productName}</h3>
            <span style={{ background: '#eaf4e9', color: '#1e6546', padding: '4px 12px', borderRadius: 12, fontSize: 12, fontWeight: 700, display: 'inline-block', marginTop: 6 }}>
              Mã QR Lô: {selectedBatch.harvestBatchCode}
            </span>
          </div>

          <div style={{ background: '#f5faf7', border: '1px solid #dcebe1', borderRadius: 10, padding: 16, marginBottom: 16 }}>
            <div style={{ display: 'grid', gridTemplateColumns: '130px 1fr', gap: 10, fontSize: 13 }}>
              <strong>Nông trại / HTX:</strong> <span>{selectedBatch.farmName}</span>
              <strong>Vị trí khu đất:</strong> <span>{selectedBatch.farmPlot}</span>
              <strong>Ngày thu hoạch:</strong> <span>{selectedBatch.harvestDate}</span>
              <strong>Tiêu chuẩn canh tác:</strong> <span style={{ color: '#1e6546', fontWeight: 700 }}>VietGAP · Không thuốc trừ sâu BVTV</span>
              <strong>Nhật ký tưới bón:</strong> <span>Đã được Verification Officer thẩm định (UC-VO-01)</span>
            </div>
          </div>

          <div className="modal-actions" style={{ justifyContent: 'space-between' }}>
            <span style={{ fontSize: 12, color: '#688273', alignSelf: 'center' }}>
              ✓ Thông tin chuỗi cung ứng minh bạch
            </span>
            <button
              type="button"
              className="primary"
              style={{ background: '#1e6546' }}
              onClick={() => {
                setTraceModalOpen(false);
                alert(`Chuyển đến trang toàn cảnh Tra cứu nguồn gốc chi tiết (SCR-TRC-01) cho mã lô ${selectedBatch.harvestBatchCode}`);
              }}
            >
              Xem Chi Tiết Đầy Đủ (SCR-TRC-01) →
            </button>
          </div>
        </Modal>
      )}

      {/* 3. Modal Đánh giá sản phẩm (UC-RC-05) */}
      {reviewModalOpen && (
        <Modal
          onClose={() => setReviewModalOpen(false)}
          title="Đánh giá sản phẩm nông sản (UC-RC-05)"
        >
          <p className="muted" style={{ fontSize: 13 }}>
            Đánh giá của bạn sẽ được gửi tới Content Moderator kiểm duyệt trước khi hiển thị công khai.
          </p>
          <div style={{ margin: '16px 0' }}>
            <label style={{ display: 'block', fontSize: 13, fontWeight: 700, marginBottom: 8 }}>Mức độ hài lòng:</label>
            <div style={{ fontSize: 24, cursor: 'pointer', color: '#ffb703' }}>
              ⭐⭐⭐⭐⭐ <span style={{ fontSize: 14, color: '#334e3d', marginLeft: 8 }}>(5 sao - Rất tươi ngon)</span>
            </div>
          </div>
          <Field label="Nội dung nhận xét chi tiết *">
            <textarea
              placeholder="Chia sẻ trải nghiệm về độ tươi, mùi vị, đóng gói của nông sản..."
              rows={4}
            />
          </Field>
          <div className="modal-actions">
            <button type="button" onClick={() => setReviewModalOpen(false)}>Hủy</button>
            <button
              type="submit"
              className="primary"
              style={{ background: '#1e6546' }}
              onClick={() => {
                setReviewModalOpen(false);
                setToastMessage('MSG48: Đánh giá của bạn đã được gửi thành công và đang chờ Content Moderator duyệt (UC-CM-02)!');
                setTimeout(() => setToastMessage(null), 4000);
              }}
            >
              Gửi đánh giá
            </button>
          </div>
        </Modal>
      )}

      {/* 4. Modal Khiếu nại (UC-RC-06) */}
      {complaintModalOpen && (
        <Modal
          onClose={() => setComplaintModalOpen(false)}
          title="Gửi yêu cầu khiếu nại đơn hàng (UC-RC-06)"
        >
          <p className="muted" style={{ fontSize: 13 }}>
            Khiếu nại sẽ được chuyển đến bộ phận Chăm sóc khách hàng (Customer Support - UC-CS-01) để xử lý trong 24h.
          </p>
          <Field label="Lý do khiếu nại *">
            <select defaultValue="damaged">
              <option value="damaged">Hàng hóa bị dập nát / hư hỏng do vận chuyển</option>
              <option value="missing">Thiếu số lượng so với đơn đặt</option>
              <option value="wrong_batch">Sản phẩm không đúng mã lô thu hoạch niêm yết</option>
              <option value="other">Lý do khác</option>
            </select>
          </Field>
          <Field label="Mô tả chi tiết và yêu cầu giải quyết *">
            <textarea
              placeholder="Mô tả hiện trạng và cung cấp hình ảnh chụp thực tế sản phẩm..."
              rows={4}
            />
          </Field>
          <div className="modal-actions">
            <button type="button" onClick={() => setComplaintModalOpen(false)}>Hủy</button>
            <button
              type="submit"
              className="primary"
              style={{ background: '#c2533c', borderColor: '#c2533c' }}
              onClick={() => {
                setComplaintModalOpen(false);
                setToastMessage('Yêu cầu khiếu nại đã được ghi nhận và gửi đến Customer Support (UC-CS-01).');
                setTimeout(() => setToastMessage(null), 4000);
              }}
            >
              Gửi khiếu nại CS
            </button>
          </div>
        </Modal>
      )}
    </div>
  );
}
