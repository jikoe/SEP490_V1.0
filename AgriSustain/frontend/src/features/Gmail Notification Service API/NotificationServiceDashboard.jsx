import React from 'react';

export default function NotificationServiceDashboard() {
  const metrics = [
    { label: 'Email đã gửi 24h', value: '418 email', sub: 'Đơn hàng, duyệt sản phẩm & thanh toán', icon: '✉️', color: 'blue' },
    { label: 'Tỷ lệ gửi thành công', value: '99,9%', sub: '0 thư bị trả về (bounce rate = 0%)', icon: '✓', color: 'green' },
    { label: 'Thông báo In-App', value: '620 tin', sub: 'Đã đẩy tức thời qua SignalR / Polling', icon: '🔔', color: 'gold' },
    { label: 'Trạng thái Gmail API', value: 'Connected', sub: 'OAuth 2.0 Token hợp lệ', icon: '⚙️', color: 'purple' },
  ];

  const recentNotifications = [
    { id: 'NOTIF-8821', type: 'Email + In-App', recipient: 'mai.nguyen@example.test', title: 'Sản phẩm “Cải ngọt hữu cơ” đã được duyệt', time: '10:40:12', status: 'Đã gửi' },
    { id: 'NOTIF-8820', type: 'Email', recipient: 'customer.duc@example.test', title: 'Xác nhận đơn hàng #ORD-98231', time: '10:35:10', status: 'Đã gửi' },
    { id: 'NOTIF-8819', type: 'In-App', recipient: 'hung.tran@example.test', title: 'Đợt chi trả doanh thu kỳ 1 đã được duyệt', time: '10:15:00', status: 'Đã gửi' },
    { id: 'NOTIF-8818', type: 'Email', recipient: 'verify.tam@example.test', title: 'Cảnh báo chứng chỉ VietGAP hết hạn sau 30 ngày', time: '08:00:22', status: 'Đã gửi' },
  ];

  return (
    <div className="notification-service-dashboard">
      <div className="section-heading">
        <div>
          <p className="eyebrow">DỊCH VỤ THÔNG BÁO TỰ ĐỘNG</p>
          <h1>Bảng Điều Khiển Gmail Notification Service API</h1>
          <p className="muted">Giám sát hàng đợi gửi email xác thực, biên nhận đơn hàng và các luồng thông báo đẩy thời gian thực.</p>
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
            <h2>Nhật ký gửi thông báo gần nhất</h2>
            <span className="badge Active">Queue: Trống (0 pending)</span>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Mã thông báo</th>
                  <th>Kênh phát</th>
                  <th>Người nhận</th>
                  <th>Tiêu đề thông báo</th>
                  <th>Thời gian</th>
                  <th>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {recentNotifications.map((n) => (
                  <tr key={n.id}>
                    <td><strong className="order-code">{n.id}</strong></td>
                    <td>{n.type}</td>
                    <td>{n.recipient}</td>
                    <td><strong>{n.title}</strong></td>
                    <td>{n.time}</td>
                    <td><span className="badge Active">{n.status}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="panel padded">
          <h2>Điều khiển dịch vụ thông báo</h2>
          <p className="muted" style={{ marginBottom: 16 }}>Công cụ quản trị hệ thống email:</p>

          <button className="quick-action-btn" type="button">
            <span>✉️</span>
            <div>
              <strong>Gửi Email kiểm tra kết nối</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Bắn email mẫu thử nghiệm qua Gmail API</small>
            </div>
          </button>

          <button className="quick-action-btn" type="button">
            <span>🔄</span>
            <div>
              <strong>Làm mới OAuth2 Refresh Token</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Gia hạn phiên làm việc Google Workspace API</small>
            </div>
          </button>

          <button className="quick-action-btn" type="button">
            <span>📑</span>
            <div>
              <strong>Mẫu Email HTML (Templates)</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Chỉnh sửa giao diện email thương hiệu Nông Sản Việt</small>
            </div>
          </button>
        </div>
      </div>
    </div>
  );
}
