import React, { useEffect, useState } from 'react';
import { request } from '../../api/client';
import { Field, Notice } from '../../components/ui/Common';

const emptyForm = { currentPassword: '', newPassword: '', confirmPassword: '' };
const successMessage = 'Đổi mật khẩu thành công. Vui lòng sử dụng mật khẩu mới cho những lần đăng nhập sau.';

/** Mirrors the server password policy so mistakes show before the request is sent. */
function validate(form) {
  if (!form.currentPassword) return 'Vui lòng nhập mật khẩu hiện tại.';
  if (form.newPassword.length < 8 || !/[A-Z]/.test(form.newPassword)
    || !/[a-z]/.test(form.newPassword) || !/[0-9]/.test(form.newPassword)) {
    return 'Mật khẩu mới cần ít nhất 8 ký tự, gồm chữ hoa, chữ thường và chữ số.';
  }
  if (form.newPassword === form.currentPassword) return 'Mật khẩu mới phải khác mật khẩu hiện tại.';
  if (form.newPassword !== form.confirmPassword) return 'Mật khẩu xác nhận không khớp.';
  return '';
}

/** UC-AUTH-06: Change Password for the signed-in account (any role). */
export default function ChangePasswordScreen({ session, navigate, backRoute }) {
  const [form, setForm] = useState(emptyForm);
  const [showPasswords, setShowPasswords] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  useEffect(() => {
    if (!message) return undefined;
    const timer = setTimeout(() => setMessage(''), 8000);
    return () => clearTimeout(timer);
  }, [message]);

  function update(name, value) {
    setForm((old) => ({ ...old, [name]: value }));
    setError(''); setMessage('');
  }

  async function submit(event) {
    event.preventDefault();
    const problem = validate(form);
    if (problem) { setError(problem); return; }
    setBusy(true); setError(''); setMessage('');
    try {
      await request('/auth/change-password', { method: 'POST', body: form });
      setMessage(successMessage);
      setForm(emptyForm);
    } catch (failure) {
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  }

  if (!session) {
    return <section className="panel padded" style={{ maxWidth: 560, margin: '0 auto' }}>
      <h1>Đổi mật khẩu</h1>
      <Notice error>Vui lòng đăng nhập bằng tài khoản thật để đổi mật khẩu.</Notice>
      <button type="button" className="primary" onClick={() => navigate('/login')}>Đăng nhập</button>
    </section>;
  }

  const type = showPasswords ? 'text' : 'password';
  return <section className="panel padded" style={{ maxWidth: 560, margin: '0 auto' }}>
    <p className="muted" style={{ margin: 0 }}>Tài khoản · UC-AUTH-06</p>
    <h1>Đổi mật khẩu</h1>
    <p className="muted">Tài khoản: <strong>{session.user.email}</strong></p>
    <Notice error>{error}</Notice>
    {message && <div style={{ position: 'fixed', top: 24, right: 24, zIndex: 1000,
      width: 'min(420px, calc(100vw - 48px))' }}>
      <Notice><span>{message}</span><button type="button" aria-label="Đóng thông báo"
        style={{ float: 'right', marginLeft: 8 }} onClick={() => setMessage('')}>×</button></Notice>
    </div>}
    <form onSubmit={submit}>
      <fieldset disabled={busy} style={{ border: 0, padding: 0, margin: 0 }}>
        <Field label="Mật khẩu hiện tại *"><input type={type} autoComplete="current-password"
          required maxLength={500} value={form.currentPassword}
          onChange={(event) => update('currentPassword', event.target.value)} /></Field>
        <Field label="Mật khẩu mới *"><input type={type} autoComplete="new-password"
          required maxLength={500} value={form.newPassword}
          onChange={(event) => update('newPassword', event.target.value)} /></Field>
        <Field label="Xác nhận mật khẩu mới *"><input type={type} autoComplete="new-password"
          required maxLength={500} value={form.confirmPassword}
          onChange={(event) => update('confirmPassword', event.target.value)} /></Field>
        <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, margin: '4px 0 16px' }}>
          <input type="checkbox" checked={showPasswords}
            onChange={(event) => setShowPasswords(event.target.checked)} />Hiện mật khẩu
        </label>
        <p className="muted" style={{ fontSize: 12 }}>
          Mật khẩu mới cần ít nhất 8 ký tự, gồm chữ hoa, chữ thường và chữ số.
        </p>
        <div style={{ display: 'flex', gap: 12, justifyContent: 'flex-end' }}>
          {backRoute && <button type="button" onClick={() => navigate(backRoute)}>Quay lại</button>}
          <button type="submit" className="primary">{busy ? 'Đang cập nhật…' : 'Đổi mật khẩu'}</button>
        </div>
      </fieldset>
    </form>
  </section>;
}
