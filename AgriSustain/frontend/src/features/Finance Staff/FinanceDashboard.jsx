import React from 'react';

export default function FinanceDashboard() {
  const metrics = [
    { label: 'Tổng doanh thu sàn (GMV)', value: '348.250.000 ₫', sub: 'Tháng 10/2026', icon: '💵', color: 'green' },
    { label: 'Chờ chi trả nông dân', value: '82.400.000 ₫', sub: 'Đợt giải ngân ngày 25 tới', icon: '📤', color: 'gold' },
    { label: 'Doanh thu phí & Hoa hồng', value: '26.118.000 ₫', sub: 'Tỷ lệ trung bình 7,5%', icon: '📈', color: 'blue' },
    { label: 'Yêu cầu bồi hoàn / Hủy', value: '1.450.000 ₫', sub: '1 giao dịch đang đối soát', icon: '🔄', color: 'purple' },
  ];

  const pendingPayouts = [
    { id: 'PAY-2610-88', farmer: 'Nguyễn Thị Mai (Nông trại Đà Lạt)', bank: 'Vietcombank - 0011004...45', amount: '24.500.000 ₫', period: 'Kỳ 1 tháng 10', status: 'Sẵn sàng duyệt' },
    { id: 'PAY-2610-89', farmer: 'Trần Văn Hùng (Vườn Cam Vĩnh Long)', bank: 'MBBank - 0888234...12', amount: '35.800.000 ₫', period: 'Kỳ 1 tháng 10', status: 'Sẵn sàng duyệt' },
    { id: 'PAY-2610-90', farmer: 'Lê Minh Phúc (HTX Lúa Sóc Trăng)', bank: 'Agribank - 6700205...90', amount: '22.100.000 ₫', period: 'Kỳ 1 tháng 10', status: 'Đang kiểm toán' },
  ];

  return (
    <div className="finance-dashboard">
      <div className="section-heading">
        <div>
          <p className="eyebrow">TÀI CHÍNH & KẾ TOÁN SÀN</p>
          <h1>Bảng Điều Khiển Tài Chính & Chi Trả</h1>
          <p className="muted">Quản trị dòng tiền ký quỹ đơn hàng, tính toán tự động hoa hồng nền tảng và phê duyệt chi trả doanh thu cho nông dân.</p>
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
            <h2>Đợt chi trả (Farmer Payouts) chuẩn bị giải ngân</h2>
            <button className="primary" style={{ padding: '6px 12px', fontSize: 11 }}>
              ✓ Duyệt chi đợt này
            </button>
          </div>
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Mã lệnh chi</th>
                  <th>Người thụ hưởng</th>
                  <th>Tài khoản ngân hàng</th>
                  <th>Kỳ doanh thu</th>
                  <th>Số tiền chi trả</th>
                  <th>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {pendingPayouts.map((p) => (
                  <tr key={p.id}>
                    <td><strong className="order-code">{p.id}</strong></td>
                    <td><strong>{p.farmer}</strong></td>
                    <td>{p.bank}</td>
                    <td>{p.period}</td>
                    <td><strong style={{ color: '#137145' }}>{p.amount}</strong></td>
                    <td><span className="badge Pending">{p.status}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="panel padded">
          <h2>Nghiệp vụ tài chính</h2>
          <p className="muted" style={{ marginBottom: 16 }}>Công cụ điều hành dòng vốn:</p>

          <button className="quick-action-btn" type="button">
            <span>💳</span>
            <div>
              <strong>Đối soát cổng thanh toán (PayOS/VNPay)</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Khớp lệnh giao dịch thực nhận vào tài khoản</small>
            </div>
          </button>

          <button className="quick-action-btn" type="button">
            <span>📊</span>
            <div>
              <strong>Cấu hình bảng hoa hồng danh mục</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Điều chỉnh tỷ lệ % phí sàn theo từng loại nông sản</small>
            </div>
          </button>

          <button className="quick-action-btn" type="button">
            <span>📑</span>
            <div>
              <strong>Xuất báo cáo thuế & Hóa đơn điện tử</strong>
              <small style={{ display: 'block', color: '#687b70' }}>Định dạng Excel / XML theo chuẩn kế toán</small>
            </div>
          </button>
        </div>
      </div>
    </div>
  );
}
