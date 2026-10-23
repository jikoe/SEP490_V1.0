import React, { useEffect, useState } from 'react';
import { request } from '../../api/client';
import { money, Notice, ProductThumbnail } from '../../components/ui/Common';
import { Confirm, Empty, Errors, useFeatureData } from '../Commerce/Shared';

export default function CartScreen({ navigate }) {
  const state = useFeatureData(signal => request('/cart', { signal }), []);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [removeIds, setRemoveIds] = useState(null);
  const cart = state.data;
  const groups = Object.values((cart?.items || []).reduce((result, item) => {
    const key = item.farmerProfileId;
    result[key] ||= { farmName: item.farmName, items: [] };
    result[key].items.push(item); return result;
  }, {}));
  const available = cart?.items.filter(item => item.isAvailable) || [];
  const selected = cart?.items.filter(item => item.isSelected) || [];
  async function mutate(path, body, method = 'PUT') {
    setBusy(true); state.setError(null); setMessage('');
    try {
      const result = await request('/cart/' + path, { method, body });
      state.setData(result); setMessage(result.messages.join(' '));
      return result;
    } catch (error) { state.setError(error); return null; }
    finally { setBusy(false); }
  }
  async function proceed() {
    const result = await mutate('prepare-checkout', { reviewToken: cart.reviewToken }, 'POST');
    if (result?.canProceedToCheckout) navigate('/checkout');
  }
  return <div className="commerce"><div className="commerce-top"><div>
    <p className="commerce-subtitle">Mua sắm · UC-RC-01</p><h1>Giỏ hàng của bạn</h1>
    <p className="muted">Chọn sản phẩm muốn mua và kiểm tra thông tin trước khi thanh toán.</p></div>
    <button type="button" onClick={() => navigate('/retail/dashboard')}>← Tiếp tục mua sắm</button></div>
    <Errors error={state.error} /><Notice>{message}</Notice>
    {state.loading ? <p role="status">Đang tải giỏ hàng…</p> : cart && !cart.items.length
      ? <section className="commerce-section"><Empty>Giỏ hàng của bạn đang trống (MSG168)</Empty>
        <button type="button" className="primary" onClick={() => navigate('/retail/dashboard')}>Khám phá nông sản</button></section>
      : cart && <div className="commerce-grid"><div>
        <div className="commerce-section commerce-actions"><label className="commerce-check">
          <input type="checkbox" disabled={busy || !available.length}
            checked={available.length > 0 && available.every(item => item.isSelected)}
            onChange={event => mutate('selection', { itemIds: available.map(item => item.cartItemId),
              isSelected: event.target.checked })} />Chọn tất cả sản phẩm có thể mua</label>
          <button type="button" disabled={busy || !selected.length}
            onClick={() => setRemoveIds(selected.map(item => item.cartItemId))}>Xóa đã chọn</button></div>
        {groups.map(group => {
          const eligible = group.items.filter(item => item.isAvailable);
          return <section className="commerce-cart-group" key={group.farmName + group.items[0].farmerProfileId}>
            <div className="commerce-cart-farm"><label className="commerce-check"><input type="checkbox"
              disabled={busy || !eligible.length} checked={eligible.length > 0 && eligible.every(item => item.isSelected)}
              onChange={event => mutate('selection', { itemIds: eligible.map(item => item.cartItemId),
                isSelected: event.target.checked })} /><strong>{group.farmName}</strong></label>
              <small>Thêm vào giỏ chưa giữ hàng</small></div>
            {group.items.map(item => <CartLine key={item.cartItemId} item={item} busy={busy}
              onSelect={isSelected => mutate('selection', { itemIds: [item.cartItemId], isSelected })}
              onQuantity={quantity => mutate('items/' + item.cartItemId + '/quantity', { quantity })}
              onRemove={() => setRemoveIds([item.cartItemId])} />)}</section>;
        })}</div>
        <aside className="commerce-section commerce-summary"><h2>Tóm tắt giỏ hàng</h2>
          <dl><div><dt>Sản phẩm đã chọn</dt><dd>{cart.selectedCount}</dd></div>
            <div><dt>Tạm tính</dt><dd className="commerce-subtotal">{money(cart.selectedSubtotal)}</dd></div></dl>
          <p className="muted">Chỉ tính các sản phẩm được chọn.</p><hr />
          <p>Phí giao hàng, voucher và điểm thưởng được xác định tại trang checkout.</p>
          <button type="button" className="primary" disabled={busy || !cart.selectedCount} onClick={proceed}>
            {busy ? 'Đang kiểm tra…' : 'Tiến hành thanh toán →'}</button>
          <small>Bạn sẽ kiểm tra địa chỉ và xác nhận đặt hàng ở bước tiếp theo.</small>
        </aside></div>}
    {removeIds && <Confirm title="Xóa sản phẩm?" busy={busy} onClose={() => setRemoveIds(null)}
      onConfirm={async () => { const result = await mutate('remove-items', { itemIds: removeIds }, 'POST');
        if (result) setRemoveIds(null); }}>Xóa {removeIds.length} sản phẩm đã chọn khỏi giỏ hàng (MSG84)?</Confirm>}
  </div>;
}

