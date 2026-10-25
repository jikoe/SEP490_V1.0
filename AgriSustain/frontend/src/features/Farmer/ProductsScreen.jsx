import React, { useState } from 'react';
import { request, queryString } from '../../api/client';
import { Field, Notice, Pager, ProductThumbnail, money } from '../../components/ui/Common';
import { Confirm, Empty, Errors, Status, Uploads, productStatus, useFeatureData } from '../Commerce/Shared';

const initialFilters = { keyword: '', categoryId: '', approvalStatus: '', isPublished: '', page: 1 };
const blank = { productName: '', categoryId: '', unit: 'kg', originProvince: '', description: '',
  baseRetailPrice: '', wholesaleMinQuantity: '', wholesalePrice: '', images: [] };

export default function ProductsScreen({ route, navigate }) {
  const [draftFilters, setDraftFilters] = useState(initialFilters);
  const [filters, setFilters] = useState(initialFilters);
  const state = useFeatureData(signal => Promise.all([
    request('/farmer/products?' + queryString(filters), { signal }),
    request('/farmer/products/options', { signal })
  ]).then(([list, options]) => ({ list, options })), [JSON.stringify(filters)]);
  const [message, setMessage] = useState('');
  const [pending, setPending] = useState(null);
  const [busy, setBusy] = useState(false);
  const suffix = route.slice('/farmer/products'.length);
  if (suffix === '/new' || /^\/\d+\/edit$/.test(suffix)) {
    const id = suffix === '/new' ? null : Number(suffix.split('/')[1]);
    return <ProductEditor id={id} navigate={navigate} />;
  }
  const list = state.data?.list;
  const options = state.data?.options;
  async function visibility() {
    setBusy(true); state.setError(null);
    try {
      await request('/farmer/products/' + pending.productId + '/visibility', {
        method: 'PUT', body: { isPublished: !pending.isPublished, version: pending.version } });
      setMessage(pending.isPublished ? 'Đã ẩn sản phẩm (MSG74).' : 'Đã hiển thị sản phẩm (MSG164).');
      setPending(null); state.reload();
    } catch (error) { state.setError(error); }
    finally { setBusy(false); }
  }
  return <div className="commerce">
    <div className="commerce-top"><div><p className="commerce-subtitle">UC-FAR-03 · Nông trại</p>
      <h1>Quản lý sản phẩm</h1><p className="muted">Theo dõi kiểm duyệt, giá bán và lượng hàng từ các lô thực tế.</p></div>
      <button type="button" className="primary" disabled={!options?.canManage}
        onClick={() => navigate('/farmer/products/new')}>+ Đăng sản phẩm</button></div>
    <Errors error={state.error} /><Notice>{message}</Notice>
    {options && !options.canManage && <Notice error>Hồ sơ nông trại chưa được duyệt.
      Bạn chưa thể gửi duyệt hoặc hiển thị sản phẩm (MSG75).</Notice>}
    <section className="panel"><form className="commerce-filters padded"
      onSubmit={event => { event.preventDefault(); setFilters({ ...draftFilters, page: 1 }); }}>
      <Field label="Tìm kiếm sản phẩm"><input maxLength={255} placeholder="Tên sản phẩm…"
        value={draftFilters.keyword} onChange={event => setDraftFilters({ ...draftFilters, keyword: event.target.value })} /></Field>
      <Field label="Danh mục"><select value={draftFilters.categoryId} onChange={event =>
        setDraftFilters({ ...draftFilters, categoryId: event.target.value })}><option value="">Tất cả</option>
        {options?.categories.map(category => <option key={category.categoryId} value={category.categoryId}>
          {category.categoryName}</option>)}</select></Field>
      <Field label="Kiểm duyệt"><select value={draftFilters.approvalStatus} onChange={event =>
        setDraftFilters({ ...draftFilters, approvalStatus: event.target.value })}><option value="">Tất cả trạng thái</option>
        {Object.entries(productStatus).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></Field>
      <Field label="Hiển thị"><select value={draftFilters.isPublished} onChange={event =>
        setDraftFilters({ ...draftFilters, isPublished: event.target.value })}><option value="">Tất cả</option>
        <option value="true">Đang bán</option><option value="false">Đã ẩn / Chưa hiển thị</option></select></Field>
      <button className="primary">Áp dụng</button><button type="button" onClick={() => {
        setDraftFilters(initialFilters); setFilters(initialFilters);
      }}>Xóa bộ lọc</button></form>
      {state.loading ? <p className="padded" role="status">Đang tải sản phẩm…</p>
        : list?.items.length === 0 ? <Empty>Không có sản phẩm phù hợp (MSG124)</Empty>
        : <div className="table-wrap"><table><thead><tr><th>Sản phẩm / Đơn vị</th>
          <th>Nhóm / Xuất xứ</th><th>Giá bán</th><th>Tồn kho / Trạng thái</th><th>Thao tác</th></tr></thead>
          <tbody>{list?.items.map(product => <tr key={product.productId}>
            <td><div className="commerce-product-cell"><ProductThumbnail
              src={product.images.find(image => image.isPrimary)?.imageUrl} name={product.productName} />
              <div><strong>{product.productName}</strong><small>{product.unit}</small></div></div></td>
            <td>{product.categoryName}<small>{product.originProvince}</small></td>
            <td>Lẻ {money(product.baseRetailPrice)}/{product.unit}
              {product.wholesalePrice != null && <small>Sỉ {money(product.wholesalePrice)}
                {' từ ' + product.wholesaleMinQuantity + ' ' + product.unit}</small>}</td>
            <td><strong>{product.availableQuantity} {product.unit}</strong><small>{product.batchCount} lô thực tế</small>
              <Status value={product.approvalStatus} labels={productStatus} />
              <small>{product.isPublished ? 'Đang bán' : 'Chưa hiển thị'}</small></td>
            <td><div className="commerce-actions"><button type="button" onClick={() =>
              navigate('/farmer/products/' + product.productId + '/edit')}>Chi tiết / sửa</button>
              {(product.isPublished || product.approvalStatus === 'Approved') && <button
                type="button" disabled={!options?.canManage} onClick={() => setPending(product)}>
                {product.isPublished ? 'Ẩn' : 'Hiển thị'}</button>}</div>
              {product.rejectionReason && <small>Lý do từ chối: {product.rejectionReason}</small>}</td>
          </tr>)}</tbody></table></div>}
      {list && <Pager total={list.total} page={list.page} onPage={page => setFilters({ ...filters, page })} />}
    </section>
    {pending && <Confirm title={pending.isPublished ? 'Ẩn sản phẩm?' : 'Hiển thị sản phẩm?'}
      busy={busy} onClose={() => setPending(null)} onConfirm={visibility}>
      {pending.productName}. Đơn hàng và lịch sử lô hàng vẫn được lưu.</Confirm>}
  </div>;
}

function ProductEditor({ id, navigate }) {
  const [form, setForm] = useState(blank);
  const [original, setOriginal] = useState(null);
  const [options, setOptions] = useState(null);
  const [dirty, setDirty] = useState(false);
  const [busy, setBusy] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [confirm, setConfirm] = useState(null);
  const [error, setError] = useState(null);
  const [message, setMessage] = useState('');
  const load = useFeatureData(signal => Promise.all([
    request('/farmer/products/options', { signal }), id
      ? request('/farmer/products/' + id, { signal }) : Promise.resolve(null)
  ]).then(([settings, product]) => {
    setOptions(settings); setOriginal(product);
    setForm(product ? { ...product, description: product.description || '',
      originProvince: product.originProvince || '', wholesalePrice: product.wholesalePrice ?? '',
      wholesaleMinQuantity: product.wholesaleMinQuantity ?? '' } : blank);
    return true;
  }), [id]);
  function update(key, value) { setForm(old => ({ ...old, [key]: value })); setDirty(true); }
  function validate() {
    if (!form.productName.trim() || !form.categoryId || !form.unit.trim()
        || !form.originProvince.trim() || !form.description.trim()) return 'Điền đầy đủ các trường bắt buộc.';
    const retail = Number(form.baseRetailPrice);
    if (!Number.isFinite(retail) || retail <= 0 || retail > 999999999999
        || Math.abs(Math.round(retail * 100) - retail * 100) > 0.000001) return 'Giá lẻ phải lớn hơn 0, tối đa 2 số thập phân (MSG77).';
    const hasWholesale = form.wholesalePrice !== '' || form.wholesaleMinQuantity !== '';
    if (hasWholesale && (!(Number(form.wholesaleMinQuantity) > 1)
        || !(Number(form.wholesalePrice) > 0) || Number(form.wholesalePrice) > retail)) {
      return 'Ngưỡng sỉ > 1; giá sỉ > 0 và không vượt giá lẻ. Nhập đủ cả hai trường (MSG77).';
    }
    if (!form.images.length || form.images.filter(image => image.isPrimary).length !== 1) {
      return 'Cần ít nhất một ảnh và đúng một ảnh đại diện (MSG76).';
    }
    return null;
  }
  function submit(event) {
    event.preventDefault(); setError(null);
    const invalid = validate();
    if (invalid) { setError(new Error(invalid)); return; }
    setConfirm('save');
  }
  async function save() {
    setBusy(true); setError(null);
    try {
      const result = await request('/farmer/products' + (id ? '/' + id : ''), {
        method: id ? 'PUT' : 'POST', body: { ...form, categoryId: Number(form.categoryId),
          baseRetailPrice: Number(form.baseRetailPrice),
          wholesaleMinQuantity: form.wholesaleMinQuantity === '' ? null : Number(form.wholesaleMinQuantity),
          wholesalePrice: form.wholesalePrice === '' ? null : Number(form.wholesalePrice) } });
      setDirty(false); setOriginal(result); setConfirm(null);
      setForm({ ...result, wholesalePrice: result.wholesalePrice ?? '',
        wholesaleMinQuantity: result.wholesaleMinQuantity ?? '' });
      setMessage(result.approvalStatus === 'Pending' ? 'Đã gửi kiểm duyệt (MSG71). Sản phẩm chưa hiển thị công khai.'
        : 'Đã lưu thay đổi giá (MSG72). Giá của đơn hàng cũ được giữ nguyên.');
      if (!id) navigate('/farmer/products/' + result.productId + '/edit');
    } catch (failure) { setError(failure); setConfirm(null); }
    finally { setBusy(false); }
  }
  function cancel() { dirty ? setConfirm('cancel') : navigate('/farmer/products'); }
  if (load.loading) return <p role="status">Đang tải thông tin sản phẩm…</p>;
  return <div className="commerce"><div className="commerce-top"><div>
    <p className="commerce-subtitle">UC-FAR-03 · Đăng / chỉnh sửa</p>
    <h1>{id ? 'Thông tin sản phẩm' : 'Đăng sản phẩm mới'}</h1>
    <p className="muted">Thay đổi nội dung đã duyệt sẽ được gửi kiểm duyệt lại.</p></div>
    <button type="button" onClick={cancel}>← Danh sách sản phẩm</button></div>
    <Errors error={error || load.error} /><Notice>{message}</Notice>
    {original?.rejectionReason && <Notice error>Lý do từ chối: {original.rejectionReason}</Notice>}
    {options && !options.canManage && <Notice error>Hồ sơ nông trại chưa được duyệt (MSG75).</Notice>}
    <form onSubmit={submit}><fieldset disabled={busy || uploading || !options?.canManage}>
      <section className="commerce-section"><h2>Thông tin sản phẩm</h2><div className="commerce-form-grid">
        <Field label="Tên sản phẩm *"><input required maxLength={255} value={form.productName}
          onChange={event => update('productName', event.target.value)} /></Field>
        <Field label="Danh mục đang hoạt động *"><select required value={form.categoryId}
          onChange={event => update('categoryId', event.target.value)}><option value="">Chọn danh mục</option>
          {options?.categories.map(category => <option key={category.categoryId} value={category.categoryId}>
            {category.categoryName}</option>)}</select></Field>
        <Field label={'Đơn vị *' + (original?.unitLocked ? ' · Đã có lô/đơn, không thể đổi' : '')}>
          <input required maxLength={50} readOnly={original?.unitLocked} value={form.unit}
            onChange={event => update('unit', event.target.value)} list="product-units" /></Field>
        <datalist id="product-units">{['kg', 'g', 'hộp', 'túi', 'quả', 'bó', 'lít'].map(unit => <option key={unit}>{unit}</option>)}</datalist>
        <Field label="Tỉnh/Thành phố xuất xứ *"><input required maxLength={100}
          value={form.originProvince} onChange={event => update('originProvince', event.target.value)} /></Field>
        <Field label="Mô tả sản phẩm *" className="field commerce-full"><textarea required rows={5}
          maxLength={10000} value={form.description} onChange={event => update('description', event.target.value)} /></Field>
        <Field label="Giá bán lẻ (đ/đơn vị) *"><input type="number" required min="0.01" step="0.01"
          max="999999999999" value={form.baseRetailPrice}
          onChange={event => update('baseRetailPrice', event.target.value)} /></Field>
        <div /><Field label="Ngưỡng bán sỉ"><input type="number" min="1.001" step="0.001"
          max="999999999" value={form.wholesaleMinQuantity}
          onChange={event => update('wholesaleMinQuantity', event.target.value)} /></Field>
        <Field label="Giá bán sỉ (đ/đơn vị)"><input type="number" min="0.01" step="0.01"
          max={form.baseRetailPrice || undefined} value={form.wholesalePrice}
          onChange={event => update('wholesalePrice', event.target.value)} /></Field>
      </div><p className="muted">Bỏ trống cả giá và ngưỡng khi không bán sỉ. Giá sỉ &gt; 0 và ≤ giá lẻ; ngưỡng sỉ &gt; 1.</p></section>
      <section className="commerce-section"><Uploads files={form.images} onError={setError} onBusyChange={setUploading}
        disabled={busy} onChange={images => update('images', images)} /></section>
      {original && <section className="commerce-section"><div className="commerce-actions">
        <Status value={original.approvalStatus} labels={productStatus} />
        <span>{original.isPublished ? 'Đang bán' : 'Chưa hiển thị'}</span>
        <span>Tồn kho: {original.availableQuantity} {original.unit} · {original.batchCount} lô</span>
      </div><p className="muted">Thao tác ẩn/hiển thị được xác nhận tại danh sách sản phẩm.</p></section>}
      <div className="commerce-actions"><button type="button" onClick={cancel}>Hủy thay đổi</button>
        <button className="primary">{busy ? 'Đang lưu…' : original?.approvalStatus === 'Approved'
          ? 'Lưu thay đổi / Gửi lại nếu sửa nội dung' : 'Gửi duyệt / Gửi lại'}</button></div>
    </fieldset></form>
    {confirm && <Confirm title={confirm === 'cancel' ? 'Hủy thay đổi?' : 'Xác nhận lưu sản phẩm'} busy={busy}
      onClose={() => setConfirm(null)} onConfirm={confirm === 'cancel'
        ? () => navigate('/farmer/products') : save}>
      {confirm === 'cancel' ? 'Các thay đổi chưa lưu sẽ bị bỏ.'
        : 'Sửa nội dung sẽ gửi kiểm duyệt lại. Thay đổi riêng giá giữ nguyên kết quả duyệt.'}</Confirm>}
  </div>;
}



