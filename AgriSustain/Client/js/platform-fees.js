/* UC-FIN-05 Manage Platform Fees – shared by FinanceStaff/fees.html (create, version, submit, end-date)
   and Administrator/fee-approvals.html (approve / reject). The server decides what each role may do. */
(function () {
  const GROUPS = {
    PlatformCommission: 'Hoa hồng sàn', PaymentProcessing: 'Phí xử lý thanh toán', Service: 'Phí dịch vụ',
    TaxWithholding: 'Thuế khấu trừ thay', Other: 'Khác'
  };
  const REFUND = { Full: 'Hoàn toàn bộ', Proportional: 'Hoàn theo tỷ lệ', NonRefundable: 'Không hoàn' };
  const METHODS = { Percentage: 'Theo %', FixedPerOrder: 'Cố định / đơn', FixedPerItem: 'Cố định / sản phẩm' };
  const BASES = { ItemSubtotal: 'Tiền hàng sau giảm giá của người bán', SellerOrderTotal: 'Tổng đơn trang trại (gồm phí giao)' };
  const STATES = {
    Draft: ['Nháp', ''], PendingApproval: ['Chờ duyệt', 'Pending'], Rejected: ['Bị từ chối', 'Suspended'],
    Scheduled: ['Sắp áp dụng', 'Pending'], Active: ['Đang áp dụng', 'Active'], Expired: ['Hết hiệu lực', '']
  };
  const ORDER_TYPES = { Retail: 'Bán lẻ', Wholesale: 'Bán sỉ', Contract: 'Hợp đồng' };
  const esc = s => Utils.escapeHtml(s == null ? '' : String(s));
  const money = v => v == null ? '—' : Utils.formatVND(v);
  const dateText = d => d ? Utils.formatDate(d) : 'không thời hạn';
  const today = () => new Date(Date.now() + 7 * 3600 * 1000).toISOString().slice(0, 10);

  const state = { role: null, page: 1, rows: [], categories: [], current: null, form: null, confirm: null };

  function isAdmin() { return state.role === 'admin'; }

  function badge(s) { const [label, cls] = STATES[s] || [s, '']; return `<span class="badge ${cls}">${label}</span>`; }

  function valueText(p) {
    const v = p.method === 'Percentage' ? `${Number(p.rate).toLocaleString('vi-VN')}%` : money(p.fixedAmount);
    const caps = [p.minAmount != null ? `tối thiểu ${money(p.minAmount)}` : '', p.maxAmount != null ? `tối đa ${money(p.maxAmount)}` : '']
      .filter(Boolean).join(', ');
    return `${v} · ${METHODS[p.method]}${caps ? ` (${caps})` : ''}`;
  }

  function scopeText(p) {
    if (p.appliesToAllCategories) return 'Tất cả danh mục';
    const names = p.categoryIds.map(id => (state.categories.find(c => c.id === id) || { name: `#${id}` }).name);
    return names.join(', ');
  }

  async function init(role) {
    state.role = role;
    const cats = await API.request('/finance/fees/categories', { suppressErrorToast: true });
    state.categories = cats.success ? (cats.data || []) : [];
    document.getElementById('f-category').innerHTML = '<option value="">Tất cả danh mục</option>'
      + state.categories.map(c => `<option value="${c.id}">${esc(c.name)}</option>`).join('');
    if (isAdmin()) document.getElementById('f-state').value = 'PendingApproval';
    document.getElementById('fee-filters').addEventListener('submit', e => { e.preventDefault(); load(1); });
    await load(1);
  }

  async function load(page) {
    state.page = page;
    const q = API.toQueryString({
      keyword: val('f-keyword') || undefined, group: val('f-group') || undefined,
      orderType: val('f-order-type') || undefined, categoryId: val('f-category') || undefined,
      state: val('f-state') || undefined, page, pageSize: 10
    });
    const tbody = document.getElementById('fee-tbody');
    tbody.innerHTML = '<tr><td colspan="6" class="empty">Đang tải…</td></tr>';
    const res = await API.request(`/finance/fees?${q}`, { suppressErrorToast: true });
    if (!res.success) {
      // MSG126: unavailable rates are never shown as 0.
      tbody.innerHTML = `<tr><td colspan="6" class="empty">${esc(res.error || 'Không thể tải danh sách phí. Hãy thử lại.')}</td></tr>`;
      return;
    }
    state.rows = res.data.items || [];
    document.getElementById('fee-count').textContent = `${res.data.total} loại phí`;
    if (state.rows.length === 0) {
      // MSG124 / MSG01
      tbody.innerHTML = `<tr><td colspan="6" class="empty">${res.data.total === 0 && !val('f-keyword') && !val('f-state')
        ? 'Chưa có loại phí nào.' : 'Không có kết quả phù hợp.'}</td></tr>`;
    } else {
      tbody.innerHTML = state.rows.map(r => `
        <tr>
          <td><strong>${esc(r.feeCode)}</strong><br><small class="muted">${esc(r.feeName)}</small></td>
          <td>${GROUPS[r.feeGroup] || r.feeGroup}${r.isMandatory ? ' <span class="badge">Bắt buộc</span>' : ''}</td>
          <td>${r.currentPolicies.length ? r.currentPolicies.map(p =>
            `<div>${ORDER_TYPES[p.orderType]} · ${esc(valueText(p))} · ${esc(scopeText(p))} ${badge(p.state)}</div>`).join('') : '<span class="muted">Chưa có mức đang áp dụng</span>'}</td>
          <td>${REFUND[r.refundMode]}</td>
          <td>${r.pendingReviews ? `<span class="badge Pending">${r.pendingReviews} chờ duyệt</span>` : '—'}</td>
          <td><button type="button" data-open="${r.id}">Chi tiết</button></td>
        </tr>`).join('');
      tbody.querySelectorAll('[data-open]').forEach(b => b.addEventListener('click', () => openDetail(Number(b.dataset.open))));
    }
    const pages = Math.max(1, Math.ceil(res.data.total / 10));
    document.getElementById('fee-pager').innerHTML = pages <= 1 ? '' :
      `<button type="button" ${page <= 1 ? 'disabled' : ''} id="pg-prev">‹ Trước</button><span>Trang ${page}/${pages}</span><button type="button" ${page >= pages ? 'disabled' : ''} id="pg-next">Sau ›</button>`;
    const prev = document.getElementById('pg-prev'), next = document.getElementById('pg-next');
    if (prev) prev.onclick = () => load(page - 1);
    if (next) next.onclick = () => load(page + 1);
  }

  async function openDetail(id) {
    const res = await API.request(`/finance/fees/${id}`, { suppressErrorToast: true });
    if (!res.success) { Utils.showToast(res.error || 'Không tải được phí.', 'error'); return; }
    state.current = res.data;
    renderDetail();
    document.getElementById('detail-modal').showModal();
  }

  function renderDetail() {
    const f = state.current;
    document.getElementById('detail-title').textContent = `${f.feeCode} · ${f.feeName}`;
    const canEditType = !isAdmin();
    const policies = f.policies.map(p => {
      const actions = [];
      if (isAdmin()) {
        if (p.state === 'PendingApproval' || p.pendingEffectiveTo) {
          actions.push(`<button type="button" class="primary" data-act="approve" data-id="${p.id}">Duyệt</button>`);
          actions.push(`<button type="button" class="danger-link" data-act="reject" data-id="${p.id}">Từ chối</button>`);
        }
      } else {
        if (p.state === 'Draft' || p.state === 'Rejected') {
          actions.push(`<button type="button" data-act="edit" data-id="${p.id}">Sửa</button>`);
          actions.push(`<button type="button" class="primary" data-act="submit" data-id="${p.id}">Gửi duyệt</button>`);
        }
        if ((p.state === 'Active' || p.state === 'Scheduled') && !p.pendingEffectiveTo) {
          actions.push(`<button type="button" data-act="end" data-id="${p.id}">Kết thúc</button>`);
        }
      }
      return `<tr>
        <td><strong>${esc(p.policyCode)}</strong><br><small class="muted">${ORDER_TYPES[p.orderType]}</small></td>
        <td>${esc(valueText(p))}<br><small class="muted">${esc(BASES[p.calculationBase])}</small></td>
        <td>${esc(scopeText(p))}</td>
        <td>${dateText(p.effectiveFrom)} – ${dateText(p.effectiveTo)}${p.pendingEffectiveTo ? `<br><small class="badge Pending">Chờ duyệt kết thúc ${Utils.formatDate(p.pendingEffectiveTo)}</small>` : ''}</td>
        <td>${badge(p.state)}${p.reviewNote ? `<br><small class="muted">${esc(p.reviewNote)}</small>` : ''}<br><small class="muted">${esc(p.reason)}</small></td>
        <td><div class="actions">${actions.join('')}</div></td>
      </tr>`;
    }).join('');
    document.getElementById('detail-body').innerHTML = `
      <div class="fee-meta">
        <span>Nhóm: <b>${GROUPS[f.feeGroup]}</b></span><span>Khi hoàn tiền: <b>${REFUND[f.refundMode]}</b></span>
        <span>Trạng thái: <b>${f.status === 'Active' ? 'Đang dùng' : 'Nháp'}</b></span>
        <span>${f.applied ? 'Đã áp dụng cho đơn hàng' : 'Chưa áp dụng cho đơn nào'}</span>
        ${f.isMandatory ? '<span class="badge">Bắt buộc</span>' : ''}
      </div>
      ${f.description ? `<p class="muted">${esc(f.description)}</p>` : ''}
      <div class="table-wrap"><table><thead><tr><th>Chính sách</th><th>Mức phí</th><th>Phạm vi</th><th>Hiệu lực</th><th>Trạng thái</th><th></th></tr></thead>
      <tbody>${policies || '<tr><td colspan="6" class="empty">Chưa có chính sách.</td></tr>'}</tbody></table></div>`;
    document.getElementById('detail-actions').innerHTML = canEditType
      ? `<button type="button" data-top="new-version">+ Phiên bản / chính sách mới</button>
         <button type="button" data-top="edit-type">Sửa thông tin phí</button>
         ${f.status === 'Draft' && !f.applied && !f.isMandatory ? '<button type="button" class="danger-link" data-top="delete">Xóa phí</button>' : ''}`
      : '';
    document.querySelectorAll('#detail-body [data-act]').forEach(b => b.addEventListener('click', () => policyAction(b.dataset.act, Number(b.dataset.id))));
    document.querySelectorAll('#detail-actions [data-top]').forEach(b => b.addEventListener('click', () => topAction(b.dataset.top)));
  }

  function policyOf(id) { return state.current.policies.find(p => p.id === id); }

  async function policyAction(act, id) {
    const p = policyOf(id);
    if (act === 'edit') return openForm('edit', p);
    if (act === 'submit') return send(`/finance/fees/policies/${id}/submit`, { version: p.version });
    if (act === 'approve') return askConfirm(`Duyệt ${p.pendingEffectiveTo ? 'việc kết thúc' : ''} chính sách ${p.policyCode}?`, false,
      note => send(`/finance/fees/policies/${id}/approve`, { version: p.version, note }));
    if (act === 'reject') return askConfirm(`Từ chối chính sách ${p.policyCode}? Hãy nhập lý do.`, true,
      note => send(`/finance/fees/policies/${id}/reject`, { version: p.version, note }));
    if (act === 'end') return askEndDate(p);
  }

  function topAction(act) {
    if (act === 'new-version') return openForm('version', null);
    if (act === 'edit-type') return openForm('type', null);
    if (act === 'delete') return askConfirm(`Xóa phí ${state.current.feeCode}? Thao tác này không thể hoàn tác.`, false,
      () => send(`/finance/fees/${state.current.id}/delete`, {}, true));
  }

  async function send(path, body, closeDetail = false) {
    const res = await API.request(path, { method: 'POST', body, suppressErrorToast: true });
    if (!res.success) {
      Utils.showToast(firstError(res), 'error');
      if (res.status === 409 && res.code === 'FEE_CHANGED' && state.current) await openDetail(state.current.id);
      return false;
    }
    Utils.showToast(res.data.message, 'success');
    if (closeDetail || !res.data.fee) document.getElementById('detail-modal').close();
    else { state.current = res.data.fee; renderDetail(); }
    await load(state.page);
    return true;
  }

  // ---- create / edit form --------------------------------------------------------------
  function openForm(mode, policy) {
    state.form = { mode, policy };
    const f = state.current;
    const typeBox = document.getElementById('form-type'), policyBox = document.getElementById('form-policy');
    typeBox.hidden = !(mode === 'create' || mode === 'type');
    policyBox.hidden = mode === 'type';
    document.getElementById('in-code').disabled = mode !== 'create';
    document.getElementById('form-title').textContent = { create: 'Thêm loại phí', type: 'Sửa thông tin phí', version: `Chính sách mới cho ${f && f.feeCode}`, edit: `Sửa chính sách ${policy && policy.policyCode}` }[mode];
    setVal('in-code', mode === 'create' ? '' : f.feeCode);
    setVal('in-name', mode === 'create' ? '' : f.feeName);
    setVal('in-desc', mode === 'create' ? '' : (f.description || ''));
    setVal('in-group', mode === 'create' ? 'PlatformCommission' : f.feeGroup);
    setVal('in-refund', mode === 'create' ? 'Proportional' : f.refundMode);
    const p = policy || {};
    setVal('in-method', p.method || 'Percentage');
    setVal('in-base', p.calculationBase || 'ItemSubtotal');
    setVal('in-rate', p.rate ?? '');
    setVal('in-fixed', p.fixedAmount ?? '');
    setVal('in-min', p.minAmount ?? '');
    setVal('in-max', p.maxAmount ?? '');
    setVal('in-order-type', p.orderType || 'Retail');
    document.getElementById('in-all').checked = p.appliesToAllCategories !== false;
    document.getElementById('in-cats').innerHTML = state.categories.map(c =>
      `<label class="check-row"><input type="checkbox" value="${c.id}" ${(p.categoryIds || []).includes(c.id) ? 'checked' : ''}> ${esc(c.name)}${c.isActive ? '' : ' <small class="muted">(đang ẩn)</small>'}</label>`).join('');
    setVal('in-from', p.effectiveFrom || today());
    setVal('in-to', p.effectiveTo || '');
    setVal('in-reason', '');
    document.getElementById('preview-box').innerHTML = '';
    clearErrors();
    syncForm();
    document.getElementById('form-modal').showModal();
  }

  function syncForm() {
    const pct = val('in-method') === 'Percentage';
    document.getElementById('row-rate').hidden = !pct;
    document.getElementById('row-fixed').hidden = pct;
    const group = state.form && state.form.mode === 'create' ? val('in-group') : (state.current || {}).feeGroup;
    const scoped = ['PlatformCommission', 'Service'].includes(group);
    document.getElementById('row-scope').hidden = !scoped;
    if (!scoped) document.getElementById('in-all').checked = true;
    document.getElementById('in-cats').hidden = document.getElementById('in-all').checked;
  }

  function readPolicy() {
    const num = id => val(id) === '' ? null : Number(val(id));
    const all = document.getElementById('in-all').checked;
    return {
      method: val('in-method'), calculationBase: val('in-base'), rate: num('in-rate'), fixedAmount: num('in-fixed'),
      minAmount: num('in-min'), maxAmount: num('in-max'), orderType: val('in-order-type'), appliesToAllCategories: all,
      categoryIds: all ? [] : [...document.querySelectorAll('#in-cats input:checked')].map(x => Number(x.value)),
      effectiveFrom: val('in-from'), effectiveTo: val('in-to') || null, reason: val('in-reason')
    };
  }

  async function saveForm(submit) {
    const { mode, policy } = state.form;
    let path, method = 'POST', body;
    if (mode === 'create') {
      path = '/finance/fees';
      body = { feeCode: val('in-code').trim().toUpperCase(), feeName: val('in-name'), description: val('in-desc') || null,
        feeGroup: val('in-group'), refundMode: val('in-refund'), policy: readPolicy(), submit, requestKey: crypto.randomUUID() };
    } else if (mode === 'type') {
      path = `/finance/fees/${state.current.id}`; method = 'PUT';
      body = { feeName: val('in-name'), description: val('in-desc') || null, feeGroup: val('in-group'), refundMode: val('in-refund') };
    } else if (mode === 'version') {
      path = `/finance/fees/${state.current.id}/policies`;
      body = { policy: readPolicy(), submit, requestKey: crypto.randomUUID() };
    } else {
      path = `/finance/fees/policies/${policy.id}`; method = 'PUT';
      body = { policy: readPolicy(), submit, version: policy.version };
    }
    clearErrors();
    const res = await API.request(path, { method, body, suppressErrorToast: true });
    if (!res.success) {
      const errors = res.data && res.data.errors;
      if (errors) showErrors(errors); else setBox('form-error', firstError(res));
      return;
    }
    document.getElementById('form-modal').close();
    Utils.showToast(res.data.message, 'success');
    if (res.data.fee) { state.current = res.data.fee; renderDetail(); }
    await load(state.page);
  }

  async function preview() {
    const lines = [...document.querySelectorAll('#preview-lines .pv-line')].map(row => ({
      categoryId: Number(row.querySelector('select').value) || null,
      subtotal: Number(row.querySelector('input').value) || 0
    }));
    const body = { orderType: val('in-order-type'), date: val('in-from') || null, lines,
      deliveryFee: Number(val('pv-delivery')) || 0, feeTypeId: state.current && state.form.mode !== 'create' ? state.current.id : null,
      feeGroup: state.form.mode === 'create' ? val('in-group') : null,
      draftPolicy: state.form.mode === 'type' ? null : readPolicy() };
    const res = await API.request('/finance/fees/preview', { method: 'POST', body, suppressErrorToast: true });
    const box = document.getElementById('preview-box');
    if (!res.success) { box.innerHTML = `<p class="notice error">${esc(firstError(res))}</p>`; return; }
    const d = res.data;
    box.innerHTML = `<table><tbody>
      <tr><td>Tiền hàng</td><td class="num">${money(d.itemsTotal)}</td></tr>
      <tr><td>Tổng đơn trang trại (gồm phí giao)</td><td class="num">${money(d.sellerOrderTotal)}</td></tr>
      ${d.fees.map(fee => `<tr><td>− ${esc(fee.feeName)} (${esc(fee.feeCode)})${fee.fromDraft ? ' <span class="badge Pending">mức đang soạn</span>' : ''}</td><td class="num">${money(fee.amount)}</td></tr>`).join('')}
      <tr><td><b>Nông trại thực nhận</b></td><td class="num"><b>${money(d.netAmount)}</b></td></tr>
    </tbody></table><p class="muted">Áp dụng ngày ${Utils.formatDate(d.date)}. Thuế khấu trừ thay: ${money(d.taxWithheld)} (nộp hộ, không phải doanh thu của sàn).</p>`;
  }

  // ---- small dialogs --------------------------------------------------------------------
  function askConfirm(message, needNote, onOk) {
    state.confirm = { onOk, needNote };
    document.getElementById('confirm-msg').textContent = message;
    document.getElementById('confirm-note-row').hidden = !needNote && !isAdmin();
    setVal('confirm-note', '');
    setBox('confirm-error', '');
    document.getElementById('confirm-modal').showModal();
  }

  function askEndDate(p) {
    state.confirm = { endPolicy: p };
    document.getElementById('end-title').textContent = `Kết thúc chính sách ${p.policyCode}`;
    setVal('end-date', today());
    setVal('end-reason', '');
    setBox('end-error', '');
    document.getElementById('end-modal').showModal();
  }

  async function confirmOk() {
    const { onOk, needNote } = state.confirm || {};
    const note = val('confirm-note').trim();
    if (needNote && !note) { setBox('confirm-error', 'Trường Lý do là bắt buộc.'); return; }
    document.getElementById('confirm-modal').close();
    if (onOk) await onOk(note || null);
  }

  async function endOk() {
    const p = state.confirm.endPolicy;
    const res = await API.request(`/finance/fees/policies/${p.id}/end-date`, { method: 'POST', suppressErrorToast: true,
      body: { effectiveTo: val('end-date'), reason: val('end-reason'), version: p.version } });
    if (!res.success) { setBox('end-error', firstError(res)); return; }
    document.getElementById('end-modal').close();
    Utils.showToast(res.data.message, 'success');
    if (res.data.fee) { state.current = res.data.fee; renderDetail(); }
    await load(state.page);
  }

  // ---- helpers --------------------------------------------------------------------------
  function val(id) { const el = document.getElementById(id); return el ? el.value : ''; }
  function setVal(id, v) { const el = document.getElementById(id); if (el) el.value = v; }
  function setBox(id, msg) { const el = document.getElementById(id); el.textContent = msg; el.style.display = msg ? 'block' : 'none'; }
  function firstError(res) {
    const errors = res.data && res.data.errors;
    if (errors) { const first = Object.values(errors).flat()[0]; if (first) return first; }
    return res.error || 'Thao tác không thành công.';
  }
  function clearErrors() { document.querySelectorAll('#form-modal .field-error').forEach(e => { e.textContent = ''; }); setBox('form-error', ''); }
  function showErrors(errors) {
    let shown = false;
    for (const [key, list] of Object.entries(errors)) {
      const name = key.replace(/^Policy\./i, '').replace(/^\$\.policy\./i, '');
      const el = document.getElementById(`err-${name.charAt(0).toUpperCase()}${name.slice(1)}`);
      if (el) { el.textContent = [].concat(list)[0]; shown = true; }
    }
    if (!shown) setBox('form-error', Object.values(errors).flat()[0]);
  }

  window.PlatformFees = {
    init, load, openForm: m => openForm(m, null), saveForm, preview, syncForm, confirmOk, endOk,
    addPreviewLine() {
      const row = document.createElement('div');
      row.className = 'pv-line';
      row.innerHTML = `<select>${state.categories.map(c => `<option value="${c.id}">${esc(c.name)}</option>`).join('')}</select><input type="number" min="0" step="1000" value="100000" aria-label="Tiền hàng">`;
      document.getElementById('preview-lines').appendChild(row);
    }
  };
})();
