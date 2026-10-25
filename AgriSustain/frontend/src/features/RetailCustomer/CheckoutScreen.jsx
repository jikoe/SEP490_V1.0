import React, { useEffect, useMemo, useRef, useState } from 'react';
import { requestUseCase as request } from '../../api/client';
import { Modal, Notice, localTime, money } from '../../components/ui/Common';
import './checkout.css';

const emptyAddress = { recipientName: '', phone: '', provinceCode: '', province: '',
  districtCode: '', district: '', wardCode: '', ward: '', detail: '', catalogVersion: '' };
const initial = { address: emptyAddress, deliveryMethod: 'Standard', paymentMethod: 'PayOS',
  voucherCode: '', points: 0, notes: '' };
// Written only after the buyer confirms. The older keys were written on every quote, so they are
// dropped rather than resumed: resuming them could place an order nobody confirmed.
const attemptStorage = 'agrisustain:checkout-placement';
const quoteStorage = 'agrisustain:checkout-placement-quote';
const legacyStorage = ['agrisustain:checkout-attempt', 'agrisustain:checkout-quote'];
const phonePattern = /^(0[35789][0-9]{8}|\+84[35789][0-9]{8})$/;
const addressErrors = ['ADDRESS_INVALID', 'VALIDATION'];
const shippingErrors = ['SHIPPING_RATE_UNAVAILABLE', 'MSG117'];
const methods = [
  { value: 'Standard', title: 'Giao tiêu chuẩn', desc: 'Hãng rẻ nhất cho từng kiện' },
  { value: 'FreshExpress', title: 'Giao nhanh nông sản', desc: 'Dịch vụ Nhanh / Hỏa tốc' },
];
const payments = [
  { value: 'PayOS', title: 'PayOS', desc: 'Chuyển khoản qua cổng PayOS' },
  { value: 'VietQR', title: 'VietQR', desc: 'Quét mã bằng ứng dụng ngân hàng' },
];

const trustedCatalog = 'goship-v2';

/** Compares place names without administrative prefixes ("Thành phố Hà Nội" = "Hà Nội"). */
function placeKey(name = '') {
  return name.normalize('NFC').toLowerCase().replace(/\s+/g, ' ').trim()
    .replace(/^(thành phố|tỉnh|tp\.?|quận|huyện|thị xã|thị trấn|phường|xã)\s+/u, '');
}

function findPlace(options, code, name) {
  const byCode = code && options.find((item) => item.code === code);
  if (byCode) return byCode;
  const key = placeKey(name);
  const matches = key ? options.filter((item) => placeKey(item.name) === key) : [];
  return matches.length === 1 ? matches[0] : undefined;
}

function readJson(key) {
  try { return JSON.parse(sessionStorage.getItem(key) || 'null'); } catch { return null; }
}

function voucherTitle(voucher) {
  return voucher.discountType === 'Percentage'
    ? `Giảm ${voucher.discountValue}%${voucher.maxDiscountAmount ? ` tối đa ${money(voucher.maxDiscountAmount)}` : ''}`
    : `Giảm ${money(voucher.discountValue)}`;
}

/**
 * Server quotes determine prices. A quote is requested automatically once the address is complete;
 * the placement key is persisted only after the buyer confirms, so a reload can resume an
 * ambiguous placement without creating a second order.
 */
