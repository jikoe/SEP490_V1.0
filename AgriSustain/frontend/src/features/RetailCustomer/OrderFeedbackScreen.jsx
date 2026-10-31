import React, { useEffect, useRef, useState } from 'react';
import { request } from '../../api/client';
import { localTime, money, Notice } from '../../components/ui/Common';
import { orderStatus } from '../Commerce/Shared';
import { Icon, ProductImage } from '../Marketplace/MarketplaceUi';
import './order-feedback.css';

const reasons = [
  ['NotAsDescribed', 'Sản phẩm không đúng mô tả'], ['Damaged', 'Sản phẩm bị hư hỏng'],
  ['MissingItems', 'Giao thiếu hàng'], ['Other', 'Khác'],
];
const ratings = ['Rất không hài lòng', 'Không hài lòng', 'Bình thường', 'Hài lòng', 'Rất hài lòng'];

function Attachments({ files, setFiles, disabled, onError }) {
  const input = useRef(null);
  const [previews, setPreviews] = useState([]);
  useEffect(() => {
    const next = files.map(file => ({ name: file.name, url: URL.createObjectURL(file) }));
    setPreviews(next);
    return () => next.forEach(preview => URL.revokeObjectURL(preview.url));
  }, [files]);

  function select(event) {
    const added = Array.from(event.target.files || []);
    event.target.value = '';
    if (files.length + added.length > 5) {
      onError('Bạn chỉ được đính kèm tối đa 5 ảnh.');
    } else if (added.some(file => !/\.(jpe?g|png)$/i.test(file.name)
      || file.size <= 0 || file.size > 5 * 1024 * 1024)) {
      onError('Vui lòng chọn ảnh JPG hoặc PNG, mỗi ảnh không quá 5 MB.');
    } else {
      onError('');
      setFiles(current => [...current, ...added]);
    }
  }

  return <section className="feedback-attachments" aria-label="Ảnh đính kèm">
    <div className="feedback-upload-copy"><Icon name="grid" /><div><strong>Thêm ảnh thực tế</strong>
      <span>Không bắt buộc · JPG, PNG · Tối đa 5 ảnh, mỗi ảnh 5 MB</span></div></div>
    <div className="feedback-previews">{previews.map((preview, index) =>
      <div className="feedback-preview" key={preview.url}>
        <img src={preview.url} alt={`Ảnh đính kèm: ${preview.name}`} />
        <button type="button" aria-label={`Xóa ảnh ${index + 1}`} disabled={disabled}
          onClick={() => setFiles(current => current.filter((_, at) => at !== index))}>×</button>
      </div>)}
      <button type="button" className="feedback-upload-button"
        disabled={disabled || files.length >= 5}
        onClick={() => input.current?.click()}>＋ Thêm ảnh</button>
    </div>
    <input ref={input} type="file" accept="image/jpeg,image/png" multiple hidden
      disabled={disabled} onChange={select} aria-label="Chọn ảnh thực tế" />
  </section>;
}

function ProductSummary({ item, order }) {
  return <div className="feedback-product">
    <ProductImage url={item.imageUrl} alt={item.name} />
    <div className="feedback-product-copy"><h2>{item.name}</h2>
      <p className="feedback-muted">{item.category}</p>
      <p className="feedback-farm"><Icon name="shield" />{item.farmName}
        {item.isFarmVerified && <span aria-label="Nhà cung cấp đã xác thực">✓</span>}</p>
      <dl className="feedback-metadata"><dt>Mã đơn hàng</dt><dd>#{order.orderCode}</dd>
          <dt>Trạng thái đơn hàng</dt><dd><span className="feedback-badge">Hoàn thành</span></dd>
          <dt>Ngày mua</dt><dd>{localTime(order.orderDate)}</dd>
          <dt>Số lượng</dt><dd>{item.quantity} {item.unit}</dd></dl>
    </div>
  </div>;
}

function ComplaintProducts({ items, selectedIds, onChange }) {
  const allSelected = selectedIds.length === items.length;
  return <fieldset className="feedback-products">
    <legend>Chọn sản phẩm cần khiếu nại <span>*</span></legend>
    <div className="feedback-product-selection">
      <p id="feedback-product-hint">Bạn có thể chọn nhiều sản phẩm để gửi trong một lần.</p>
      <button type="button" onClick={() => onChange(allSelected ? [] : items.map(item => String(item.id)))}>
        {allSelected ? 'Bỏ chọn tất cả' : 'Chọn tất cả'}</button>
    </div>
    <div className="feedback-product-options" aria-describedby="feedback-product-hint">
      {items.map(product => {
        const id = String(product.id);
        const checked = selectedIds.includes(id);
        return <label className={`feedback-product-option ${checked ? 'selected' : ''}`} key={product.id}>
          <input type="checkbox" checked={checked}
            aria-label={`Khiếu nại ${product.name} từ ${product.farmName}`}
            onChange={event => onChange(event.target.checked
              ? [...selectedIds, id] : selectedIds.filter(value => value !== id))} />
          <ProductImage url={product.imageUrl} alt={product.name} />
          <span><strong>{product.name}</strong><small>{product.farmName}</small>
            <small>{money(product.price)} / {product.unit}</small></span>
        </label>;
      })}
    </div>
    <p className="feedback-selection-count" role="status">Đã chọn {selectedIds.length} / {items.length} sản phẩm</p>
  </fieldset>;
}

