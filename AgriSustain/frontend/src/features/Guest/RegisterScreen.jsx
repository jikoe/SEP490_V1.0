import React, { useState } from 'react';
import { Notice, Field, Modal } from '../../components/ui/Common';

/**
 * UC-AUTH-01: Register RetailCustomer or Farmer Account (SCR-07)
 * Includes UC-AUTH-08: Verify Email OTP modal
 * Styled with vibrant green agricultural aesthetic matching Figma SCR-TRC-01
 */
export default function RegisterScreen({ navigate }) {
  const [role, setRole] = useState('RetailCustomer');
  const [form, setForm] = useState({
    fullName: '',
    phoneNumber: '',
    email: '',
    password: '',
    confirmPassword: '',
    agreeTerms: false,
  });
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(false);
  const [otpModalOpen, setOtpModalOpen] = useState(false);
  const [otpDigits, setOtpDigits] = useState(['', '', '', '', '', '']);
  const [otpError, setOtpError] = useState(null);
  const [otpCountdown, setOtpCountdown] = useState(300);
  const [otpInfo, setOtpInfo] = useState('');
  const [successNotice, setSuccessNotice] = useState(null);

  function handleChange(field, value) {
    setForm((prev) => ({ ...prev, [field]: value }));
    setError(null);
  }

  async function handleRegisterSubmit(e) {
    e.preventDefault();
    if (!form.fullName.trim()) {
      setError('Vui lòng nhập Họ và tên (tối đa 50 ký tự).');
      return;
    }
    if (!/^0\d{9}$/.test(form.phoneNumber.trim())) {
      setError('Số điện thoại không hợp lệ (phải bắt đầu bằng số 0 và đủ 10 chữ số).');
      return;
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim())) {
      setError('Địa chỉ email không đúng định dạng.');
      return;
    }
    if (form.password.length < 8) {
      setError('Mật khẩu phải có ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường và chữ số.');
      return;
    }
    if (form.password !== form.confirmPassword) {
      setError('Mật khẩu xác nhận không khớp.');
      return;
    }
    if (!form.agreeTerms) {
      setError('Bạn cần đồng ý với Điều khoản dịch vụ & Chính sách bảo mật của AgriSustain.');
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const payload = {
        fullName: form.fullName.trim(),
        phoneNumber: form.phoneNumber.trim(),
        email: form.email.trim(),
        password: form.password,
        confirmPassword: form.confirmPassword,
        role: role,
        agreeTerms: form.agreeTerms,
      };
      const res = await fetch('/api/auth/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error(data.title || data.message || 'Đăng ký không thành công. Vui lòng kiểm tra lại thông tin.');
      }

      // The server reply states where the OTP was sent (and, in this build, the demo code).
      setOtpInfo(await res.json().catch(() => ''));
      setLoading(false);
      setOtpModalOpen(true);
      setOtpDigits(['', '', '', '', '', '']);
      setOtpError(null);
    } catch (err) {
      setLoading(false);
      setError(err.message || 'Không thể kết nối đến máy chủ.');
    }
  }

  function handleOtpDigitChange(index, value) {
    if (!/^\d*$/.test(value)) return;
    const newDigits = [...otpDigits];
    newDigits[index] = value.slice(-1);
    setOtpDigits(newDigits);
    setOtpError(null);

    // Auto focus next input
    if (value && index < 5) {
      const nextInput = document.getElementById(`otp-input-${index + 1}`);
      nextInput?.focus();
    }
  }

  function handleOtpKeyDown(index, e) {
    if (e.key === 'Backspace' && !otpDigits[index] && index > 0) {
      const prevInput = document.getElementById(`otp-input-${index - 1}`);
      prevInput?.focus();
    }
  }

  async function handleVerifyOtp(e) {
    e.preventDefault();
    const code = otpDigits.join('');
    if (code.length < 6) {
      setOtpError('Vui lòng nhập đủ 6 chữ số mã OTP.');
      return;
    }

    setLoading(true);
    setOtpError(null);
    try {
      const res = await fetch('/api/auth/verify-otp', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          email: form.email.trim(),
          otpCode: code,
        }),
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error(data.title || data.message || 'Mã xác thực OTP không chính xác hoặc đã hết hạn.');
      }

      const user = await res.json();
      sessionStorage.removeItem('agrisustain:current-user');
      sessionStorage.removeItem('agrisustain:demo-fixture');
      setLoading(false);
      setOtpModalOpen(false);
      setSuccessNotice(`MSG56: Đăng ký tài khoản thành công cho ${user.fullName || form.fullName}! Thông tin đã được lưu vào cơ sở dữ liệu.`);
      setTimeout(() => {
        if (navigate) navigate('/login');
      }, 1200);
    } catch (err) {
      setLoading(false);
      setOtpError(err.message || 'Xác thực OTP thất bại.');
    }
  }

  async function handleResendOtp() {
    try {
      const res = await fetch('/api/auth/resend-otp', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: form.email.trim() }),
      });
      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error(data.title || 'Không thể gửi lại mã OTP.');
      }
      setOtpInfo(await res.json().catch(() => 'Mã OTP mới đã được gửi lại vào email của bạn.'));
      setOtpDigits(['', '', '', '', '', '']);
      setOtpError(null);
    } catch (err) {
      setOtpError(err.message);
    }
  }

  return (
    <div style={{ maxWidth: 680, margin: '20px auto' }}>
      {/* Header card with agricultural banner */}
      <div
        style={{
          background: 'linear-gradient(135deg, #1e6546 0%, #2d8a5e 100%)',
          borderRadius: '16px 16px 0 0',
          padding: '28px 32px',
          color: '#ffffff',
          boxShadow: '0 4px 15px rgba(30, 101, 70, 0.15)',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 8 }}>
          <span style={{ fontSize: 24 }}>🌱</span>
          <span style={{ fontSize: 11, fontWeight: 700, letterSpacing: 1.5, color: '#a7e4c2' }}>
            AGRISUSTAIN XÁC THỰC TÀI KHOẢN (SCR-07 / UC-AUTH-01)
          </span>
        </div>
        <h1 style={{ color: '#ffffff', fontSize: 26, margin: '0 0 8px', fontWeight: 800 }}>
          Đăng ký tài khoản mới
        </h1>
        <p style={{ margin: 0, fontSize: 13, color: '#d8f3dc', lineHeight: 1.5 }}>
          Tham gia mạng lưới nông sản minh bạch nguồn gốc từ trang trại đến bàn ăn.
        </p>
      </div>

      <div
        className="panel padded"
        style={{
          borderRadius: '0 0 16px 16px',
          borderTop: 'none',
          boxShadow: '0 10px 30px rgba(0,0,0,0.06)',
          padding: '32px',
        }}
      >
        <Notice error={Boolean(error)}>{error}</Notice>
        {successNotice && <Notice>{successNotice}</Notice>}

        <form onSubmit={handleRegisterSubmit}>
          {/* 1. Chọn vai trò tài khoản */}
          <div style={{ marginBottom: 24 }}>
            <label style={{ display: 'block', fontSize: 13, fontWeight: 700, color: '#1e4834', marginBottom: 10 }}>
              1. BẠN MUỐN THAM GIA VỚI VAI TRÒ NÀO?
            </label>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 14 }}>
              {/* Card RetailCustomer */}
              <div
                onClick={() => setRole('RetailCustomer')}
                style={{
                  border: `2px solid ${role === 'RetailCustomer' ? '#1e6546' : '#d7e2da'}`,
                  borderRadius: 12,
                  padding: 16,
                  cursor: 'pointer',
                  background: role === 'RetailCustomer' ? '#edf6f0' : '#ffffff',
                  transition: 'all 0.2s ease',
                  position: 'relative',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
                  <span style={{ fontSize: 22 }}>🛒</span>
                  <div>
                    <strong style={{ fontSize: 14, color: '#1b4332' }}>RetailCustomer</strong>
                    <div style={{ fontSize: 11, color: '#5b7566' }}>Người mua lẻ / Hộ gia đình</div>
                  </div>
                </div>
                <p style={{ margin: 0, fontSize: 11.5, color: '#688273', lineHeight: 1.4 }}>
                  Mua sắm nông sản VietGAP, theo dõi đơn hàng và tra cứu nguồn gốc mã lô.
                </p>
                {role === 'RetailCustomer' && (
                  <span style={{ position: 'absolute', top: 10, right: 10, color: '#1e6546', fontWeight: 800 }}>✓</span>
                )}
              </div>

              {/* Card Farmer */}
              <div
                onClick={() => setRole('Farmer')}
                style={{
                  border: `2px solid ${role === 'Farmer' ? '#1e6546' : '#d7e2da'}`,
                  borderRadius: 12,
                  padding: 16,
                  cursor: 'pointer',
                  background: role === 'Farmer' ? '#edf6f0' : '#ffffff',
                  transition: 'all 0.2s ease',
                  position: 'relative',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
                  <span style={{ fontSize: 22 }}>🚜</span>
                  <div>
                    <strong style={{ fontSize: 14, color: '#1b4332' }}>Farmer</strong>
                    <div style={{ fontSize: 11, color: '#5b7566' }}>Nông dân / Hợp tác xã</div>
                  </div>
                </div>
                <p style={{ margin: 0, fontSize: 11.5, color: '#688273', lineHeight: 1.4 }}>
                  Quản lý nông trại, đăng bán sản phẩm và cập nhật nhật ký canh tác số.
                </p>
                {role === 'Farmer' && (
                  <span style={{ position: 'absolute', top: 10, right: 10, color: '#1e6546', fontWeight: 800 }}>✓</span>
                )}
              </div>
            </div>
          </div>

          {/* 2. Thông tin đăng ký */}
          <div style={{ marginBottom: 20 }}>
            <label style={{ display: 'block', fontSize: 13, fontWeight: 700, color: '#1e4834', marginBottom: 12 }}>
              2. THÔNG TIN ĐĂNG KÝ TÀI KHOẢN
            </label>

            <Field label="Họ và tên đầy đủ *">
              <input
                type="text"
                placeholder={role === 'Farmer' ? 'Ví dụ: Trần Văn Nông (hoặc Tên HTX)' : 'Ví dụ: Nguyễn Văn A'}
                value={form.fullName}
                onChange={(e) => handleChange('fullName', e.target.value)}
                maxLength={50}
                required
              />
            </Field>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
              <Field label="Số điện thoại di động *">
                <input
                  type="tel"
                  placeholder="0912345678"
                  value={form.phoneNumber}
                  onChange={(e) => handleChange('phoneNumber', e.target.value)}
                  maxLength={10}
                  required
                />
              </Field>

              <Field label="Địa chỉ Email *">
                <input
                  type="email"
                  placeholder="tenban@email.com"
                  value={form.email}
                  onChange={(e) => handleChange('email', e.target.value)}
                  required
                />
              </Field>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
              <Field label="Mật khẩu khởi tạo *">
                <input
                  type="password"
                  placeholder="Tối thiểu 8 ký tự..."
                  value={form.password}
                  onChange={(e) => handleChange('password', e.target.value)}
                  required
                />
              </Field>

              <Field label="Xác nhận lại mật khẩu *">
                <input
                  type="password"
                  placeholder="Nhập lại chính xác..."
                  value={form.confirmPassword}
                  onChange={(e) => handleChange('confirmPassword', e.target.value)}
                  required
                />
              </Field>
            </div>
          </div>

          {/* 3. Checkbox điều khoản */}
          <div style={{ margin: '20px 0 24px', padding: 14, background: '#f5faf6', borderRadius: 8 }}>
            <label style={{ display: 'flex', alignItems: 'flex-start', gap: 10, cursor: 'pointer', fontSize: 12.5, color: '#334e3d' }}>
              <input
                type="checkbox"
                checked={form.agreeTerms}
                onChange={(e) => handleChange('agreeTerms', e.target.checked)}
                style={{ marginTop: 2, accentColor: '#1e6546' }}
              />
              <span>
                Tôi đã đọc và đồng ý với{' '}
                <strong style={{ color: '#1e6546' }}>Điều khoản dịch vụ</strong> và{' '}
                <strong style={{ color: '#1e6546' }}>Chính sách bảo mật thông tin</strong> của Sàn nông sản AgriSustain.
              </span>
            </label>
          </div>

          {/* Action Button */}
          <button
            type="submit"
            className="primary"
            style={{
              width: '100%',
              padding: '13px 20px',
              fontSize: 15,
              fontWeight: 700,
              borderRadius: 10,
              background: '#1e6546',
              boxShadow: '0 4px 12px rgba(30, 101, 70, 0.25)',
            }}
            disabled={loading}
          >
            {loading ? 'Đang gửi mã xác thực...' : 'Đăng Ký & Nhận Mã OTP Xác Thực →'}
          </button>
        </form>

        <div style={{ textAlign: 'center', marginTop: 20, fontSize: 13, color: '#688273' }}>
          Đã có tài khoản AgriSustain?{' '}
          <a
            href="#/login"
            onClick={(e) => { e.preventDefault(); navigate('/login'); }}
            style={{ color: '#1e6546', fontWeight: 700, textDecoration: 'none' }}
          >
            Đăng nhập ngay
          </a>
        </div>
      </div>

      {/* Modal xác thực OTP (UC-AUTH-08) */}
      {otpModalOpen && (
        <Modal
          onClose={() => setOtpModalOpen(false)}
          title="Xác thực OTP qua Email (UC-AUTH-08)"
        >
          <div style={{ textAlign: 'center', padding: '10px 0 20px' }}>
            <div
              style={{
                width: 54,
                height: 54,
                borderRadius: '50%',
                background: '#e8f5ed',
                color: '#1e6546',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                margin: '0 auto 16px',
                fontSize: 24,
              }}
            >
              ✉️
            </div>
            <p style={{ margin: '0 0 6px', fontSize: 14, color: '#2d4d3c' }}>
              Hệ thống AgriSustain vừa gửi mã xác nhận 6 chữ số tới:
            </p>
            <strong style={{ fontSize: 15, color: '#1e6546' }}>{form.email}</strong>
          </div>

          {otpError && <Notice error>{otpError}</Notice>}

          <form onSubmit={handleVerifyOtp}>
            <div style={{ display: 'flex', justifyContent: 'center', gap: 10, margin: '20px 0 24px' }}>
              {otpDigits.map((digit, idx) => (
                <input
                  key={idx}
                  id={`otp-input-${idx}`}
                  type="text"
                  inputMode="numeric"
                  maxLength={1}
                  value={digit}
                  onChange={(e) => handleOtpDigitChange(idx, e.target.value)}
                  onKeyDown={(e) => handleOtpKeyDown(idx, e)}
                  style={{
                    width: 46,
                    height: 54,
                    fontSize: 24,
                    fontWeight: 700,
                    textAlign: 'center',
                    borderRadius: 10,
                    border: '2px solid #2d6a4f',
                    background: '#f8fbf9',
                    color: '#1b4332',
                  }}
                  autoFocus={idx === 0}
                />
              ))}
            </div>

            <div style={{ textAlign: 'center', fontSize: 13, color: '#688273', marginBottom: 24 }}>
              {otpInfo && <div role="status" style={{ marginBottom: 10, background: '#f0fdf4', border: '1px solid #bbf7d0', padding: '6px 12px', borderRadius: 8, display: 'inline-block', color: '#166534' }}>
                💡 {otpInfo}
              </div>}
              <div>Mã OTP có hiệu lực trong: <strong style={{ color: '#d9534f' }}>05:00</strong></div>
              <div style={{ marginTop: 8 }}>
                Chưa nhận được mã?{' '}
                <button
                  type="button"
                  className="text-button"
                  style={{ color: '#1e6546', fontWeight: 700 }}
                  onClick={handleResendOtp}
                >
                  Gửi lại mã OTP
                </button>
              </div>
            </div>

            <div className="modal-actions" style={{ justifyContent: 'center', gap: 14 }}>
              <button
                type="button"
                onClick={() => setOtpModalOpen(false)}
                style={{ padding: '10px 20px', borderRadius: 8 }}
              >
                Hủy bỏ
              </button>
              <button
                type="submit"
                className="primary"
                style={{
                  padding: '10px 28px',
                  borderRadius: 8,
                  background: '#1e6546',
                  fontWeight: 700,
                }}
                disabled={loading}
              >
                {loading ? 'Đang xác thực...' : 'Xác Nhận & Hoàn Tất'}
              </button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
