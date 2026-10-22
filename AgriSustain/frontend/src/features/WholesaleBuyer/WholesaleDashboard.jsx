import React from 'react';

export default function WholesaleDashboard() {
  const metrics = [
    { label: 'Hợp đồng B2B hiệu lực', value: '4 hợp đồng', sub: 'Hợp tác xã Lâm Đồng & Vĩnh Long', icon: '📜', color: 'blue' },
    { label: 'Đơn hàng sỉ đang giao', value: '2 đơn', sub: 'Tổng 1.200 kg nông sản tươi', icon: '🚛', color: 'green' },
    { label: 'Hạn mức giao dịch', value: '180.000.000 ₫', sub: 'Đã bảo lãnh thanh toán qua ngân hàng', icon: '💳', color: 'gold' },
    { label: 'Chiết khấu bao tiêu', value: '14,5%', sub: 'Theo cam kết kỳ hạn vụ mùa', icon: '🏷️', color: 'purple' },
  ];

  const contracts = [
    { code: 'B2B-2026-DL01', partner: 'HTX Rau Sạch Đà Lạt', item: 'Rau củ hữu cơ tổng hợp', volume: '2.5 tấn/tháng', validUntil: '31/12/2026', status: 'Đang thực hiện' },
    { code: 'B2B-2026-TG04', partner: 'Vườn Cam Tân Phú (Vĩnh Long)', item: 'Cam sành loại 1 xuất khẩu', volume: '5.0 tấn/vụ', validUntil: '15/11/2026', status: 'Đang giao nhận' },
    { code: 'B2B-2026-ST02', partner: 'HTX Lúa Thơm Sóc Trăng', item: 'Gạo ST25 chuẩn hữu cơ', volume: '10.0 tấn/vụ', validUntil: '28/02/2027', status: 'Đã ký kết' },
  ];

  return (
    <div className="wholesale-dashboard">
      <div className="section-heading">
        <div>
          <p className="eyebrow">KHÁCH HÀNG DOANH NGHIỆP B2B</p>
          <h1>Bảng Điều Khiển Mua Buôn</h1>
          <p className="muted">Theo dõi tiến độ hợp đồng thu mua bao tiêu, đặt hàng trước theo mùa vụ và đối soát hóa đơn sỉ.</p>
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
            <h2>Hợp đồng thu mua nông sản theo vụ</h2>
            <span className="badge Active">Ký số điện tử</span>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Mã hợp đồng</th>
                  <th>Đối tác cung ứng</th>
                  <th>Nông sản</th>
                  <th>Khối lượng cam kết</th>
                  <th>Hạn hợp đồng</th>
                  <th>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {contracts.map((c) => (
                  <tr key={c.code}>
                    <td><strong className="order-code">{c.code}</strong></td>
                    <td>{c.partner}</td>
                    <td>{c.item}</td>
                    <td>{c.volume}</td>
                    <td>{c.validUntil}</td>
                    <td><span className="badge Active">{c.status}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="panel padded">
          <h2>Tác vụ mua sỉ</h2>
          <p className="muted" style={{ marginBottom: 16 }}>Khám phá cơ hội hợp tác cung ứng:</p>

          <button className="quick-action-btn" type="button">
            <span>🔍</span>
            <div>
              <strong>Tìm kiếm nguồn hàng bao tiêu</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Xem danh sách nông trại đạt chuẩn VietGAP</small>
            </div>
          </button>

          <button className="quick-action-btn" type="button">
            <span>📝</span>
            <div>
              <strong>Tạo yêu cầu hợp đồng mới</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Đàm phán số lượng, giá trần và lịch giao nhận</small>
            </div>
          </button>

          <button className="quick-action-btn" type="button">
            <span>📊</span>
            <div>
              <strong>Biểu đồ giá thị trường</strong>
              <small style={{ display: 'block', color: '#687b70' }}>So sánh với giá sàn vùng miền cập nhật liên tục</small>
            </div>
          </button>
        </div>
      </div>
    </div>
  );
}
