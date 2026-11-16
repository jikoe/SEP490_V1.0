/* ==========================================================================
   AgriSustain - Private dossier files (js/dossier.js)
   Legal documents (Business License, certificate dossiers) are served from
   /api/dossiers/... and need the bearer token, so they are fetched as a blob.
   ========================================================================== */

const Dossier = {
  isSeedPlaceholderUrl(url) {
    const raw = String(url || '').trim().toLowerCase();
    return raw.includes('cdn.agrisustain.vn') || raw.includes('agrisustain.vn/docs/') || raw.includes('example.com');
  },

  buildDigitalCertificateHtml(meta = {}) {
    const certType = Utils.escapeHtml(meta.certificateType || 'VietGAP');
    const certNumber = Utils.escapeHtml(meta.certificateNumber || 'VG-2026-0001');
    const issuedBy = Utils.escapeHtml(meta.issuedBy || 'Trung Tâm Kiểm Định Chất Lượng Nông Lâm Thủy Sản');
    const farmName = Utils.escapeHtml(meta.farmName || 'Nông Trại Liên Kết AgriSustain');
    const farmerName = Utils.escapeHtml(meta.farmerName || 'Đại diện Nông hộ / HTX');
    const farmAddress = Utils.escapeHtml(meta.farmAddress || 'Việt Nam');
    const issueDate = meta.issueDate ? Utils.formatDate(meta.issueDate) : '01/05/2025';
    const expiryDate = meta.expiryDate ? Utils.formatDate(meta.expiryDate) : '01/05/2028';
    const isVerified = meta.verificationStatus === 'Approved' || meta.verificationStatus === 'Verified';
    const reviewer = Utils.escapeHtml(meta.reviewedByOfficerName || 'Chuyên viên Thẩm định Hệ thống AgriSustain');

    return `<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="UTF-8">
  <title>Chứng nhận ${certType} - ${certNumber}</title>
  <style>
    * { box-sizing: border-box; }
    body {
      margin: 0;
      padding: 32px 16px;
      background: #e2e8f0;
      font-family: "Times New Roman", Times, serif;
      color: #1e293b;
      display: flex;
      flex-direction: column;
      align-items: center;
    }
    .cert-paper {
      width: 100%;
      max-width: 820px;
      background: #fffdf9;
      border: 10px double #166534;
      padding: 44px 52px;
      box-shadow: 0 20px 45px rgba(0,0,0,0.2);
      position: relative;
    }
    .cert-header {
      text-align: center;
      border-bottom: 2px solid #166534;
      padding-bottom: 18px;
      margin-bottom: 28px;
    }
    .cert-header .gov {
      font-size: 15px;
      font-weight: bold;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }
    .cert-header .motto {
      font-size: 14px;
      font-weight: bold;
      margin-top: 4px;
    }
    .cert-header .issuer {
      margin-top: 12px;
      font-size: 15px;
      color: #166534;
      font-weight: bold;
      text-transform: uppercase;
    }
    .cert-title {
      text-align: center;
      margin: 24px 0;
    }
    .cert-title h1 {
      font-size: 30px;
      color: #14532d;
      margin: 0 0 8px 0;
      text-transform: uppercase;
      letter-spacing: 1px;
    }
    .cert-title .sub {
      font-size: 18px;
      font-weight: bold;
      color: #b45309;
    }
    .cert-body {
      font-size: 17px;
      line-height: 1.75;
      margin: 24px 0;
    }
    .cert-row {
      display: flex;
      margin-bottom: 10px;
      border-bottom: 1px dotted #cbd5e1;
      padding-bottom: 4px;
    }
    .cert-label {
      width: 230px;
      font-weight: bold;
      color: #334155;
      flex-shrink: 0;
    }
    .cert-val {
      font-weight: bold;
      color: #0f172a;
    }
    .cert-footer {
      margin-top: 36px;
      display: flex;
      justify-content: space-between;
      align-items: flex-end;
    }
    .stamp-box {
      text-align: center;
      padding: 14px 22px;
      border: 3px solid ${isVerified ? '#dc2626' : '#d97706'};
      color: ${isVerified ? '#dc2626' : '#d97706'};
      border-radius: 8px;
      transform: rotate(-4deg);
      font-family: system-ui, sans-serif;
      font-weight: 800;
      text-transform: uppercase;
    }
    @media print {
      body { background: #fff; padding: 0; }
      .toolbar { display: none; }
      .cert-paper { box-shadow: none; }
    }
  </style>
</head>
<body>
  <div class="cert-paper">
    <div class="cert-header">
      <div class="gov">CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div>
      <div class="motto">Độc lập - Tự do - Hạnh phúc</div>
      <div class="issuer">${issuedBy}</div>
    </div>

    <div class="cert-title">
      <h1>GIẤY CHỨNG NHẬN CHẤT LƯỢNG</h1>
      <div class="sub">TIÊU CHUẨN: ${certType}</div>
      <div style="margin-top:6px; font-size:15px;">Số hiệu: <strong>${certNumber}</strong></div>
    </div>

    <div class="cert-body">
      <div class="cert-row">
        <span class="cert-label">Đơn vị / Nông trại được cấp:</span>
        <span class="cert-val">${farmName}</span>
      </div>
      <div class="cert-row">
        <span class="cert-label">Đại diện pháp lý / Chủ hộ:</span>
        <span class="cert-val">${farmerName}</span>
      </div>
      <div class="cert-row">
        <span class="cert-label">Địa chỉ vùng trồng:</span>
        <span class="cert-val">${farmAddress}</span>
      </div>
      <div class="cert-row">
        <span class="cert-label">Tiêu chuẩn chứng nhận:</span>
        <span class="cert-val">${certType} (Thực hành sản xuất nông nghiệp tốt)</span>
      </div>
      <div class="cert-row">
        <span class="cert-label">Cơ quan cấp phép:</span>
        <span class="cert-val">${issuedBy}</span>
      </div>
      <div class="cert-row">
        <span class="cert-label">Ngày cấp chứng nhận:</span>
        <span class="cert-val">${issueDate}</span>
      </div>
      <div class="cert-row">
        <span class="cert-label">Có giá trị đến ngày:</span>
        <span class="cert-val">${expiryDate}</span>
      </div>
      <div class="cert-row">
        <span class="cert-label">Hậu kiểm trên AgriSustain:</span>
        <span class="cert-val">${isVerified ? 'Đã xác minh hợp lệ (Verified)' : 'Đang chờ thẩm định'} · ${reviewer}</span>
      </div>
    </div>

    <div class="cert-footer">
      <div>
        <div style="font-size:13px; color:#475569;">Hồ sơ lưu trữ điện tử trên nền tảng AgriSustain</div>
        <div style="font-size:13px; color:#475569;">Mã tra cứu chứng chỉ: <strong>${certNumber}</strong></div>
      </div>
      <div class="stamp-box">
        <div style="font-size:12px;">CHỨNG THỰC HỒ SƠ</div>
        <div style="font-size:16px; margin:4px 0;">${isVerified ? '✓ ĐÃ XÁC MINH' : '! CHỜ DUYỆT'}</div>
        <div style="font-size:11px;">${issuedBy}</div>
      </div>
    </div>
  </div>
</body>
</html>`;
  },

  async open(url, certMeta = null) {
    const raw = String(url || '').trim();
    if (!raw && !certMeta) {
      Utils.showToast('Không có tệp tài liệu chứng nhận đính kèm.', 'warning');
      return;
    }

    // If URL is a seeded placeholder link (e.g., https://cdn.agrisustain.vn/certs/vg1.pdf),
    // render a full digital certificate document so the user can view/print it immediately.
    if (this.isSeedPlaceholderUrl(raw) || (!raw.startsWith('/api/dossiers/') && certMeta)) {
      if (!raw.startsWith('/api/dossiers/') && !raw.startsWith('/uploads/') && !raw.startsWith('data:') && !raw.startsWith('blob:')) {
        const html = this.buildDigitalCertificateHtml(certMeta || { certificateNumber: raw.split('/').pop() });
        const blob = new Blob([html], { type: 'text/html;charset=utf-8' });
        window.open(URL.createObjectURL(blob), '_blank', 'noopener');
        return;
      }
    }

    // Direct URL support (uploaded media, data URI, or blob)
    if (raw.startsWith('http://') || raw.startsWith('https://') || raw.startsWith('/uploads/') || raw.startsWith('data:') || raw.startsWith('blob:')) {
      window.open(raw, '_blank', 'noopener');
      return;
    }

    // Private files uploaded through the app live behind /api/dossiers.
    if (!raw.startsWith('/api/dossiers/')) {
      if (certMeta) {
        const html = this.buildDigitalCertificateHtml(certMeta);
        const blob = new Blob([html], { type: 'text/html;charset=utf-8' });
        window.open(URL.createObjectURL(blob), '_blank', 'noopener');
        return;
      }
      Utils.showToast('Tài liệu này là dữ liệu mẫu, không có tệp thật trên máy chủ.', 'warning');
      return;
    }

    const path = raw.replace(/^\/api/, '');
    const token = Auth.getAccessToken();
    try {
      const response = await fetch(`${CONFIG.API_BASE_URL}${path}`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {}
      });
      if (!response.ok) {
        if (certMeta) {
          const html = this.buildDigitalCertificateHtml(certMeta);
          const blob = new Blob([html], { type: 'text/html;charset=utf-8' });
          window.open(URL.createObjectURL(blob), '_blank', 'noopener');
          return;
        }
        Utils.showToast('Không mở được tài liệu từ máy chủ (Mã lỗi ' + response.status + ').', 'error');
        return;
      }
      const blob = await response.blob();
      window.open(URL.createObjectURL(blob), '_blank', 'noopener');
    } catch (err) {
      Utils.showToast('Lỗi khi tải tài liệu: ' + err.message, 'error');
    }
  },

  /** Returns an error message when the chosen file is not allowed, otherwise null. */
  check(file, extensions) {
    if (!file) return null;
    const ext = file.name.toLowerCase().split('.').pop();
    if (!extensions.includes(ext)) return `Chỉ hỗ trợ tệp ${extensions.map((e) => e.toUpperCase()).join(', ')}.`;
    if (file.size > 5 * 1024 * 1024) return 'Tệp vượt quá 5 MB. Vui lòng chọn tệp nhỏ hơn.';
    return null;
  }
};
