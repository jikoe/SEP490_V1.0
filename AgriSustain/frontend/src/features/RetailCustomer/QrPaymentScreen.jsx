import React, { useState, useEffect, useCallback, useMemo } from 'react';
import { request } from '../../api/client';
import { money, localTime } from '../../components/ui/Common';
import './qrPayment.css';

// Tài khoản nhận chuyển khoản (SePay theo dõi biến động số dư) - cấu hình qua .env hoặc backend config
const DEFAULT_BENEFICIARY = {
  name: 'SePay',
  title: 'Chuyển khoản ngân hàng qua SePay',
  bankName: import.meta.env.VITE_SEPAY_BANK_NAME || 'MB Bank (Ngân hàng TMCP Quân Đội)',
  bankBin: import.meta.env.VITE_SEPAY_BANK_BIN || '970422',
  bankShortName: import.meta.env.VITE_SEPAY_BANK_SHORT_NAME || 'MB',
  accountNumber: import.meta.env.VITE_SEPAY_ACCOUNT_NUMBER || '0988776655',
  accountName: import.meta.env.VITE_SEPAY_ACCOUNT_NAME || 'CONG TY CP NONG SAN VIET',
  badge: 'Doanh nghiệp MB Bank',
  note: 'Chuyển khoản tới tài khoản MB Bank của Nông Sản Việt với đúng nội dung bên trên. SePay tự động xác nhận đơn hàng ngay khi tiền về tài khoản.'
};

// SVG QR Code generator (fallback độc lập không phụ thuộc kết nối internet)
function SvgQrFallback({ value, size = 230 }) {
  return (
    <svg width={size} height={size} viewBox="0 0 100 100" style={{ shapeRendering: 'crispedges' }}>
      <rect width="100" height="100" fill="#ffffff" />
      {/* Góc trên-trái */}
      <rect x="5" y="5" width="28" height="28" fill="#1b4332" />
      <rect x="9" y="9" width="20" height="20" fill="#ffffff" />
      <rect x="13" y="13" width="12" height="12" fill="#1b4332" />
      {/* Góc trên-phải */}
      <rect x="67" y="5" width="28" height="28" fill="#1b4332" />
      <rect x="71" y="9" width="20" height="20" fill="#ffffff" />
      <rect x="75" y="13" width="12" height="12" fill="#1b4332" />
      {/* Góc dưới-trái */}
      <rect x="5" y="67" width="28" height="28" fill="#1b4332" />
      <rect x="9" y="71" width="20" height="20" fill="#ffffff" />
      <rect x="13" y="75" width="12" height="12" fill="#1b4332" />
      {/* Dải timing */}
      <rect x="36" y="8" width="4" height="4" fill="#1b4332" />
      <rect x="44" y="8" width="4" height="4" fill="#1b4332" />
      <rect x="52" y="8" width="4" height="4" fill="#1b4332" />
      <rect x="60" y="8" width="4" height="4" fill="#1b4332" />
      <rect x="8" y="36" width="4" height="4" fill="#1b4332" />
      <rect x="8" y="44" width="4" height="4" fill="#1b4332" />
      <rect x="8" y="52" width="4" height="4" fill="#1b4332" />
      <rect x="8" y="60" width="4" height="4" fill="#1b4332" />
      {/* Dữ liệu mã hóa trung tâm */}
      <rect x="38" y="38" width="24" height="24" fill="#1b4332" rx="3" />
    </svg>
  );
}

function getInitialExpiry(orderCode, expiresAtParam) {
  if (expiresAtParam) {
    const parsed = new Date(expiresAtParam).getTime();
    if (!isNaN(parsed) && parsed > Date.now()) {
      return parsed;
    }
  }

  const storageKey = `order_expiry_${orderCode}`;
  try {
    const saved = localStorage.getItem(storageKey);
    if (saved) {
      const parsedSaved = Number(saved);
      if (!isNaN(parsedSaved) && parsedSaved > Date.now()) {
        return parsedSaved;
      }
    }
  } catch (_) {}

  const defaultMs = Date.now() + 15 * 60 * 1000;
  try {
    localStorage.setItem(storageKey, String(defaultMs));
  } catch (_) {}
  return defaultMs;
}

