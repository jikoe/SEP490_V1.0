import React from 'react';

export default function PromotionsDashboard() {
  const metrics = [
    { label: 'Voucher đang hoạt động', value: '6 mã', sub: 'Giảm giá trực tiếp & Miễn phí vận chuyển', icon: '🎟️', color: 'gold' },
    { label: 'Lượt đổi voucher tuần', value: '342 lượt', sub: 'Tăng 18% so với tuần trước', icon: '📈', color: 'green' },
    { label: 'Điểm thưởng đã phát hành', value: '28.500 pts', sub: 'Tỷ lệ tiêu dùng điểm đạt 78%', icon: '🎁', color: 'purple' },
    { label: 'Chiến dịch Pre-Order', value: '2 chiến dịch', sub: 'Cam sành Vụ Đông & Dâu tây Đà Lạt', icon: '📢', color: 'blue' },
  ];

  const activeVouchers = [
    { code: 'WELCOME10', type: 'Giảm % đơn đầu', value: '10% (tối đa 30.000 ₫)', minOrder: '150.000 ₫', used: '120/500', status: 'Đang chạy' },
    { code: 'FREESHIP', type: 'Hỗ trợ cước vận chuyển', value: '25.000 ₫', minOrder: '200.000 ₫', used: '215/300', status: 'Đang chạy' },
    { code: 'VEG20', type: 'Giảm giá rau củ hữu cơ', value: '20.000 ₫', minOrder: '180.000 ₫', used: '64/200', status: 'Đang chạy' },
    { code: 'B2BPREORDER', type: 'Chiết khấu đặt sỉ sớm', value: '5%', minOrder: '5.000.000 ₫', used: '12/50', status: 'Sắp kết thúc' },
  ];

  return (
    <div className="promotions-dashboard">
      <div className="section-heading">
        <div>
          <p className="eyebrow">MARKETING & KHUYẾN MẠI NÔNG SẢN</p>
          <h1>Bảng Quản Trị Chiến Dịch Khuyến Mại</h1>
          <p className="muted">Cấu hình mã giảm giá, chương trình khách hàng thân thiết, điểm thưởng tích lũy và chiến dịch bao tiêu nông sản đặt trước.</p>
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
            <h2>Chương trình Voucher đang áp dụng</h2>
            <button className="primary" style={{ padding: '6px 12px', fontSize: 11 }}>
              + Thêm Voucher Mới
            </button>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Mã Voucher</th>
                  <th>Hình thức ưu đãi</th>
                  <th>Giá trị giảm</th>
                  <th>Đơn tối thiểu</th>
                  <th>Lượt sử dụng</th>
                  <th>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {activeVouchers.map((v) => (
                  <tr key={v.code}>
                    <td><strong className="order-code">{v.code}</strong></td>
                    <td>{v.type}</td>
                    <td><strong style={{ color: '#1a7547' }}>{v.value}</strong></td>
                    <td>{v.minOrder}</td>
                    <td>{v.used}</td>
                    <td><span className="badge Active">{v.status}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="panel padded">
          <h2>Tác vụ khuyến mại</h2>
          <p className="muted" style={{ marginBottom: 16 }}>Công cụ tăng trưởng doanh số:</p>

          <button className="quick-action-btn" type="button">
            <span>🎟️</span>
            <div>
              <strong>Tạo mã Voucher theo ngành hàng</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Chỉ áp dụng cho Rau củ sạch hoặc Trái cây</small>
            </div>
          </button>

          <button className="quick-action-btn" type="button">
            <span>🎁</span>
            <div>
              <strong>Quy đổi điểm thưởng (Reward Points)</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Tặng điểm khi khách mua lại hoặc đánh giá 5 sao</small>
            </div>
          </button>

          <button className="quick-action-btn" type="button">
            <span>🌾</span>
            <div>
              <strong>Kích hoạt chiến dịch Pre-Order vụ mùa</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Huy động đặt cọc trước ngày thu hoạch 2 tuần</small>
            </div>
          </button>
        </div>
      </div>
    </div>
  );
}
