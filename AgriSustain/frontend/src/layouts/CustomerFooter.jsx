import React from 'react';

export default function CustomerFooter() {
  return (
    <footer className="customer-footer">
      <div className="footer-top-banner">
        <div className="footer-container banner-content">
          <div className="banner-badge">
            <span className="badge-icon">🌿</span>
            <div>
              <strong>100% Nông sản truy xuất nguồn gốc</strong>
              <p>Mỗi sản phẩm đều gắn mã lô thu hoạch và nhật ký canh tác số minh bạch.</p>
            </div>
          </div>
          <div className="banner-badge">
            <span className="badge-icon">🛡️</span>
            <div>
              <strong>Kiểm duyệt chứng chỉ VietGAP / OCOP</strong>
              <p>Hồ sơ canh tác được thẩm định trực tiếp bởi Verification Officer.</p>
            </div>
          </div>
          <div className="banner-badge">
            <span className="badge-icon">🚚</span>
            <div>
              <strong>Giao hàng tươi ngon tận nơi</strong>
              <p>Chuỗi cung ứng khép kín từ nhà vườn đến bàn ăn người tiêu dùng.</p>
            </div>
          </div>
        </div>
      </div>

      <div className="footer-main">
        <div className="footer-container footer-grid">
          <div className="footer-col">
            <div className="footer-brand">
              <span className="brand-leaf-icon">🌱</span>
              <span className="footer-brand-name">AgriSustain - Nông Sản Việt</span>
            </div>
            <p className="footer-desc">
              Hệ thống sàn giao dịch thương mại điện tử kết nối Người tiêu dùng và Nông dân, 
              cam kết nguồn gốc xuất xứ minh bạch và phát triển nông nghiệp bền vững.
            </p>
            <div className="footer-contact">
              <p>📍 <strong>Trụ sở:</strong> Tầng 5, Tòa nhà Công nghệ Nông nghiệp, Hà Nội</p>
              <p>📞 <strong>Hotline:</strong> 1900 6868 (8:00 - 21:00 hàng ngày)</p>
              <p>✉️ <strong>Email:</strong> support@agrisustain.vn</p>
            </div>
          </div>

          <div className="footer-col">
            <h4>DANH MỤC NÔNG SẢN</h4>
            <ul>
              <li><a href="#/orders">Rau củ quả hữu cơ</a></li>
              <li><a href="#/orders">Trái cây đặc sản vùng miền</a></li>
              <li><a href="#/orders">Gạo & Ngũ cốc canh tác sinh thái</a></li>
              <li><a href="#/orders">Nông sản đạt chuẩn VietGAP/GlobalGAP</a></li>
              <li><a href="#/orders">Sản phẩm OCOP 3 - 5 sao</a></li>
            </ul>
          </div>

          <div className="footer-col">
            <h4>HỖ TRỢ KHÁCH HÀNG</h4>
            <ul>
              <li><a href="#/orders">Tra cứu hành trình đơn hàng</a></li>
              <li><a href="#/profile">Quản lý hồ sơ & sổ địa chỉ</a></li>
              <li><a href="#/register">Đăng ký tài khoản Nông dân / Mua lẻ</a></li>
              <li><a href="#/orders">Chính sách khiếu nại & hoàn tiền (UC-CS-01)</a></li>
              <li><a href="#/orders">Hướng dẫn quét mã QR lô thu hoạch</a></li>
            </ul>
          </div>

          <div className="footer-col">
            <h4>CHỨNG NHẬN & ĐỐI TÁC</h4>
            <div className="cert-tags">
              <span className="cert-tag">VietGAP Certified</span>
              <span className="cert-tag">GlobalGAP Standard</span>
              <span className="cert-tag">OCOP Vietnam</span>
              <span className="cert-tag">Organic Farm</span>
            </div>
            <p style={{ marginTop: 16, fontSize: 12, color: '#7a8f81' }}>
              Dự án Capstone SEP490 - Đại học FPT · Đề tài AgriSustain Chuỗi Cung Ứng Nông Sản Minh Bạch.
            </p>
          </div>
        </div>
      </div>

      <div className="footer-bottom">
        <div className="footer-container bottom-bar">
          <span>© 2026 AgriSustain (Nông Sản Việt) - Bản quyền thuộc về Nhóm dự án SEP490.</span>
          <span>Hệ thống đạt chuẩn an toàn thông tin & bảo vệ quyền lợi người tiêu dùng.</span>
        </div>
      </div>
    </footer>
  );
}
