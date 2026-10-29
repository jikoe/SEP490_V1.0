import React, { useEffect, useState } from 'react';
import { requestUseCase as request, queryString } from '../../api/client';
import { Field, Modal, Notice, Pager, localTime, money } from '../../components/ui/Common';

const statuses = {
  Active: 'Đang hoạt động', Scheduled: 'Chưa bắt đầu', Inactive: 'Ngừng hoạt động',
  Depleted: 'Hết lượt dùng', Expired: 'Hết hạn',
};
const types = { Percentage: 'Giảm theo %', FixedAmount: 'Giảm số tiền cố định' };
const pad = (value) => String(value).padStart(2, '0');
/** Formats a UTC instant for a datetime-local input in the browser's timezone. */
const toLocalInput = (value) => {
  const date = new Date(value);
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
};
const emptyForm = () => {
  const start = new Date();
  const end = new Date(start.getTime() + 30 * 24 * 3600 * 1000);
  return {
    voucherCode: '', discountType: 'Percentage', discountValue: '', maxDiscountAmount: '',
    minOrderAmount: '0', usageLimit: '', startDate: toLocalInput(start), endDate: toLocalInput(end),
    isActive: true,
  };
};

function StatusBadge({ status }) {
  const tone = status === 'Active' ? 'Active' : status === 'Inactive' || status === 'Expired' ? 'Rejected' : '';
  return <span className={`badge ${tone}`}>{statuses[status] || status}</span>;
}

function discountText(voucher) {
  return voucher.discountType === 'Percentage'
    ? `${voucher.discountValue}%${voucher.maxDiscountAmount ? ` (tối đa ${money(voucher.maxDiscountAmount)})` : ''}`
    : money(voucher.discountValue);
}

