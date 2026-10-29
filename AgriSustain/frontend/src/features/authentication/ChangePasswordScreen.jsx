import React, { useEffect, useState } from 'react';
import { request } from '../../api/client';
import { Field, Notice } from '../../components/ui/Common';

const emptyForm = { currentPassword: '', newPassword: '', confirmPassword: '' };
const successMessage = 'Đổi mật khẩu thành công. Vui lòng sử dụng mật khẩu mới cho những lần đăng nhập sau.';
const failureMessage = 'Đã xảy ra lỗi khi đổi mật khẩu. Vui lòng thử lại sau.';
const passwordFields = [
  ['currentPassword', 'Mật khẩu hiện tại *', 'Vui lòng nhập mật khẩu hiện tại.'],
  ['newPassword', 'Mật khẩu mới *', 'Vui lòng nhập mật khẩu mới.'],
  ['confirmPassword', 'Xác nhận mật khẩu mới *', 'Vui lòng xác nhận mật khẩu mới.'],
];

/** Mirrors the server password policy so mistakes show before the request is sent. */
function validate(form) {
  const errors = {};
  for (const [name, , requiredMessage] of passwordFields) {
    if (!form[name]) errors[name] = requiredMessage;
  }
  if (!errors.newPassword && (form.newPassword.length < 8 || !/[A-Z]/.test(form.newPassword)
    || !/[a-z]/.test(form.newPassword) || !/[0-9]/.test(form.newPassword))) {
    errors.newPassword = 'Mật khẩu mới không đáp ứng các tiêu chí bảo mật bắt buộc.';
  }
  if (!errors.newPassword && form.newPassword === form.currentPassword) {
    errors.newPassword = 'Mật khẩu mới không được trùng với mật khẩu hiện tại.';
  }
  if (!errors.confirmPassword && form.newPassword !== form.confirmPassword) {
    errors.confirmPassword = 'Mật khẩu xác nhận không khớp.';
  }
  return errors;
}

/** UC-AUTH-06: Change Password for the signed-in account (any role). */
export default function ChangePasswordScreen({ session, navigate, backRoute }) {
  const [form, setForm] = useState(emptyForm);
  const [showPasswords, setShowPasswords] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [fieldErrors, setFieldErrors] = useState({});
  const [message, setMessage] = useState('');
  const [toastError, setToastError] = useState(false);

  useEffect(() => {
    if (!message) return undefined;
    const timer = setTimeout(() => setMessage(''), 8000);
    return () => clearTimeout(timer);
  }, [message]);

  function update(name, value) {
    setForm((old) => ({ ...old, [name]: value }));
    setFieldErrors(old => ({ ...old, [name]: '' }));
    setError(''); setMessage('');
  }

  async function submit(event) {
    event.preventDefault();
    const problem = validate(form);
    setFieldErrors(problem); setError(''); setMessage(''); setToastError(false);
    if (Object.keys(problem).length) return;
    setBusy(true);
    try {
      await request('/auth/change-password', { method: 'POST', body: form });
      setMessage(successMessage);
      setForm(emptyForm);
    } catch (failure) {
      const fields = {};
      for (const [name] of passwordFields) {
        const messages = failure.fields?.[name] || failure.fields?.[name[0].toUpperCase() + name.slice(1)];
        if (messages?.length) fields[name] = messages.join(' ');
      }
      if (failure.code === 'INVALID_CURRENT_PASSWORD') {
        fields.currentPassword = 'Mật khẩu hiện tại bạn nhập không chính xác. Vui lòng thử lại.';
      }
      if (!failure.status || failure.status >= 500 || failure.status === 409
        || failure.code === 'PASSWORD_UPDATE_FAILED') {
        setToastError(true); setMessage(failureMessage);
      } else if (Object.keys(fields).length) setFieldErrors(fields);
      else setError(failure.message);
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
    <h1>Đổi mật khẩu</h1>
    <p className="muted">Tài khoản: <strong>{session.user.email}</strong></p>
    <Notice error>{error}</Notice>
    {message && <div style={{ position: 'fixed', top: 24, right: 24, zIndex: 1000,
      width: 'min(420px, calc(100vw - 48px))' }}>
      <Notice error={toastError}><span>{message}</span><button type="button" aria-label="Đóng thông báo"
        style={{ float: 'right', marginLeft: 8 }} onClick={() => setMessage('')}>×</button></Notice>
    </div>}
    <form onSubmit={submit} noValidate>
      <fieldset disabled={busy} style={{ border: 0, padding: 0, margin: 0 }}>
        {passwordFields.map(([name, label]) => <div key={name}>
          <Field label={label}><input type={type}
            autoComplete={name === 'currentPassword' ? 'current-password' : 'new-password'}
            required maxLength={500} value={form[name]} aria-invalid={Boolean(fieldErrors[name])}
            aria-describedby={fieldErrors[name] ? `change-password-${name}-error` : undefined}
            onChange={event => update(name, event.target.value)} /></Field>
          {fieldErrors[name] && <p id={`change-password-${name}-error`} role="alert"
            style={{ color: '#a0362c', fontSize: 13, margin: '4px 0 12px' }}>{fieldErrors[name]}</p>}
        </div>)}
        <label style={{ display: 'flex', gap: 8, alignItems: 'center', fontSize: 13, margin: '4px 0 16px' }}>
          <input type="checkbox" checked={showPasswords}
            onChange={(event) => setShowPasswords(event.target.checked)} />Hiện mật khẩu
        </label>
        <p className="muted" style={{ fontSize: 12 }}>
          Mật khẩu mới cần ít nhất 8 ký tự, gồm chữ hoa, chữ thường và chữ số.
        </p>
        <div style={{ display: 'flex', gap: 12, justifyContent: 'flex-end' }}>
          <button type="button" onClick={() => {
            setForm(emptyForm); setFieldErrors({}); setError(''); setMessage(''); setShowPasswords(false);
            navigate(backRoute || '/marketplace');
          }}>Hủy</button>
          <button type="submit" className="primary">{busy ? 'Đang cập nhật…' : 'Đổi mật khẩu'}</button>
        </div>
      </fieldset>
    </form>
  </section>;
}
