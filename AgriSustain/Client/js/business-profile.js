/* ==========================================================================
   AgriSustain - Business dossier panel (js/business-profile.js)
   UC-AUTH-02 (part 2): a WholesaleBuyer submits the company dossier for KYC
   from the Profile screen. Tax code is required; the Business License file is
   optional. Editable only while NotSubmitted or Rejected.
   Usage: BusinessProfilePanel.mount(document.getElementById('business-panel'));
   ========================================================================== */

const BusinessProfilePanel = (() => {
  const TYPES = {
    Supermarket: 'Siêu thị / Chuỗi bán lẻ', Restaurant: 'Nhà hàng / Khách sạn', Processor: 'Cơ sở chế biến',
    Exporter: 'Doanh nghiệp xuất khẩu', Distributor: 'Nhà phân phối', Other: 'Khác'
  };
  const STATUS = {
    NotSubmitted: ['Chưa nộp hồ sơ', ''], PendingApproval: ['Chờ xác minh', 'Pending'],
    Approved: ['Đã xác minh', 'Active'], Rejected: ['Từ chối', 'Rejected']
  };
  const esc = (v) => Utils.escapeHtml(v == null ? '' : String(v));
  let root = null;
  let profile = null;

  async function load() {
    root.innerHTML = '<p class="muted">Đang tải…</p>';
    const res = await API.request('/profile/business', { suppressErrorToast: true });
    if (!res.success) { root.innerHTML = `<div class="notice error">${esc(res.error)}</div>`; return; }
    profile = res.data;
    render();
  }

  function render() {
    const p = profile;
    const status = p.verificationStatus;
    const editable = status === 'NotSubmitted' || status === 'Rejected';
    const dis = editable ? '' : 'disabled';
    const s = STATUS[status] || [status, ''];
    const options = Object.entries(TYPES).map(([v, l]) => `<option value="${v}" ${p.businessType === v ? 'selected' : ''}>${l}</option>`).join('');
    root.innerHTML = `
      <div style="display:flex; justify-content:space-between; align-items:center; gap:12px; margin-bottom:12px;">
        <div>
          <h3 style="margin:0; color:#103e2d;">Hồ sơ doanh nghiệp &amp; KYC</h3>
          <p class="muted" style="font-size:12px; margin:4px 0 0;">Đặt hàng sỉ và hợp đồng bao tiêu được mở sau khi Nhân viên thẩm định duyệt hồ sơ.</p>
        </div>
        <span class="badge ${s[1]}">${esc(s[0])}</span>
      </div>
      ${status === 'NotSubmitted' ? '<div class="notice">Bạn chưa gửi hồ sơ doanh nghiệp. Điền thông tin và gửi để được thẩm định.</div>' : ''}
      ${status === 'PendingApproval' ? '<div class="notice">Hồ sơ đang chờ Nhân viên thẩm định duyệt.</div>' : ''}
      ${status === 'Rejected' ? `<div class="notice error">Hồ sơ bị từ chối: ${esc(p.rejectReason || 'Không có lý do.')} Vui lòng sửa và gửi lại.</div>` : ''}
      <form id="biz-form" novalidate>
        <div style="display:grid; grid-template-columns:1fr 1fr; gap:0 16px;">
          <label class="field"><span>Tên doanh nghiệp *</span><input name="companyName" maxlength="255" value="${esc(p.companyName)}" ${dis}><small class="err" data-biz="companyName"></small></label>
          <label class="field"><span>Mã số thuế *</span><input name="taxCode" maxlength="20" placeholder="10 số, hoặc 13 số với chi nhánh" value="${esc(p.taxCode)}" ${dis}><small class="err" data-biz="taxCode"></small></label>
          <label class="field"><span>Người đại diện *</span><input name="representativeName" maxlength="150" value="${esc(p.representativeName)}" ${dis}><small class="err" data-biz="representativeName"></small></label>
          <label class="field"><span>Loại hình doanh nghiệp</span><select name="businessType" ${dis}><option value="">-- Chọn loại hình --</option>${options}</select><small class="err" data-biz="businessType"></small></label>
          <label class="field" style="grid-column:1 / -1;"><span>Địa chỉ kinh doanh *</span><input name="businessAddress" maxlength="500" value="${esc(p.businessAddress)}" ${dis}><small class="err" data-biz="businessAddress"></small></label>
          <label class="field" style="grid-column:1 / -1;"><span>Giấy phép kinh doanh (không bắt buộc, PDF/JPG, tối đa 5 MB)</span>
            ${editable ? '<input type="file" name="file" accept=".pdf,.jpg,.jpeg">' : ''}<small class="err" data-biz="file"></small></label>
        </div>
        ${p.documentUrl ? '<button type="button" id="biz-doc" style="margin-bottom:12px;">Xem giấy phép đã nộp</button>' : ''}
        ${editable ? '<div style="text-align:right;"><button type="submit" class="primary" id="biz-submit">Gửi thẩm định KYC</button></div>' : ''}
      </form>`;
    if (p.documentUrl) root.querySelector('#biz-doc').onclick = () => Dossier.open(p.documentUrl);
    if (editable) root.querySelector('#biz-form').onsubmit = submit;
  }

  function setErrors(map) {
    root.querySelectorAll('[data-biz]').forEach((el) => { el.textContent = map[el.dataset.biz] || ''; });
  }

  async function submit(event) {
    event.preventDefault();
    const form = event.target;
    const e = {};
    ['companyName', 'taxCode', 'representativeName', 'businessAddress'].forEach((k) => { if (!form[k].value.trim()) e[k] = 'Trường này là bắt buộc.'; });
    const digits = form.taxCode.value.replace(/[\s-]/g, '');
    if (form.taxCode.value.trim() && !/^\d{10}(\d{3})?$/.test(digits)) {
      e.taxCode = 'Mã số thuế gồm 10 chữ số, hoặc 13 chữ số với chi nhánh (VD: 0101234567-001).';
    }
    const file = form.file.files[0];
    const fileError = Dossier.check(file, ['pdf', 'jpg', 'jpeg']);
    if (fileError) e.file = fileError;
    setErrors(e);
    if (Object.keys(e).length) return;

    const data = new FormData(form);
    if (!file) data.delete('file');
    const btn = root.querySelector('#biz-submit');
    btn.disabled = true; btn.textContent = 'Đang gửi…';
    const res = await API.request('/profile/business', { method: 'POST', body: data, suppressErrorToast: true });
    btn.disabled = false; btn.textContent = 'Gửi thẩm định KYC';
    if (!res.success) {
      const fields = (res.data && res.data.errors) || {};
      setErrors(Object.fromEntries(Object.entries(fields).map(([k, v]) => [k, Array.isArray(v) ? v[0] : v])));
      if (!Object.keys(fields).length) Utils.showToast(res.error, 'error');
      return;
    }
    Utils.showToast(res.data.message, 'success');
    profile = res.data.profile;
    render();
  }

  return { mount(element) { root = element; load(); } };
})();
