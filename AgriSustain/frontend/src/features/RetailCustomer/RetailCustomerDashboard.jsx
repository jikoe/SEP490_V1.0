import React from 'react';

export default function RetailCustomerDashboard({ navigate }) {
  const categories = [
    { name: 'Rau củ hữu cơ', count: '42 sản phẩm', icon: '🥬', color: '#e8f5e9' },
    { name: 'Trái cây nhiệt đới', count: '28 sản phẩm', icon: '🍊', color: '#fff3e0' },
    { name: 'Gạo & Ngũ cốc', count: '16 sản phẩm', icon: '🌾', color: '#f3e5f5' },
    { name: 'Nấm & Thảo mộc', count: '12 sản phẩm', icon: '🍄', color: '#e0f2f1' },
  ];

  const featuredProducts = [
    { id: 1, name: 'Cải ngọt hữu cơ Đà Lạt', farm: 'Nông trại Xanh Đà Lạt', price: '32.000 ₫/kg', cert: 'VietGAP', icon: '🥬' },
    { id: 2, name: 'Cam sành Vĩnh Long mọng nước', farm: 'Vườn cam Tân Phú', price: '38.000 ₫/kg', cert: 'Hữu cơ', icon: '🍊' },
    { id: 3, name: 'Gạo ST25 mùa mới túi 5kg', farm: 'HTX Lúa thơm Sóc Trăng', price: '160.000 ₫/túi', cert: 'OCOP 5 sao', icon: '🌾' },
    { id: 4, name: 'Cà chua Nova giống Israel', farm: 'Trang trại Nắng Sớm', price: '45.000 ₫/kg', cert: 'GlobalGAP', icon: '🍅' },
  ];

  return (
    <div className="retail-dashboard" style={{ maxWidth: 1200, margin: '0 auto' }}>
      <div className="section-heading" style={{ background: 'linear-gradient(135deg, #165639, #247c54)', padding: '36px 40px', borderRadius: 16, color: 'white', marginBottom: 28 }}>
        <div style={{ maxWidth: 680 }}>
          <p style={{ color: '#a2dcba', fontSize: 11, fontWeight: 700, letterSpacing: 1.5, margin: '0 0 8px' }}>SÀN NÔNG SẢN VIỆT TRỰC TUYẾN</p>
          <h1 style={{ color: 'white', fontSize: 32, marginBottom: 12 }}>Nông Sản Tươi Lành Từ Tận Nguồn</h1>
          <p style={{ color: '#d5ede0', fontSize: 14, margin: '0 0 20px', lineHeight: 1.6 }}>
            Kết nối trực tiếp tới các hợp tác xã nông nghiệp sạch. 100% minh bạch nguồn gốc với mã QR xác thực đến từng ngày gieo cấy và thu hoạch.
          </p>
          <div style={{ display: 'flex', gap: 12 }}>
            <button
              className="primary"
              style={{ background: '#fff', color: '#165639', borderColor: '#fff', fontWeight: 650, padding: '12px 22px' }}
              onClick={() => document.getElementById('retail-featured-products')?.scrollIntoView({ behavior: 'smooth', block: 'start' })}
            >
              🛒 Khám phá nông sản
            </button>
          </div>
        </div>
      </div>

      <div className="dashboard-grid">
        <div className="metric-card">
          <div className="metric-icon gold">🎁</div>
          <div className="metric-info">
            <p className="metric-label">Điểm thưởng tích luỹ</p>
            <h3 className="metric-value">1.250 điểm</h3>
            <p className="metric-sub">Giảm tối đa 50.000 ₫ cho đơn kế</p>
          </div>
        </div>

        <div className="metric-card">
          <div className="metric-icon green">🎟️</div>
          <div className="metric-info">
            <p className="metric-label">Voucher khả dụng</p>
            <h3 className="metric-value">3 mã ưu đãi</h3>
            <p className="metric-sub">WELCOME10 · FREESHIP · VEG20</p>
          </div>
        </div>

        <div className="metric-card">
          <div className="metric-icon blue">📦</div>
          <div className="metric-info">
            <p className="metric-label">Đơn hàng đã đặt</p>
            <h3 className="metric-value">4 đơn hoàn tất</h3>
            <p className="metric-sub">Tất cả đều đạt chuẩn tươi ngon</p>
          </div>
        </div>

        <div className="metric-card">
          <div className="metric-icon purple">🛡️</div>
          <div className="metric-info">
            <p className="metric-label">Cam kết chất lượng</p>
            <h3 className="metric-value">Đổi trả 24h</h3>
            <p className="metric-sub">Nếu nông sản không tươi chuẩn</p>
          </div>
        </div>
      </div>

      <div style={{ marginBottom: 28 }}>
        <h2 style={{ fontSize: 18, marginBottom: 16 }}>Danh mục mùa vụ tuyển chọn</h2>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 16 }}>
          {categories.map((c, i) => (
            <div key={i} className="panel padded" style={{ display: 'flex', alignItems: 'center', gap: 14, }}>
              <div style={{ fontSize: 32, width: 50, height: 50, borderRadius: 10, background: c.color, display: 'grid', placeItems: 'center' }}>
                {c.icon}
              </div>
              <div>
                <strong style={{ fontSize: 14, color: '#1a3727' }}>{c.name}</strong>
                <small style={{ color: '#6d8074', display: 'block' }}>{c.count}</small>
              </div>
            </div>
          ))}
        </div>
      </div>

      <div id="retail-featured-products" className="panel padded">
        <div className="record-heading" style={{ padding: '0 0 16px' }}>
          <h2>Sản phẩm tươi mới hôm nay</h2>
          <button className="text-button" onClick={() => navigate('/cart')}>
            Xem giỏ hàng →
          </button>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Sản phẩm</th>
                <th>Nhà vườn / Nông trại</th>
                <th>Giá bán lẻ niêm yết</th>
                <th>Chứng chỉ chất lượng</th>
                <th>Hành động</th>
              </tr>
            </thead>
            <tbody>
              {featuredProducts.map((p) => (
                <tr key={p.id}>
                  <td>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                      <span style={{ fontSize: 24 }}>{p.icon}</span>
                      <strong>{p.name}</strong>
                    </div>
                  </td>
                  <td>{p.farm}</td>
                  <td><strong style={{ color: '#166e43' }}>{p.price}</strong></td>
                  <td><span className="badge Active">✓ {p.cert}</span></td>
                  <td>
                    <button className="primary" style={{ padding: '6px 14px', fontSize: 11 }} onClick={() => navigate('/cart')}>
                      Đến giỏ hàng
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

