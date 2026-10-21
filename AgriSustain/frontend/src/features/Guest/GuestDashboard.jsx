import React from 'react';

export default function GuestDashboard({ navigate }) {
  const highlights = [
    { title: 'Minh bạch nguồn gốc', desc: 'Mọi lô nông sản đều có mã QR truy xuất đến tận ngày gieo cấy, chăm bón và kiểm định.', icon: '🔍', color: 'green' },
    { title: 'Bao tiêu sỉ B2B', desc: 'Kết nối doanh nghiệp chế biến, siêu thị trực tiếp với các hợp tác xã nông nghiệp sạch.', icon: '📜', color: 'blue' },
    { title: 'Thanh toán bảo chứng', desc: 'Hệ thống giữ tiền ký quỹ và chỉ giải ngân cho nông dân khi đơn hàng được giao tươi chuẩn.', icon: '🛡️', color: 'gold' },
    { title: 'Chứng chỉ chuẩn quốc gia', desc: 'Hồ sơ kiểm duyệt kỹ lưỡng: VietGAP, GlobalGAP, OCOP 4-5 sao và tiêu chuẩn Hữu cơ.', icon: '✓', color: 'purple' },
  ];

  return (
    <div className="guest-dashboard">
      <div className="section-heading" style={{ background: 'linear-gradient(135deg, #144630, #1f6b47)', padding: '36px 40px', borderRadius: 16, color: 'white', marginBottom: 28 }}>
        <div style={{ maxWidth: 720 }}>
          <p style={{ color: '#97dcb2', fontSize: 11, fontWeight: 700, letterSpacing: 1.5, margin: '0 0 8px' }}>CHÀO MỪNG ĐẾN VỚI AGRISUSTAIN</p>
          <h1 style={{ color: 'white', fontSize: 32, marginBottom: 12 }}>Nền Tảng Nông Sản Việt Bền Vững</h1>
          <p style={{ color: '#d5ede0', fontSize: 14, margin: '0 0 22px', lineHeight: 1.6 }}>
            Hệ sinh thái số hóa nông nghiệp kết nối đa vai trò: Nông dân, Doanh nghiệp thu mua B2B, Người tiêu dùng và Kiểm duyệt viên chất lượng.
          </p>
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            <button
              className="primary"
              style={{ background: '#fff', color: '#165639', borderColor: '#fff', fontWeight: 650, padding: '11px 20px' }}
              onClick={() => navigate('/checkout')}
            >
              🛒 Xem thử giỏ hàng & sản phẩm
            </button>
            <button
              style={{ background: 'transparent', color: '#fff', borderColor: '#97dcb2', fontWeight: 600, padding: '11px 20px' }}
              onClick={() => navigate('/login')}
            >
              🔑 Đăng nhập tài khoản chính thức
            </button>
          </div>
        </div>
      </div>

      <div className="dashboard-grid">
        {highlights.map((h, i) => (
          <div key={i} className="metric-card">
            <div className={`metric-icon ${h.color}`}>{h.icon}</div>
            <div className="metric-info">
              <p className="metric-label">{h.title}</p>
              <p className="metric-sub" style={{ fontSize: 12, color: '#445b4e', marginTop: 2 }}>{h.desc}</p>
            </div>
          </div>
        ))}
      </div>

      <div className="dashboard-sections">
        <div className="panel padded">
          <h2>Các vai trò trên nền tảng AgriSustain</h2>
          <p className="muted" style={{ marginBottom: 18 }}>Mỗi đối tượng tham gia đều có không gian làm việc và công cụ số hóa chuyên biệt:</p>

          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: 14 }}>
            <div style={{ border: '1px solid #e1e9e3', padding: 16, borderRadius: 10, background: '#fafcfa' }}>
              <div style={{ fontSize: 24, marginBottom: 6 }}>🌾</div>
              <strong>Nông dân & Hợp tác xã</strong>
              <small style={{ color: '#6a7d71', display: 'block', marginTop: 4 }}>Nhật ký mùa vụ canh tác, in tem mã QR truy xuất và nhận tiền chi trả tự động.</small>
            </div>

            <div style={{ border: '1px solid #e1e9e3', padding: 16, borderRadius: 10, background: '#fafcfa' }}>
              <div style={{ fontSize: 24, marginBottom: 6 }}>🏬</div>
              <strong>Khách mua sỉ B2B</strong>
              <small style={{ color: '#6a7d71', display: 'block', marginTop: 4 }}>Ký hợp đồng thu mua kỳ hạn, theo dõi tiến độ giao hàng số lượng lớn.</small>
            </div>

            <div style={{ border: '1px solid #e1e9e3', padding: 16, borderRadius: 10, background: '#fafcfa' }}>
              <div style={{ fontSize: 24, marginBottom: 6 }}>✓</div>
              <strong>Kiểm duyệt & Thẩm định</strong>
              <small style={{ color: '#6a7d71', display: 'block', marginTop: 4 }}>Soát xét giấy phép VietGAP, kiểm định chất lượng hình ảnh và vùng trồng.</small>
            </div>

            <div style={{ border: '1px solid #e1e9e3', padding: 16, borderRadius: 10, background: '#fafcfa' }}>
              <div style={{ fontSize: 24, marginBottom: 6 }}>🛒</div>
              <strong>Người tiêu dùng lẻ</strong>
              <small style={{ color: '#6a7d71', display: 'block', marginTop: 4 }}>Mua nông sản tươi hái tận vườn, áp dụng voucher và giao hàng nhanh.</small>
            </div>
          </div>
        </div>

        <div className="panel padded">
          <h2>Bắt đầu với AgriSustain</h2>
          <p className="muted" style={{ marginBottom: 16 }}>Đăng nhập để vào bảng điều khiển vai trò của bạn:</p>

          <button className="quick-action-btn" onClick={() => navigate('/login')}>
            <span>🔑</span>
            <div>
              <strong>Đăng nhập tài khoản</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Sử dụng tài khoản được cấp bởi ban quản trị</small>
            </div>
          </button>

          <button className="quick-action-btn" onClick={() => navigate('/checkout')}>
            <span>▣</span>
            <div>
              <strong>Thử nghiệm mua lẻ</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Trải nghiệm đặt hàng và áp dụng mã giảm giá</small>
            </div>
          </button>
        </div>
      </div>
    </div>
  );
}
