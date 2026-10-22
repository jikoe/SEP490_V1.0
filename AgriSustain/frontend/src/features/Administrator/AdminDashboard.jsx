import React from 'react';

export default function AdminDashboard({ navigate }) {
  const metrics = [
    { label: 'Tổng người dùng', value: '24', sub: '8 vai trò đang hoạt động', icon: '👥', color: 'green' },
    { label: 'Danh mục sản phẩm', value: '18', sub: 'Phân loại nông sản chuẩn', icon: '▦', color: 'blue' },
    { label: 'Sản phẩm niêm yết', value: '152', sub: '98 đã duyệt, 12 chờ duyệt', icon: '🌱', color: 'gold' },
    { label: 'Hệ thống & CSDL', value: 'Sẵn sàng', sub: 'SQL Server · EF Core', icon: '⚡', color: 'purple' },
  ];

  const recentUsers = [
    { id: 1, name: 'Quản Trị Viên Hệ Thống', username: 'admin', role: 'Administrator', status: 'Active' },
    { id: 2, name: 'Kiểm Duyệt Viên Nội Dung', username: 'content_moderator', role: 'Content Moderator', status: 'Active' },
    { id: 3, name: 'Hợp Tác Xã Rau Sạch Đà Lạt', username: 'farmer1', role: 'Farmer', status: 'Active' },
    { id: 4, name: 'Chuỗi Siêu Thị VinMart', username: 'buyer_wholesale', role: 'WholesaleBuyer', status: 'Active' },
    { id: 5, name: 'Chuyên Viên Thẩm Định Vùng', username: 'verification_officer', role: 'Verification Officer', status: 'Active' },
  ];

  return (
    <div className="admin-dashboard">
      <div className="section-heading">
        <div>
          <p className="eyebrow">QUẢN TRỊ TỔNG QUAN</p>
          <h1>Bảng Điều Khiển Hệ Thống</h1>
          <p className="muted">Theo dõi toàn diện tài khoản người dùng, danh mục nông sản và trạng thái vận hành sàn.</p>
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
            <h2>Người dùng hệ thống gần đây</h2>
            <button className="text-button" onClick={() => navigate('/administration/users')}>
              Xem tất cả →
            </button>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Họ và tên</th>
                  <th>Tài khoản</th>
                  <th>Vai trò</th>
                  <th>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {recentUsers.map((u) => (
                  <tr key={u.id}>
                    <td><strong>{u.name}</strong></td>
                    <td>{u.username}</td>
                    <td><span className="badge role-badge">{u.role}</span></td>
                    <td><span className="badge Active">Hoạt động</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="panel padded">
          <h2>Tác vụ quản trị nhanh</h2>
          <p className="muted" style={{ marginBottom: 16 }}>Truy cập nhanh các khu vực nghiệp vụ quản trị:</p>

          <button className="quick-action-btn" onClick={() => navigate('/administration/users')}>
            <span>♙</span>
            <div>
              <strong>Quản lý người dùng</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Phân quyền, kích hoạt và khóa tài khoản</small>
            </div>
          </button>

          <button className="quick-action-btn" onClick={() => navigate('/administration/categories')}>
            <span>▦</span>
            <div>
              <strong>Danh mục nông sản</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Quản lý cây trồng, vật nuôi, mức hoa hồng</small>
            </div>
          </button>

          <button className="quick-action-btn" onClick={() => navigate('/moderation/products')}>
            <span>✓</span>
            <div>
              <strong>Sản phẩm chờ kiểm duyệt</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Xem xét quy cách, nguồn gốc và xuất xứ</small>
            </div>
          </button>
        </div>
      </div>
    </div>
  );
}
