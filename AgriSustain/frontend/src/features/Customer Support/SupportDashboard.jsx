import React from 'react';

export default function SupportDashboard() {
  const metrics = [
    { label: 'Phiếu khiếu nại đang mở', value: '5 phiếu', sub: '2 phiếu cần phản hồi gấp', icon: '🎫', color: 'gold' },
    { label: 'Tranh chấp chờ giải quyết', value: '2 vụ', sub: 'Chất lượng hàng & Giao trễ', icon: '⚖️', color: 'red' },
    { label: 'Đã xử lý hôm nay', value: '14 yêu cầu', sub: 'Tỷ lệ hài lòng 98%', icon: '✓', color: 'green' },
    { label: 'Thời gian xử lý TB', value: '24 phút', sub: 'Đạt chuẩn SLA hỗ trợ 1 giờ', icon: '⏱️', color: 'blue' },
  ];

  const recentTickets = [
    { id: 'DSP-2610-09', customer: 'Lê Hoàng Nam', orderCode: 'ORD-98231', reason: 'Dập nát khi vận chuyển (Cam sành)', status: 'Chờ bằng chứng ảnh', priority: 'Cao' },
    { id: 'DSP-2610-08', customer: 'Trần Thị Thu', orderCode: 'ORD-98190', reason: 'Sai quy cách đóng gói (Gạo ST25)', status: 'Đang thương lượng hoàn tiền', priority: 'Trung bình' },
    { id: 'DSP-2610-06', customer: 'Nguyễn Văn Đức', orderCode: 'ORD-98045', reason: 'Hỏi cách tra cứu mã QR lô thu hoạch', status: 'Đã giải quyết', priority: 'Thấp' },
  ];

  return (
    <div className="support-dashboard">
      <div className="section-heading">
        <div>
          <p className="eyebrow">CHĂM SÓC KHÁCH HÀNG & HỖ TRỢ</p>
          <h1>Trung Tâm Khiếu Nại & Tranh Chấp</h1>
          <p className="muted">Tiếp nhận yêu cầu từ người mua và nông dân, thu thập chứng cứ và điều phối phương án xử lý theo chính sách nền tảng.</p>
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
            <h2>Phiếu tranh chấp & khiếu nại gần đây</h2>
            <span className="badge Pending">Ưu tiên xử lý trong 24h</span>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Mã phiếu</th>
                  <th>Khách hàng</th>
                  <th>Mã đơn hàng</th>
                  <th>Vấn đề phản ánh</th>
                  <th>Mức độ</th>
                  <th>Trạng thái</th>
                  <th>Hành động</th>
                </tr>
              </thead>
              <tbody>
                {recentTickets.map((t) => (
                  <tr key={t.id}>
                    <td><strong className="order-code">{t.id}</strong></td>
                    <td>{t.customer}</td>
                    <td><span className="order-code">{t.orderCode}</span></td>
                    <td>{t.reason}</td>
                    <td>
                      <span className={`badge ${t.priority === 'Cao' ? 'Rejected' : 'neutral'}`}>
                        {t.priority}
                      </span>
                    </td>
                    <td><span className="badge Pending">{t.status}</span></td>
                    <td>
                      <button className="primary" style={{ padding: '5px 10px', fontSize: 11 }}>
                        Xử lý
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="panel padded">
          <h2>Kênh hỗ trợ nhanh</h2>
          <p className="muted" style={{ marginBottom: 16 }}>Thao tác giải quyết sự vụ:</p>

          <button className="quick-action-btn" type="button">
            <span>📷</span>
            <div>
              <strong>Yêu cầu người mua bổ sung ảnh</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Gửi thông báo In-App yêu cầu upload hình ảnh lỗi</small>
            </div>
          </button>

          <button className="quick-action-btn" type="button">
            <span>🔄</span>
            <div>
              <strong>Đề xuất lệnh hoàn tiền</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Chuyển sang bộ phận Tài chính thực hiện bồi hoàn</small>
            </div>
          </button>

          <button className="quick-action-btn" type="button">
            <span>📞</span>
            <div>
              <strong>Liên hệ hợp tác xã / Nông dân</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Xác minh quy trình đóng gói và bảo quản</small>
            </div>
          </button>
        </div>
      </div>
    </div>
  );
}
