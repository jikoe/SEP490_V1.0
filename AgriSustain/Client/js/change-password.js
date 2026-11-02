import { cleanMessage } from './page-ui.js';

if (Auth.requireAuth()) {
  const form = document.getElementById('password-form');
  const fields = ['currentPassword', 'newPassword', 'confirmPassword'];
  const notice = document.getElementById('password-notice');
  const button = document.getElementById('password-submit');
  let busy = false;
  document.getElementById('password-account').textContent = Auth.getUserInfo()?.email || Auth.getUserInfo()?.username || '';
  document.getElementById('password-back').href = Auth.getLandingPageForRole(Auth.getUserRole());

  function setError(name, message = '') {
    document.getElementById(`${name}-error`).textContent = cleanMessage(message);
    form.elements[name].setAttribute('aria-invalid', String(Boolean(message)));
  }
  form.addEventListener('input', event => {
    if (fields.includes(event.target.name)) setError(event.target.name);
    notice.hidden = true;
  });
  document.getElementById('password-reveal').addEventListener('change', event => {
    fields.forEach(name => { form.elements[name].type = event.target.checked ? 'text' : 'password'; });
  });
  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (busy) return;
    notice.hidden = true;
    fields.forEach(name => setError(name));
    const body = Object.fromEntries(fields.map(name => [name, form.elements[name].value]));
    const errors = {};
    if (!body.currentPassword) errors.currentPassword = 'Vui lòng nhập mật khẩu hiện tại.';
    if (!body.newPassword || body.newPassword.length < 8 || !/[A-Z]/.test(body.newPassword)
        || !/[a-z]/.test(body.newPassword) || !/[0-9]/.test(body.newPassword)) {
      errors.newPassword = 'Mật khẩu mới cần ít nhất 8 ký tự, gồm chữ hoa, chữ thường và chữ số.';
    } else if (body.newPassword === body.currentPassword) {
      errors.newPassword = 'Mật khẩu mới không được trùng với mật khẩu hiện tại.';
    }
    if (!body.confirmPassword || body.confirmPassword !== body.newPassword) {
      errors.confirmPassword = 'Mật khẩu xác nhận không khớp.';
    }
    Object.entries(errors).forEach(([name, message]) => setError(name, message));
    if (Object.keys(errors).length) {
      form.elements[Object.keys(errors)[0]].focus();
      return;
    }
    busy = true;
    document.getElementById('password-fields').disabled = true;
    button.textContent = 'Đang cập nhật…';
    try {
      const result = await API.changePassword(body);
      if (result.success) {
        form.reset();
        fields.forEach(name => { form.elements[name].type = 'password'; });
        notice.className = 'notice';
        notice.textContent = 'Đổi mật khẩu thành công. Vui lòng sử dụng mật khẩu mới cho những lần đăng nhập sau.';
      } else {
        for (const [key, value] of Object.entries(result.data?.errors || {})) {
          const name = key.charAt(0).toLowerCase() + key.slice(1);
          if (fields.includes(name)) setError(name, Array.isArray(value) ? value.join(' ') : value);
        }
        notice.className = 'notice error';
        notice.textContent = cleanMessage(result.error || 'Không thể đổi mật khẩu. Vui lòng thử lại.');
      }
      notice.hidden = false;
    } finally {
      busy = false;
      document.getElementById('password-fields').disabled = false;
      button.textContent = 'Đổi mật khẩu';
    }
  });
}
