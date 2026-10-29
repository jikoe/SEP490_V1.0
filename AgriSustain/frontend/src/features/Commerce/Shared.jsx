import React, { useEffect, useRef, useState } from 'react';
import { request } from '../../api/client';
import { displayMessage } from '../../utils/displayMessage';
import { productListingMessages } from '../Farmer/productListingMessages';
import { Modal, Notice } from '../../components/ui/Common';

export const productStatus = { Pending: 'Chờ duyệt', Approved: 'Đã duyệt', Rejected: 'Từ chối' };
export const orderStatus = { Pending: 'Chờ xác nhận', Confirmed: 'Đang chuẩn bị',
  Preparing: 'Đang chuẩn bị', ReadyToShip: 'Sẵn sàng giao', Ready: 'Sẵn sàng giao',
  Shipping: 'Đang giao', InTransit: 'Đang giao', Delivered: 'Đã giao', Received: 'Đã nhận',
  Completed: 'Hoàn thành', Cancelled: 'Đã hủy', DeliveryFailed: 'Giao thất bại', Returned: 'Hàng trả' };
export function Status({ value, labels = orderStatus }) {
  return <span className={'commerce-status status-' + value}>{labels[value] || value}</span>;
}
export function Errors({ error, hideMessageCodes = false }) {
  const text = value => hideMessageCodes ? displayMessage(value) : value;
  return error ? <Notice error><strong>{text(error.message || error)}</strong>
    {Object.entries(error.fields || {}).map(([key, values]) =>
      <div key={key}>{({ ProductName: 'Tên sản phẩm', CategoryId: 'Danh mục', Unit: 'Đơn vị',
        Description: 'Mô tả', BaseRetailPrice: 'Giá bán lẻ',
        WholesalePrice: 'Giá sỉ', WholesaleMinQuantity: 'Ngưỡng bán sỉ', Images: 'Ảnh sản phẩm'
      })[key] || 'Thông tin nhập'}: {text(values.join(' '))}</div>)}</Notice> : null;
}
export function Confirm({ title, children, busy, onConfirm, onClose }) {
  return <Modal title={title} onClose={() => { if (!busy) onClose(); }}>
    <div className="commerce"><p>{children}</p><div className="commerce-actions">
      <button type="button" disabled={busy} onClick={onClose}>Hủy</button>
      <button type="button" className="primary" disabled={busy} onClick={onConfirm}>
        {busy ? 'Đang xử lý…' : 'Xác nhận'}</button></div></div></Modal>;
}
export function Empty({ children }) {
  return <div className="commerce-empty"><span>♧</span><h2>{children}</h2></div>;
}
export function Uploads({ evidence = false, files, onChange, disabled, onError, onBusyChange }) {
  const [busy, setBusy] = useState(false);
  const ref = useRef(null);
  async function upload(event) {
    const selected = [...event.target.files];
    const limit = evidence ? 5 : 10;
    if (selected.length + files.length > limit) {
      onError(new Error(evidence ? 'Tối đa ' + limit + ' tệp.' : productListingMessages.MSG76)); return;
    }
    const allowed = evidence ? ['image/jpeg', 'image/png', 'application/pdf']
      : ['image/jpeg', 'image/png'];
    if (selected.some(file => file.size > 5 * 1024 * 1024 || file.size === 0
        || !allowed.includes(file.type))) {
      onError(new Error(evidence ? 'Bằng chứng phải là tệp JPG, JPEG, PNG hoặc PDF hợp lệ, không rỗng và không vượt quá 5 MB.' : productListingMessages.MSG78)); return;
    }
    setBusy(true); onBusyChange?.(true);
    const added = [];
    try {
      for (const file of selected) {
        const body = new FormData(); body.append('file', file);
        const result = await request(evidence ? '/files/evidence' : '/files/images', {
          method: 'POST', body });
        added.push(evidence ? { fileUrl: result.fileUrl, name: file.name }
          : { imageUrl: result.imageUrl, isPrimary: false, altText: file.name });
      }
    } catch (error) { onError(error); }
    finally {
      if (added.length) {
        const next = [...files, ...added];
        if (!evidence && !next.some(file => file.isPrimary)) next[0].isPrimary = true;
        onChange(next);
      }
      setBusy(false); onBusyChange?.(false); if (ref.current) ref.current.value = '';
    }
  }
  return <div className="commerce-upload"><label>{evidence ? 'Bằng chứng' : 'Thư viện ảnh sản phẩm'}
    <input ref={ref} type="file" multiple disabled={disabled || busy}
      accept={evidence ? '.jpg,.jpeg,.png,.pdf' : '.jpg,.jpeg,.png'} onChange={upload} /></label>
    <p className="muted">{evidence ? 'JPG, JPEG, PNG, PDF' : 'JPG, JPEG, PNG'} · tối đa 5 MB/tệp
      {busy && ' · Đang tải lên…'}</p>
    <div className="commerce-gallery">{files.map((file, index) => <div
      className="commerce-image" key={file.imageUrl || file.fileUrl}>
      {file.imageUrl ? <img src={file.imageUrl} alt={file.altText || 'Ảnh sản phẩm'} />
        : <span className="commerce-file-name">{file.name || 'Bằng chứng ' + (index + 1)}</span>}
      {!evidence && <><label className="commerce-check"><input type="radio" name="primary-image"
        disabled={disabled || busy} checked={file.isPrimary} onChange={() => onChange(files.map(
          (item, i) => ({ ...item, isPrimary: index === i })))} />Ảnh đại diện</label>
        <label>Mô tả ảnh<input maxLength={255} value={file.altText || ''} disabled={disabled || busy}
          onChange={event => onChange(files.map((item, i) => i === index
            ? { ...item, altText: event.target.value } : item))} /></label></>}
      <button type="button" disabled={disabled || busy} onClick={() => {
        const next = files.filter((_, i) => i !== index);
        if (!evidence && next.length && !next.some(item => item.isPrimary)) next[0].isPrimary = true;
        onChange(next);
      }}>Bỏ tệp</button></div>)}</div></div>;
}
export function useFeatureData(load, dependencies) {
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError(null);
    load(controller.signal).then(setData).catch(failure => {
      if (failure.name !== 'AbortError') setError(failure);
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [...dependencies, revision]);
  return { data, setData, error, setError, loading, reload: () => setRevision(value => value + 1) };
}



