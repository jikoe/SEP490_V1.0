import React from 'react';

export default function FarmerDashboard() {
  const metrics = [
    { label: 'Diện tích canh tác', value: '3,8 ha', sub: 'Đà Lạt & Lâm Hà', icon: '🌾', color: 'green' },
    { label: 'Mùa vụ đang diễn ra', value: '2 vụ', sub: 'Vụ thu đông & Vụ rau thơm', icon: '🌱', color: 'blue' },
    { label: 'Lô hàng đã thu hoạch', value: '14 lô', sub: 'Đã tạo mã QR truy xuất', icon: '📦', color: 'gold' },
    { label: 'Số dư chờ chi trả', value: '38.450.000 ₫', sub: 'Kỳ thanh toán 25 hàng tháng', icon: '💰', color: 'purple' },
  ];

  const harvestBatches = [
    { code: 'DL-VEG-261018-A', crop: 'Rau củ hữu cơ Đà Lạt', season: 'Vụ thu đông 2026', yieldKg: '120 kg', date: '18/10/2026', qr: 'QR-VERIFIED', status: 'Sẵn sàng bán' },
    { code: 'DL-TOM-261012-B', crop: 'Cà chua Nova hữu cơ', season: 'Vụ thu đông 2026', yieldKg: '250 kg', date: '12/10/2026', qr: 'QR-VERIFIED', status: 'Đang giao sỉ' },
    { code: 'DL-STR-261005-C', crop: 'Dâu tây giống Nhật', season: 'Vụ dâu 2026', yieldKg: '80 kg', date: '05/10/2026', qr: 'QR-VERIFIED', status: 'Đã hoàn tất' },
  ];

  return (
    <div className="farmer-dashboard">
      <div className="section-heading">
        <div>
          <p className="eyebrow">TRANG TRẠI & NÔNG HỘ</p>
          <h1>Bảng Điều Khiển Nông Dân</h1>
          <p className="muted">Quản lý mùa vụ canh tác, xuất bản lô thu hoạch với mã QR truy xuất và theo dõi số dư chi trả.</p>
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
            <h2>Lô thu hoạch gần đây & Truy xuất nguồn gốc</h2>
            <span className="badge Active">VietGAP & Hữu cơ</span>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Mã lô (Batch Code)</th>
                  <th>Cây trồng</th>
                  <th>Mùa vụ</th>
                  <th>Sản lượng</th>
                  <th>Ngày thu hoạch</th>
                  <th>Truy xuất QR</th>
                  <th>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {harvestBatches.map((b) => (
                  <tr key={b.code}>
                    <td><strong className="order-code">{b.code}</strong></td>
                    <td>{b.crop}</td>
                    <td>{b.season}</td>
                    <td>{b.yieldKg}</td>
                    <td>{b.date}</td>
                    <td><span className="badge Active">✓ {b.qr}</span></td>
                    <td><span className="badge Active">{b.status}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="panel padded">
          <h2>Thao tác mùa vụ</h2>
          <p className="muted" style={{ marginBottom: 16 }}>Ghi nhận hoạt động canh tác nhanh:</p>

          <button className="quick-action-btn" type="button">
            <span>🌱</span>
            <div>
              <strong>Khai báo mùa vụ mới</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Đăng ký giống cây, diện tích canh tác</small>
            </div>
          </button>

          <button className="quick-action-btn" type="button">
            <span>🚜</span>
            <div>
              <strong>Nhập lô thu hoạch</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Tự động sinh mã QR truy xuất nguồn gốc</small>
            </div>
          </button>

          <button className="quick-action-btn" type="button">
            <span>🏷️</span>
            <div>
              <strong>Đăng ký bán buôn B2B</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Tham gia hợp đồng thu mua bao tiêu</small>
            </div>
          </button>
        </div>
      </div>
    </div>
  );
}

