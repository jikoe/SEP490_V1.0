import { productUnitGroups, productUnits } from './productUnits';
import React, { useEffect, useRef, useState } from 'react';
import { productListingMessages as messages, productFieldError, productRejected } from './productListingMessages';
import { request, queryString } from '../../api/client';
import { Field, Notice, Pager, ProductThumbnail, money } from '../../components/ui/Common';
import { Confirm, Empty, Errors, Status, Uploads, productStatus, useFeatureData } from '../Commerce/Shared';

const initialFilters = { keyword: '', categoryId: '', approvalStatus: '', isPublished: '', page: 1 };
const blank = {
  productName: '', categoryId: '', unit: '', description: '',
  baseRetailPrice: '', wholesaleMinQuantity: '', wholesalePrice: '', images: []
};

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
        method: 'PUT', body: { isPublished: !pending.isPublished, version: pending.version }
      });
      setMessage(pending.isPublished ? messages.MSG74 : messages.MSG164);
      setPending(null); state.reload();
    } catch (error) { state.setError(error); }
    finally { setBusy(false); }
  }
  return <div className="commerce product-listing">
    <div className="commerce-top"><div><p className="commerce-subtitle">UC-FAR-03 · Nông trại</p>
      <h1>Quản lý sản phẩm</h1><p className="muted">Theo dõi kiểm duyệt, giá bán và lượng hàng từ các lô thực tế.</p></div>
      <button type="button" className="primary" disabled={!options?.canManage}
        onClick={() => navigate('/farmer/products/new')}>+ Đăng sản phẩm</button></div>
    <Errors hideMessageCodes error={state.error} /><Notice>{message}</Notice>
    {options && !options.canManage && <Notice error>{messages.MSG75}</Notice>}
    <section className="panel"><form className="commerce-filters product-filters padded"
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
        : list?.items.length === 0 ? <Empty>{messages.MSG124}</Empty>
          : <div className="table-wrap"><table><thead><tr><th>Sản phẩm / Đơn vị</th>
            <th>Danh mục</th><th>Giá bán</th><th>Tồn kho / Trạng thái</th><th>Thao tác</th></tr></thead>
            <tbody>{list?.items.map(product => <tr key={product.productId}>
              <td><div className="commerce-product-cell"><ProductThumbnail
                src={product.images.find(image => image.isPrimary)?.imageUrl} name={product.productName} />
                <div><strong>{product.productName}</strong><small>{product.unit}</small></div></div></td>
              <td>{product.categoryName}</td>
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
                {product.rejectionReason && <small>{productRejected(product.productName, product.rejectionReason)}</small>}</td>
            </tr>)}</tbody></table></div>}
      {list && <Pager total={list.total} page={list.page} onPage={page => setFilters({ ...filters, page })} />}
    </section>
    {pending && <Confirm title={pending.isPublished ? 'Ẩn sản phẩm?' : 'Hiển thị sản phẩm?'}
      busy={busy} onClose={() => setPending(null)} onConfirm={visibility}>
      {pending.isPublished ? messages.MSG73 : 'Hiển thị sản phẩm đã được duyệt?'} {pending.productName}. Đơn hàng và lịch sử lô hàng vẫn được lưu.</Confirm>}
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
  const [priceOnly, setPriceOnly] = useState(false);
  const saveGuard = useRef(false);
  useEffect(() => {
    const preventLoss = event => { if (dirty) { event.preventDefault(); event.returnValue = ''; } };
    window.addEventListener('beforeunload', preventLoss);
    return () => window.removeEventListener('beforeunload', preventLoss);
  }, [dirty]);
  const [error, setError] = useState(null);
  const [message, setMessage] = useState('');
  const load = useFeatureData(signal => Promise.all([
    request('/farmer/products/options', { signal }), id
      ? request('/farmer/products/' + id, { signal }) : Promise.resolve(null)
  ]).then(([settings, product]) => {
    setOptions(settings); setOriginal(product);
    setForm(product ? {
      ...product, description: product.description || '',
      wholesalePrice: product.wholesalePrice ?? '',
      wholesaleMinQuantity: product.wholesaleMinQuantity ?? ''
    } : blank);
    return true;
  }), [id]);
  function update(key, value) { setForm(old => ({ ...old, [key]: value })); setDirty(true); }
  function validate() {
    if (!form.productName.trim() || !form.categoryId || !form.unit.trim()
      || !form.description.trim() || (!productUnits.includes(form.unit) && form.unit !== original?.unit)) return productFieldError('thông tin sản phẩm', 'điền đầy đủ các trường bắt buộc');
    const retail = Number(form.baseRetailPrice);
    if (!Number.isFinite(retail) || retail <= 0 || retail > 999999999999
      || Math.abs(Math.round(retail * 100) - retail * 100) > 0.000001) return messages.MSG77 + ' Giá bán lẻ tối đa 999999999999 và 2 chữ số thập phân.';
    const hasWholesale = form.wholesalePrice !== '' || form.wholesaleMinQuantity !== '';
    if (hasWholesale && (!Number.isFinite(Number(form.wholesaleMinQuantity))
      || !Number.isFinite(Number(form.wholesalePrice)) || !(Number(form.wholesaleMinQuantity) > 1)
      || Number(form.wholesaleMinQuantity) > 999999999
      || Math.abs(Number(form.wholesaleMinQuantity) * 1000 - Math.round(Number(form.wholesaleMinQuantity) * 1000)) > 1e-6
      || Math.abs(Number(form.wholesalePrice) * 100 - Math.round(Number(form.wholesalePrice) * 100)) > 1e-6
      || !(Number(form.wholesalePrice) > 0) || Number(form.wholesalePrice) > retail)) {
      return messages.MSG77;
    }
    if (!form.images.length || form.images.length > 10 || new Set(form.images.map(image => image.imageUrl)).size !== form.images.length || form.images.filter(image => image.isPrimary).length !== 1) {
      return messages.MSG76;
    }
    return null;
  }
  function contentChanged() {
    if (!original) return true;
    return ['productName', 'categoryId', 'unit', 'description'].some(key =>
      String(form[key] ?? '').trim() !== String(original[key] ?? '').trim())
      || JSON.stringify(form.images) !== JSON.stringify(original.images);
  }
  function submit(event, onlyPrice = false) {
    event.preventDefault(); setError(null);
    if (onlyPrice && contentChanged()) {
      setError(new Error('Nội dung sản phẩm đã thay đổi. Vui lòng chọn Gửi duyệt để kiểm duyệt lại.')); return;
    }
    setPriceOnly(onlyPrice);
    const invalid = validate();
    if (invalid) { setError(new Error(invalid)); return; }
    setConfirm('save');
  }
  async function save() {
    if (saveGuard.current) return;
    saveGuard.current = true;
    setBusy(true); setError(null);
    try {
      const result = await request('/farmer/products' + (id ? '/' + id : ''), {
        method: id ? 'PUT' : 'POST', body: {
          ...form, priceOnly, categoryId: Number(form.categoryId),
          baseRetailPrice: Number(form.baseRetailPrice),
          wholesaleMinQuantity: form.wholesaleMinQuantity === '' ? null : Number(form.wholesaleMinQuantity),
          wholesalePrice: form.wholesalePrice === '' ? null : Number(form.wholesalePrice)
        }
      });
      setDirty(false); setOriginal(result); setConfirm(null);
      setForm({
        ...result, wholesalePrice: result.wholesalePrice ?? '',
        wholesaleMinQuantity: result.wholesaleMinQuantity ?? ''
      });
      setMessage((result.message || ((!id || (!priceOnly && original?.approvalStatus === 'Rejected')) ? messages.MSG71 : messages.MSG72))
        + (result.notificationPending ? ' ' + result.notificationMessage : ''));
      if (!id) navigate('/farmer/products/' + result.productId + '/edit');
    } catch (failure) { setError(failure); setConfirm(null); }
    finally { saveGuard.current = false; setBusy(false); }
  }
  function cancel() { dirty ? setConfirm('cancel') : navigate('/farmer/products'); }
  if (load.loading) return <p role="status">Đang tải thông tin sản phẩm…</p>;
  return <div className="commerce product-listing"><div className="commerce-top"><div>
    <p className="commerce-subtitle">UC-FAR-03 · Đăng / chỉnh sửa</p>
    <h1>{id ? 'Thông tin sản phẩm' : 'Đăng sản phẩm mới'}</h1>
    <p className="muted">Thay đổi nội dung đã duyệt sẽ được gửi kiểm duyệt lại.</p></div>
    <button type="button" onClick={cancel}>← Danh sách sản phẩm</button></div>
    <Errors hideMessageCodes error={error || load.error} /><Notice>{message}</Notice>
    {original?.rejectionReason && <Notice error={original.approvalStatus === 'Rejected'}>
      {original.approvalStatus === 'Rejected' ? productRejected(original.productName, original.rejectionReason)
        : 'Lý do từ chối của lần duyệt trước: ' + original.rejectionReason}</Notice>}
    {options && !options.canManage && <Notice error>{messages.MSG75}</Notice>}
    <form onSubmit={submit} noValidate><fieldset disabled={busy || uploading || !options?.canManage}>
      <section className="commerce-section"><h2>Thông tin sản phẩm</h2><div className="commerce-form-grid product-form-grid">
        <Field label="Tên sản phẩm *"><input required maxLength={255} value={form.productName}
          onChange={event => update('productName', event.target.value)} /></Field>
        <Field label="Danh mục đang hoạt động *"><select required value={form.categoryId}
          onChange={event => update('categoryId', event.target.value)}><option value="">Chọn danh mục</option>
          {options?.categories.map(category => <option key={category.categoryId} value={category.categoryId}>
            {category.categoryName}</option>)}</select></Field>
        <Field label={'Đơn vị *' + (original?.unitLocked ? ' · Đã có lô/đơn, không thể đổi' : '')}>
          <select required disabled={original?.unitLocked} value={form.unit}
            onChange={event => update('unit', event.target.value)}>
            <option value="">Chọn đơn vị</option>
            {original?.unit && !productUnits.includes(original.unit) && <option value={original.unit}>{original.unit} (đơn vị hiện có)</option>}
            {productUnitGroups.map(group => <optgroup label={group.label} key={group.label}>
              {group.values.map(([value, label]) => <option key={value} value={value}>{label}</option>)}
            </optgroup>)}
          </select></Field>
        <Field label="Mô tả sản phẩm *" className="field commerce-full"><textarea required rows={5}
          maxLength={10000} value={form.description} onChange={event => update('description', event.target.value)} /></Field>
        <Field label="Giá bán lẻ (đ/đơn vị) *"><input type="number" required min="0.01" step="0.01"
          max="999999999999" value={form.baseRetailPrice}
          onChange={event => update('baseRetailPrice', event.target.value)} /></Field>
        <Field label="Ngưỡng bán sỉ"><input type="number" min="1.001" step="0.001"
          max="999999999" value={form.wholesaleMinQuantity}
          onChange={event => update('wholesaleMinQuantity', event.target.value)} /></Field>
        <Field label="Giá bán sỉ (đ/đơn vị)"><input type="number" min="0.01" step="0.01"
          max={form.baseRetailPrice || undefined} value={form.wholesalePrice}
          onChange={event => update('wholesalePrice', event.target.value)} /></Field>
        <Field label="Chiết khấu giá sỉ"><output>{Number(form.baseRetailPrice) > 0 && form.wholesalePrice !== '' && Number(form.wholesalePrice) > 0 && Number(form.wholesalePrice) <= Number(form.baseRetailPrice)
          ? ((1 - Number(form.wholesalePrice) / Number(form.baseRetailPrice)) * 100).toLocaleString('vi-VN', { maximumFractionDigits: 2 }) + '%' : '—'}</output></Field>
      </div><p className="muted">Bỏ trống cả giá và ngưỡng khi không bán sỉ. Giá sỉ &gt; 0 và ≤ giá lẻ; ngưỡng sỉ &gt; 1.</p></section>
      <section className="commerce-section"><Uploads showAltText={false} files={form.images} onError={setError} onBusyChange={setUploading}
        disabled={busy} onChange={images => {
          const unique = images.filter((image, index) => images.findIndex(x => x.imageUrl === image.imageUrl) === index);
          if (unique.length !== images.length) setError(new Error(messages.MSG76));
          if (unique.length && !unique.some(image => image.isPrimary)) unique[0] = { ...unique[0], isPrimary: true };
          update('images', unique);
        }} /></section>
      {original && <section className="commerce-section"><div className="commerce-actions">
        <Status value={original.approvalStatus} labels={productStatus} />
        <span>{original.isPublished ? 'Đang bán' : 'Chưa hiển thị'}</span>
        <span>Tồn kho: {original.availableQuantity} {original.unit} · {original.batchCount} lô</span>
      </div><p className="muted">Chỉ sản phẩm đã duyệt và đủ điều kiện mới được hiển thị. Ẩn sản phẩm vẫn giữ lịch sử và các đơn hàng đã đặt.</p></section>}
      <div className="commerce-actions"><button className="primary">{busy ? 'Đang lưu…' : 'Gửi duyệt'}</button>
        {original && <button type="button" onClick={event => submit(event, true)}>Lưu thay đổi giá</button>}
        <button type="button" onClick={cancel}>Hủy thay đổi</button>
        {original && (original.isPublished || original.approvalStatus === 'Approved') &&
          <button type="button" onClick={() => {
            if (dirty) { setError(new Error('Vui lòng lưu hoặc hủy thay đổi trước khi thay đổi hiển thị.')); return; }
            setConfirm('visibility');
          }}>{original.isPublished ? 'Ẩn sản phẩm' : 'Hiển thị sản phẩm đã duyệt'}</button>}
      </div>
      {original?.batches?.length > 0 && <section className="commerce-section"><h2>Lô hàng liên quan</h2>
        <div className="table-wrap"><table><thead><tr><th>Lô hàng</th><th>Trạng thái</th><th>Có thể bán</th><th>Hạn sử dụng</th></tr></thead>
          <tbody>{original.batches.map(batch => <tr key={batch.batchCode}><td>{batch.batchCode}</td>
            <td>{({ Available: 'Có thể bán', Active: 'Đang hoạt động', InStock: 'Còn hàng', Harvested: 'Đã thu hoạch', SoldOut: 'Hết hàng', Expired: 'Hết hạn', Recalled: 'Đã thu hồi', Blocked: 'Bị khóa' })[batch.status] || batch.status}</td><td>{batch.availableQuantity} {original.unit}</td>
            <td>{batch.expirationDate ? new Date(batch.expirationDate + 'T00:00:00').toLocaleDateString('vi-VN') : '—'}</td></tr>)}</tbody></table></div>
      </section>}
    </fieldset></form>
    {confirm && <Confirm title={confirm === 'cancel' ? 'Hủy thay đổi?' : confirm === 'visibility'
      ? (original.isPublished ? 'Ẩn sản phẩm?' : 'Hiển thị sản phẩm?') : 'Xác nhận lưu sản phẩm'} busy={busy}
      onClose={() => setConfirm(null)} onConfirm={confirm === 'cancel'
        ? () => navigate('/farmer/products') : confirm === 'visibility' ? async () => {
          setBusy(true); setError(null);
          try {
            const result = await request('/farmer/products/' + id + '/visibility', {
              method: 'PUT', body: { isPublished: !original.isPublished, version: original.version } });
            setOriginal(result); setForm({ ...result, wholesalePrice: result.wholesalePrice ?? '',
              wholesaleMinQuantity: result.wholesaleMinQuantity ?? '' });
            setMessage(result.isPublished ? messages.MSG164 : messages.MSG74); setConfirm(null);
          } catch (failure) { setError(failure); setConfirm(null); }
          finally { setBusy(false); }
        } : save}>
      {confirm === 'cancel' ? 'Các thay đổi chưa lưu sẽ bị bỏ.' : confirm === 'visibility'
        ? (original.isPublished ? messages.MSG73 : 'Hiển thị sản phẩm đã được duyệt?')
        : priceOnly ? 'Chỉ cập nhật giá, giữ nguyên kết quả duyệt và giá đã chốt của các đơn hàng.'
        : 'Sửa nội dung sẽ gửi kiểm duyệt lại. Thay đổi riêng giá giữ nguyên kết quả duyệt.'}</Confirm>}
  </div>;
}
