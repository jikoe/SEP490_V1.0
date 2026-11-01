/* ==========================================================================
   AgriSustain - Consumer profile (js/consumer-profile.js)
   UC-AUTH-03 Manage Profile for RetailCustomer and WholesaleBuyer:
   personal info + avatar, default delivery address (province/district/ward),
   bank account (js/bank-account.js) and change password (UC-AUTH-05).
   Used by RetailCustomer/profile.html and WholesaleBuyer/profile.html.
   ========================================================================== */

const ConsumerProfile = (() => {
  const GENDER = { Nam: 'Male', 'Nữ': 'Female', 'Khác': 'Other', Male: 'Male', Female: 'Female', Other: 'Other' };
  const $ = (id) => document.getElementById(id);
  let saved = null;
  let bankMounted = false;

  function setError(field, message) {
    const el = document.querySelector(`[data-error="${field}"]`);
    if (el) el.textContent = message || '';
  }

  function clearErrors() {
    document.querySelectorAll('[data-error]').forEach((el) => { el.textContent = ''; });
  }

  function notice(message, isError = false) {
    const el = $('profile-alert');
    if (!message) { el.style.display = 'none'; return; }
    el.className = `notice ${isError ? 'error' : ''}`;
    el.textContent = message;
    el.style.display = 'block';
  }

  function switchTab(tab) {
    ['info', 'address', 'bank', 'security'].forEach((name) => {
      const panel = $(`tab-${name}`);
      if (panel) panel.style.display = name === tab ? 'block' : 'none';
    });
    document.querySelectorAll('.tab-btn[data-tab]').forEach((b) => b.classList.toggle('active', b.dataset.tab === tab));
    if (tab === 'bank' && !bankMounted) {
      BankAccountPanel.mount($('bank-panel'));
      bankMounted = true;
    }
  }

  async function loadOptions(select, path, selected, placeholder) {
    select.innerHTML = `<option value="">${placeholder}</option>`;
    select.disabled = !path;
    if (!path) return;
    const res = await API.request(path, { suppressErrorToast: true });
    const items = res.success && Array.isArray(res.data) ? res.data : [];
    select.innerHTML += items.map((x) =>
      `<option value="${Utils.escapeHtml(x.code)}" ${x.code === selected ? 'selected' : ''}>${Utils.escapeHtml(x.name)}</option>`).join('');
  }

  const provincesPath = () => '/profile/address/provinces';
  const districtsPath = (code) => code ? `/profile/address/provinces/${encodeURIComponent(code)}/districts` : null;
  const wardsPath = (code) => code ? `/profile/address/districts/${encodeURIComponent(code)}/wards` : null;

  function renderAvatar(url, name) {
    const box = $('prof-avatar');
    box.innerHTML = url
      ? `<img src="${Utils.escapeHtml(url)}" alt="Ảnh đại diện" style="width:100%; height:100%; border-radius:50%; object-fit:cover;">`
      : Utils.escapeHtml((name || 'U').trim().charAt(0).toUpperCase());
  }

  async function load() {
    const res = await API.getProfile();
    if (!res.success || !res.data) {
      notice(res.error || 'Không thể tải hồ sơ. Vui lòng đăng nhập lại.', true);
      return;
    }
    const d = res.data;
    const a = d.deliveryAddress || {};
    saved = d;
    $('prof-name').value = d.fullName || '';
    $('prof-email').value = d.email || '';
    $('prof-phone').value = d.phoneNumber || '';
    $('prof-gender').value = GENDER[d.gender] || '';
    $('prof-birth').value = d.birthDate ? String(d.birthDate).slice(0, 10) : '';
    $('prof-points').textContent = `⭐ ${d.rewardPoints ?? 0}`;
    $('hero-name').textContent = d.fullName || '';
    $('hero-email').textContent = d.email || '';
    renderAvatar(d.avatarUrl, d.fullName);

    $('addr-recipient').value = a.recipientName || '';
    $('addr-phone').value = a.recipientPhone || '';
    $('addr-detail').value = a.addressDetail || '';
    await loadOptions($('addr-province'), provincesPath(), a.provinceCode, '-- Chọn Tỉnh / Thành phố --');
    await loadOptions($('addr-district'), districtsPath(a.provinceCode), a.districtCode, '-- Chọn Quận / Huyện --');
    await loadOptions($('addr-ward'), wardsPath(a.districtCode), a.wardCode, '-- Chọn Phường / Xã --');
    renderAddressSummary(a);
  }

  function renderAddressSummary(a) {
    const box = $('addr-summary');
    if (!a || !a.addressDetail) {
      box.innerHTML = '<p class="muted" style="margin:0;">Chưa có địa chỉ nhận hàng mặc định.</p>';
      return;
    }
    const parts = [a.addressDetail, a.ward, a.district, a.provinceCity].filter(Boolean).join(', ');
    box.innerHTML = `
      <div style="display:flex; justify-content:space-between; align-items:center;">
        <strong>${Utils.escapeHtml(a.recipientName || '')}</strong><span class="badge Active">Mặc định</span>
      </div>
      <p style="margin:4px 0; font-size:13px; color:#405247;">Số điện thoại: ${Utils.escapeHtml(a.recipientPhone || '')}</p>
      <p style="margin:4px 0; font-size:13px; color:#405247;">Địa chỉ: ${Utils.escapeHtml(parts)}</p>`;
  }

  function selectedText(select) {
    return select.value ? select.options[select.selectedIndex].text : null;
  }

  function validate() {
    const e = {};
    const name = $('prof-name').value.trim();
    const phone = $('prof-phone').value.trim();
    if (!name) e.fullName = 'Họ và tên không được để trống.';
    else if (name.length < 2 || name.length > 50) e.fullName = 'Họ và tên phải từ 2 đến 50 ký tự.';
    if (!phone) e.phoneNumber = 'Số điện thoại không được để trống.';
    else if (!/^0\d{9}$/.test(phone)) e.phoneNumber = 'Số điện thoại không hợp lệ. Số điện thoại phải gồm đúng 10 chữ số và bắt đầu bằng 0.';
    const birth = $('prof-birth').value;
    if (birth && birth > new Date().toISOString().slice(0, 10)) e.birthDate = 'Ngày sinh không được ở tương lai.';
    const rPhone = $('addr-phone').value.trim();
    if (rPhone && !/^0\d{9}$/.test(rPhone)) e.recipientPhone = 'Số điện thoại không hợp lệ. Số điện thoại phải gồm đúng 10 chữ số và bắt đầu bằng 0.';
    if ($('addr-recipient').value.trim().length > 50) e.recipientName = 'Họ tên người nhận không quá 50 ký tự.';
    const anyAddress = $('addr-province').value || $('addr-detail').value.trim();
    if (anyAddress) {
      if (!$('addr-province').value) e.provinceCode = 'Vui lòng chọn Tỉnh / Thành phố.';
      if (!$('addr-district').value) e.districtCode = 'Vui lòng chọn Quận / Huyện.';
      if (!$('addr-ward').value) e.wardCode = 'Vui lòng chọn Phường / Xã.';
      if (!$('addr-detail').value.trim()) e.addressDetail = 'Vui lòng nhập số nhà, tên đường.';
    }
    return e;
  }

  // Personal info and address are saved together (PUT /api/profile/consumer).
  async function save(event) {
    event.preventDefault();
    clearErrors();
    notice('');
    const errors = validate();
    if (Object.keys(errors).length) {
      Object.entries(errors).forEach(([k, v]) => setError(k, v));
      notice('Vui lòng kiểm tra lại các trường thông tin bị lỗi.', true);
      return;
    }
    const payload = {
      fullName: $('prof-name').value.trim(),
      phoneNumber: $('prof-phone').value.trim(),
      gender: $('prof-gender').value || null,
      birthDate: $('prof-birth').value || null,
      provinceCity: selectedText($('addr-province')),
      district: selectedText($('addr-district')),
      ward: selectedText($('addr-ward')),
      addressDetail: $('addr-detail').value.trim() || null,
      provinceCode: $('addr-province').value || null,
      districtCode: $('addr-district').value || null,
      wardCode: $('addr-ward').value || null,
      recipientName: $('addr-recipient').value.trim() || null,
      recipientPhone: $('addr-phone').value.trim() || null
    };
    const buttons = document.querySelectorAll('.btn-save-profile');
    buttons.forEach((b) => { b.disabled = true; });
    const res = await API.updateConsumerProfile(payload);
    buttons.forEach((b) => { b.disabled = false; });
    if (!res.success) {
      const fields = (res.data && res.data.errors) || {};
      const alias = { address: 'addressDetail' };
      Object.entries(fields).forEach(([k, v]) => setError(alias[k] || k, Array.isArray(v) ? v[0] : v));
      notice(res.error || 'Cập nhật hồ sơ thất bại.', true);
      return;
    }
    const user = Auth.getUserInfo() || {};
    user.fullName = payload.fullName;
    Auth.setUserInfo(user);
    Utils.showToast('Cập nhật hồ sơ thành công!', 'success');
    await load();
  }

  async function uploadAvatar(event) {
    const file = event.target.files?.[0];
    event.target.value = '';
    if (!file) return;
    if (!/\.(png|jpe?g)$/i.test(file.name) || file.size > 5 * 1024 * 1024) {
      notice('Vui lòng chọn ảnh PNG hoặc JPG có dung lượng tối đa 5 MB.', true);
      return;
    }
    const res = await API.uploadAvatar(file);
    if (res.success) {
      renderAvatar(res.data?.avatarUrl, $('prof-name').value);
      Utils.showToast('Đã cập nhật ảnh đại diện.', 'success');
    } else {
      notice(res.error || 'Không tải được ảnh đại diện.', true);
    }
  }

  async function changePassword(event) {
    event.preventDefault();
    clearErrors();
    const currentPassword = $('old-pass').value;
    const newPassword = $('new-pass').value;
    const confirmPassword = $('confirm-pass').value;
    if (!currentPassword) { setError('currentPassword', 'Vui lòng nhập mật khẩu hiện tại.'); return; }
    if (newPassword !== confirmPassword) { setError('confirmPassword', 'Mật khẩu xác nhận không khớp.'); return; }
    const res = await API.changePassword({ currentPassword, newPassword, confirmPassword });
    if (!res.success) {
      const fields = (res.data && res.data.errors) || {};
      Object.entries(fields).forEach(([k, v]) => setError(k, Array.isArray(v) ? v[0] : v));
      notice(res.error || 'Không thể đổi mật khẩu. Vui lòng kiểm tra lại.', true);
      return;
    }
    $('old-pass').value = '';
    $('new-pass').value = '';
    $('confirm-pass').value = '';
    Utils.showToast('Đổi mật khẩu thành công!', 'success');
  }

  function bind() {
    document.querySelectorAll('.tab-btn[data-tab]').forEach((b) => { b.onclick = () => switchTab(b.dataset.tab); });
    $('form-info').onsubmit = save;
    $('form-address').onsubmit = save;
    $('form-password').onsubmit = changePassword;
    $('avatar-input').onchange = uploadAvatar;
    $('addr-province').onchange = async () => {
      setError('provinceCode', '');
      await loadOptions($('addr-district'), districtsPath($('addr-province').value), null, '-- Chọn Quận / Huyện --');
      await loadOptions($('addr-ward'), null, null, '-- Chọn Phường / Xã --');
    };
    $('addr-district').onchange = async () => {
      setError('districtCode', '');
      await loadOptions($('addr-ward'), wardsPath($('addr-district').value), null, '-- Chọn Phường / Xã --');
    };
  }

  return {
    init(role) {
      Components.init();
      if (!Auth.requireAuth(role)) return;
      bind();
      const tab = new URLSearchParams(window.location.search).get('tab');
      switchTab(['info', 'address', 'bank', 'security'].includes(tab) ? tab : 'info');
      load();
    }
  };
})();
