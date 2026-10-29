import React, { useEffect, useState } from 'react';
import { backendUrl } from '../../api/client';
import { Notice } from '../../components/ui/Common';

const RESEND_SECONDS = 60;
const PASSWORD_POLICY = 'Tối thiểu 8 ký tự, gồm chữ hoa, chữ thường và chữ số.';

/** Posts to the anonymous UC-AUTH-07 endpoints; field errors are folded into the message. */
async function post(path, body) {
  const response = await fetch(backendUrl(`/api/auth${path}`), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  // Plain-text success messages arrive as text/plain; problem details arrive as JSON.
  const text = await response.text();
  let data = text;
  try {
    data = text ? JSON.parse(text) : {};
  } catch {
    data = text;
  }
  if (!response.ok) {
    data = typeof data === 'object' ? data : { title: data };
    const fields = data.errors ? Object.values(data.errors).flat().join(' ') : '';
    const error = new Error(fields || data.title || 'Không thể hoàn tất thao tác. Vui lòng thử lại.');
    error.code = data.code;
    throw error;
  }
  return data;
}

function PasswordField({ label, value, onChange, autoComplete }) {
  const [visible, setVisible] = useState(false);
  return <label className="field"><span>{label}</span><span className="password-control">
    <input type={visible ? 'text' : 'password'} autoComplete={autoComplete} required
      maxLength={128} value={value} onChange={(event) => onChange(event.target.value)} />
    <button type="button" className="password-toggle" aria-label={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
      onClick={() => setVisible((current) => !current)}>
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M2.5 12s3.3-6 9.5-6 9.5 6 9.5 6-3.3 6-9.5 6-9.5-6-9.5-6Z" />
        <circle cx="12" cy="12" r="2.5" />
        {visible && <path d="m4 4 16 16" />}
      </svg>
    </button></span></label>;
}

/**
 * UC-AUTH-07 Reset Password: 1. registered e-mail, 2. UC-AUTH-08 code, 3. new password.
 * The reset token from step 2 lives only in this component's state.
 */
export default function ForgotPasswordScreen({ navigate }) {
  const [step, setStep] = useState(1);
  const [email, setEmail] = useState('');
  const [otpCode, setOtpCode] = useState('');
  const [grant, setGrant] = useState(null);
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [resendIn, setResendIn] = useState(0);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');

  useEffect(() => {
    if (resendIn <= 0) return undefined;
    const timer = setTimeout(() => setResendIn((seconds) => seconds - 1), 1000);
    return () => clearTimeout(timer);
  }, [resendIn]);

  function backToSignIn() {
    if (navigate) navigate('/login');
    else window.location.hash = '#/login';
  }

  async function run(action) {
    setBusy(true);
    setError('');
    try {
      await action();
    } catch (failure) {
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  }

  function sendCode(event) {
    event.preventDefault();
    run(async () => {
      const message = await post('/forgot-password', { email: email.trim() });
      setNotice(message);
      setOtpCode('');
      setResendIn(RESEND_SECONDS);
      setStep(2);
    });
  }

  function resendCode() {
    if (resendIn > 0) return;
    run(async () => {
      const message = await post('/forgot-password/resend-otp', { email: email.trim() });
      setNotice(message);
      setOtpCode('');
      setResendIn(RESEND_SECONDS);
    });
  }

  function verifyCode(event) {
    event.preventDefault();
    if (!/^\d{6}$/.test(otpCode)) {
      setError('Vui lòng nhập đủ 6 chữ số mã OTP.');
      return;
    }
    run(async () => {
      setGrant(await post('/forgot-password/verify-otp', { email: email.trim(), otpCode }));
      setNotice('');
      setStep(3);
    });
  }

  function resetPassword(event) {
    event.preventDefault();
    if (newPassword !== confirmPassword) {
      setError('Mật khẩu xác nhận không khớp.');
      return;
    }
    run(async () => {
      try {
        const message = await post('/reset-password', {
          email: grant.email, resetToken: grant.resetToken, newPassword, confirmPassword,
        });
        setGrant(null);
        setNotice(message);
        setStep(4);
        setTimeout(backToSignIn, 1500);
      } catch (failure) {
        if (failure.code === 'RESET_SESSION_INVALID') {
          setGrant(null);
          setStep(1);
        }
        throw failure;
      }
    });
  }

  return <div className="login-page">
    <main className="login-main">
      <section className="login-card">
        <h1>Quên mật khẩu</h1>
        <p className="login-subtitle">
          {step === 1 && 'Nhập email đã đăng ký để nhận mã xác thực'}
          {step === 2 && `Nhập mã 6 chữ số đã gửi tới ${email.trim()}`}
          {step === 3 && 'Tạo mật khẩu mới cho tài khoản của bạn'}
          {step === 4 && 'Hoàn tất'}
        </p>
        <Notice error>{error}</Notice>
        <Notice>{notice}</Notice>

        {step === 1 && <form onSubmit={sendCode} className="login-form">
          <label className="field"><span>Email đã đăng ký *</span><input type="email"
            autoComplete="email" autoFocus required maxLength={255} value={email}
            onChange={(event) => setEmail(event.target.value)} placeholder="ten@example.com" /></label>
          <button className="primary login-submit" type="submit" disabled={busy}>
            {busy ? 'Đang gửi mã…' : 'Gửi mã xác thực'}
          </button>
        </form>}

        {step === 2 && <form onSubmit={verifyCode} className="login-form">
          <label className="field"><span>Mã xác thực OTP *</span><input inputMode="numeric"
            autoComplete="one-time-code" autoFocus required maxLength={6} value={otpCode}
            onChange={(event) => setOtpCode(event.target.value.replace(/\D/g, ''))}
            placeholder="______" /></label>
          <button className="primary login-submit" type="submit" disabled={busy}>
            {busy ? 'Đang xác thực…' : 'Xác thực'}
          </button>
          <button type="button" className="login-demo" disabled={busy || resendIn > 0}
            onClick={resendCode}>
            {resendIn > 0 ? `Gửi lại mã sau ${resendIn}s` : 'Gửi lại mã'}
          </button>
        </form>}

        {step === 3 && <form onSubmit={resetPassword} className="login-form">
          <label className="field"><span>Email đã xác thực</span>
            <input value={grant?.email || ''} readOnly /></label>
          <PasswordField label="Mật khẩu mới *" value={newPassword} onChange={setNewPassword}
            autoComplete="new-password" />
          <PasswordField label="Xác nhận mật khẩu mới *" value={confirmPassword}
            onChange={setConfirmPassword} autoComplete="new-password" />
          <p className="muted">{PASSWORD_POLICY}</p>
          <button className="primary login-submit" type="submit" disabled={busy}>
            {busy ? 'Đang lưu…' : 'Đặt lại mật khẩu'}
          </button>
        </form>}

        <div style={{ marginTop: 15, textAlign: 'center', fontSize: 14 }}>
          <a href="#/login" onClick={(event) => { event.preventDefault(); backToSignIn(); }}
            style={{ color: '#2e7d32', fontWeight: 600, textDecoration: 'none' }}>
            ← Quay lại đăng nhập
          </a>
        </div>
      </section>
    </main>
  </div>;
}