export default function OrderFeedbackScreen({ orderId, mode, onBack, navigate }) {
  const complaint = mode === 'complaint';
  const [context, setContext] = useState(null);
  const [selectedId, setSelectedId] = useState('');
  const [selectedComplaintIds, setSelectedComplaintIds] = useState([]);
  const [rating, setRating] = useState(0);
  const [comment, setComment] = useState('');
  const [reason, setReason] = useState('');
  const [description, setDescription] = useState('');
  const [files, setFiles] = useState([]);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [success, setSuccess] = useState('');
  const [reload, setReload] = useState(0);
  const heading = useRef(null);

  useEffect(() => { heading.current?.focus(); }, []);
  useEffect(() => {
    const controller = new AbortController();
    setContext(null);
    setError('');
    request(`/orders/${orderId}/feedback`, { signal: controller.signal }).then(result => {
      if (controller.signal.aborted) return;
      setContext(result);
      if (complaint) {
        const available = result.items.filter(item => item.canComplain).map(item => String(item.id));
        setSelectedComplaintIds(current => {
          const retained = current.filter(id => available.includes(id));
          return retained.length ? retained : available.slice(0, 1);
        });
        return;
      }
      const first = result.items.find(item => item.canReview || item.canEditReview);
      setSelectedId(current => result.items.some(item => String(item.id) === current
        && (item.canReview || item.canEditReview))
        ? current : String(first?.id || ''));
    }).catch(failure => { if (failure.name !== 'AbortError') setError(failure.message); });
    return () => controller.abort();
  }, [orderId, complaint, reload]);

  const eligible = context?.items.filter(item => complaint ? item.canComplain
    : item.canReview || item.canEditReview) || [];
  const item = context?.items.find(item => String(item.id) === selectedId);
  const selectedComplaintItems = eligible.filter(item => selectedComplaintIds.includes(String(item.id)));
  const editing = !complaint && Boolean(item?.canEditReview);
  const title = complaint ? 'Gửi khiếu nại đơn hàng'
    : editing ? 'Sửa đánh giá sản phẩm' : 'Đánh giá sản phẩm';

  useEffect(() => {
    setRating(item?.review?.rating || 0);
    setComment(item?.review?.comment || '');
    setFiles([]);
    setError('');
  }, [item?.id, item?.review]);

  async function submit(event) {
    event.preventDefault();
    if (busy || !context || (!complaint && !item)) return;
    setError('');
    if (complaint ? !selectedComplaintItems.length || !reason || !description.trim()
      : !rating || !comment.trim()) {
      setError(complaint ? 'Vui lòng chọn ít nhất một sản phẩm, lý do và mô tả vấn đề.'
        : 'Vui lòng chọn số sao và nhập nhận xét của bạn.');
      return;
    }
    const body = new FormData();
    if (complaint) {
      selectedComplaintItems.forEach(product => body.append('orderItemIds', product.id));
      body.append('reason', reason);
      body.append('description', description.trim());
      files.forEach(file => body.append('files', file));
    } else {
      body.append('rating', rating);
      body.append('comment', comment.trim());
    }
    setBusy(true);
    try {
      const result = await request(complaint ? `/orders/${orderId}/complaints`
        : `/orders/${orderId}/reviews/${item.id}`, { method: editing ? 'PUT' : 'POST', body });
      setSuccess(result.message);
      setFiles([]);
    } catch (failure) {
      const fields = Object.values(failure.fields || {}).flat().join(' ');
      setError(fields || failure.message);
    } finally { setBusy(false); }
  }

  return <div className="order-feedback">
    <nav className="feedback-breadcrumb" aria-label="Đường dẫn">
      <a href="#/retail/profile" onClick={event => {
        if (navigate) { event.preventDefault(); navigate('/retail/profile'); }
      }}><Icon name="user" />Tài khoản</a><span>›</span>
      <button type="button" disabled={busy} onClick={onBack}>Đơn hàng của tôi</button><span>›</span>
      <span>{title}</span>
    </nav>
    <header className="feedback-heading"><button type="button" className="feedback-back"
      disabled={busy} onClick={onBack}>← Quay lại</button>
      <h1 ref={heading} tabIndex={-1}>{title}</h1>
      <p>{complaint ? 'Nếu bạn gặp vấn đề với sản phẩm hoặc đơn hàng, vui lòng gửi khiếu nại để được hỗ trợ.'
        : 'Chia sẻ trải nghiệm của bạn để giúp người mua khác lựa chọn tốt hơn.'}</p>
    </header>
    <Notice error>{error}</Notice>
    {success ? <section className="feedback-panel feedback-success" role="status">
      <Icon name="shield" /><h2>{complaint ? 'Đã nhận khiếu nại'
        : editing ? 'Đã cập nhật đánh giá' : 'Cảm ơn bạn đã đánh giá'}</h2>
      <p>{success}</p><button type="button" className="feedback-primary" onClick={onBack}>
        Trở về đơn hàng</button></section>
      : !context ? <section className="feedback-panel feedback-loading" role="status">
        {error ? <button type="button" onClick={() => setReload(value => value + 1)}>Thử lại</button>
          : 'Đang tải thông tin đơn hàng…'}</section>
        : !eligible.length ? <section className="feedback-panel feedback-empty" role="status">
          <h2>{complaint ? 'Đơn hàng chưa đủ điều kiện gửi khiếu nại' : 'Không còn sản phẩm đủ điều kiện đánh giá'}</h2>
          <p>{complaint ? 'Chỉ có thể gửi khiếu nại khi đơn ở trạng thái Đã giao, trong vòng 48 giờ sau giao và chưa có khiếu nại đang xử lý.'
            : 'Bạn có thể gửi hoặc sửa đánh giá của mình cho sản phẩm trong đơn đã hoàn thành và đã nhận.'}</p>
          {complaint && <p>Đơn #{context.orderCode} hiện ở trạng thái <strong>
            {orderStatus[context.status] || context.status}</strong>.
            {' '}Nếu cần trợ giúp thêm, vui lòng <a href={`mailto:support@agrisustain.vn?subject=${encodeURIComponent(`Hỗ trợ đơn hàng ${context.orderCode}`)}`}>
              liên hệ hỗ trợ</a>.</p>}
          <button type="button" onClick={onBack}>Trở về đơn hàng</button></section>
          : <div className="feedback-layout"><form className="feedback-main" onSubmit={submit}>
            <fieldset disabled={busy}>
              <section className="feedback-panel">
                <h2 className="feedback-section-title"><Icon name={complaint ? 'card' : 'leaf'} />
                  {complaint ? `Đơn hàng #${context.orderCode}` : 'Thông tin sản phẩm'}</h2>
                {complaint && <p className="feedback-order-caption">Đặt ngày {localTime(context.orderDate)}
                  <span className="feedback-badge">Đã giao hàng</span></p>}
                {!complaint && eligible.length > 1 && <label className="feedback-field">Sản phẩm đánh giá
                  <select value={selectedId} onChange={event => {
                    setSelectedId(event.target.value);
                  }}>{eligible.map(product => <option key={product.id} value={product.id}>
                    {product.name} · {product.sellerOrderCode}{product.canEditReview ? ' · Đã đánh giá' : ''}
                  </option>)}</select></label>}
                {!complaint && item && <ProductSummary item={item} order={context} />}
                {complaint && <><ComplaintProducts items={eligible} selectedIds={selectedComplaintIds}
                  onChange={ids => { setSelectedComplaintIds(ids); setError(''); }} />
                  <div className="feedback-total"><span>Tổng tiền đơn hàng</span>
                    <strong>{money(context.total)}</strong></div></>}
                {!complaint && <div className="feedback-review-fields">
                  <fieldset className="feedback-rating"><legend>★ Mức độ hài lòng của bạn <span>*</span></legend>
                    <div className="feedback-stars">{ratings.map((label, index) => <label key={label}
                      className={rating >= index + 1 ? 'selected' : ''}>
                      <input type="radio" name="rating" value={index + 1} checked={rating === index + 1}
                        onChange={() => setRating(index + 1)} required aria-label={`${index + 1} sao – ${label}`} />
                      <span aria-hidden="true">{rating >= index + 1 ? '★' : '☆'}</span></label>)}</div>
                    <p className="feedback-muted">{rating ? `${rating}/5 – ${ratings[rating - 1]}` : 'Chọn số sao để đánh giá'}</p>
                  </fieldset>
                  <label className="feedback-field">Nhận xét của bạn <span>*</span>
                    <textarea value={comment} onChange={event => setComment(event.target.value)}
                      required maxLength={1000} rows={5} placeholder="Chia sẻ trải nghiệm thực tế về sản phẩm…" />
                  </label><p className="feedback-counter">{comment.length}/1.000 ký tự</p>
                  <div className="feedback-actions"><button type="button" onClick={onBack}>Hủy</button>
                    <button className="feedback-primary" type="submit">★ {busy ? 'Đang lưu…'
                      : editing ? 'Lưu thay đổi' : 'Gửi đánh giá'}</button></div>
                </div>}
              </section>
              {complaint && <section className="feedback-panel"><h2 className="feedback-section-title">
                <Icon name="shield" />Thông tin khiếu nại</h2>
                <fieldset className="feedback-reasons"><legend>Lý do khiếu nại <span>*</span></legend>
                  <div>{reasons.map(([value, label]) => <label key={value}>
                    <input type="radio" name="reason" value={value} checked={reason === value}
                      required onChange={() => setReason(value)} />{label}</label>)}</div></fieldset>
                <label className="feedback-field">Mô tả chi tiết vấn đề <span>*</span>
                  <textarea value={description} onChange={event => setDescription(event.target.value)}
                    maxLength={500} required rows={5}
                    placeholder="Vui lòng mô tả rõ vấn đề bạn gặp phải: bao bì, chất lượng hoặc số lượng…" /></label>
                <p className="feedback-counter">{description.length}/500 ký tự</p>
                <Attachments files={files} setFiles={setFiles} disabled={busy} onError={setError} />
                <div className="feedback-actions"><button type="button" onClick={onBack}>Hủy</button>
                  <button className="feedback-primary" type="submit" disabled={!selectedComplaintItems.length}>
                    <Icon name="share" />
                    {busy ? 'Đang gửi…' : 'Gửi khiếu nại'}</button></div>
              </section>}
            </fieldset>
          </form><aside className="feedback-sidebar">
            <section className="feedback-panel"><h2 className="feedback-section-title"><Icon name="shield" />
              {complaint ? 'Thông tin đơn hàng' : 'Lưu ý khi đánh giá'}</h2>
              {complaint ? <dl className="feedback-metadata"><dt>Mã đơn hàng</dt><dd>#{context.orderCode}</dd>
                <dt>Ngày đặt</dt><dd>{localTime(context.orderDate)}</dd><dt>Trạng thái</dt>
                <dd><span className="feedback-badge">Đã giao hàng</span></dd><dt>Tổng tiền</dt>
                <dd className="feedback-price">{money(context.total)}</dd></dl>
                : <ul className="feedback-tips"><li><Icon name="shield" />Chỉ đánh giá sản phẩm trong đơn hàng đã hoàn tất và đã nhận.</li>
                  <li><Icon name="card" />Mỗi dòng hàng có một đánh giá. Bạn có thể sửa đánh giá của mình.</li>
                  <li><Icon name="check" />Nội dung trung thực, lịch sự và liên quan đến sản phẩm.</li></ul>}
            </section>
            <section className="feedback-panel"><h2 className="feedback-section-title"><Icon name={complaint ? 'user' : 'cart'} />
              {complaint ? 'Thông tin người nhận' : 'Đơn hàng của bạn'}</h2>
              {complaint ? <div className="feedback-recipient"><strong>{context.recipientName}</strong>
                <p>{context.recipientPhone}</p><p>{context.address}</p></div>
                : <><p className="feedback-muted">Mã đơn hàng</p><strong>#{context.orderCode}</strong>
                  <p><span className="feedback-badge">Hoàn thành</span></p>{item && <div className="feedback-mini-product">
                    <ProductImage url={item.imageUrl} alt={item.name} /><div><strong>{item.name}</strong>
                      <p>{item.quantity} {item.unit}</p><span>{money(item.total)}</span></div></div>}</>}
            </section>
            {complaint && <section className="feedback-panel"><h2 className="feedback-section-title">
              <Icon name="user" />Hỗ trợ nhanh</h2><p className="feedback-muted">Cần trợ giúp? Hãy liên hệ và cung cấp mã đơn hàng.</p>
              <a className="feedback-support" href={`mailto:support@agrisustain.vn?subject=${encodeURIComponent(`Hỗ trợ đơn hàng ${context.orderCode}`)}`}>
                Liên hệ hỗ trợ</a></section>}
            <div className="feedback-assurance"><Icon name="shield" /><p>{complaint
              ? 'Vui lòng gửi yêu cầu trong vòng 48 giờ sau khi giao và giữ lại ảnh thực tế để hỗ trợ quá trình xử lý.'
              : 'Đánh giá của bạn giúp người mua lựa chọn tốt hơn và góp phần xây dựng cộng đồng nông sản minh bạch.'}</p></div>
          </aside></div>}
  </div>;
}
