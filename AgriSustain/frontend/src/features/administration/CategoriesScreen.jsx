import React, { useEffect, useState } from 'react';
import { request, queryString } from '../../api/client';
import { Field, Modal, Notice, Pager } from '../../components/ui/Common';

const blank = { name: '', parentId: '', description: '', priority: 0, isActive: true };
const listRoute = '/administration/categories';

/** Separates Product Category List and Details while retaining the approved image-free contract. */
export default function CategoriesScreen({ fixture, route = listRoute, navigate }) {
  const [draft, setDraft] = useState({ keyword: '', isActive: '', parent: '' });
  const [filters, setFilters] = useState({ page: 1 });
  const [result, setResult] = useState({ items: [], total: 0 });
  const [parents, setParents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [form, setForm] = useState(null);
  const [refresh, setRefresh] = useState(0);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [pending, setPending] = useState(null);
  const [busy, setBusy] = useState(false);
  const creating = route === `${listRoute}/new`;
  const categoryId = /^\/administration\/categories\/\d+$/.test(route) ? Number(route.split('/').at(-1)) : null;
  const inDetails = creating || Boolean(categoryId);
  /** Loads parent options through server paging; never assumes all categories fit in the first page. */
  async function loadParents(signal) {
    const first = await request('/categories?pageSize=100', { fixture, signal });
    const items = [...first.items];
    for (let page = 2; items.length < first.total; page += 1) {
      const next = await request(`/categories?pageSize=100&page=${page}`, { fixture, signal });
      if (!next.items.length) break;
      items.push(...next.items);
    }
    return items;
  }
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError('');
    Promise.all([request(`/categories?${queryString(filters)}`, { fixture, signal: controller.signal }),
      loadParents(controller.signal)]).then(([data, options]) => { setResult(data); setParents(options); })
      .catch((failure) => { if (failure.name !== 'AbortError') setError(failure.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [fixture, filters, refresh]);
  useEffect(() => {
    setError('');
    if (creating) { setForm({ ...blank }); setDetailLoading(false); return; }
    if (!categoryId) { setForm(null); setDetailLoading(false); return; }
    const controller = new AbortController(); setForm(null); setDetailLoading(true);
    request(`/categories/${categoryId}`, { fixture, signal: controller.signal }).then((item) =>
      setForm({ ...item, originalIsActive: item.isActive, parentId: item.parentId || '',
        description: item.description || '' }))
      .catch((failure) => { if (failure.name !== 'AbortError') setError(failure.message); })
      .finally(() => { if (!controller.signal.aborted) setDetailLoading(false); });
    return () => controller.abort();
  }, [fixture, categoryId, creating, refresh]);
  /** Applies the explicit search/filter draft while keeping list state across detail navigation. */
  function search(event) {
    event.preventDefault();
    setFilters({ keyword: draft.keyword, isActive: draft.isActive,
      parentId: draft.parent === 'root' ? '' : draft.parent, rootOnly: draft.parent === 'root' ? true : '', page: 1 });
  }
  /** Changes only local approved category fields; Cancel discards this draft without an API write. */
  function change(key, value) { setForm((old) => ({ ...old, [key]: value })); }
  /** Persists the dedicated category form; changed availability still requires explicit confirmation. */
  async function save(event) {
    event.preventDefault(); setError('');
    const statusChanged = Boolean(form.id) && form.originalIsActive !== form.isActive;
    if (statusChanged && !window.confirm('Xác nhận thay đổi trạng thái danh mục?')) return;
    setBusy(true);
    try {
      await request(`/categories${form.id ? `/${form.id}` : ''}`, { fixture,
        method: form.id ? 'PUT' : 'POST', body: { name: form.name.trim(),
          parentId: form.parentId ? Number(form.parentId) : null, description: form.description,
          priority: Number(form.priority), isActive: form.isActive, confirmed: statusChanged } });
      setMessage(form.id ? 'Đã cập nhật danh mục.' : 'Đã tạo danh mục.');
      navigate(listRoute); setRefresh((value) => value + 1);
    } catch (failure) { setError(failure.message); }
    finally { setBusy(false); }
  }
  /** Opens confirmation for one existing category, preserving server reference checks as authoritative. */
  function prepare(item, action) { setPending({ item, action }); setError(''); }
  /** Executes confirmed availability/deletion and refreshes persisted fields only after success. */
  async function execute() {
    setBusy(true); setError('');
    try {
      await request(`/categories/${pending.item.id}/${pending.action}`, {
        fixture, method: 'POST', body: { confirmed: true } });
      setMessage(pending.action === 'delete' ? 'Đã xóa danh mục.' : 'Đã cập nhật trạng thái danh mục.');
      if (pending.action === 'delete') navigate(listRoute);
      setPending(null); setRefresh((value) => value + 1);
    } catch (failure) { setError(failure.message); }
    finally { setBusy(false); }
  }
  const confirmation = pending && <Modal title="Xác nhận thay đổi danh mục"
    onClose={() => { if (!busy) setPending(null); }}><p>{pending.action === 'delete'
      ? 'Xóa danh mục chưa có sản phẩm? Các liên kết còn lại sẽ được kiểm tra trước khi xóa.'
      : `Xác nhận ${pending.action === 'activate' ? 'kích hoạt' : 'ẩn / khóa'} danh mục?`}</p>
    <p><strong>{pending.item.name}</strong></p><Notice error>{error}</Notice>
    <div className="modal-actions"><button disabled={busy} onClick={() => setPending(null)}>Hủy</button>
      <button className={pending.action === 'delete' ? 'danger' : 'primary'} disabled={busy}
        onClick={execute}>{busy ? 'Đang xử lý…' : 'Xác nhận'}</button></div></Modal>;
  if (inDetails) return <section><div className="breadcrumbs"><button className="text-button"
    onClick={() => navigate(listRoute)}>Danh mục sản phẩm</button><span>/</span>
    <span>{creating ? 'Thêm mới' : 'Chi tiết danh mục'}</span></div>
    <div className="section-heading"><div><h1>{creating ? 'Thêm danh mục sản phẩm' : 'Chi tiết danh mục'}</h1>
      <p className="muted">Thiết lập phân loại, thứ tự hiển thị và trạng thái sử dụng.</p></div></div>
    <Notice error>{!pending && error}</Notice>{detailLoading && <p className="empty" role="status">
      Đang tải chi tiết danh mục…</p>}
    {form && <form className="panel category-details" onSubmit={save}><aside className="category-overview">
      <div className="category-symbol" aria-hidden="true">▦</div><h2>{creating ? 'Danh mục mới' : form.name}</h2>
      {form.id && <><p className="muted">Mã danh mục: #{form.id}</p>
        <span className={`badge ${form.originalIsActive ? 'Active' : 'Suspended'}`}>
          {form.originalIsActive ? 'Hoạt động' : 'Ẩn / khóa'}</span>
        <dl className="details category-facts"><dt>Sản phẩm liên kết</dt><dd>{form.productCount}</dd>
          <dt>Danh mục cha</dt><dd>{form.parentName || 'Danh mục gốc'}</dd></dl>
        {form.productCount > 0 && <p className="muted">Danh mục có sản phẩm chỉ được ẩn hoặc khóa.</p>}
      </>}</aside><div className="category-fields"><fieldset disabled={busy}>
      <Field label="Tên danh mục *"><input required minLength={2} maxLength={100} value={form.name}
        placeholder="Nhập tên danh mục sản phẩm" onChange={(event) => change('name', event.target.value)} /></Field>
      <Field label="Danh mục cha"><select value={form.parentId}
        onChange={(event) => change('parentId', event.target.value)}>
        <option value="">Không có — Danh mục gốc</option>{parents.filter((item) => item.isActive && item.id !== form.id)
          .map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
      </select></Field><Field label="Mô tả"><textarea maxLength={500} value={form.description}
        placeholder="Nhập mô tả danh mục (không bắt buộc)" onChange={(event) => change('description', event.target.value)} />
      </Field><small className="character-count">{form.description.length}/500</small>
      <div className="form-grid"><Field label="Ưu tiên hiển thị"><input type="number" min={0}
        max={2147483647} step={1} required value={form.priority}
        onChange={(event) => change('priority', event.target.value)} /></Field>
      <Field label="Trạng thái *"><select required value={String(form.isActive)}
        onChange={(event) => change('isActive', event.target.value === 'true')}>
        <option value="true">Hoạt động</option><option value="false">Ẩn / khóa</option>
      </select></Field></div><p className="muted">Số ưu tiên nhỏ hơn được hiển thị trước.</p>
      </fieldset><div className="detail-footer"><button type="button" disabled={busy}
        onClick={() => navigate(listRoute)}>Hủy</button><button className="primary" disabled={busy}>
        {busy ? 'Đang lưu…' : 'Lưu danh mục'}</button></div></div>
    </form>}{!form && !detailLoading && <button onClick={() => navigate(listRoute)}>Quay lại danh sách</button>}
    {confirmation}</section>;
  return <section><div className="section-heading"><div><h1>Danh mục sản phẩm</h1>
    <p className="muted">Quản lý phân loại, trạng thái và thứ tự hiển thị trên Marketplace.</p>
  </div><button className="primary" onClick={() => navigate(`${listRoute}/new`)}>+ Thêm danh mục</button></div>
    <Notice>{message}</Notice><Notice error>{!pending && error}</Notice>
    <form className="panel search-panel" onSubmit={search}>
      <Field label="Từ khóa tìm kiếm"><input maxLength={100} placeholder="Nhập tên danh mục"
        value={draft.keyword} onChange={(event) => setDraft((old) => ({ ...old, keyword: event.target.value }))} /></Field>
      <Field label="Trạng thái"><select value={draft.isActive}
        onChange={(event) => setDraft((old) => ({ ...old, isActive: event.target.value }))}>
        <option value="">Tất cả trạng thái</option><option value="true">Hoạt động</option>
        <option value="false">Ẩn / khóa</option></select></Field>
      <Field label="Danh mục cha"><select value={draft.parent}
        onChange={(event) => setDraft((old) => ({ ...old, parent: event.target.value }))}>
        <option value="">Tất cả danh mục cha</option><option value="root">Chỉ danh mục gốc</option>
        {parents.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
      </select></Field><button className="primary" type="submit">Tìm kiếm</button></form>
    <div className="panel record-panel"><div className="record-heading"><h2>Danh sách danh mục</h2>
      <span className="count">{result.total} danh mục</span></div><div className="table-wrap"><table>
      <thead><tr><th>Mã danh mục</th><th>Tên danh mục</th><th>Danh mục cha</th><th>Ưu tiên</th>
        <th>Trạng thái</th><th>Sản phẩm</th><th>Thao tác</th></tr></thead><tbody>
      {!loading && result.items.map((item) => <tr key={item.id}><td>#{item.id}</td><td>
        <button className="text-button name-link" onClick={() => navigate(`${listRoute}/${item.id}`)}>
          {item.name}</button></td><td>{item.parentName || 'Danh mục gốc'}</td><td>{item.priority}</td>
        <td><span className={`badge ${item.isActive ? 'Active' : 'Suspended'}`}>
          {item.isActive ? 'Hoạt động' : 'Ẩn / khóa'}</span></td><td>{item.productCount}</td>
        <td><div className="actions"><button onClick={() => navigate(`${listRoute}/${item.id}`)}>Chi tiết / Sửa</button>
          <button onClick={() => prepare(item, item.isActive ? 'deactivate' : 'activate')}>
            {item.isActive ? 'Ẩn / khóa' : 'Kích hoạt'}</button>
          <button className="danger-link" disabled={item.productCount > 0}
            title={item.productCount > 0 ? 'Có sản phẩm: chỉ được ẩn hoặc khóa' : 'Xóa danh mục'}
            onClick={() => prepare(item, 'delete')}>Xóa</button></div></td></tr>)}
      </tbody></table>{loading && <p className="empty" role="status">Đang tải danh mục…</p>}
      {!loading && !result.items.length && !error && <p className="empty">Không tìm thấy kết quả phù hợp.</p>}
    </div><Pager total={result.total} page={filters.page}
      onPage={(page) => setFilters((old) => ({ ...old, page }))} /></div>{confirmation}</section>;
}
