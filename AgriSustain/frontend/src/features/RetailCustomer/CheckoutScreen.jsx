import React, { useEffect, useRef, useState } from 'react';
import { request } from '../../api/client';
import { Field, Modal, Notice, localTime, money, ProductThumbnail } from '../../components/ui/Common';

const emptyAddress = { recipientName: '', phone: '', province: '', ward: '', detail: '',
  provinceCode: '', wardCode: '', catalogVersion: '' };
const initial = { address: emptyAddress, deliveryMethod: 'Standard', paymentMethod: 'PayOS',
  voucherCode: '', points: 0, notes: '' };

/**
 * Coordinates the owned checkout context, linked province/ward options, server quote and
 * placement confirmation. Cart editing and QR/payment screens remain external modules.
 */
export default function CheckoutScreen({ fixture }) {
  const [context, setContext] = useState(null);
  const [provinces, setProvinces] = useState([]);
  const [wards, setWards] = useState([]);
  const [form, setForm] = useState(initial);
  const [quote, setQuote] = useState(null);
  const [reviewItems, setReviewItems] = useState([]);
  const [order, setOrder] = useState(null);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [refresh, setRefresh] = useState(0);
  const [confirm, setConfirm] = useState(false);
  const key = useRef(crypto.randomUUID());
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    Promise.all([request('/checkout', { fixture, signal: controller.signal }),
      request('/checkout/provinces', { fixture, signal: controller.signal })])
      .then(async ([data, provinceOptions]) => {
      setProvinces(provinceOptions);
      setContext(data); setReviewItems(data.selectedItems || []);
      const values = { ...initial, address: data.addresses[0] || { ...emptyAddress } };
      setForm(values);
      if (data.items.some((item) => item.selected) && data.addresses.length) {
        const preview = await request('/checkout/quotes', { fixture, method: 'POST', body: values,
          signal: controller.signal });
        setQuote(preview); setReviewItems(preview.lines); setForm(preview.input);
      }
    }).catch((failure) => { if (failure.name !== 'AbortError') setError(failure.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [fixture, refresh]);

  // Load linked wards from the same bundled catalog validated by the backend.
  useEffect(() => {
    const controller = new AbortController();
    setWards([]);
    if (form.address.provinceCode) {
      request(`/checkout/provinces/${form.address.provinceCode}/wards`, {
        fixture, signal: controller.signal }).then(setWards).catch((failure) => {
        if (failure.name !== 'AbortError') setError(failure.message);
      });
    }
    return () => controller.abort();
  }, [fixture, form.address.provinceCode]);

  /**
   * Changes one checkout field and invalidates the accepted quote, requiring a new server
   * calculation before placement. A new draft receives a new local demo retry key.
   */
  function update(keyName, value) {
    setForm((old) => ({ ...old, [keyName]: value })); setQuote(null); setError('');
    key.current = crypto.randomUUID();
  }
  /**
   * Updates recipient/contact/detail input through the shared quote-invalidation path; it never
   * computes prices or claims administrative validity on the client.
   */
  function address(keyName, value) {
    update('address', { ...form.address, [keyName]: value });
  }
  /**
   * Requests a new server quote, accepts its normalized address and cleaned voucher input, and
   * displays feedback without reserving inventory.
   */
  async function preview(event) {
    event.preventDefault(); setBusy(true); setError(''); setMessage(''); setQuote(null);
    try {
      const data = await request('/checkout/quotes', { fixture, method: 'POST',
        body: { ...form, points: Number(form.points) } });
      setQuote(data); setReviewItems(data.lines); setForm(data.input); setMessage(data.warning || 'Đã tính lại tổng tiền hiện tại.');
    } catch (failure) { setError(failure.message); }
    finally { setBusy(false); }
  }
  /**
   * Submits only the confirmed quote and provisional demo retry key; preserves the key on
   * network failure and invalidates rejected stale quotes. No payment provider is called.
   */
  async function place() {
    setBusy(true); setError('');
    try {
      // DEMO-INTEGRATION: preserve this key after timeouts/network failures; retries
      // are idempotent inside this DEMO process only. DB-06 remains undecided; SQL
      // does not yet store this key. A real handoff needs the team's durable
      // checkout-attempt state; no cart CRUD or payment module is implemented here.
      const placed = await request('/checkout/orders', { fixture, method: 'POST',
        body: { quoteId: quote.id, requestKey: key.current, confirmed: true } });
      setOrder(placed); setConfirm(false);
    } catch (failure) {
      setError(failure.message);
      if (['MSG88', 'MSG116', 'MSG117', 'MSG118', 'QUOTE_EXPIRED'].includes(failure.code)) {
        setQuote(null); setConfirm(false);
      }
    } finally { setBusy(false); }
  }

  if (order) return <section><p className="eyebrow">UC-RC-02 · KẾT QUẢ ĐẶT HÀNG</p>
    <div className="success-panel"><div className="success-icon">✓</div>
      <h1>Đã tạo đơn hàng</h1><p>Đơn đang chờ thanh toán. Hàng đã được giữ và sản phẩm đã đặt
        được xóa khỏi giỏ.</p><dl className="details"><dt>Mã đơn</dt><dd className="order-code">
        {order.code}</dd><dt>Trạng thái</dt><dd>Chờ thanh toán</dd><dt>Tổng tiền</dt>
        <dd><strong>{money(order.total)}</strong></dd><dt>Hạn giữ hàng</dt>
        <dd>{localTime(order.reservationExpiresAt)}</dd></dl>
      <Notice>{order.message}</Notice><p className="muted">
        Tích hợp thanh toán QR đang chờ module của nhóm. Chưa có giao dịch thanh toán nào được gửi.
      </p><button onClick={() => { setOrder(null); setQuote(null); setContext(null);
        // Reload the remaining SQL cart instead of displaying already-ordered cached products.
        setReviewItems([]); setError(''); setMessage(''); setRefresh((value) => value + 1);
      }}>Đóng kết quả</button></div></section>;

  return <section><div className="breadcrumbs"><span>Giỏ hàng</span><span>/</span><span>Thanh toán</span></div>
    <div className="section-heading"><div><h1>Thanh toán đơn bán lẻ</h1>
      <p className="muted">Kiểm tra sản phẩm, thông tin giao hàng và tổng tiền trước khi đặt.</p>
    </div><span className="step"><strong>01 Xác nhận đơn</strong><span>→ 02 Thanh toán</span></span></div>
    <Notice error>{error}</Notice><Notice>{message}</Notice>
    {loading ? <p className="empty" role="status">Đang chuẩn bị thông tin checkout…</p>
      : <form onSubmit={preview} className="checkout-grid"><div className="checkout-main">
        <section className="panel"><div className="record-heading"><h2>Sản phẩm đã chọn</h2>
          <span className="count">{reviewItems.length} sản phẩm</span></div>
          <div className="table-wrap checkout-items-wrap"><table className="checkout-items-table">
            <thead><tr><th>Sản phẩm</th><th>Số lượng</th><th>Đơn giá</th><th>Thành tiền</th></tr></thead>
            <tbody>{reviewItems.map((line) => <tr key={line.cartItemId}><td><div className="name-cell">
              <ProductThumbnail src={line.imageUrl} name={line.productName} /><div>
                <strong>{line.productName}</strong><small>{line.sellerName}</small>
                <small>{line.allocations.length ? line.allocations.map((item) =>
                  `${item.batchCode}: ${item.quantity} ${line.unit}`).join(' · ')
                  : 'Lô hàng được kiểm tra khi tính lại đơn'}</small></div></div></td>
              <td>{line.quantity} {line.unit}</td><td>{money(line.unitPrice)}</td>
              <td><strong>{money(line.subtotal)}</strong></td></tr>)}</tbody></table></div>
          {!reviewItems.length && <p className="empty">Chưa có sản phẩm được chọn để thanh toán.</p>}
          {!quote && reviewItems.length > 0 && <p className="quote-hint">Thông tin sản phẩm gần nhất.
            Chọn “Tính lại tổng tiền” để kiểm tra giá và tồn kho trước khi đặt.</p>}
        </section><fieldset disabled={busy}><section className="panel padded"><h2>Địa chỉ giao hàng</h2>
          {context?.addresses.length > 0 && <Field label="Chọn địa chỉ đã lưu">
            <select value={context.addresses.findIndex((saved) => saved.recipientName === form.address.recipientName
              && saved.phone === form.address.phone && saved.detail === form.address.detail
              && saved.provinceCode === form.address.provinceCode && saved.wardCode === form.address.wardCode)}
              onChange={(event) => update('address', event.target.value === '-1'
                ? { ...emptyAddress } : context.addresses[Number(event.target.value)])}>
              {context.addresses.map((saved, index) => <option key={index} value={index}>
                {saved.recipientName} · {saved.detail} · {saved.province}</option>)}
              <option value="-1">Nhập địa chỉ khác</option></select></Field>}
          <div className="form-grid"><Field label="Tên người nhận *"><input required minLength={2}
            maxLength={100} value={form.address.recipientName}
            onChange={(event) => address('recipientName', event.target.value)} /></Field>
          <Field label="Số điện thoại *"><input required type="tel" maxLength={20} value={form.address.phone}
            onChange={(event) => address('phone', event.target.value)} /></Field>
          <Field label="Tỉnh / thành phố *"><select required value={form.address.provinceCode}
            onChange={(event) => update('address', { ...form.address, provinceCode: event.target.value,
              province: provinces.find((item) => item.code === event.target.value)?.name || '',
              ward: '', wardCode: '', catalogVersion: '' })}><option value="">Chọn tỉnh / thành phố</option>
            {provinces.map((item) => <option key={item.code} value={item.code}>{item.name}</option>)}
          </select></Field><Field label="Phường / xã *"><select required disabled={!wards.length}
            value={form.address.wardCode} onChange={(event) => update('address', { ...form.address,
              wardCode: event.target.value, ward: wards.find((item) => item.code === event.target.value)?.name || '' })}>
              <option value="">Chọn phường / xã</option>{wards.map((item) =>
                <option key={item.code} value={item.code}>{item.name}</option>)}</select></Field></div>
          <Field label="Địa chỉ chi tiết *"><textarea required maxLength={255} rows={2}
            placeholder="Số nhà, tên đường và hướng dẫn giao hàng"
            value={form.address.detail} onChange={(event) => address('detail', event.target.value)} /></Field>
          <h3 className="form-subheading">Phương thức giao hàng</h3><div className="delivery-options">
            {['Standard', 'FreshExpress'].map((method) => <label key={method}
              className={`choice-card ${form.deliveryMethod === method ? 'chosen' : ''}`}>
              <input type="radio" name="deliveryMethod" value={method} checked={form.deliveryMethod === method}
                onChange={() => update('deliveryMethod', method)} /><span><strong>{method === 'Standard'
                ? 'Giao hàng tiêu chuẩn' : 'Giao nhanh nông sản tươi'}</strong>
                <small>{form.deliveryMethod === method && quote ? `Phí giao: ${money(quote.deliveryFee)}`
                  : 'Phí được tính lại theo địa chỉ và nhà cung cấp'}</small>
                <small>Thời gian giao dự kiến: chờ đơn vị vận chuyển xác nhận</small></span>
            </label>)}
          </div></section><section className="panel padded"><h2>Phương thức thanh toán</h2>
            <div className="payment-options">{['PayOS', 'VietQR'].map((method) => <label key={method}
              className={`choice-card ${form.paymentMethod === method ? 'chosen' : ''}`}>
              <input type="radio" name="paymentMethod" value={method} checked={form.paymentMethod === method}
                onChange={() => update('paymentMethod', method)} /><span><strong>{method}</strong>
                <small>Tiếp tục thanh toán sau khi tạo đơn</small></span></label>)}</div>
            <Field label="Ghi chú đơn hàng"><textarea maxLength={300} rows={2} value={form.notes || ''}
              placeholder="Yêu cầu giao hàng (không bắt buộc)"
              onChange={(event) => update('notes', event.target.value)} /></Field>
          </section></fieldset></div><aside className="checkout-summary"><div className="panel padded summary">
          <h2>Tóm tắt đơn hàng</h2><fieldset disabled={busy}><Field label="Mã voucher">
            <input maxLength={100} list="checkout-vouchers" value={form.voucherCode || ''}
              placeholder="Nhập hoặc chọn voucher" onChange={(event) => update('voucherCode', event.target.value)} />
          </Field><datalist id="checkout-vouchers">{context?.voucherCodes.map((code) =>
            <option value={code} key={code} />)}</datalist>
          <button type="submit" className="voucher-apply" disabled={busy || !reviewItems.length}>Áp dụng voucher</button>
          {quote?.voucherDiscount > 0 && <p className="applied-voucher">✓ Đã áp dụng {quote.input.voucherCode}</p>}
          <div className="points-heading"><strong>Điểm thưởng</strong><span>
            {context?.availablePoints ?? 0} điểm khả dụng</span></div>
          <Field label="Điểm muốn sử dụng"><input type="number" min={0} step={1}
            max={context?.availablePoints || 0} value={form.points}
            onChange={(event) => update('points', event.target.value)} /></Field>
          </fieldset><div className="price-lines"><p><span>Tiền sản phẩm</span><span>
            {quote ? money(quote.subtotal) : 'Chưa tính'}</span></p><p><span>Phí giao hàng</span>
            <span>{quote ? money(quote.deliveryFee) : 'Chưa tính'}</span></p><p><span>Giảm voucher</span>
            <span>{quote ? `−${money(quote.voucherDiscount)}` : '—'}</span></p><p><span>Giảm điểm thưởng</span>
            <span>{quote ? `−${money(quote.pointsDiscount)}` : '—'}</span></p><p className="total">
            <strong>Tổng thanh toán</strong><strong>{quote ? money(quote.total) : 'Chưa tính'}</strong></p></div>
          {quote && <small>Bản tính giá có hiệu lực đến {localTime(quote.expiresAt)}.</small>}
          <button type="submit" className="full" disabled={busy || !reviewItems.length}>
            {busy ? 'Đang xử lý…' : 'Tính lại tổng tiền'}</button>
          <button type="button" className="primary full" disabled={busy || !quote}
            onClick={() => setConfirm(true)}>Đặt hàng</button>
          <button type="button" className="text-button full" disabled={busy} onClick={() => {
            // DEMO-HANDOFF (UC-RC-01 owner): replace with the team's cart route. Do not
            // implement cart CRUD here or claim navigation succeeded before integration.
            setMessage('Liên kết đến giỏ hàng đang chờ tích hợp UC-RC-01. Chưa có đơn được tạo.');
          }}>Trở về giỏ hàng</button></div>
          <p className="subtle">Chỉ giữ hàng sau khi đặt đơn thành công.</p></aside></form>}
    {confirm && quote && <Modal title="Xác nhận đặt đơn bán lẻ"
      onClose={() => { if (!busy) setConfirm(false); }}><p>Đặt đơn với tổng tiền <strong>
        {money(quote.total)}</strong> và giữ hàng để tiếp tục thanh toán?</p><p>
        Người nhận: {form.address.recipientName} · {form.address.phone}</p>
      <Notice error>{error}</Notice><div className="modal-actions"><button disabled={busy}
        onClick={() => setConfirm(false)}>Kiểm tra lại</button><button className="primary"
        disabled={busy} onClick={place}>{busy ? 'Đang tạo đơn…' : 'Xác nhận đặt hàng'}</button></div>
    </Modal>}
  </section>;
}
