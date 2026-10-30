import { cartMessages } from '../RetailCustomer/cartMessages';
import React, { useEffect, useState } from 'react';
import { request } from '../../api/client';
import { displayMessage } from '../../utils/displayMessage';
import { Icon, Price, ProductImage, Rating, safeUrl } from './MarketplaceUi';

const dateLabel = value => value ? new Date(`${value}T00:00:00`).toLocaleDateString('vi-VN') : 'Không có';

/** UC-RC-01 entry point: adds the chosen batch to the RetailCustomer cart. */
function AddToCart({ product, batches = [], session, navigate }) {
  const [batchId, setBatchId] = useState(batches[0]?.id ?? '');
  const [quantity, setQuantity] = useState('1');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  if (!session) return <p className="mkt-buy-note"><a href="#/login">Đăng nhập</a> bằng tài khoản
    khách lẻ để thêm sản phẩm vào giỏ hàng.</p>;
  if (session.user.role !== 'RetailCustomer') return <p className="mkt-buy-note">
    Chỉ tài khoản khách lẻ (RetailCustomer) có thể đặt mua sản phẩm.</p>;
  if (!batches.length) return <p className="mkt-buy-note">Sản phẩm hiện chưa có lô hàng khả dụng.</p>;

  const batch = batches.find(item => String(item.id) === String(batchId));
  async function add(event) {
    event.preventDefault();
    const amount = Number(quantity);
    if (!Number.isFinite(amount) || amount <= 0) {
      setError(cartMessages.invalidQuantity(product.unit)); return;
    }
    setBusy(true); setError(''); setMessage('');
    try {
      const cart = await request('/cart/items', { method: 'POST',
        body: { productId: product.id, harvestBatchId: Number(batchId), quantity: amount } });
      setMessage(displayMessage(cart.messages.join(' ')));
    } catch (err) {
      setError(displayMessage(err.message));
    } finally {
      setBusy(false);
    }
  }

  return <form className="mkt-buy" onSubmit={add}>
    <label className="mkt-field">Lô hàng<select value={batchId}
      onChange={event => { setBatchId(event.target.value); setMessage(''); setError(''); }}>
      {batches.map(item => <option key={item.id} value={item.id}>
        {item.code} · còn {item.availableQuantity.toLocaleString('vi-VN')} {product.unit}</option>)}
    </select></label>
    <label className="mkt-field">Số lượng ({product.unit})<input type="number" min="0" step="any"
      max={batch?.availableQuantity} value={quantity}
      onChange={event => { setQuantity(event.target.value); setMessage(''); setError(''); }} /></label>
    <button type="submit" className="mkt-primary" disabled={busy}>
      <Icon name="cart" />{busy ? 'Đang thêm…' : 'Thêm vào giỏ hàng'}</button>
    {error && <p role="alert" className="mkt-buy-error">{error}</p>}
    {message && <p role="status" className="mkt-buy-message">{message}{' '}
      <button type="button" onClick={() => navigate('/cart')}>Xem giỏ hàng</button></p>}
  </form>;
}