function CartLine({ item, busy, onQuantity, onSelect, onRemove }) {
  const [quantity, setQuantity] = useState(String(item.quantity));
  const [error, setError] = useState('');
  useEffect(() => { setQuantity(String(item.quantity)); setError(''); }, [item.quantity]);
  async function accept(value) {
    const number = Number(value);
    const ratio = number / item.quantityStep;
    if (!String(value).trim() || !Number.isFinite(number) || number <= 0
        || number > 999999999 || Math.abs(ratio - Math.round(ratio)) > 1e-7) {
      setQuantity(String(item.quantity));
      setError('Nhập số lượng dương theo bước ' + item.quantityStep + ' ' + item.unit + ' (MSG167).'); return;
    }
    setError('');
    if (number !== item.quantity) {
      const updated = await onQuantity(number);
      const line = updated?.items.find(row => row.cartItemId === item.cartItemId);
      setQuantity(String(line?.quantity ?? item.quantity));
    } else setQuantity(String(item.quantity));
  }
  return <div className={'commerce-cart-line' + (item.isAvailable ? '' : ' unavailable')}>
    <input type="checkbox" aria-label={'Chọn ' + item.productName + ' lô ' + item.batchCode}
      checked={item.isSelected} disabled={busy || !item.isAvailable} onChange={event => onSelect(event.target.checked)} />
    <ProductThumbnail src={item.imageUrl} name={item.productName} />
    <div><strong>{item.productName}</strong><small>Đơn vị: {item.unit} · Lô: {item.batchCode}</small>
      <div>{money(item.unitPrice)}/{item.unit}</div>
      {item.wholesalePrice != null && <small>Giá sỉ {money(item.wholesalePrice)} từ {item.wholesaleMinQuantity} {item.unit}</small>}
      {item.unitPrice < item.retailPrice && <small>Giá lẻ: <del>{money(item.retailPrice)}</del> · Đang áp dụng giá sỉ</small>}
      <small>Còn {item.availableQuantity} {item.unit}</small>
      {!item.isAvailable && <Notice error>{item.unavailableReason}</Notice>}
      {error && <div role="alert" style={{ color: '#a0362c', fontSize: 12 }}>{error}</div>}
    </div>
    <div className="commerce-quantity"><button type="button" aria-label={'Giảm ' + item.productName}
      disabled={busy || !item.isAvailable || item.quantity <= item.quantityStep}
      onClick={() => accept(item.quantity - item.quantityStep)}>−</button>
      <input type="number" aria-label={'Số lượng ' + item.productName + ' ' + item.batchCode}
        min={item.quantityStep} step={item.quantityStep} value={quantity} disabled={busy || !item.isAvailable}
        onChange={event => setQuantity(event.target.value)} onBlur={() => accept(quantity)}
        onKeyDown={event => { if (event.key === 'Enter') { event.preventDefault(); accept(quantity); } }} />
      <button type="button" aria-label={'Tăng ' + item.productName} disabled={busy || !item.isAvailable}
        onClick={() => accept(item.quantity + item.quantityStep)}>+</button></div>
    <div className="commerce-line-price"><strong>{money(item.subtotal)}</strong><br />
      <button type="button" disabled={busy} onClick={onRemove}>Xóa</button></div>
  </div>;
}

