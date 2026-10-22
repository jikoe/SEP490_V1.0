import React, { useState } from 'react';
import { login } from '../../api/client';
import { Notice } from '../../components/ui/Common';

function LeafMark() {
  return <svg className="login-leaf-mark" viewBox="0 0 64 64" aria-hidden="true">
    <path d="M31 57C11 48 5 31 8 13c15 4 25 13 27 27C38 23 48 10 59 5c2 25-7 43-28 52Z" />
    <path d="M30 58c-2-17-8-28-20-39m21 37c5-18 13-31 26-43" />
  </svg>;
}

function DecorativeBranch({ className }) {
  return <svg className={`login-branch ${className}`} viewBox="0 0 180 350" aria-hidden="true">
    <path className="branch-stem" d="M5 350 55 190 92 14m-38 176 69-48m-72 57-30-79m14 157 91-64m-99 77 48 12" />
    <path className="branch-leaf" d="M91 14C58 42 70 73 111 94c13-34 5-61-20-80ZM47 141C15 124 4 97 12 78c34-1 55 20 59 56-10 4-17 6-24 7Zm14 21c-2-33 14-56 44-66 21 28 11 58-22 77-9-4-16-7-22-11Zm-22 65c-29-16-39-39-30-60 32 2 51 24 50 59-8 2-14 2-20 1Zm15 20c2-31 19-52 49-57 17 30 4 58-31 73-8-6-14-11-18-16Zm-36 72c-18-22-18-46-3-63 28 13 38 38 25 68-9-1-16-3-22-5Z" />
  </svg>;
}

export default function LoginScreen({ onLogin, onDemo }) {
  const [identifier, setIdentifier] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  async function submit(event) {
    event.preventDefault();
    setBusy(true);
    setError('');
    try {
      onLogin(await login({ identifier: identifier.trim(), password }));
    } catch (failure) {
      if (failure.name !== 'AbortError') setError(failure.message);
    } finally {
      setBusy(false);
    }
  }

  return <div className="login-page">
    <header className="login-header">
      <a className="login-brand" href="#/login">
        <LeafMark />
        <strong>Nông Sản Việt</strong>
      </a>
      <span className="login-header-divider" aria-hidden="true" />
      <p>Nền tảng nông sản Việt – Kết nối nông dân, bền vững tương lai</p>
    </header>
    <main className="login-main">
      <DecorativeBranch className="login-branch-left" />
      <DecorativeBranch className="login-branch-right" />
      <section className="login-card">
        <h1>Đăng nhập</h1>
        <p className="login-subtitle">Chào mừng bạn đến với Nông Sản Việt</p>
        <Notice error>{error}</Notice>
        <form onSubmit={submit} className="login-form">
          <label className="field"><span>Email hoặc tên đăng nhập</span><input
            autoComplete="username" autoFocus required maxLength={255} value={identifier}
            onChange={(event) => setIdentifier(event.target.value)}
            placeholder="Nhập email hoặc tên đăng nhập" /></label>
          <label className="field"><span>Mật khẩu</span><span className="password-control">
            <input type={showPassword ? 'text' : 'password'} autoComplete="current-password"
              required maxLength={500} value={password} onChange={(event) => setPassword(event.target.value)}
              placeholder="Nhập mật khẩu" />
            <button type="button" className="password-toggle" aria-label={showPassword
              ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'} onClick={() => setShowPassword((value) => !value)}>
              <svg viewBox="0 0 24 24" aria-hidden="true">
                <path d="M2.5 12s3.3-6 9.5-6 9.5 6 9.5 6-3.3 6-9.5 6-9.5-6-9.5-6Z" />
                <circle cx="12" cy="12" r="2.5" />
                {showPassword && <path d="m4 4 16 16" />}
              </svg>
            </button></span></label>
          <button className="primary login-submit" type="submit" disabled={busy}>
            {busy ? 'Đang đăng nhập…' : 'Đăng nhập'}
          </button>
        </form>
        <div style={{ marginTop: 15, textAlign: 'center', fontSize: 14 }}>
          <span>Chưa có tài khoản? </span>
          <a href="#/register" style={{ color: '#2e7d32', fontWeight: 600, textDecoration: 'none' }}>
            Đăng ký ngay
          </a>
        </div>
        <button className="login-demo" type="button" onClick={onDemo}>
          Tiếp tục khám phá sản phẩm
        </button>
      </section>
    </main>
  </div>;
}
