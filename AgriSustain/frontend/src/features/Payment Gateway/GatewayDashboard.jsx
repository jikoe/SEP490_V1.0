import React from 'react';

export default function GatewayDashboard() {
  const metrics = [
    { label: 'Tỷ lệ thành công', value: '99,85%', sub: 'Không có giao dịch treo quá 5 phút', icon: '⚡', color: 'green' },
    { label: 'Giao dịch hôm nay', value: '186 lượt', sub: 'Tổng giá trị 45.200.000 ₫', icon: '💳', color: 'blue' },
    { label: 'Webhook PayOS/VNPay', value: '542 events', sub: 'Xử lý bất đồng bộ thành công 100%', icon: '📡', color: 'gold' },
    { label: 'Thời gian phản hồi API', value: '142 ms', sub: 'Kết nối mạng ổn định', icon: '⏱️', color: 'purple' },
  ];

  const recentTransactions = [
    { id: 'TXN-98231-POS', order: 'ORD-98231', method: 'PayOS QR', amount: '214.500 ₫', time: '10:42:15', webhook: 'Đã xác nhận', status: 'Thành công' },
    { id: 'TXN-98190-VNP', order: 'ORD-98190', method: 'VNPay QR', amount: '185.000 ₫', time: '10:35:02', webhook: 'Đã xác nhận', status: 'Thành công' },
    { id: 'TXN-98144-POS', order: 'ORD-98144', method: 'PayOS QR', amount: '320.000 ₫', time: '10:18:49', webhook: 'Đã xác nhận', status: 'Thành công' },
    { id: 'TXN-98102-COD', order: 'ORD-98102', method: 'Thanh toán khi nhận (COD)', amount: '145.000 ₫', time: '09:55:20', webhook: 'Không áp dụng', status: 'Chờ giao' },
  ];

  return (
    <div className="gateway-dashboard">
      <div className="section-heading">
        <div>
          <p className="eyebrow">HẠ TẦNG KẾT NỐI THANH TOÁN</p>
          <h1>Bảng Giám Sát Cổng Thanh Toán</h1>
          <p className="muted">Theo dõi thời gian thực các luồng giao dịch điện tử, sự kiện Webhook IPN từ PayOS / VNPay và trạng thái khớp lệnh đơn hàng.</p>
        </div>
      </div>

      <div className="dashboard-grid">
        {metrics.map((item, idx) => (
          <div key={idx} className="metric-card">
            <div className={`metric-icon ${item.color}`}>{item.icon}</div>
            <div className="metric-info">
              <p className="metric-label">{item.label}</p>
              <h3 className="metric-value">{item.value}</h3>
              <p className="metric-sub">{item.sub}</p>
            </div>
          </div>
        ))}
      </div>

      <div className="dashboard-sections">
        <div className="panel padded">
          <div className="record-heading" style={{ padding: '0 0 16px' }}>
            <h2>Giao dịch cổng thanh toán thời gian thực</h2>
            <span className="badge Active">Webhook IPN: Online</span>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Mã giao dịch</th>
                  <th>Mã đơn hàng</th>
                  <th>Phương thức</th>
                  <th>Số tiền</th>
                  <th>Thời gian</th>
                  <th>Trạng thái Webhook</th>
                  <th>Kết quả</th>
                </tr>
              </thead>
              <tbody>
                {recentTransactions.map((t) => (
                  <tr key={t.id}>
                    <td><strong className="order-code">{t.id}</strong></td>
                    <td><span className="order-code">{t.order}</span></td>
                    <td>{t.method}</td>
                    <td><strong style={{ color: '#187447' }}>{t.amount}</strong></td>
                    <td>{t.time}</td>
                    <td><span className="badge Active">{t.webhook}</span></td>
                    <td><span className="badge Active">{t.status}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="panel padded">
          <h2>Kiểm tra kết nối cổng</h2>
          <p className="muted" style={{ marginBottom: 16 }}>Công cụ chẩn đoán & cấu hình:</p>

          <button className="quick-action-btn" type="button">
            <span>⚡</span>
            <div>
              <strong>Gửi Ping thử nghiệm Webhook</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Kiểm tra chữ ký số HMAC-SHA256 endpoint</small>
            </div>
          </button>

          <button className="quick-action-btn" type="button">
            <span>🔄</span>
            <div>
              <strong>Khớp lệnh lại giao dịch treo</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Chủ động gọi Query Payment Status sang PayOS</small>
            </div>
          </button>

          <button className="quick-action-btn" type="button">
            <span>🛡️</span>
            <div>
              <strong>Xoay vòng Khóa Bí Mật (API Secret)</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Đổi Secret Key kết nối định kỳ 90 ngày</small>
            </div>
          </button>
        </div>
      </div>
    </div>
  );
}
