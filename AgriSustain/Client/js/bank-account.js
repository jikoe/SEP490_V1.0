/* ==========================================================================
   AgriSustain - Bank Account panel (js/bank-account.js)
   UC-AUTH-03 Manage Profile – "Tài khoản ngân hàng" (BR-BA-01..06), layout of Figma page UC-AUTH-03.
   Farmer: account that receives payouts. RetailCustomer / WholesaleBuyer: default refund account.
   Flow: choose bank → type number → lookup after 600 ms (or "Kiểm tra") → tick confirmation
         → 6-digit e-mail OTP → save. Needs css/account.css.
   Usage: BankAccountPanel.mount(document.getElementById('bank-panel'));
   ========================================================================== */

const BankAccountPanel = (() => {
  const LOOKUP_DELAY_MS = 600;
  const RESEND_SECONDS = 60;
  // MSG-BA-04
  const NOT_FOUND = 'Không tìm thấy tài khoản này tại ngân hàng đã chọn. Vui lòng kiểm tra lại ngân hàng và số tài khoản.';

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
  const isFarmer = () => overview?.role === 'Farmer';
  const unavailable = () => lookup.status === 'Unavailable';
  const holderName = () => (lookup.status === 'Found' ? lookup.holder : form.accountHolder.trim());

  function formatTime(value) {
    if (!value) return '';
    const text = String(value);
    return new Date(/[zZ]|[+-]\d\d:\d\d$/.test(text) ? text : `${text}Z`).toLocaleString('vi-VN');
  }

  function firstError(fields, key) {
    const value = fields?.[key];
    return Array.isArray(value) ? value[0] : value;
  }

  const fieldsOf = (res) => (res && res.data && typeof res.data === 'object' && res.data.errors) || {};
  const flatten = (fields) => Object.fromEntries(Object.keys(fields).map((k) => [k, firstError(fields, k)]));
  const err = (message) => (message ? `<small class="acc-err">${esc(message)}</small>` : '');

  async function load() {
    root.innerHTML = '<p class="muted">Đang tải tài khoản ngân hàng…</p>';
    const res = await API.request('/profile/bank-account', { suppressErrorToast: true });
    if (!res.success) {
      root.innerHTML = `<div class="notice error">${esc(res.error || 'Không tải được tài khoản ngân hàng.')}</div>
        <button type="button" class="acc-btn" id="ba-retry">Thử lại</button>`;
      root.querySelector('#ba-retry').onclick = load;
      return;
    }
    overview = res.data;
    render();
  }

  function canSubmit() {
    return editing && form.confirmRecipient && !busy
      && (lookup.status === 'Found' || (unavailable() && form.accountHolder.trim().length > 0));
  }

  function render() {
    root.innerHTML = (editing ? formHtml() : viewHtml()) + dialogsHtml();
    bind();
  }

  // (1) purpose, (2) change, (8) remove – Figma frames A0/A/C0/C.
  function viewHtml() {
    const account = overview.account;
    const farmer = isFarmer();
    const title = farmer ? 'Tài khoản nhận quyết toán' : 'Tài khoản nhận hoàn tiền';
    const holdActive = account?.payoutHoldUntil
      && new Date(String(account.payoutHoldUntil).endsWith('Z') ? account.payoutHoldUntil : `${account.payoutHoldUntil}Z`) > new Date();

    if (!account) {
      return `
        <h2>${title}</h2>
        <p class="acc-desc">${esc(overview.purpose)}</p>
        ${farmer ? '<div class="acc-warn">⚠ Bạn cần thêm tài khoản nhận quyết toán trước khi gửi thẩm định KYC và nhận tiền bán hàng.</div>' : ''}
        <div class="acc-empty">
          <span class="acc-empty-icon">🏛</span>
          <strong>${farmer ? 'Chưa có tài khoản nhận quyết toán' : 'Chưa có tài khoản ngân hàng'}</strong>
          <p>${farmer
            ? 'Thêm tài khoản ngân hàng để AgriSustain chuyển tiền quyết toán đơn hàng của nông trại.'
            : 'Lưu một tài khoản để được điền sẵn khi nhận tiền hoàn. Bạn vẫn có thể nhập tài khoản khác cho từng yêu cầu hoàn tiền.'}</p>
          <button type="button" class="acc-btn primary" id="ba-edit">+ Thêm tài khoản ngân hàng</button>
        </div>`;
    }

    const verified = account.verificationStatus === 'Verified';
    const badge = verified
      ? `<span class="bank-badge">✓ Đã xác minh${farmer ? ' · nhận quyết toán' : ''}</span>`
      : `<span class="bank-badge warn">Chưa xác minh${farmer ? ' · chưa nhận được tiền' : ''}</span>`;
    return `
      <h2>${title}</h2>
      <p class="acc-desc">${esc(overview.purpose)}</p>
      <div class="bank-card">
        <div class="bank-icon">🏛</div>
        <div style="flex:1;">
          <div class="bank-name">${esc(account.bankShortName)} &nbsp;${esc(account.maskedAccountNumber)}</div>
          <div class="bank-holder">${esc(account.accountHolder)}</div>
          ${badge}
        </div>
        ${!farmer && overview.canRemove ? '<button type="button" class="acc-btn danger" id="ba-remove">Xóa</button>' : ''}
        <button type="button" class="acc-btn" id="ba-edit">Thay đổi</button>
      </div>
      ${holdActive ? `<div class="acc-warn" style="margin-top:14px;">Thanh toán đang tạm giữ đến ${esc(formatTime(account.payoutHoldUntil))} do vừa đổi tài khoản (48 giờ).</div>` : ''}
      <p class="acc-note">${farmer
        ? 'Tài khoản nhận quyết toán là bắt buộc nên không thể xóa, chỉ có thể thay đổi. Khi thay đổi, hệ thống gửi mã OTP tới email để xác nhận, gửi email thông báo và tạm giữ quyết toán 48 giờ.'
        : 'Khi thay đổi hoặc xóa, hệ thống gửi mã OTP tới email để xác nhận. Các yêu cầu hoàn tiền đã gửi vẫn giữ tài khoản bạn đã xác nhận.'}
        ${account.updatedAt ? `<br>Cập nhật: ${esc(formatTime(account.updatedAt))}` : ''}</p>`;
  }

  // (3) bank, (4) number + Kiểm tra, (5) holder, (6) confirmation, (7) Hủy / Lưu – Figma frames B/C1.
  function formHtml() {
    const account = overview.account;
    const banks = (overview.banks || []).map((b) =>
      `<option value="${esc(b.bankCode)}" ${b.bankCode === form.bankCode ? 'selected' : ''}>${esc(b.shortName)} – ${esc(b.fullName)}</option>`).join('');
    const numberError = errors.accountNumber || (lookup.status === 'NotFound' ? NOT_FOUND : '');
    const hint = lookup.status === 'checking'
      ? 'Đang tra cứu tài khoản tại ngân hàng…'
      : 'Tự kiểm tra sau khi bạn ngừng gõ khoảng 0,6 giây. Bấm Kiểm tra để thử lại.';
    const holderBox = lookup.status === 'Found'
      ? `<div class="acc-holder">${esc(lookup.holder)}<span>✓ Đã xác minh với ngân hàng</span></div>`
      : unavailable()
        ? `<input id="ba-holder" maxlength="100" placeholder="VD: NGUYEN VAN A" value="${esc(form.accountHolder)}">
           <small class="acc-hint">Hiện không tra cứu được ngân hàng. Tài khoản sẽ được lưu ở trạng thái Chưa xác minh${isFarmer() ? ' và chưa nhận được tiền quyết toán' : ''}.</small>`
        : '<input value="" readonly placeholder="Tự điền sau khi kiểm tra số tài khoản">';
    const disabled = busy ? 'disabled' : '';
    return `
      <h2>${account ? 'Thay đổi tài khoản ngân hàng' : 'Thêm tài khoản ngân hàng'}</h2>
      <p class="acc-desc">${account
        ? `Tài khoản hiện tại: ${esc(account.bankShortName)} ${esc(account.maskedAccountNumber)} – ${esc(account.accountHolder)}`
        : esc(overview.purpose)}</p>
      ${isFarmer() && account ? '<div class="acc-warn">⚠ Sau khi đổi, quyết toán sẽ tạm giữ 48 giờ và hệ thống gửi email thông báo tới email đăng ký.</div>' : ''}
      <form id="ba-form" novalidate>
        <label class="field"><span>Ngân hàng *</span>
          <select id="ba-bank" ${disabled}><option value="">-- Chọn ngân hàng --</option>${banks}</select>${err(errors.bankCode)}</label>
        <label class="field"><span>Số tài khoản *</span>
          <div class="acc-input-row">
            <input id="ba-number" inputmode="numeric" autocomplete="off" maxlength="19" placeholder="6–19 chữ số"
              value="${esc(form.accountNumber)}" ${disabled}>
            <button type="button" class="acc-btn" id="ba-check" ${disabled}>Kiểm tra</button>
          </div>
          <small class="acc-hint">${esc(hint)}</small>${err(numberError)}</label>
        <label class="field"><span>Tên chủ tài khoản (tự điền) *</span>${holderBox}${err(errors.accountHolder)}</label>
        <label style="display:flex; gap:8px; align-items:center; font-size:13px; cursor:pointer; margin:4px 0 6px;">
          <input type="checkbox" id="ba-confirm" ${form.confirmRecipient ? 'checked' : ''}
            ${lookup.status === 'Found' || unavailable() ? '' : 'disabled'} ${disabled}>
          Tôi xác nhận nhận tiền vào tài khoản${holderName() ? ` của ${esc(holderName())}` : ' này'}</label>
        ${err(errors.confirmRecipient)}
        <div class="acc-info">✉ Sau khi bấm Lưu, mã OTP 6 số sẽ được gửi tới email đăng ký để xác nhận thay đổi.</div>
        <div class="acc-footer">
          <button type="button" class="acc-btn" id="ba-cancel" ${disabled}>Hủy</button>
          <button type="submit" class="acc-btn primary" id="ba-submit" ${canSubmit() ? '' : 'disabled'}>${busy ? 'Đang gửi mã…' : 'Lưu tài khoản'}</button>
        </div>
      </form>`;
  }

  function dialogsHtml() {
    const account = overview.account;
    return `
      <dialog id="ba-remove-dialog" style="width:460px;">
        <div class="modal-heading"><h2 style="font-size:18px;">Xóa tài khoản ngân hàng?</h2>
          <button type="button" data-close="ba-remove-dialog">&times;</button></div>
        <p style="font-size:13px;">${esc(account?.bankShortName)} ${esc(account?.maskedAccountNumber)} sẽ không còn được gợi ý khi nhận tiền hoàn.
          Các yêu cầu hoàn tiền đã gửi vẫn giữ tài khoản bạn đã xác nhận.</p>
        <div class="modal-actions">
          <button type="button" class="acc-btn" data-close="ba-remove-dialog">Hủy</button>
          <button type="button" class="acc-btn danger" id="ba-remove-otp">Gửi mã OTP để xóa</button>
        </div>
      </dialog>
      <dialog id="ba-otp-dialog" style="width:440px;">
        <form id="ba-otp-form" novalidate>
          <h2 style="font-size:18px; margin:0 0 6px;" id="ba-otp-title"></h2>
          <p id="ba-otp-info" style="margin:0; font-size:12px; color:#5d6f63;"></p>
          <div class="otp-boxes">${[0, 1, 2, 3, 4, 5].map((i) =>
            `<input class="ba-otp-digit" data-i="${i}" inputmode="numeric" maxlength="1" autocomplete="${i === 0 ? 'one-time-code' : 'off'}">`).join('')}</div>
          <div class="otp-meta"><button type="button" id="ba-resend"></button></div>
          <div style="text-align:center;"><small class="acc-err" id="ba-otp-error"></small></div>
          <div class="acc-footer">
            <button type="button" class="acc-btn" data-close="ba-otp-dialog">Hủy</button>
            <button type="submit" class="acc-btn primary" id="ba-otp-submit">Xác nhận</button>
          </div>
        </form>
      </dialog>`;
  }

  function bind() {
    const $ = (id) => root.querySelector(`#${id}`);
    root.querySelectorAll('[data-close]').forEach((b) => { b.onclick = () => { if (!busy) $(b.dataset.close).close(); }; });
    if ($('ba-edit')) $('ba-edit').onclick = startEdit;
    if ($('ba-remove')) $('ba-remove').onclick = () => $('ba-remove-dialog').showModal();
    $('ba-remove-otp').onclick = async () => { $('ba-remove-dialog').close(); await sendOtp('remove'); };
    $('ba-otp-form').onsubmit = confirmOtp;
    $('ba-resend').onclick = () => sendOtp(otpFor);
    bindOtpBoxes();
    if (!editing) return;

    $('ba-bank').onchange = (e) => { form.bankCode = e.target.value; errors.bankCode = ''; scheduleLookup(); };
    $('ba-number').oninput = (e) => {
      e.target.value = e.target.value.replace(/\D/g, '');
      form.accountNumber = e.target.value;
      errors.accountNumber = '';
      scheduleLookup();
    };
    $('ba-check').onclick = () => scheduleLookup(0);
    if ($('ba-holder')) $('ba-holder').oninput = (e) => { form.accountHolder = e.target.value; refreshSubmit(); };
    $('ba-confirm').onchange = (e) => { form.confirmRecipient = e.target.checked; refreshSubmit(); };
    $('ba-form').onsubmit = handleSave;
    $('ba-cancel').onclick = () => { editing = false; form = emptyForm(); errors = {}; render(); };
  }

  function bindOtpBoxes() {
    const boxes = [...root.querySelectorAll('.ba-otp-digit')];
    boxes.forEach((box, i) => {
      box.oninput = () => {
        const digits = box.value.replace(/\D/g, '');
        if (digits.length > 1) { // pasted code
          digits.slice(0, 6).split('').forEach((d, k) => { if (boxes[k]) boxes[k].value = d; });
          boxes[Math.min(digits.length, 6) - 1].focus();
        } else {
          box.value = digits;
          if (digits && boxes[i + 1]) boxes[i + 1].focus();
        }
        root.querySelector('#ba-otp-error').textContent = '';
      };
      box.onkeydown = (e) => { if (e.key === 'Backspace' && !box.value && boxes[i - 1]) boxes[i - 1].focus(); };
    });
  }

  const otpValue = () => [...root.querySelectorAll('.ba-otp-digit')].map((b) => b.value).join('');

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

  // Step 3: look the account up about 600 ms after the user stops typing ("Kiểm tra" looks up at once).
  function scheduleLookup(delay = LOOKUP_DELAY_MS) {
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
    }, delay);
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
    const message = typeof data === 'string' ? data : (data && data.message) || 'Mã OTP 6 số đã được gửi tới email của bạn.';
    const target = purpose === 'remove'
      ? `${overview.account?.bankShortName || ''} ${overview.account?.maskedAccountNumber || ''}`
      : `${(overview.banks || []).find((b) => b.bankCode === form.bankCode)?.shortName || form.bankCode} •••• ${form.accountNumber.slice(-4)} – ${holderName()}`;
    root.querySelector('#ba-otp-title').textContent = purpose === 'remove' ? 'Xác nhận xóa tài khoản' : 'Xác nhận thay đổi tài khoản';
    root.querySelector('#ba-otp-info').textContent = `${message} Nhập mã để ${purpose === 'remove' ? 'xóa' : 'lưu'} ${target}.`;
    root.querySelectorAll('.ba-otp-digit').forEach((b) => { b.value = ''; });
    root.querySelector('#ba-otp-error').textContent = '';
    const submit = root.querySelector('#ba-otp-submit');
    submit.textContent = purpose === 'remove' ? 'Xóa tài khoản' : 'Xác nhận';
    submit.className = `acc-btn ${purpose === 'remove' ? 'danger' : 'primary'}`;
    const dlg = root.querySelector('#ba-otp-dialog');
    if (!dlg.open) dlg.showModal();
    root.querySelector('.ba-otp-digit').focus();
    startResendCountdown();
  }

  function startResendCountdown() {
    resendIn = RESEND_SECONDS;
    clearInterval(resendTimer);
    const tick = () => {
      const btn = root.querySelector('#ba-resend');
      if (!btn) { clearInterval(resendTimer); return; }
      btn.disabled = resendIn > 0;
      btn.textContent = resendIn > 0 ? `Mã hết hạn sau 5 phút · Gửi lại mã sau ${resendIn} giây` : 'Gửi lại mã';
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
    const otp = otpValue();
    const otpError = root.querySelector('#ba-otp-error');
    if (!/^\d{6}$/.test(otp)) { otpError.textContent = 'Mã OTP gồm 6 chữ số.'; return; }
    busy = true;
    const submit = root.querySelector('#ba-otp-submit');
    submit.disabled = true;
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