export default function CheckoutScreen({ navigate }) {
  const [context, setContext] = useState(null);
  const [provinces, setProvinces] = useState([]);
  const [districts, setDistricts] = useState([]);
  const [districtLoading, setDistrictLoading] = useState(false);
  const [districtError, setDistrictError] = useState('');
  const [districtRefresh, setDistrictRefresh] = useState(0);
  const [wards, setWards] = useState([]);
  const [wardLoading, setWardLoading] = useState(false);
  const [wardError, setWardError] = useState('');
  const [wardRefresh, setWardRefresh] = useState(0);
  const [form, setForm] = useState(initial);
  const [savedIndex, setSavedIndex] = useState(-1);
  const [voucherInput, setVoucherInput] = useState('');
  const [voucherOpen, setVoucherOpen] = useState(false);
  const voucherRef = useRef(null);
  const voucherToggleRef = useRef(null);
  const voucherInputRef = useRef(null);
  const [quote, setQuote] = useState(null);
  const [quotedFor, setQuotedFor] = useState('');
  const [quoteError, setQuoteError] = useState(null);
  const [quoting, setQuoting] = useState(false);
  const [quoteRefresh, setQuoteRefresh] = useState(0);
  const [feeCache, setFeeCache] = useState({ area: '', fees: {} });
  const [order, setOrder] = useState(null);
  const [placeError, setPlaceError] = useState(null);
  const [loadError, setLoadError] = useState('');
  const [busy, setBusy] = useState(false);
  const [recovering, setRecovering] = useState(false);
  const [loading, setLoading] = useState(true);
  const [confirm, setConfirm] = useState(false);
  const inFlight = useRef(false);

  useEffect(() => {
    const controller = new AbortController();
    Promise.all([request('/checkout', { signal: controller.signal }),
      request('/checkout/provinces', { signal: controller.signal })])
      .then(([data, catalog]) => {
        setContext(data); setProvinces(catalog);
        if (data.addresses.length) {
          setSavedIndex(0);
          setForm({ ...initial, address: savedAddress(data.addresses[0], catalog) });
        }
      })
      .catch((failure) => { if (failure.name !== 'AbortError') setLoadError(failure.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, []);

  // A confirmed placement whose response never arrived is resumed with the same request key.
  useEffect(() => {
    if (loading) return;
    legacyStorage.forEach((key) => sessionStorage.removeItem(key));
    if (readJson(attemptStorage)) {
      setRecovering(true);
      void place(true);
    }
  }, [loading]);

  // Abort obsolete requests when the province changes so districts stay linked to their parent.
  useEffect(() => {
    const controller = new AbortController();
    const provinceCode = form.address.provinceCode;
    setDistricts([]); setDistrictError(''); setDistrictLoading(Boolean(provinceCode));
    if (provinceCode) {
      request(`/checkout/provinces/${encodeURIComponent(provinceCode)}/districts`, {
        signal: controller.signal,
      }).then((options) => {
        if (controller.signal.aborted) return;
        setDistricts(options);
        setForm((old) => {
          if (old.address.provinceCode !== provinceCode
            || (!old.address.districtCode && !old.address.district)) return old;
          const selected = findPlace(options, old.address.districtCode, old.address.district);
          return selected ? { ...old, address: { ...old.address, districtCode: selected.code,
            district: selected.name } }
            : { ...old, address: { ...old.address, districtCode: '', district: '',
              wardCode: '', ward: '', catalogVersion: '' } };
        });
      }).catch((failure) => {
        if (failure.name !== 'AbortError') setDistrictError(failure.message);
      }).finally(() => {
        if (!controller.signal.aborted) setDistrictLoading(false);
      });
    }
    return () => controller.abort();
  }, [form.address.provinceCode, districtRefresh]);

  // Wards follow the selected district the same way.
  useEffect(() => {
    const controller = new AbortController();
    const districtCode = form.address.districtCode;
    setWards([]); setWardError(''); setWardLoading(Boolean(districtCode));
    if (districtCode) {
      request(`/checkout/districts/${encodeURIComponent(districtCode)}/wards`, {
        signal: controller.signal,
      }).then((options) => {
        if (controller.signal.aborted) return;
        setWards(options);
        setForm((old) => {
          if (old.address.districtCode !== districtCode
            || (!old.address.wardCode && !old.address.ward)) return old;
          const selected = findPlace(options, old.address.wardCode, old.address.ward);
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
  }, [form.address.districtCode, wardRefresh]);

  /**
   * Only addresses saved against the Goship catalog carry usable codes. Older ones (bundled
   * two-level catalog, seed data) are matched by name level by level: province here, district and
   * ward in the effects above once their lists load.
   */
  function savedAddress(saved, catalog = provinces) {
    const values = { ...emptyAddress, ...saved, catalogVersion: '' };
    const trusted = saved.catalogVersion === trustedCatalog;
    const province = findPlace(catalog, trusted ? values.provinceCode : '', values.province);
    if (!province) {
      return { ...values, provinceCode: '', province: '', districtCode: '', district: '',
        wardCode: '', ward: '' };
    }
    return { ...values, provinceCode: province.code, province: province.name,
      districtCode: trusted ? values.districtCode : '', wardCode: trusted ? values.wardCode : '' };
  }

  const recipient = form.address;
  const areaReady = Boolean(recipient.provinceCode && recipient.districtCode)
    && !districtLoading && !wardLoading
    && districts.some((item) => item.code === recipient.districtCode)
    && wards.some((item) => item.code === recipient.wardCode);
  const recipientReady = recipient.recipientName.trim().length >= 2
    && phonePattern.test(recipient.phone.trim()) && recipient.detail.trim().length > 0;
  const quoteReady = areaReady && recipientReady && Boolean(context?.selectedItems.length);
  const body = useMemo(() => ({ ...form, voucherCode: form.voucherCode || null,
    points: Math.max(0, Math.floor(Number(form.points) || 0)) }), [form]);
  const formKey = JSON.stringify(body);
  const areaKey = `${recipient.provinceCode}|${recipient.districtCode}|${recipient.wardCode}`;
  const fresh = Boolean(quote) && quotedFor === formKey;

  // Quotes refresh automatically (debounced) whenever an input that affects the price changes.
  useEffect(() => {
    if (!quoteReady || order) return undefined;
    const controller = new AbortController();
    const timer = setTimeout(async () => {
      setQuoting(true);
      try {
        const next = await request('/checkout/quotes', { method: 'POST', body,
          signal: controller.signal });
        setQuote(next); setQuotedFor(formKey); setQuoteError(null);
        setFeeCache((old) => ({ area: areaKey, fees: { ...(old.area === areaKey ? old.fees : {}),
          [body.deliveryMethod]: next.deliveryFee } }));
      } catch (failure) {
        if (failure.name === 'AbortError') return;
        setQuote(null); setQuotedFor(formKey);
        setQuoteError({ code: failure.code, message: failure.message });
      } finally {
        if (!controller.signal.aborted) setQuoting(false);
      }
    }, 500);
    return () => { clearTimeout(timer); controller.abort(); setQuoting(false); };
  }, [formKey, quoteReady, quoteRefresh, order]);

  function update(name, value) {
    setForm((old) => ({ ...old, [name]: value }));
    setPlaceError(null);
  }

  /** Any manual edit means the form no longer matches the highlighted saved address. */
  function changeAddress(patch) {
    setForm((old) => ({ ...old, address: { ...old.address, ...patch } }));
    setSavedIndex(-1); setPlaceError(null);
  }

  function address(name, value) {
    changeAddress({ [name]: value });
  }

  function pickSaved(index) {
    setSavedIndex(index); setPlaceError(null);
    setForm((old) => ({ ...old, address: index < 0 ? emptyAddress
      : savedAddress(context.addresses[index]) }));
    // Re-run the lookups even when the province/district code did not change.
    setDistrictRefresh((value) => value + 1); setWardRefresh((value) => value + 1);
  }

  function closeVouchers() {
    setVoucherOpen(false);
    voucherToggleRef.current?.focus();
  }

  function applyVoucher(code) {
    const next = code.trim();
    setVoucherInput(next);
    update('voucherCode', next);
    if (voucherOpen) closeVouchers();
  }

  // The voucher panel opens with the code box focused and closes on a click outside it.
  useEffect(() => {
    if (!voucherOpen) return undefined;
    voucherInputRef.current?.focus();
    const closeOnOutside = (event) => {
      if (!voucherRef.current?.contains(event.target)) setVoucherOpen(false);
    };
    document.addEventListener('mousedown', closeOnOutside);
    return () => document.removeEventListener('mousedown', closeOnOutside);
  }, [voucherOpen]);

  async function place(retry = false) {
    if (inFlight.current) return;
    let pending = retry ? readJson(attemptStorage) : null;
    if (!pending) {
      if (retry) { setRecovering(false); return; }
      if (!fresh) return;
      pending = { quoteId: quote.id, requestKey: crypto.randomUUID(), confirmed: true };
      sessionStorage.setItem(attemptStorage, JSON.stringify(pending));
      sessionStorage.setItem(quoteStorage, JSON.stringify(quote));
    }
    inFlight.current = true; setBusy(true); setPlaceError(null);
    try {
      const result = await request('/checkout/orders', { method: 'POST', body: pending });
      const placedQuote = readJson(quoteStorage);
      if (placedQuote) setQuote(placedQuote);
      setOrder(result); setConfirm(false);
      sessionStorage.removeItem(attemptStorage); sessionStorage.removeItem(quoteStorage);
    } catch (failure) {
      // Without an HTTP status the outcome is unknown: keep the key so "Thử lại" stays idempotent.
      const unknown = !failure.status;
      if (!unknown) { sessionStorage.removeItem(attemptStorage); sessionStorage.removeItem(quoteStorage); }
      setPlaceError({ message: failure.message, retry: unknown });
      if (['MSG88', 'MSG118', 'QUOTE_EXPIRED'].includes(failure.code)) {
        setConfirm(false); setQuoteRefresh((value) => value + 1);
      }
    } finally { inFlight.current = false; setBusy(false); setRecovering(false); }
  }

  const lines = (fresh ? quote.lines : null) || context?.selectedItems || [];
  const parcels = useMemo(() => {
    const groups = new Map();
    lines.forEach((line) => {
      if (!groups.has(line.farmerId)) groups.set(line.farmerId, { farmerId: line.farmerId,
        sellerName: line.sellerName, lines: [] });
      groups.get(line.farmerId).lines.push(line);
    });
    return [...groups.values()];
  }, [lines]);
  const subtotal = lines.reduce((sum, line) => sum + line.subtotal, 0);
  const fees = feeCache.area === areaKey ? feeCache.fees : {};
  const vouchers = context?.vouchers || [];
  const quoteMessage = quoteError?.message;
  const addressMessage = addressErrors.includes(quoteError?.code) ? quoteMessage : '';
  const shippingMessage = shippingErrors.includes(quoteError?.code) ? quoteMessage : '';
  const summaryMessage = quoteError && !addressMessage && !shippingMessage ? quoteMessage : '';

  function methodPrice(method) {
    if (!areaReady) return 'Nhập địa chỉ';
    if (fees[method] !== undefined) return money(fees[method]);
    return form.deliveryMethod === method && quoting ? 'Đang tính…' : 'Chọn để xem phí';
  }

  if (order) return <section className="co-page"><div className="co-wrap">
    <h1>Đặt hàng thành công</h1>
    <div className="co-card"><h2>Thông tin đơn hàng</h2>
      <dl className="details"><dt>Mã đơn</dt><dd>{order.code}</dd>
        <dt>Trạng thái</dt><dd>{order.status}</dd>
        <dt>Người nhận</dt><dd>{quote?.input.address.recipientName}</dd>
        <dt>Số điện thoại</dt><dd>{quote?.input.address.phone}</dd>
        <dt>Địa chỉ</dt><dd>{quote && `${quote.input.address.detail}, ${quote.input.address.ward}, ${quote.input.address.district}, ${quote.input.address.province}`}</dd>
        <dt>Vận chuyển</dt><dd>{quote?.input.deliveryMethod === 'Standard' ? 'Giao tiêu chuẩn (Goship)' : 'Giao nhanh nông sản (Goship)'}</dd>
        <dt>Ngày giao dự kiến</dt><dd>{quote && new Date(quote.expectedDeliveryAt).toLocaleDateString('vi-VN')}</dd>
        <dt>Phương thức thanh toán</dt><dd>{order.paymentMethod}</dd>
        <dt>Hạn giữ hàng</dt><dd>{localTime(order.reservationExpiresAt)}</dd>
        <dt><strong>Số tiền cần thanh toán</strong></dt><dd><strong>{money(order.total)}</strong></dd></dl>
      <Notice>{order.message}</Notice>
      <div className="co-actions">
        <button className="primary" onClick={() => navigate('/retail/orders')}>Theo dõi đơn hàng</button>
        <button onClick={() => navigate('/cart')}>Xem giỏ hàng còn lại</button>
      </div>
    </div></div></section>;

  return <section className="co-page"><div className="co-wrap">
    <div>
      <nav className="co-crumbs" aria-label="Đường dẫn"><button type="button" className="text-button"
        onClick={() => navigate('/cart')}>Giỏ hàng</button><span aria-hidden="true">/</span><span>Thanh toán</span></nav>
      <h1>Thanh toán</h1>
      {context && <p className="co-sub">{lines.length} sản phẩm từ {parcels.length} nông trại · mỗi nông trại gửi một kiện riêng qua Goship</p>}
    </div>
    <Notice error>{loadError}</Notice>
    {recovering && <Notice>Đang kiểm tra kết quả đơn hàng bạn vừa gửi…</Notice>}
    {loading ? <p role="status">Đang tải checkout…</p> : context && <div className="co-layout">
      <div className="co-main">
        <fieldset className="co-card" disabled={busy}>
          <legend className="co-head"><span className="co-num">1</span>Giao đến</legend>
          {context.addresses.length > 0 && <div className="co-chips">
            {context.addresses.map((saved, index) => <button key={index} type="button"
              className="co-chip" aria-pressed={savedIndex === index} onClick={() => pickSaved(index)}>
              <strong>{saved.recipientName} · {saved.phone}</strong>
              <span>{[saved.detail, saved.ward, saved.district, saved.province].filter(Boolean).join(', ')}</span>
            </button>)}
            <button type="button" className="co-chip co-chip-add" aria-pressed={savedIndex === -1}
              onClick={() => pickSaved(-1)}>+ Địa chỉ mới</button>
          </div>}
          <div className="co-grid-2">
            <label className="co-field">Tên người nhận *<input required minLength={2} maxLength={100}
              value={recipient.recipientName} onChange={(e) => address('recipientName', e.target.value)} /></label>
            <label className="co-field">Số điện thoại *<input required type="tel" value={recipient.phone}
              onChange={(e) => address('phone', e.target.value)} />
              {recipient.phone && !phonePattern.test(recipient.phone.trim())
                && <small className="co-hint-error">Số di động Việt Nam gồm 10 số, ví dụ 0912345678.</small>}</label>
          </div>
          <div className="co-grid-3">
            <label className="co-field">Tỉnh / thành phố *<select required value={recipient.provinceCode}
              onChange={(e) => changeAddress({ provinceCode: e.target.value,
                province: provinces.find((item) => item.code === e.target.value)?.name || '',
                districtCode: '', district: '', wardCode: '', ward: '', catalogVersion: '' })}>
              <option value="">Chọn tỉnh / thành phố</option>{provinces.map((item) =>
                <option key={item.code} value={item.code}>{item.name}</option>)}</select></label>
            <label className="co-field">Quận / huyện *<select required value={recipient.districtCode}
              disabled={!recipient.provinceCode || districtLoading || !districts.length}
              onChange={(e) => changeAddress({ districtCode: e.target.value,
                district: districts.find((item) => item.code === e.target.value)?.name || '',
                wardCode: '', ward: '', catalogVersion: '' })}>
              <option value="">{districtLoading ? 'Đang tải…'
                : !recipient.provinceCode ? 'Chọn tỉnh trước' : 'Chọn quận / huyện'}</option>
              {districts.map((item) => <option key={item.code} value={item.code}>{item.name}</option>)}
            </select></label>
            <label className="co-field">Phường / xã *<select required value={recipient.wardCode}
              disabled={!recipient.districtCode || wardLoading || !wards.length}
              onChange={(e) => changeAddress({ wardCode: e.target.value,
                ward: wards.find((item) => item.code === e.target.value)?.name || '', catalogVersion: '' })}>
              <option value="">{wardLoading ? 'Đang tải…'
                : !recipient.districtCode ? 'Chọn quận trước' : 'Chọn phường / xã'}</option>
              {wards.map((item) => <option key={item.code} value={item.code}>{item.name}</option>)}
            </select></label>
          </div>
          {!provinces.length && <Notice error>Không tải được danh sách tỉnh / thành phố. Vui lòng tải lại trang.</Notice>}
          {districtError && <div className="co-inline-error"><span>{districtError}</span><button type="button"
            onClick={() => setDistrictRefresh((value) => value + 1)}>Tải lại quận / huyện</button></div>}
          {wardError && <div className="co-inline-error"><span>{wardError}</span><button type="button"
            onClick={() => setWardRefresh((value) => value + 1)}>Tải lại phường / xã</button></div>}
          <label className="co-field">Số nhà, tên đường *<input required maxLength={255} value={recipient.detail}
            onChange={(e) => address('detail', e.target.value)} placeholder="Ví dụ: Số 12 Nguyễn Trãi" /></label>
          <Notice error>{addressMessage}</Notice>
        </fieldset>

        <fieldset className="co-card" disabled={busy}>
          <legend className="co-head"><span className="co-num">2</span>Hình thức giao
            <span className="co-head-hint">Giá từ Goship theo địa chỉ</span></legend>
          <div className="co-grid-2">
            {methods.map((method) => <button key={method.value} type="button" className="co-choice"
              aria-pressed={form.deliveryMethod === method.value}
              onClick={() => update('deliveryMethod', method.value)}>
              <span className="co-choice-row"><span className="co-choice-title">{method.title}</span>
                <span className="co-choice-price">{methodPrice(method.value)}</span></span>
              <span className="co-choice-desc">{method.desc}</span>
            </button>)}
          </div>
          {fresh && <p className="co-muted">Dự kiến giao: {new Date(quote.expectedDeliveryAt).toLocaleDateString('vi-VN')}</p>}
          <Notice error>{shippingMessage}</Notice>
        </fieldset>

        <section className="co-card">
          <h2 className="co-head"><span className="co-num">3</span>Kiện hàng</h2>
          {parcels.map((parcel, index) => <div key={parcel.farmerId} className="co-parcel">
            <div className="co-parcel-head"><strong>{parcel.sellerName}</strong>
              <span>Kiện {index + 1} · phí giao <strong>{fresh && quote.sellerDeliveryFees
                ? money(quote.sellerDeliveryFees[parcel.farmerId] ?? 0) : '—'}</strong></span></div>
            {parcel.lines.map((line) => <div key={line.cartItemId} className="co-item">
              {line.imageUrl ? <img className="co-thumb" src={line.imageUrl} alt="" />
                : <span className="co-thumb" aria-hidden="true" />}
              <div className="co-item-name"><span>{line.productName}</span>
                <small>{line.quantity} {line.unit} × {money(line.unitPrice)}</small></div>
              <span className="co-item-price">{money(line.subtotal)}</span>
            </div>)}
          </div>)}
          {!lines.length && <p>Chưa có sản phẩm được chọn. Hãy cập nhật giỏ hàng.</p>}
        </section>

        <fieldset className="co-card" disabled={busy}>
          <legend className="co-head"><span className="co-num">4</span>Thanh toán</legend>
          <div className="co-grid-2">
            {payments.map((payment) => <button key={payment.value} type="button" className="co-choice"
              aria-pressed={form.paymentMethod === payment.value}
              onClick={() => update('paymentMethod', payment.value)}>
              <span className="co-choice-title">{payment.title}</span>
              <span className="co-choice-desc">{payment.desc}</span>
            </button>)}
          </div>
          <label className="co-field">Ghi chú cho người bán (không bắt buộc)<textarea maxLength={300} rows={2}
            value={form.notes || ''} onChange={(e) => update('notes', e.target.value)}
            placeholder="Ví dụ: gọi trước khi giao" /></label>
        </fieldset>
      </div>

      <aside className="co-aside"><div className="co-card">
        <h2 className="co-head">Tóm tắt đơn hàng</h2>
        <fieldset className="co-plain" disabled={busy}>
          <div className="co-voucher-picker" ref={voucherRef}
            onKeyDown={(e) => { if (e.key === 'Escape' && voucherOpen) { e.stopPropagation(); closeVouchers(); } }}>
            <span className="co-label" id="co-voucher-label">Voucher</span>
            <div className="co-voucher-row">
              <button type="button" ref={voucherToggleRef} className="co-voucher-field"
                aria-labelledby="co-voucher-label co-voucher-value" aria-expanded={voucherOpen}
                aria-controls="co-voucher-panel" onClick={() => (voucherOpen ? closeVouchers() : setVoucherOpen(true))}>
                <span id="co-voucher-value">{form.voucherCode
                  ? <><strong>{form.voucherCode}</strong>{fresh && quote.voucherDiscount > 0
                    ? ` · −${money(quote.voucherDiscount)}` : ''}</>
                  : <span className="co-placeholder">Chọn hoặc nhập mã voucher</span>}</span>
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
                  strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"
                  style={{ transform: voucherOpen ? 'rotate(180deg)' : 'none' }}><path d="M6 9l6 6 6-6" /></svg>
              </button>
              {form.voucherCode && <button type="button" className="text-button"
                onClick={() => applyVoucher('')}>Bỏ</button>}
            </div>
            {voucherOpen && <div id="co-voucher-panel" className="co-voucher-panel" role="group"
              aria-label="Chọn voucher">
              <div className="co-voucher-entry">
                <label className="co-field">Nhập mã voucher<input ref={voucherInputRef} maxLength={100}
                  value={voucherInput} placeholder="Ví dụ: WELCOME10"
                  onChange={(e) => setVoucherInput(e.target.value)}
                  onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); applyVoucher(voucherInput); } }} /></label>
                <button type="button" className="co-btn-outline" disabled={!voucherInput.trim()
                  || voucherInput.trim() === form.voucherCode} onClick={() => applyVoucher(voucherInput)}>Áp dụng</button>
              </div>
              <span className="co-label">Voucher dành cho bạn</span>
              {vouchers.length ? <div className="co-vouchers">
                {vouchers.map((voucher) => <button key={voucher.code} type="button"
                  className="co-voucher" aria-pressed={voucher.code === form.voucherCode}
                  disabled={!voucher.eligible && voucher.code !== form.voucherCode}
                  onClick={() => applyVoucher(voucher.code === form.voucherCode ? '' : voucher.code)}>
                  <span className="co-voucher-top"><strong>{voucher.code}</strong><span>{voucherTitle(voucher)}</span></span>
                  <small>
                    {voucher.minOrderAmount > 0 ? `Đơn từ ${money(voucher.minOrderAmount)}` : 'Không yêu cầu tối thiểu'}
                    {voucher.farmName ? ` · chỉ cho ${voucher.farmName}` : ''}
                    {` · HSD ${new Date(voucher.endDate).toLocaleDateString('vi-VN')}`}
                  </small>
                  {!voucher.eligible && <small className="co-hint-error">Chưa đủ giá trị đơn tối thiểu</small>}
                </button>)}
              </div> : <p className="co-muted">Hiện chưa có voucher phù hợp với giỏ hàng.</p>}
            </div>}
          </div>
          {fresh && quote.warning && <Notice error>{quote.warning}</Notice>}
          <div className="co-points">
            <label className="co-field">Điểm thưởng ({context.availablePoints} khả dụng)
              <input type="number" min={0} max={context.availablePoints} step={1} value={form.points}
                onChange={(e) => update('points', e.target.value)} /></label>
            <button type="button" className="co-btn-outline" disabled={!context.availablePoints}
              onClick={() => update('points', context.availablePoints)}>Dùng hết</button>
          </div>
        </fieldset>
        <dl className="co-lines">
          <dt>Tiền hàng</dt><dd>{money(fresh ? quote.subtotal : subtotal)}</dd>
          <dt>Phí giao ({parcels.length} kiện)</dt><dd>{fresh ? money(quote.deliveryFee) : '—'}</dd>
          <dt>Giảm voucher</dt><dd>{fresh ? `−${money(quote.voucherDiscount)}` : '—'}</dd>
          <dt>Giảm điểm</dt><dd>{fresh ? `−${money(quote.pointsDiscount)}` : '—'}</dd>
        </dl>
        <div className="co-total"><span>Tổng thanh toán</span>
          <span>{fresh ? money(quote.total) : quoting ? 'Đang tính…' : '—'}</span></div>
        {!quoteReady && <p className="co-muted">Điền đủ người nhận, số điện thoại và địa chỉ để tính phí giao và tổng tiền.</p>}
        <Notice error>{summaryMessage}</Notice>
        <button type="button" className="co-btn-primary" disabled={busy || !fresh || quoting}
          onClick={() => setConfirm(true)}>{fresh ? `Đặt hàng · ${money(quote.total)}` : 'Đặt hàng'}</button>
        {placeError && <div className="co-inline-error"><span>{placeError.message}</span>
          {placeError.retry && <button type="button" disabled={busy} onClick={() => place(true)}>Thử lại</button>}</div>}
        {fresh && <p className="co-muted">Giá giữ đến {localTime(quote.expiresAt)}. Sau khi đặt, hàng được giữ để bạn hoàn tất thanh toán.</p>}
        <button type="button" className="text-button co-back" onClick={() => navigate('/cart')}>← Quay lại giỏ hàng</button>
      </div></aside>
    </div>}
    {confirm && fresh && <Modal title="Xác nhận đặt đơn bán lẻ"
      onClose={() => { if (!busy) setConfirm(false); }}>
      <p>Đặt đơn {money(quote.total)} giao đến {recipient.recipientName} ({recipient.phone}) và giữ hàng để thanh toán?</p>
      {placeError && <Notice error>{placeError.message}</Notice>}
      <div className="co-actions">
        <button disabled={busy} onClick={() => setConfirm(false)}>Kiểm tra lại</button>
        <button className="primary" disabled={busy} onClick={() => place()}>Xác nhận đặt hàng</button>
      </div></Modal>}
  </div></section>;
}