/** UC-PS-01 Manage Promotions and Vouchers (SRS 4.4.1 fields 1–8). */
export default function VoucherManagementScreen() {
  const [draft, setDraft] = useState({ status: '', discountType: '', search: '' });
  const [filters, setFilters] = useState({ ...draft, page: 1 });
  const [result, setResult] = useState({ items: [], total: 0 });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [refresh, setRefresh] = useState(0);
  const [editing, setEditing] = useState(null);
  const [confirm, setConfirm] = useState(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError('');
    request(`/promotions/vouchers?${queryString(filters)}`, { signal: controller.signal })
      .then(setResult).catch((failure) => { if (failure.name !== 'AbortError') setError(failure.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [filters, refresh]);

  function openEditor(voucher) {
    setError('');
    setEditing(voucher ? {
      id: voucher.voucherId, termsLocked: voucher.termsLocked, form: {
        voucherCode: voucher.voucherCode, discountType: voucher.discountType,
        discountValue: String(voucher.discountValue),
        maxDiscountAmount: voucher.maxDiscountAmount == null ? '' : String(voucher.maxDiscountAmount),
        minOrderAmount: String(voucher.minOrderAmount),
        usageLimit: voucher.usageLimit == null ? '' : String(voucher.usageLimit),
        startDate: toLocalInput(voucher.startDate), endDate: toLocalInput(voucher.endDate),
        isActive: voucher.isActive,
      },
    } : { id: null, termsLocked: false, form: emptyForm() });
  }

  function setForm(field, value) {
    setEditing((current) => ({ ...current, form: { ...current.form, [field]: value } }));
  }

  async function run(action, done) {
    setBusy(true); setError('');
    try {
      const response = await action();
      setMessage(response.message);
      done();
      setRefresh((value) => value + 1);
    } catch (failure) {
      setError(failure.message);
    } finally {
      setBusy(false);
    }
  }

  function save(event) {
    event.preventDefault();
    const { form } = editing;
    if (new Date(form.endDate) <= new Date(form.startDate)) {
      setError('Ngày kết thúc phải sau ngày bắt đầu.');
      return;
    }
    const body = {
      voucherCode: form.voucherCode.trim(),
      discountType: form.discountType,
      discountValue: Number(form.discountValue),
      maxDiscountAmount: form.maxDiscountAmount === '' ? null : Number(form.maxDiscountAmount),
      minOrderAmount: Number(form.minOrderAmount || 0),
      usageLimit: form.usageLimit === '' ? null : Number(form.usageLimit),
      startDate: new Date(form.startDate).toISOString(),
      endDate: new Date(form.endDate).toISOString(),
      isActive: form.isActive,
    };
    run(() => request(editing.id ? `/promotions/vouchers/${editing.id}` : '/promotions/vouchers',
      { method: editing.id ? 'PUT' : 'POST', body }), () => setEditing(null));
  }

  function confirmAction() {
    const { voucher, action } = confirm;
    run(() => action === 'delete'
      ? request(`/promotions/vouchers/${voucher.voucherId}`, { method: 'DELETE' })
      : request(`/promotions/vouchers/${voucher.voucherId}/${action}`, { method: 'POST' }),
    () => setConfirm(null));
  }

  const locked = editing?.termsLocked;
  return <section>
    <div className="section-heading"><div><p className="eyebrow">KHUYẾN MẠI & CHIẾN DỊCH</p>
      <h1>Quản lý khuyến mại & voucher</h1>
      <p className="muted">Cấu hình mã giảm giá, điều kiện áp dụng, giới hạn lượt dùng và thời hạn.</p></div>
      <button className="primary" type="button" onClick={() => openEditor(null)}>+ Tạo voucher mới</button></div>
    <Notice>{message}</Notice><Notice error>{!editing && !confirm && error}</Notice>

    <form className="panel filters" onSubmit={(event) => { event.preventDefault(); setFilters({ ...draft, page: 1 }); }}>
      <Field label="Tìm kiếm"><input value={draft.search} maxLength={50} placeholder="Mã voucher"
        onChange={(event) => setDraft({ ...draft, search: event.target.value })} /></Field>
      <Field label="Trạng thái"><select value={draft.status}
        onChange={(event) => setDraft({ ...draft, status: event.target.value })}>
        <option value="">Tất cả trạng thái</option>
        {Object.entries(statuses).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
      </select></Field>
      <Field label="Loại"><select value={draft.discountType}
        onChange={(event) => setDraft({ ...draft, discountType: event.target.value })}>
        <option value="">Tất cả loại</option>
        {Object.entries(types).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
      </select></Field>
      <button className="primary" type="submit">Tìm kiếm</button>
    </form>

    <div className="panel"><div className="table-wrap"><table>
      <thead><tr><th>Mã voucher</th><th>Mức giảm</th><th>Đơn tối thiểu</th><th>Lượt dùng</th>
        <th>Hiệu lực</th><th>Trạng thái</th><th>Thao tác</th></tr></thead>
      <tbody>{result.items.map((voucher) => <tr key={voucher.voucherId}>
        <td><strong>{voucher.voucherCode}</strong></td>
        <td>{discountText(voucher)}</td><td>{money(voucher.minOrderAmount)}</td>
        <td>{voucher.usedCount}{voucher.reservedCount ? ` (+${voucher.reservedCount} giữ chỗ)` : ''}
          {' / '}{voucher.usageLimit ?? '∞'}</td>
        <td>{localTime(voucher.startDate)}<br />→ {localTime(voucher.endDate)}</td>
        <td><StatusBadge status={voucher.status} /></td>
        <td><div className="actions">
          <button type="button" onClick={() => openEditor(voucher)}>Sửa</button>
          {voucher.isActive
            ? <button type="button" onClick={() => setConfirm({ voucher, action: 'deactivate' })}>Ngừng</button>
            : <button type="button" onClick={() => setConfirm({ voucher, action: 'activate' })}>Kích hoạt</button>}
          <button type="button" className="danger-link"
            onClick={() => setConfirm({ voucher, action: 'delete' })}>Xóa</button>
        </div></td>
      </tr>)}</tbody>
    </table>
    {loading && <p className="empty" role="status">Đang tải danh sách voucher…</p>}
    {!loading && !result.items.length && <p className="empty">
      Không có khuyến mại hoặc voucher nào khớp với điều kiện đã chọn.</p>}
    </div>
    <Pager total={result.total} page={filters.page} pageSize={result.pageSize || 10}
      onPage={(page) => setFilters({ ...filters, page })} /></div>

    {editing && <Modal title={editing.id ? 'Cập nhật voucher' : 'Tạo voucher mới'}
      onClose={() => { if (!busy) setEditing(null); }}>
      <form onSubmit={save}>
        {locked && <p className="muted">Voucher đã được sử dụng: chỉ đổi được thời hạn, giới hạn lượt dùng và
          trạng thái.</p>}
        <Field label="Mã voucher *"><input required autoFocus minLength={3} maxLength={50}
          pattern="[A-Za-z0-9_\-]+" disabled={locked} value={editing.form.voucherCode}
          onChange={(event) => setForm('voucherCode', event.target.value.toUpperCase())} /></Field>
        <Field label="Kiểu giảm giá *"><select required disabled={locked} value={editing.form.discountType}
          onChange={(event) => setForm('discountType', event.target.value)}>
          {Object.entries(types).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select></Field>
        <Field label={editing.form.discountType === 'Percentage' ? 'Giá trị giảm (%) *' : 'Giá trị giảm (₫) *'}>
          <input type="number" required min="0.01" step="0.01" disabled={locked}
            max={editing.form.discountType === 'Percentage' ? 100 : undefined}
            value={editing.form.discountValue}
            onChange={(event) => setForm('discountValue', event.target.value)} /></Field>
        <Field label="Mức giảm tối đa (₫)"><input type="number" min="1" step="1" disabled={locked}
          value={editing.form.maxDiscountAmount}
          onChange={(event) => setForm('maxDiscountAmount', event.target.value)} /></Field>
        <Field label="Giá trị đơn tối thiểu (₫) *"><input type="number" required min="0" step="1"
          disabled={locked} value={editing.form.minOrderAmount}
          onChange={(event) => setForm('minOrderAmount', event.target.value)} /></Field>
        <Field label="Giới hạn lượt dùng" hint="Để trống nếu không giới hạn."><input type="number" min="1"
          step="1" value={editing.form.usageLimit}
          onChange={(event) => setForm('usageLimit', event.target.value)} /></Field>
        <Field label="Bắt đầu *"><input type="datetime-local" required value={editing.form.startDate}
          onChange={(event) => setForm('startDate', event.target.value)} /></Field>
        <Field label="Kết thúc *"><input type="datetime-local" required value={editing.form.endDate}
          onChange={(event) => setForm('endDate', event.target.value)} /></Field>
        <label><input type="checkbox" checked={editing.form.isActive}
          onChange={(event) => setForm('isActive', event.target.checked)} /> Kích hoạt</label>
        <Notice error>{error}</Notice>
        <div className="modal-actions"><button type="button" disabled={busy} onClick={() => setEditing(null)}>
          Hủy</button><button className="primary" disabled={busy}>{busy ? 'Đang lưu…' : 'Lưu'}</button></div>
      </form>
    </Modal>}

    {confirm && <Modal title="Xác nhận" onClose={() => { if (!busy) setConfirm(null); }}>
      <p>{confirm.action === 'delete' ? 'Xóa' : confirm.action === 'activate' ? 'Kích hoạt' : 'Ngừng hoạt động'}
        {' '}voucher <strong>{confirm.voucher.voucherCode}</strong>?</p>
      {confirm.action === 'delete' && confirm.voucher.termsLocked && <p className="muted">
        Voucher đã được sử dụng nên sẽ được lưu trữ (ngừng hoạt động) thay vì xóa.</p>}
      <Notice error>{error}</Notice>
      <div className="modal-actions"><button type="button" disabled={busy} onClick={() => setConfirm(null)}>
        Hủy</button><button className={confirm.action === 'delete' ? 'danger' : 'primary'} type="button"
        disabled={busy} onClick={confirmAction}>{busy ? 'Đang xử lý…' : 'Xác nhận'}</button></div>
    </Modal>}
  </section>;
}
