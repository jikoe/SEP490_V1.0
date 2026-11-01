/* ==========================================================================
   AgriSustain - Bank Account panel (js/bank-account.js)
   UC-AUTH-03 Manage Profile – "Tài khoản ngân hàng" tab (BR-BA-01..06).
   Farmer: account that receives payouts. RetailCustomer / WholesaleBuyer: default refund account.
   Flow: choose bank → type number → lookup after 600 ms → tick confirmation → e-mail OTP → save.
   Usage: BankAccountPanel.mount(document.getElementById('bank-panel'));
   ========================================================================== */

const BankAccountPanel = (() => {
  const LOOKUP_DELAY_MS = 600;
  const RESEND_SECONDS = 60;
  // MSG-BA-04
  const NOT_FOUND = 'Không tìm thấy tài khoản này tại ngân hàng đã chọn. Vui lòng kiểm tra lại ngân hàng và số tài khoản.';
  const BADGE = {
    Verified: '<span class="badge Active">Đã xác minh</span>',
    Unverified: '<span class="badge Pending">Chưa xác minh</span>'
  };

  let root = null;
  let overview = null;
  let editing = false;
  let form = emptyForm();
  let lookup = { status: 'idle', holder: '' };
  let errors = {};
  let otpFor = null; // 'save' | 'remove'
  let resendIn = 0;
  let resendTimer = null;
  let lookupTimer = null;
  let lookupSeq = 0;
  let busy = false;

  function emptyForm() {
    return { bankCode: '', accountNumber: '', accountHolder: '', confirmRecipient: false };
  }

  const esc = (v) => Utils.escapeHtml(v == null ? '' : String(v));

  function formatTime(value) {
    if (!value) return '';
    const text = String(value);
    return new Date(/[zZ]|[+-]\d\d:\d\d$/.test(text) ? text : `${text}Z`).toLocaleString('vi-VN');
  }

  function firstError(fields, key) {
    const value = fields?.[key];
    return Array.isArray(value) ? value[0] : value;
  }

  function fieldsOf(res) {
    return (res && res.data && typeof res.data === 'object' && res.data.errors) || {};
  }

  async function load() {
    root.innerHTML = '<p class="muted">Đang tải tài khoản ngân hàng…</p>';
    const res = await API.request('/profile/bank-account', { suppressErrorToast: true });
    if (!res.success) {
      root.innerHTML = `<div class="notice error">${esc(res.error || 'Không tải được tài khoản ngân hàng.')}</div>
        <button type="button" id="ba-retry">Thử lại</button>`;
      root.querySelector('#ba-retry').onclick = load;
      return;
    }
    overview = res.data;
    render();
  }

  function unavailable() { return lookup.status === 'Unavailable'; }

  function canSubmit() {
    return editing && form.confirmRecipient && !busy
      && (lookup.status === 'Found' || (unavailable() && form.accountHolder.trim().length > 0));
  }

  function render() {
    const account = overview.account;
    const isFarmer = overview.role === 'Farmer';
    const holdActive = account?.payoutHoldUntil
      && new Date(String(account.payoutHoldUntil).endsWith('Z') ? account.payoutHoldUntil : `${account.payoutHoldUntil}Z`) > new Date();

    const card = account ? `
      <div style="border:1px solid #d7e5dc; border-radius:12px; padding:20px; background:#f7fbf8; margin-bottom:20px;">
        <div style="display:flex; justify-content:space-between; align-items:center; gap:12px; flex-wrap:wrap;">
          <div>
            <div style="font-weight:700; color:#1b4332; font-size:16px;">
              ${esc(account.bankShortName)} <span style="letter-spacing:2px;">${esc(account.maskedAccountNumber)}</span>
            </div>
            <div style="color:#52695c; font-size:13px; margin-top:4px;">${esc(account.bankFullName)}</div>
            <div style="margin-top:8px; font-size:14px;">Chủ tài khoản: <strong>${esc(account.accountHolder)}</strong></div>
          </div>
          ${BADGE[account.verificationStatus] || esc(account.verificationStatus)}
        </div>
        ${account.verificationStatus === 'Unverified' ? `<div style="margin-top:12px; font-size:13px; color:#92400e;">
          Tài khoản chưa được ngân hàng xác minh${isFarmer ? ' nên chưa thể nhận tiền thanh toán. Vui lòng đổi lại tài khoản khi dịch vụ tra cứu hoạt động.' : '.'}</div>` : ''}
        ${holdActive ? `<div style="margin-top:12px; font-size:13px; color:#92400e;">
          Thanh toán đang tạm giữ đến ${esc(formatTime(account.payoutHoldUntil))} do vừa đổi tài khoản (48 giờ).</div>` : ''}
        ${account.updatedAt ? `<div style="margin-top:8px; font-size:12px; color:#7a8f83;">Cập nhật: ${esc(formatTime(account.updatedAt))}</div>` : ''}
      </div>` : `
      <div class="notice">${isFarmer
        ? 'Bạn chưa có tài khoản nhận tiền. Nông trại cần có tài khoản này trước khi gửi thẩm định KYC.'
        : 'Bạn chưa lưu tài khoản ngân hàng nào.'}</div>`;

    const buttons = editing ? '' : `
      <div style="display:flex; gap:12px; margin-top:12px;">
        <button type="button" class="primary" id="ba-edit">${account ? 'Đổi tài khoản' : 'Thêm tài khoản'}</button>
        ${account && overview.canRemove ? '<button type="button" id="ba-remove" style="color:#b91c1c; border-color:#fca5a5;">Xóa tài khoản</button>' : ''}
      </div>`;

    root.innerHTML = `
      <h3 style="margin:0 0 6px; color:#103e2d;">Tài khoản ngân hàng</h3>
      <p class="muted" style="margin:0 0 20px; font-size:14px;">${esc(overview.purpose)}</p>
      ${card}
      ${buttons}
      ${editing ? formHtml() : ''}
      ${dialogsHtml()}`;
    bind();
  }

  function formHtml() {
    const banks = (overview.banks || []).map((b) =>
      `<option value="${esc(b.bankCode)}" ${b.bankCode === form.bankCode ? 'selected' : ''}>${esc(b.shortName)} – ${esc(b.fullName)}</option>`).join('');
    const numberError = errors.accountNumber || (lookup.status === 'NotFound' ? NOT_FOUND : '');
    const hint = lookup.status === 'checking' ? 'Đang tra cứu tài khoản tại ngân hàng…' : '6–19 chữ số';
    const isFarmer = overview.role === 'Farmer';
    return `
      <form id="ba-form" novalidate style="margin-top:8px; display:grid; gap:4px; max-width:520px;">
        <label class="field"><span>Ngân hàng *</span>
          <select id="ba-bank" ${busy ? 'disabled' : ''}><option value="">-- Chọn ngân hàng --</option>${banks}</select>
          ${err(errors.bankCode)}</label>
        <label class="field"><span>Số tài khoản *</span>
          <input id="ba-number" inputmode="numeric" autocomplete="off" maxlength="19" placeholder="Nhập số tài khoản"
            value="${esc(form.accountNumber)}" ${busy ? 'disabled' : ''}>
          <small id="ba-hint">${esc(hint)}</small>${err(numberError)}</label>
        ${lookup.status === 'Found' ? `<label class="field"><span>Chủ tài khoản</span>
          <input value="${esc(lookup.holder)}" readonly style="background:#f1f5f2; font-weight:700;">
          <small>Tên do ngân hàng trả về, không thể sửa.</small></label>` : ''}
        ${unavailable() ? `<div class="notice">Hiện không tra cứu được ngân hàng. Bạn vẫn có thể lưu tài khoản ở trạng thái
            <strong>Chưa xác minh</strong>${isFarmer ? '; tài khoản này chưa nhận được tiền thanh toán cho đến khi được xác minh.' : '.'}</div>
          <label class="field"><span>Tên chủ tài khoản *</span>
            <input id="ba-holder" maxlength="100" placeholder="VD: NGUYEN VAN A" value="${esc(form.accountHolder)}" ${busy ? 'disabled' : ''}>
            ${err(errors.accountHolder)}</label>` : ''}
        ${lookup.status === 'Found' || unavailable() ? `<label style="display:flex; gap:8px; align-items:flex-start; font-size:14px; cursor:pointer; margin-bottom:12px;">
            <input type="checkbox" id="ba-confirm" ${form.confirmRecipient ? 'checked' : ''} style="margin-top:3px;" ${busy ? 'disabled' : ''}>
            Tôi xác nhận đây là đúng tài khoản nhận tiền của tôi.</label>${err(errors.confirmRecipient)}` : ''}
        <div style="display:flex; gap:12px;">
          <button type="submit" class="primary" id="ba-submit" ${canSubmit() ? '' : 'disabled'}>${busy ? 'Đang gửi mã…' : 'Lưu tài khoản'}</button>
          <button type="button" id="ba-cancel" ${busy ? 'disabled' : ''}>Hủy</button>
        </div>
      </form>`;
  }

  function err(message) {
    return message ? `<small style="color:#b3261e; font-size:12px;">${esc(message)}</small>` : '';
  }

  function dialogsHtml() {
    const account = overview.account;
    return `
      <dialog id="ba-remove-dialog">
        <div class="modal-heading"><h2>Xóa tài khoản ngân hàng</h2>
          <button type="button" data-close="ba-remove-dialog">&times;</button></div>
        <p>Xóa tài khoản ${esc(account?.bankShortName)} ${esc(account?.maskedAccountNumber)}? Các yêu cầu hoàn tiền
          đã gửi vẫn giữ tài khoản bạn đã xác nhận.</p>
        <div class="modal-actions">
          <button type="button" data-close="ba-remove-dialog">Không</button>
          <button type="button" class="primary" id="ba-remove-otp">Gửi mã OTP để xóa</button>
        </div>
      </dialog>
      <dialog id="ba-otp-dialog">
        <div class="modal-heading"><h2>Xác nhận bằng mã OTP</h2>
          <button type="button" data-close="ba-otp-dialog">&times;</button></div>
        <form id="ba-otp-form" novalidate style="display:grid; gap:8px; margin-top:12px;">
          <p id="ba-otp-info" style="margin:0; font-size:14px; color:#3f5a4b;"></p>
          <label class="field"><span>Mã OTP (6 chữ số)</span>
            <input id="ba-otp" inputmode="numeric" autocomplete="one-time-code" maxlength="6">
            <small id="ba-otp-error" style="color:#b3261e; font-size:12px;"></small></label>
          <div style="display:flex; justify-content:space-between; align-items:center; gap:12px;">
            <button type="button" class="text-button" id="ba-resend"></button>
            <button type="submit" class="primary" id="ba-otp-submit"></button>
          </div>
        </form>
      </dialog>`;
  }

  function bind() {
    const $ = (id) => root.querySelector(`#${id}`);
    root.querySelectorAll('[data-close]').forEach((b) => {
      b.onclick = () => { if (!busy) $(b.dataset.close).close(); };
    });
    if ($('ba-edit')) $('ba-edit').onclick = startEdit;
    if ($('ba-remove')) $('ba-remove').onclick = () => $('ba-remove-dialog').showModal();
    $('ba-remove-otp').onclick = async () => { $('ba-remove-dialog').close(); await sendOtp('remove'); };
    $('ba-otp-form').onsubmit = confirmOtp;
    $('ba-resend').onclick = () => sendOtp(otpFor);
    $('ba-otp').oninput = (e) => { e.target.value = e.target.value.replace(/\D/g, ''); $('ba-otp-error').textContent = ''; };
    if (!editing) return;

    $('ba-bank').onchange = (e) => { form.bankCode = e.target.value; errors.bankCode = ''; scheduleLookup(); };
    $('ba-number').oninput = (e) => {
      e.target.value = e.target.value.replace(/\D/g, '');
      form.accountNumber = e.target.value;
      errors.accountNumber = '';
      scheduleLookup();
    };
    if ($('ba-holder')) $('ba-holder').oninput = (e) => { form.accountHolder = e.target.value; refreshSubmit(); };
    if ($('ba-confirm')) $('ba-confirm').onchange = (e) => { form.confirmRecipient = e.target.checked; refreshSubmit(); };
    $('ba-form').onsubmit = handleSave;
    $('ba-cancel').onclick = () => { editing = false; form = emptyForm(); errors = {}; render(); };
  }

  function refreshSubmit() {
    const btn = root.querySelector('#ba-submit');
    if (btn) btn.disabled = !canSubmit();
  }

  function startEdit() {
    form = { ...emptyForm(), bankCode: overview?.account?.bankCode || '' };
    lookup = { status: 'idle', holder: '' };
    errors = {};
    editing = true;
    render();
  }

  // Step 3: look the account up about 600 ms after the user stops typing.
  function scheduleLookup() {
    clearTimeout(lookupTimer);
    form.confirmRecipient = false;
    const { bankCode, accountNumber } = form;
    const seq = ++lookupSeq;
    if (!bankCode || !/^\d{6,19}$/.test(accountNumber)) {
      setLookup({ status: 'idle', holder: '' });
      return;
    }
    setLookup({ status: 'checking', holder: '' });
    lookupTimer = setTimeout(async () => {
      const res = await API.request('/profile/bank-account/lookup', {
        method: 'POST', body: { bankCode, accountNumber }, suppressErrorToast: true
      });
      if (seq !== lookupSeq) return;
      if (res.success) {
        setLookup({ status: res.data.status, holder: res.data.accountHolder || '' });
      } else {
        errors = { ...errors, ...flatten(fieldsOf(res)) };
        setLookup({ status: 'error', holder: '' });
      }
    }, LOOKUP_DELAY_MS);
  }

  // Re-render the form but keep the caret in the field being typed in.
  function setLookup(next) {
    const changed = next.status !== lookup.status || next.holder !== lookup.holder;
    lookup = next;
    if (!changed) { refreshSubmit(); return; }
    const active = document.activeElement?.id;
    const pos = document.activeElement?.selectionStart;
    render();
    if (active) {
      const el = root.querySelector(`#${active}`);
      if (el) { el.focus(); if (pos != null && el.setSelectionRange) el.setSelectionRange(pos, pos); }
    }
  }

  function flatten(fields) {
    return Object.fromEntries(Object.keys(fields).map((k) => [k, firstError(fields, k)]));
  }

  async function sendOtp(purpose) {
    busy = true;
    const res = await API.request('/profile/bank-account/otp', { method: 'POST', suppressErrorToast: true });
    busy = false;
    if (!res.success) {
      Utils.showToast(res.error || 'Không gửi được mã OTP.', 'error');
      return;
    }
    otpFor = purpose;
    const data = res.data;
    const message = typeof data === 'string' ? data : (data && data.message) || 'Mã OTP đã được gửi tới email của bạn.';
    const dlg = root.querySelector('#ba-otp-dialog');
    root.querySelector('#ba-otp-info').textContent = message;
    root.querySelector('#ba-otp').value = '';
    root.querySelector('#ba-otp-error').textContent = '';
    root.querySelector('#ba-otp-submit').textContent = purpose === 'remove' ? 'Xác nhận xóa' : 'Xác nhận lưu';
    if (!dlg.open) dlg.showModal();
    startResendCountdown();
  }

  function startResendCountdown() {
    resendIn = RESEND_SECONDS;
    clearInterval(resendTimer);
    const tick = () => {
      const btn = root.querySelector('#ba-resend');
      if (!btn) { clearInterval(resendTimer); return; }
      btn.disabled = resendIn > 0;
      btn.textContent = resendIn > 0 ? `Gửi lại mã sau ${resendIn}s` : 'Gửi lại mã';
      if (resendIn <= 0) clearInterval(resendTimer);
      resendIn -= 1;
    };
    tick();
    resendTimer = setInterval(tick, 1000);
  }

  // Step 5: validate locally, then send the OTP.
  async function handleSave(event) {
    event.preventDefault();
    const e = {};
    if (!form.bankCode) e.bankCode = 'Trường này là bắt buộc.';
    if (!form.accountNumber) e.accountNumber = 'Trường này là bắt buộc.';
    else if (!/^\d{6,19}$/.test(form.accountNumber)) {
      e.accountNumber = 'Vui lòng sửa Số tài khoản: số tài khoản chỉ gồm chữ số, dài từ 6 đến 19 ký tự.';
    }
    if (unavailable() && !form.accountHolder.trim()) e.accountHolder = 'Trường này là bắt buộc.';
    if (!form.confirmRecipient) e.confirmRecipient = 'Vui lòng xác nhận đây là đúng tài khoản nhận tiền.';
    errors = e;
    if (Object.keys(e).length || !canSubmit()) { render(); return; }
    await sendOtp('save');
  }

  async function confirmOtp(event) {
    event.preventDefault();
    const otp = root.querySelector('#ba-otp').value;
    const otpError = root.querySelector('#ba-otp-error');
    if (!/^\d{6}$/.test(otp)) { otpError.textContent = 'Mã OTP gồm 6 chữ số.'; return; }
    busy = true;
    const submit = root.querySelector('#ba-otp-submit');
    submit.disabled = true;
    submit.textContent = 'Đang xác nhận…';
    const res = otpFor === 'remove'
      ? await API.request('/profile/bank-account/remove', { method: 'POST', body: { otpCode: otp }, suppressErrorToast: true })
      : await API.request('/profile/bank-account', {
        method: 'PUT',
        suppressErrorToast: true,
        body: {
          bankCode: form.bankCode,
          accountNumber: form.accountNumber,
          accountHolder: unavailable() ? form.accountHolder.trim() : null,
          confirmRecipient: form.confirmRecipient,
          otpCode: otp
        }
      });
    busy = false;
    submit.disabled = false;
    submit.textContent = otpFor === 'remove' ? 'Xác nhận xóa' : 'Xác nhận lưu';

    if (res.success) {
      root.querySelector('#ba-otp-dialog').close();
      overview = res.data.overview || overview;
      Utils.showToast(res.data.message || 'Đã lưu tài khoản ngân hàng.', 'success');
      otpFor = null;
      editing = false;
      form = emptyForm();
      render();
      return;
    }
    const fields = fieldsOf(res);
    const code = (res.data && res.data.code) || res.code || '';
    if (firstError(fields, 'otpCode') || /OTP/i.test(code)) {
      otpError.textContent = firstError(fields, 'otpCode') || res.error;
    } else {
      // The account itself was rejected: close the OTP box and show it on the form.
      root.querySelector('#ba-otp-dialog').close();
      otpFor = null;
      errors = flatten(fields);
      Utils.showToast(res.error || 'Không lưu được tài khoản.', 'error');
      render();
    }
  }

  return {
    mount(element) {
      root = element;
      editing = false;
      form = emptyForm();
      errors = {};
      load();
    },
    reload: () => root && load()
  };
})();
