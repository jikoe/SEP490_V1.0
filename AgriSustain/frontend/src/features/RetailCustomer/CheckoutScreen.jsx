import React, { useEffect, useRef, useState } from 'react';
import { requestUseCase as request } from '../../api/client';
import { Field, Modal, Notice, localTime, money } from '../../components/ui/Common';

const emptyAddress = { recipientName: '', phone: '', provinceCode: '', province: '',
  district: '', wardCode: '', ward: '', detail: '', catalogVersion: '' };
const initial = { address: emptyAddress, deliveryMethod: 'Standard', paymentMethod: 'PayOS',
  voucherCode: '', points: 0, notes: '' };
const attemptKey = 'agrisustain:checkout-attempt';
const quoteKey = 'agrisustain:checkout-quote';

/** Server quotes determine prices. Persist the attempt key for retries after ambiguous failures. */
export default function CheckoutScreen({ navigate }) {
  const [context, setContext] = useState(null);
  const [provinces, setProvinces] = useState([]);
  const [wards, setWards] = useState([]);
  const [wardLoading, setWardLoading] = useState(false);
  const [wardError, setWardError] = useState('');
  const [wardRefresh, setWardRefresh] = useState(0);
  const [form, setForm] = useState(initial);
  const [quote, setQuote] = useState(null);
  const [order, setOrder] = useState(null);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [confirm, setConfirm] = useState(false);
  const inFlight = useRef(false);
  const attempt = useRef(null);

  useEffect(() => {
    const controller = new AbortController();
    Promise.all([request('/checkout', { signal: controller.signal }),
      request('/checkout/provinces', { signal: controller.signal })])
      .then(([data, catalog]) => {
        setContext(data); setProvinces(catalog);
        setForm({ ...initial, address: savedAddress(data.addresses[0], catalog) });
      })
      .catch((failure) => { if (failure.name !== 'AbortError') setError(failure.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, []);

  // Abort obsolete requests when the province changes so wards stay linked to their parent.
  useEffect(() => {
    const controller = new AbortController();
    const provinceCode = form.address.provinceCode;
    setWards([]); setWardError(''); setWardLoading(Boolean(provinceCode));
    if (provinceCode) {
      request(`/checkout/provinces/${encodeURIComponent(provinceCode)}/wards`, {
        signal: controller.signal,
      }).then((options) => {
        if (controller.signal.aborted) return;
        setWards(options);
        setForm((old) => {
          if (old.address.provinceCode !== provinceCode || !old.address.wardCode) return old;
          const selected = options.find((item) => item.code === old.address.wardCode);
          return { ...old, address: { ...old.address, wardCode: selected?.code || '',
            ward: selected?.name || '', catalogVersion: '' } };
        });
      }).catch((failure) => {
        if (failure.name !== 'AbortError') setWardError(failure.message);
      }).finally(() => {
        if (!controller.signal.aborted) setWardLoading(false);
      });
    }
    return () => controller.abort();
  }, [form.address.provinceCode, wardRefresh]);

  function savedAddress(saved, catalog = provinces) {
    const values = { ...emptyAddress, ...saved, district: '', catalogVersion: '' };
    const province = catalog.find((item) => item.code === values.provinceCode);
    return province ? { ...values, province: province.name }
      : { ...values, provinceCode: '', province: '', wardCode: '', ward: '' };
  }

  function update(name, value) {
    setForm((old) => ({ ...old, [name]: value }));
    setQuote(null); setMessage(''); setError('');
  }

  function address(name, value) {
    update('address', { ...form.address, [name]: value });
  }

  async function requestQuote(nextForm) {
    if (inFlight.current) return;
    inFlight.current = true; setBusy(true); setError(''); setQuote(null);
    try {
      const next = await request('/checkout/quotes', { method: 'POST',
        body: { ...nextForm, points: Number(nextForm.points) } });
      setQuote(next); setForm(next.input); setMessage(next.warning || 'Đã kiểm tra giá và tồn kho.');
      attempt.current = { quoteId: next.id, requestKey: crypto.randomUUID(), confirmed: true };
      sessionStorage.setItem(attemptKey, JSON.stringify(attempt.current));
      sessionStorage.setItem(quoteKey, JSON.stringify(next));
    } catch (failure) { setError(failure.message); }
    finally { inFlight.current = false; setBusy(false); }
  }

  async function preview(event) {
    event.preventDefault();
    await requestQuote(form);
  }

  function selectDeliveryMethod(deliveryMethod) {
    const nextForm = { ...form, deliveryMethod };
    setForm(nextForm); setQuote(null); setMessage(''); setError('');
    const recipient = nextForm.address;
    if (!recipient.recipientName || !recipient.phone || !recipient.detail
      || !recipient.provinceCode || !recipient.wardCode || !context?.selectedItems.length
      || wardLoading || !wards.some((ward) => ward.code === recipient.wardCode)) {
      setMessage('Hãy nhập địa chỉ nhận hàng hợp lệ để xem cước vận chuyển GHTK.');
      return;
    }
    void requestQuote(nextForm);
  }

  async function place(retry = false) {
    if (inFlight.current) return;
    const pending = retry ? JSON.parse(sessionStorage.getItem(attemptKey) || 'null') : attempt.current;
    if (!pending) { setError('Chưa có yêu cầu đặt hàng để thử lại.'); return; }
    if (retry) {
      try {
        const savedQuote = JSON.parse(sessionStorage.getItem(quoteKey) || 'null');
        if (savedQuote) setQuote(savedQuote);
      } catch { sessionStorage.removeItem(quoteKey); }
    }
    inFlight.current = true; setBusy(true); setError('');
    try {
      const result = await request('/checkout/orders', { method: 'POST', body: pending });
      setOrder(result); setConfirm(false); sessionStorage.removeItem(attemptKey);
      sessionStorage.removeItem(quoteKey);
    } catch (failure) {
      setError(failure.message);
      if (['MSG88', 'MSG118', 'QUOTE_EXPIRED'].includes(failure.code)) {
        setQuote(null); setConfirm(false); sessionStorage.removeItem(quoteKey);
      }
    } finally { inFlight.current = false; setBusy(false); }
  }

  const lines = quote?.lines || context?.selectedItems || [];

  if (order) return <section><h1>Checkout đã hoàn tất</h1>
    <div className="panel padded"><h2>Thông tin đơn hàng</h2>
      <dl className="details"><dt>Mã đơn</dt><dd>{order.code}</dd>
        <dt>Trạng thái</dt><dd>{order.status}</dd>
        <dt>Người nhận</dt><dd>{quote?.input.address.recipientName}</dd>
        <dt>Số điện thoại</dt><dd>{quote?.input.address.phone}</dd>
        <dt>Địa chỉ</dt><dd>{quote && `${quote.input.address.detail}, ${quote.input.address.ward}, ${quote.input.address.province}`}</dd>
        <dt>Vận chuyển</dt><dd>{quote?.input.deliveryMethod === 'Standard' ? 'Giao tiêu chuẩn (GHTK)' : 'Giao nhanh nông sản (GHTK)'}</dd>
        <dt>Ngày giao dự kiến</dt><dd>{quote && new Date(quote.expectedDeliveryAt).toLocaleDateString('vi-VN')}</dd>
        <dt>Phương thức thanh toán</dt><dd>{order.paymentMethod}</dd>
        <dt>Hạn giữ hàng</dt><dd>{localTime(order.reservationExpiresAt)}</dd></dl>
      <h3>Sản phẩm</h3><div className="table-wrap"><table><thead><tr>
        <th>Sản phẩm / Nông trại</th><th>Số lượng</th><th>Đơn giá</th><th>Thành tiền</th>
      </tr></thead><tbody>{(quote?.lines || []).map((line) => <tr key={line.cartItemId}>
        <td>{line.productName}<small>{line.sellerName}</small></td><td>{line.quantity} {line.unit}</td>
        <td>{money(line.unitPrice)}</td><td>{money(line.subtotal)}</td></tr>)}</tbody></table></div>
      <dl className="details"><dt>Tiền sản phẩm</dt><dd>{quote && money(quote.subtotal)}</dd>
        <dt>Phí giao hàng GHTK</dt><dd>{quote && money(quote.deliveryFee)}</dd>
        <dt>Giảm voucher</dt><dd>{quote && money(quote.voucherDiscount)}</dd>
        <dt>Giảm điểm</dt><dd>{quote && money(quote.pointsDiscount)}</dd>
        <dt><strong>Số tiền cần chuyển khoản</strong></dt><dd><strong>{money(order.total)}</strong></dd></dl>
      {quote && <><h3>Cước theo nông trại</h3><dl className="details">
        {[...new Map(quote.lines.map((line) => [line.farmerId, line.sellerName])).entries()]
          .map(([farmerId, sellerName]) => <React.Fragment key={farmerId}>
            <dt>{sellerName}</dt><dd>{money(quote.sellerDeliveryFees?.[farmerId] ?? 0)}</dd>
          </React.Fragment>)}
      </dl></>}
      <Notice>{order.message}</Notice>
      <button className="primary" onClick={() => navigate('/retail/orders')}>Theo dõi đơn hàng</button>
      <button onClick={() => navigate('/cart')}>Xem giỏ hàng còn lại</button>
    </div></section>;

  return <section><div className="breadcrumbs"><button type="button" className="text-button" onClick={() => navigate('/cart')}>Giỏ hàng</button><span>/</span><span>Thanh toán</span></div><h1>Thanh toán đơn bán lẻ</h1><Notice error>{error}</Notice><Notice>{message}</Notice>
    {loading ? <p role="status">Đang tải checkout…</p> : <>
      <button disabled={busy} onClick={() => place(true)}>Khôi phục kết quả yêu cầu trước</button>
      <form className="checkout-grid" onSubmit={preview}><div className="checkout-main">
        <section className="panel padded"><h2>Sản phẩm đã chọn</h2><div className="table-wrap">
          <table><thead><tr><th>Sản phẩm / Nông trại</th><th>Số lượng</th><th>Đơn giá</th>
            <th>Thành tiền</th></tr></thead><tbody>{lines.map((line) => <tr key={line.cartItemId}>
            <td>{line.productName}<small>{line.sellerName}</small><small>{line.allocations.map((x) =>
              `${x.batchCode}: ${x.quantity}`).join(' · ')}</small></td><td>{line.quantity} {line.unit}</td>
            <td>{money(line.unitPrice)}</td><td>{money(line.subtotal)}</td></tr>)}</tbody></table></div>
          {!lines.length && <p>Chưa có sản phẩm được chọn. Hãy cập nhật giỏ hàng.</p>}</section>
        <fieldset disabled={busy}><section className="panel padded"><h2>Địa chỉ giao hàng</h2>
          {context?.addresses.length > 0 && <Field label="Địa chỉ đã lưu"><select
            value={context.addresses.findIndex((saved) => saved.recipientName === form.address.recipientName
              && saved.phone === form.address.phone && saved.detail === form.address.detail
              && saved.provinceCode === form.address.provinceCode && saved.wardCode === form.address.wardCode)}
            onChange={(event) => update('address', savedAddress(
              context.addresses[Number(event.target.value)]))}>
            <option value={-1}>Nhập địa chỉ khác</option>{context.addresses.map((saved, index) =>
              <option key={index} value={index}>{saved.recipientName} · {saved.detail}</option>)}
          </select></Field>}
          <Field label="Tên người nhận *"><input required minLength={2} maxLength={100}
            value={form.address.recipientName} onChange={(e) => address('recipientName', e.target.value)} /></Field>
          <Field label="Số điện thoại *"><input required type="tel" value={form.address.phone}
            onChange={(e) => address('phone', e.target.value)} /></Field>
          <Field label="Tỉnh / thành phố *"><select required value={form.address.provinceCode}
            onChange={(e) => update('address', { ...form.address, provinceCode: e.target.value,
              province: provinces.find((item) => item.code === e.target.value)?.name || '',
              district: '', wardCode: '', ward: '', catalogVersion: '' })}>
            <option value="">Chọn tỉnh / thành phố</option>{provinces.map((item) =>
              <option key={item.code} value={item.code}>{item.name}</option>)}</select></Field>
          <Field label="Phường / xã *"><select required value={form.address.wardCode}
            disabled={!form.address.provinceCode || wardLoading || !wards.length}
            onChange={(e) => update('address', { ...form.address, wardCode: e.target.value,
              ward: wards.find((item) => item.code === e.target.value)?.name || '', catalogVersion: '' })}>
            <option value="">{wardLoading ? 'Đang tải phường / xã…'
              : !form.address.provinceCode ? 'Chọn tỉnh / thành phố trước' : 'Chọn phường / xã'}</option>
            {wards.map((item) => <option key={item.code} value={item.code}>{item.name}</option>)}
          </select></Field>
          {wardError && <><Notice error>{wardError}</Notice><button type="button"
            onClick={() => setWardRefresh((value) => value + 1)}>Tải lại phường / xã</button></>}
          {!provinces.length && <Notice error>Không tải được danh sách tỉnh / thành phố.
            Vui lòng tải lại trang.</Notice>}
          <Field label="Địa chỉ chi tiết *"><textarea required maxLength={255} value={form.address.detail}
            onChange={(e) => address('detail', e.target.value)} /></Field>
          <Field label="Phương thức giao hàng *"><select value={form.deliveryMethod}
            onChange={(e) => selectDeliveryMethod(e.target.value)}>
            <option value="Standard">Giao tiêu chuẩn</option><option value="FreshExpress">Giao nhanh nông sản</option>
          </select></Field><Field label="Phương thức thanh toán *"><select value={form.paymentMethod}
            onChange={(e) => update('paymentMethod', e.target.value)}><option>PayOS</option><option>VietQR</option>
          </select></Field><Field label="Ghi chú đơn hàng"><textarea maxLength={300} value={form.notes || ''}
            onChange={(e) => update('notes', e.target.value)} /></Field></section></fieldset>
      </div><aside className="panel padded"><h2>Tóm tắt đơn hàng</h2><fieldset disabled={busy}>
        <Field label="Mã voucher"><input maxLength={100} list="vouchers" value={form.voucherCode || ''}
          onChange={(e) => update('voucherCode', e.target.value)} /></Field><datalist id="vouchers">
          {context?.voucherCodes.map((code) => <option key={code}>{code}</option>)}</datalist>
        <Field label={`Điểm muốn sử dụng (${context?.availablePoints ?? 0} khả dụng)`}>
          <input type="number" min={0} step={1} value={form.points}
            onChange={(e) => update('points', e.target.value)} /></Field></fieldset>
        <dl className="details"><dt>Tiền sản phẩm</dt><dd>{quote ? money(quote.subtotal) : 'Chưa tính'}</dd>
          <dt>Phí giao hàng</dt><dd>{quote ? money(quote.deliveryFee) : 'Chưa tính'}</dd>
          <dt>Giảm voucher</dt><dd>{quote ? money(quote.voucherDiscount) : 'Chưa tính'}</dd>
          <dt>Giảm điểm</dt><dd>{quote ? money(quote.pointsDiscount) : 'Chưa tính'}</dd>
          <dt>Tổng tiền</dt><dd>{quote ? money(quote.total) : 'Chưa tính'}</dd></dl>
        {quote && <p>Bản tính giá hết hạn: {localTime(quote.expiresAt)}</p>}
        <button disabled={busy || !lines.length || wardLoading || !form.address.provinceCode
          || !wards.some((item) => item.code === form.address.wardCode)} type="submit">Tính lại tổng tiền</button>
        <button className="primary" type="button" disabled={busy || !quote}
          onClick={() => setConfirm(true)}>Đặt hàng</button>
        <a href="#/retail/dashboard">Trở về mua sắm</a>
      </aside></form></>}
    {confirm && quote && <Modal title="Xác nhận đặt đơn bán lẻ"
      onClose={() => { if (!busy) setConfirm(false); }}><p>Đặt đơn {money(quote.total)} cho
        {form.address.recipientName} và giữ hàng để thanh toán?</p><Notice error>{error}</Notice>
      <button disabled={busy} onClick={() => setConfirm(false)}>Kiểm tra lại</button>
      <button disabled={busy} onClick={() => place()}>Xác nhận đặt hàng</button></Modal>}
  </section>;
}