export default function ProductDetail({ productId, query, reload, session, navigate }) {
  const [detail, setDetail] = useState(null);
  const [error, setError] = useState('');
  const [retry, setRetry] = useState(0);
  const [imageIndex, setImageIndex] = useState(0);
  const [tab, setTab] = useState('information');
  const [shareMessage, setShareMessage] = useState('');
  const [showFarm, setShowFarm] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    setDetail(null);
    setError('');
    request(`/marketplace/products/${encodeURIComponent(productId)}`, { signal: controller.signal })
      .then(result => { setDetail(result.data); setImageIndex(0); })
      .catch(err => { if (err.name !== 'AbortError') setError(err.message); });
    return () => controller.abort();
  }, [productId, reload, retry]);

  async function share() {
    try {
      if (navigator.share) await navigator.share({ title: detail.product.name, url: window.location.href });
      else { await navigator.clipboard.writeText(window.location.href); setShareMessage('Đã sao chép liên kết sản phẩm.'); }
    } catch (err) {
      if (err.name !== 'AbortError') setShareMessage(`Liên kết sản phẩm: ${window.location.href}`);
    }
  }

  if (error) return <main className="mkt-detail"><div className="mkt-state" role="alert">
    <h1>Sản phẩm không khả dụng</h1><p>{error}</p><a href={`#/marketplace${query}`}>Quay lại danh sách</a>
    <button onClick={() => setRetry(value => value + 1)}>Thử lại</button></div></main>;
  if (!detail) return <main className="mkt-detail" aria-busy="true"><p role="status">Đang tải chi tiết sản phẩm…</p></main>;

  const { product, images, batches, certificates, reviews, similarProducts } = detail;
  const tabs = [['information', 'Thông tin sản phẩm'], ['reviews', `Đánh giá (${product.reviewCount})`],
    ['delivery', 'Chính sách giao hàng']];

  return <main className="mkt-detail">
    <nav className="mkt-breadcrumb" aria-label="Đường dẫn">
      <a href="#/marketplace">⌂ <span>Trang chủ</span></a><span>›</span>
      <a href={`#/marketplace${query}`}>Nông sản</a><span>›</span>
      <a href={`#/marketplace?categoryId=${product.categoryId}`}>{product.category}</a><span>›</span>
      <span aria-current="page">{product.name}</span>
    </nav>
    <div className="mkt-detail-grid">
      <section className="mkt-gallery" aria-label="Ảnh sản phẩm">
        <div className="mkt-main-photo">
          <ProductImage url={images[imageIndex]?.url} alt={images[imageIndex]?.altText || product.name} />
          {product.certifications[0] && <span className="mkt-cert-badge"><Icon name="shield" />{product.certifications[0]}</span>}
          {images.length > 1 && <><button aria-label="Ảnh trước" className="mkt-photo-prev"
            onClick={() => setImageIndex((imageIndex + images.length - 1) % images.length)}>‹</button>
            <button aria-label="Ảnh sau" className="mkt-photo-next"
              onClick={() => setImageIndex((imageIndex + 1) % images.length)}>›</button></>}
        </div>
        {images.length > 1 && <div className="mkt-thumbnails">{images.map((image, index) =>
          <button key={image.url} aria-label={`Xem ảnh ${index + 1}`} aria-pressed={index === imageIndex}
            onClick={() => setImageIndex(index)}>
            <ProductImage url={image.url} alt={image.altText || `Ảnh ${index + 1} của ${product.name}`} />
          </button>)}</div>}
      </section>
      <section className="mkt-product-information">
        <a className="mkt-category-pill" href={`#/marketplace?categoryId=${product.categoryId}`}>{product.category}</a>
        <h1>{product.name}</h1>
        <p className="mkt-short-description">{product.region && `Nông sản ${product.region} – `}{product.supplier}</p>
        <Rating product={product} />
        <div className="mkt-detail-price"><Price product={product} /></div>
        <ul className="mkt-highlights">
          <li><Icon />Nguồn gốc: {product.region || 'Chưa cập nhật'}</li>
          <li><Icon name="shield" />{certificates.length > 0
            ? `Chứng nhận: ${product.certifications.join(', ')}` : 'Nhà cung cấp đã xác thực'}</li>
          <li><Icon name="grid" />Còn {product.availableQuantity.toLocaleString('vi-VN')} {product.unit} khả dụng</li>
          {detail.wholesalePrice != null && detail.wholesaleMinQuantity != null && <li>
            <Icon name="card" />Giá sỉ: {detail.wholesalePrice.toLocaleString('vi-VN')}đ / {product.unit}
            {' '}từ {detail.wholesaleMinQuantity} {product.unit}</li>}
        </ul>
        <AddToCart key={product.id} product={product} batches={batches} session={session} navigate={navigate} />
        <button className="mkt-share" onClick={share}><Icon name="share" />Chia sẻ</button>
        {shareMessage && <p className="mkt-share-message" role="status">{shareMessage}</p>}
      </section>
      <aside className="mkt-detail-sidebar">
        <section className="mkt-farm-card">
          <div className="mkt-farm-heading"><span className="mkt-farm-avatar"><Icon /></span>
            <div><h2>{product.supplier}</h2><p><Icon name="shield" />Đã xác thực</p></div></div>
          <button onClick={() => setShowFarm(value => !value)} aria-expanded={showFarm}>
            {showFarm ? 'Ẩn thông tin gian hàng' : 'Xem gian hàng'} <Icon name="arrow" /></button>
          {showFarm && <div className="mkt-farm-extra"><p>{detail.farmAddress}</p>
            <a href={`#/marketplace?supplierId=${product.supplierId}`}>Xem sản phẩm của nhà cung cấp</a></div>}
        </section>
        <section className="mkt-service-card" aria-label="Thông tin mua hàng">
          {[['truck', 'Vận chuyển', 'Phí và phạm vi giao hàng được xác nhận khi thanh toán.'],
            ['card', 'Thanh toán', 'Kiểm tra phương thức và tổng tiền tại bước thanh toán.'],
            ['shield', 'Thông tin minh bạch', 'Xem chứng nhận và truy xuất lô hàng bên dưới.']].map(([icon, title, text]) =>
            <div key={icon}><Icon name={icon} /><div><h2>{title}</h2><p>{text}</p></div></div>)}
        </section>
      </aside>
      <section className="mkt-detail-content">
        <div className="mkt-tabs" role="tablist" aria-label="Thông tin chi tiết"
          onKeyDown={event => {
            const index = tabs.findIndex(([id]) => id === tab);
            const next = event.key === 'ArrowRight' ? (index + 1) % tabs.length
              : event.key === 'ArrowLeft' ? (index + tabs.length - 1) % tabs.length
                : event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1 : null;
            if (next != null) {
              event.preventDefault(); setTab(tabs[next][0]);
              event.currentTarget.querySelectorAll('[role="tab"]')[next].focus();
            }
          }}>
          {tabs.map(([id, title]) => <button key={id} role="tab" id={`tab-${id}`}
            aria-controls="product-panel" aria-selected={tab === id} tabIndex={tab === id ? 0 : -1}
            onClick={() => setTab(id)}>{title}</button>)}
        </div>
        <div id="product-panel" role="tabpanel" aria-labelledby={`tab-${tab}`} tabIndex={0}>
          {tab === 'information' && <>
            <div className="mkt-description-grid"><div><h2>Mô tả sản phẩm</h2>
              <p className="mkt-description">{detail.description || 'Nhà cung cấp chưa cập nhật mô tả sản phẩm.'}</p>
              {certificates.length > 0 && <><h2>Chứng nhận đã xác thực</h2><ul className="mkt-certificates">
                {certificates.map(certificate => <li key={`${certificate.type}-${certificate.number}`}>
                  <Icon name="check" /><div><strong>{certificate.type}</strong> · {certificate.number}
                    <p>{certificate.issuedBy}{certificate.expiryDate && ` · Hết hạn: ${dateLabel(certificate.expiryDate)}`}</p>
                  </div></li>)}</ul></>}
            </div><section className="mkt-specifications"><h2><Icon name="grid" />Thông số sản phẩm</h2>
              <dl><dt>Loại sản phẩm</dt><dd>{product.category}</dd><dt>Xuất xứ</dt><dd>{product.region || 'Chưa cập nhật'}</dd>
                <dt>Đơn vị</dt><dd>{product.unit}</dd><dt>Tồn kho</dt><dd>{product.availableQuantity} {product.unit}</dd>
                {batches[0]?.variety && <><dt>Giống</dt><dd>{batches[0].variety}</dd></>}
                {batches[0]?.harvestDate && <><dt>Thu hoạch</dt><dd>{dateLabel(batches[0].harvestDate)}</dd></>}
                {batches[0]?.expirationDate && <><dt>Hạn sử dụng</dt><dd>{dateLabel(batches[0].expirationDate)}</dd></>}
              </dl></section></div>
            {batches.length > 0 && <section className="mkt-batches"><h2>Lô hàng & truy xuất nguồn gốc</h2>
              {batches.map(batch => <article key={batch.id}><div><h3>{batch.code}</h3>
                <p>{batch.season} · {batch.availableQuantity} {product.unit} khả dụng</p>
                {batch.qualityNotes && <p>{batch.qualityNotes}</p>}</div>
                {safeUrl(batch.traceabilityUrl) && <a href={safeUrl(batch.traceabilityUrl)} target="_blank" rel="noreferrer">
                  Xem truy xuất <Icon name="arrow" /></a>}</article>)}</section>}
          </>}
          {tab === 'reviews' && <section><h2>Đánh giá từ khách hàng</h2><Rating product={product} />
            {reviews.length === 0 ? <p>Chưa có đánh giá từ khách hàng đã mua sản phẩm.</p>
              : <><p>Hiển thị {reviews.length} đánh giá mới nhất trong {product.reviewCount} đánh giá.</p>
                {reviews.map(review => <article className="mkt-review" key={review.id}>
                  <h3>{review.author}</h3><span className="mkt-review-stars" aria-label={`${review.rating} trên 5 sao`}>{'★'.repeat(review.rating)}</span>
                  <span> · Đã mua hàng · {new Date(`${review.date.replace(/Z$/, '')}Z`).toLocaleDateString('vi-VN')}</span>
                  <p>{review.comment || 'Khách hàng đã đánh giá sản phẩm.'}</p></article>)}</>}
          </section>}
          {tab === 'delivery' && <section><h2>Chính sách giao hàng</h2>
            <p>Phí vận chuyển và khả năng phục vụ phụ thuộc địa chỉ nhận hàng và được xác nhận khi thanh toán.</p>
            <p>Kiểm tra thông tin lô hàng, hạn sử dụng và tổng tiền trước khi xác nhận đơn hàng.</p>
          </section>}
        </div>
      </section>
      {similarProducts.length > 0 && <aside className="mkt-similar"><div className="mkt-similar-heading">
        <h2>Sản phẩm tương tự</h2><a href={`#/marketplace?categoryId=${product.categoryId}`}>Xem tất cả</a></div>
        {similarProducts.map(item => <a className="mkt-similar-item" href={`#/marketplace/products/${item.id}${query}`} key={item.id}>
          <ProductImage url={item.imageUrl} alt={item.name} loading="lazy" />
          <div><h3>{item.name}</h3><Price product={item} /><Rating product={item} /></div><span>›</span>
        </a>)}</aside>}
    </div>
  </main>;
}
