import React from 'react';

export default function VerificationDashboard() {
  const metrics = [
    { label: 'Chứng chỉ chờ duyệt', value: '7 hồ sơ', sub: 'VietGAP & Hữu cơ nông nghiệp', icon: '📑', color: 'gold' },
    { label: 'Hồ sơ nông trại mới', value: '3 nông trại', sub: 'Lâm Đồng, Đắk Lắk, Đồng Tháp', icon: '🏡', color: 'blue' },
    { label: 'Đã xác thực tháng này', value: '18 chứng chỉ', sub: 'Tỷ lệ đạt chuẩn 94,5%', icon: '✓', color: 'green' },
    { label: 'Khảo sát thực địa', value: '2 lịch hẹn', sub: 'Trong 48 giờ tới', icon: '📍', color: 'purple' },
  ];

  const pendingCertificates = [
    { id: 101, farm: 'Nông trại Xanh Măng Đen', type: 'Chứng nhận Hữu cơ (Organic)', issuer: 'Control Union', date: '20/10/2026', expiry: '20/10/2028', status: 'Chờ đối chiếu' },
    { id: 102, farm: 'Vườn Bưởi Da Xanh Bến Tre', type: 'VietGAP Trồng trọt', issuer: 'Trung tâm Chất lượng NLTS', date: '18/10/2026', expiry: '18/10/2029', status: 'Đang thẩm định' },
    { id: 103, farm: 'HTX Nấm Đông Trùng Tam Đảo', type: 'ISO 22000:2018', issuer: 'Quacert', date: '15/10/2026', expiry: '15/10/2029', status: 'Chờ xác nhận' },
  ];

  return (
    <div className="verification-dashboard">
      <div className="section-heading">
        <div>
          <p className="eyebrow">CHUYÊN VIÊN THẨM ĐỊNH CHẤT LƯỢNG</p>
          <h1>Bảng Thẩm Định & Xác Minh Nông Trại</h1>
          <p className="muted">Kiểm tra tính hợp lệ của chứng nhận VietGAP, GlobalGAP, Organic và xác minh pháp lý của nông hộ trước khi niêm yết.</p>
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
            <h2>Hồ sơ chứng chỉ đang chờ xử lý</h2>
            <span className="badge Pending">7 hồ sơ tồn đọng</span>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Nông trại đề nghị</th>
                  <th>Loại chứng nhận</th>
                  <th>Cơ quan cấp</th>
                  <th>Ngày nộp</th>
                  <th>Thời hạn</th>
                  <th>Trạng thái</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {pendingCertificates.map((c) => (
                  <tr key={c.id}>
                    <td><strong>{c.farm}</strong></td>
                    <td>{c.type}</td>
                    <td>{c.issuer}</td>
                    <td>{c.date}</td>
                    <td>{c.expiry}</td>
                    <td><span className="badge Pending">{c.status}</span></td>
                    <td>
                      <button className="primary" style={{ padding: '5px 10px', fontSize: 11 }}>
                        Thẩm định
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="panel padded">
          <h2>Quy trình thẩm định</h2>
          <p className="muted" style={{ marginBottom: 16 }}>Tiêu chuẩn đánh giá xác minh:</p>

          <button className="quick-action-btn" type="button">
            <span>📋</span>
            <div>
              <strong>Đối chiếu cổng thông tin Bộ NN&PTNT</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Tra cứu mã hiệu và đơn vị cấp phép</small>
            </div>
          </button>

          <button className="quick-action-btn" type="button">
            <span>🔍</span>
            <div>
              <strong>Kiểm tra định vị GPS nông trại</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Xác định quy mô vùng trồng và nhật ký canh tác</small>
            </div>
          </button>

          <button className="quick-action-btn" type="button">
            <span>📜</span>
            <div>
              <strong>Lịch sử thu hồi chứng nhận</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Kiểm tra cảnh báo vi phạm an toàn thực phẩm</small>
            </div>
          </button>
        </div>
      </div>
    </div>
  );
}