export default function QrPaymentScreen({ navigate }) {
  const queryParams = useMemo(() => {
    const hash = window.location.hash || '';
    const queryPart = hash.includes('?') ? hash.split('?')[1] : '';
    const params = new URLSearchParams(queryPart);
    return {
      orderId: params.get('orderId') || '98231',
      orderCode: params.get('orderCode') || 'ORD-98231',
      amount: Number(params.get('amount')) || 214500,
      expiresAt: params.get('expiresAt') || null,
    };
  }, []);

  const [targetExpiry, setTargetExpiry] = useState(() =>
    getInitialExpiry(queryParams.orderCode, queryParams.expiresAt)
  );

  const calculateTimeLeft = useCallback(() => {
    if (!targetExpiry) return 0;
    const diffSeconds = Math.floor((targetExpiry - Date.now()) / 1000);
    return Math.max(0, diffSeconds);
  }, [targetExpiry]);

  const [timeLeft, setTimeLeft] = useState(() => calculateTimeLeft());
  const [status, setStatus] = useState('PENDING');
  const [feedbackMessage, setFeedbackMessage] = useState('');
  const [copiedField, setCopiedField] = useState(null);
  const [qrImgFailed, setQrImgFailed] = useState(false);
  const [autoRedirectTimer, setAutoRedirectTimer] = useState(null);

  // Thông tin ngân hàng thụ hưởng SePay
  const [beneficiary, setBeneficiary] = useState(DEFAULT_BENEFICIARY);

  useEffect(() => {
    request('/payment/config')
      .then((cfg) => {
        if (cfg && cfg.accountNumber) {
          setBeneficiary((prev) => ({
            ...prev,
            bankName: cfg.bankName || prev.bankName,
            bankBin: cfg.bankBin || prev.bankBin,
            bankShortName: cfg.bankShortName || prev.bankShortName,
            accountNumber: cfg.accountNumber || prev.accountNumber,
            accountName: cfg.accountName || prev.accountName,
          }));
        }
      })
      .catch(() => {});
  }, []);

  const activeBeneficiary = beneficiary;

  // Mã QR Napas giới hạn nội dung chuyển khoản 19 ký tự, không ký tự đặc biệt; webhook khớp "RC" + 16 ký tự hex đầu của mã đơn.
  const transferContent = useMemo(() => {
    return queryParams.orderCode.replace(/[^a-z0-9]/gi, '').toUpperCase().slice(0, 18);
  }, [queryParams.orderCode]);

  // Ảnh QR động của SePay
  const qrUrl = useMemo(() => {
    const params = new URLSearchParams({
      acc: activeBeneficiary.accountNumber,
      bank: activeBeneficiary.bankShortName,
      amount: String(queryParams.amount),
      des: transferContent,
      template: 'compact',
    });
    return `https://qr.sepay.vn/img?${params}`;
  }, [transferContent, queryParams.amount, activeBeneficiary]);

  // Tự động kiểm tra trạng thái đơn hàng
  useEffect(() => {
    let active = true;
    if (!queryParams.orderCode || status !== 'PENDING') {
      return;
    }

    const checkStatus = () => {
      request(`/orders/by-code/${encodeURIComponent(queryParams.orderCode)}`)
        .then((detail) => {
          if (!active || !detail) return;
          if (detail.orderStatus === 'Confirmed' || detail.paymentStatus === 'Paid') {
            setStatus('SUCCESS');
            setFeedbackMessage('Thanh toán thành công! Hệ thống đã tự động xác nhận chuyển khoản cho đơn hàng của bạn.');
            const redirect = setTimeout(() => {
              if (navigate) navigate('/retail/orders');
              else window.location.hash = '#/retail/orders';
            }, 4000);
            setAutoRedirectTimer(redirect);
            return;
          }
          if (detail.reservationExpiresAt) {
            const expMs = new Date(detail.reservationExpiresAt).getTime();
            if (!isNaN(expMs) && expMs > 0) {
              setTargetExpiry(expMs);
              try { localStorage.setItem(`order_expiry_${queryParams.orderCode}`, String(expMs)); } catch (_) {}
            }
          }
        })
        .catch(() => {});
    };

    checkStatus();
    const interval = setInterval(checkStatus, 3000);
    return () => {
      active = false;
      clearInterval(interval);
    };
  }, [queryParams.orderCode, status, navigate]);

  // Đếm ngược thời gian thanh toán
  useEffect(() => {
    if (status !== 'PENDING') return;

    const currentRemaining = calculateTimeLeft();
    setTimeLeft(currentRemaining);

    if (currentRemaining <= 0) {
      setStatus('EXPIRED');
      setFeedbackMessage('Phiên thanh toán đã hết hạn. Đơn hàng đã hết thời gian giữ hàng (15 phút). Bạn có thể bấm "Thử lại thanh toán" để tạo mã mới.');
      return;
    }

    const timer = setInterval(() => {
      const remaining = calculateTimeLeft();
      setTimeLeft(remaining);
      if (remaining <= 0) {
        clearInterval(timer);
        setStatus('EXPIRED');
        setFeedbackMessage('Phiên thanh toán đã hết hạn. Đơn hàng đã hết thời gian giữ hàng (15 phút). Bạn có thể bấm "Thử lại thanh toán" để tạo mã mới.');
      }
    }, 1000);

    return () => clearInterval(timer);
  }, [targetExpiry, status, calculateTimeLeft]);

  const formatTime = (seconds) => {
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  };

  const copyToClipboard = useCallback((text, fieldName) => {
    if (navigator.clipboard) {
      navigator.clipboard.writeText(text);
    }
    setCopiedField(fieldName);
    setTimeout(() => setCopiedField(null), 2500);
  }, []);

  const handleReturnToOrder = () => {
    if (navigate) navigate('/retail/orders');
    else window.location.hash = '#/retail/orders';
  };

  const handleRetryPayment = () => {
    const newExpiry = Date.now() + 15 * 60 * 1000;
    setTargetExpiry(newExpiry);
    try { localStorage.setItem(`order_expiry_${queryParams.orderCode}`, String(newExpiry)); } catch (_) {}
    setTimeLeft(900);
    setStatus('PENDING');
    setFeedbackMessage('');
    setQrImgFailed(false);
  };

  const handleCancelPayment = () => {
    if (window.confirm('Bạn có chắc chắn muốn hủy phiên thanh toán này? Đơn hàng sẽ vẫn được lưu ở trạng thái "Chờ thanh toán" trong lịch sử đơn hàng.')) {
      setStatus('CANCELLED');
      setFeedbackMessage('Bạn đã hủy phiên thanh toán. Đơn hàng vẫn được lưu lại, bạn có thể thanh toán sau hoặc chọn Thử lại.');
    }
  };

  useEffect(() => {
    return () => {
      if (autoRedirectTimer) clearTimeout(autoRedirectTimer);
    };
  }, [autoRedirectTimer]);

  const isExpired = status === 'EXPIRED';
  const isTimeWarning = timeLeft < 120 && status === 'PENDING';

  return (
    <div className="qr-pay-page">
      {/* Breadcrumbs */}
      <nav className="qr-crumbs" aria-label="Đường dẫn">
        <button type="button" onClick={() => navigate ? navigate('/cart') : window.location.hash = '#/cart'}>Giỏ hàng</button>
        <span aria-hidden="true">/</span>
        <button type="button" onClick={() => navigate ? navigate('/checkout') : window.location.hash = '#/checkout'}>Thanh toán</button>
        <span aria-hidden="true">/</span>
        <span>Chuyển khoản SePay</span>
      </nav>

      {/* Header */}
      <div className="qr-header">
        <div>
          <span className="order-badge">⏳ Chờ thanh toán · Mã đơn #{queryParams.orderCode}</span>
          <h1>Thanh toán đơn hàng qua SePay</h1>
          <p style={{ margin: 0, color: 'var(--pay-muted)', fontSize: 14 }}>
            Quét mã QR bằng ứng dụng ngân hàng hoặc ví điện tử. SePay tự động xác nhận ngay khi tiền về tài khoản.
          </p>
        </div>

        {/* Countdown Timer */}
        {status === 'PENDING' && (
          <div className={`qr-timer-box ${isTimeWarning ? 'warning' : ''}`} title="Thời gian giữ hàng và phiên thanh toán">
            <span style={{ fontSize: 20 }}>⏱️</span>
            <div>
              <div style={{ fontSize: 11, textTransform: 'uppercase', fontWeight: 600, color: isTimeWarning ? 'var(--pay-error)' : 'var(--pay-muted)' }}>
                Thời gian giữ hàng
              </div>
              <div className="qr-timer-digits" style={{ color: isTimeWarning ? 'var(--pay-error)' : 'var(--pay-primary)' }}>
                {formatTime(timeLeft)}
              </div>
            </div>
          </div>
        )}
      </div>

      {/* Thông báo trạng thái */}
      {feedbackMessage && (
        <div className={`qr-status-banner ${status === 'SUCCESS' ? 'success' : 'error'}`} role="alert">
          <div className="banner-icon">
            {status === 'SUCCESS' ? '🎉' : '⚠️'}
          </div>
          <div>
            <strong>{status === 'SUCCESS' ? 'Thành công!' : 'Thông báo:'}</strong> {feedbackMessage}
          </div>
        </div>
      )}

      {/* Màn hình thành công hoàn tất */}
      {status === 'SUCCESS' ? (
        <div className="qr-card qr-success-card">
          <div className="success-icon-large">✓</div>
          <h2>Đã thanh toán thành công!</h2>
          <p className="success-sub">
            Cảm ơn bạn đã đặt hàng nông sản bền vững. Giao dịch chuyển khoản <strong>{money(queryParams.amount)}</strong> cho đơn hàng <strong>#{queryParams.orderCode}</strong> đã được xác nhận.
          </p>
          <div className="success-meta">
            <div>
              <span>Mã đơn hàng:</span>
              <strong>#{queryParams.orderCode}</strong>
            </div>
            <div>
              <span>Phương thức:</span>
              <strong>Chuyển khoản SePay ({activeBeneficiary.bankShortName} Bank)</strong>
            </div>
            <div>
              <span>Số tiền:</span>
              <strong style={{ color: 'var(--pay-primary)' }}>{money(queryParams.amount)}</strong>
            </div>
            <div>
              <span>Thời gian:</span>
              <strong>{localTime(new Date().toISOString())}</strong>
            </div>
          </div>
          <p style={{ fontSize: 13, color: 'var(--pay-muted)' }}>
            Hệ thống đang tự động điều hướng sang trang Đơn hàng sau vài giây...
          </p>
          <div className="qr-actions" style={{ justifyContent: 'center' }}>
            <button type="button" className="btn-primary-pay" onClick={handleReturnToOrder}>
              Xem chi tiết đơn hàng
            </button>
            <button type="button" className="btn-secondary-pay" onClick={() => navigate ? navigate('/marketplace') : window.location.hash = '#/marketplace'}>
              Tiếp tục mua hàng
            </button>
          </div>
        </div>
      ) : (
        <>
          <div className="qr-grid">
            {/* Cột 1: Mã QR Code */}
            <div className="qr-card qr-display">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 14 }}>
                <h3 style={{ fontSize: 17, fontWeight: 700, margin: 0, color: '#1b4332' }}>
                  Quét mã QR ({activeBeneficiary.bankShortName})
                </h3>
                <span style={{ fontSize: 11, fontWeight: 700, padding: '3px 8px', borderRadius: 6, background: '#eaf4e9', color: '#1e6546' }}>
                  ✓ NAPAS 247
                </span>
              </div>

              <div className="qr-img-wrapper">
                {isExpired ? (
                  <div className="qr-expired-overlay">
                    <span style={{ fontSize: 36 }}>⏰</span>
                    <strong style={{ color: 'var(--pay-error)', fontSize: 15 }}>Mã QR đã hết hạn</strong>
                    <p style={{ margin: 0, fontSize: 12, color: 'var(--pay-muted)' }}>Vui lòng bấm Thử lại để tạo mã mới.</p>
                    <button type="button" className="copy-btn" onClick={handleRetryPayment} style={{ padding: '6px 14px' }}>
                      Tạo lại mã QR
                    </button>
                  </div>
                ) : qrImgFailed ? (
                  <SvgQrFallback value={qrUrl} size={230} />
                ) : (
                  <img
                    src={qrUrl}
                    alt={`Mã QR thanh toán SePay cho đơn hàng ${queryParams.orderCode}`}
                    onError={() => setQrImgFailed(true)}
                  />
                )}
              </div>

              <p className="qr-scan-guide">
                Sử dụng app <strong>Ngân hàng</strong> hoặc <strong>Ví điện tử</strong> quét mã QR phía trên để chuyển khoản trực tiếp.
              </p>
              <div className="qr-app-logos">
                <span>Vietcombank</span> · <span>MB</span> · <span>BIDV</span> · <span>Techcombank</span> · <span>MoMo</span>
              </div>
            </div>

            {/* Cột 2: Thông tin chuyển khoản chi tiết */}
            <div className="qr-card">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
                <h3 style={{ fontSize: 17, fontWeight: 700, margin: 0, color: '#1b4332' }}>
                  Thông tin chuyển khoản ngân hàng
                </h3>
              </div>

              <div className="transfer-info-list">
                <div className="transfer-item">
                  <div>
                    <div className="label">Kênh thanh toán</div>
                    <div className="val" style={{ color: '#1b4332', fontWeight: 700 }}>
                      SePay · Chuyển khoản Napas 247
                    </div>
                  </div>
                </div>

                <div className="transfer-item">
                  <div>
                    <div className="label">Ngân hàng thụ hưởng</div>
                    <div className="val">{activeBeneficiary.bankName}</div>
                  </div>
                </div>

                <div className="transfer-item">
                  <div>
                    <div className="label">Chủ tài khoản</div>
                    <div className="val">{activeBeneficiary.accountName}</div>
                  </div>
                </div>

                <div className="transfer-item">
                  <div>
                    <div className="label">Số tài khoản</div>
                    <div className="val" style={{ fontFamily: 'monospace', letterSpacing: 1 }}>
                      {activeBeneficiary.accountNumber}
                    </div>
                  </div>
                  <button
                    type="button"
                    className={`copy-btn ${copiedField === 'acc' ? 'copied' : ''}`}
                    onClick={() => copyToClipboard(activeBeneficiary.accountNumber, 'acc')}
                  >
                    {copiedField === 'acc' ? '✓ Đã sao chép' : 'Sao chép'}
                  </button>
                </div>

                <div className="transfer-item">
                  <div>
                    <div className="label">Số tiền cần thanh toán</div>
                    <div className="val highlight">{money(queryParams.amount)}</div>
                  </div>
                  <button
                    type="button"
                    className={`copy-btn ${copiedField === 'amount' ? 'copied' : ''}`}
                    onClick={() => copyToClipboard(queryParams.amount.toString(), 'amount')}
                  >
                    {copiedField === 'amount' ? '✓ Đã sao chép' : 'Sao chép'}
                  </button>
                </div>

                <div className="transfer-item" style={{ background: '#f5faf6', borderColor: '#cfe5d7' }}>
                  <div>
                    <div className="label" style={{ color: 'var(--pay-primary)' }}>
                      Nội dung chuyển khoản (bắt buộc)
                    </div>
                    <div className="val" style={{ color: 'var(--pay-primary)', fontFamily: 'monospace', fontWeight: 700 }}>
                      {transferContent}
                    </div>
                  </div>
                  <button
                    type="button"
                    className={`copy-btn ${copiedField === 'content' ? 'copied' : ''}`}
                    onClick={() => copyToClipboard(transferContent, 'content')}
                  >
                    {copiedField === 'content' ? '✓ Đã sao chép' : 'Sao chép'}
                  </button>
                </div>
              </div>

              <p style={{ fontSize: 12, color: 'var(--pay-muted)', margin: '14px 0 0', lineHeight: 1.5, background: '#f9fbf9', padding: '10px 12px', borderRadius: 8, border: '1px solid #e2ece5' }}>
                💡 <em>{activeBeneficiary.note}</em>
              </p>

              {/* Nút thao tác dưới form */}
              <div className="qr-actions">
                {status === 'PENDING' ? (
                  <>
                    <button type="button" className="btn-secondary-pay" onClick={handleCancelPayment}>
                      Hủy thanh toán
                    </button>
                    <button type="button" className="btn-secondary-pay" onClick={handleReturnToOrder}>
                      Quay lại đơn hàng
                    </button>
                  </>
                ) : (
                  <>
                    <button type="button" className="btn-primary-pay" onClick={handleRetryPayment}>
                      🔄 Thử lại thanh toán
                    </button>
                    <button type="button" className="btn-secondary-pay" onClick={handleReturnToOrder}>
                      Quay lại đơn hàng
                    </button>
                  </>
                )}
              </div>
            </div>
          </div>


        </>
      )}
    </div>
  );
}

