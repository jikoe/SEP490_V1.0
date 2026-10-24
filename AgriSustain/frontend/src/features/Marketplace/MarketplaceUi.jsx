import React, { useState } from 'react';
import { money } from '../../components/ui/Common';

export function Icon({ name = 'leaf', ...props }) {
  const paths = {
    leaf: 'M20 3C9 2 3 7 5 15c7 3 14-2 15-12ZM3 21 16 8M9 15l-1-5m1 5 5 1',
    search: 'm21 21-5-5M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0',
    cart: 'M2 3h3l3 12h11l3-9H6M9 21h.01M19 21h.01',
    user: 'M20 21a8 8 0 0 0-16 0M17 7a5 5 0 1 1-10 0 5 5 0 0 1 10 0',
    grid: 'M3 3h6v6H3ZM15 3h6v6h-6ZM3 15h6v6H3ZM15 15h6v6h-6Z',
    pin: 'M20 10c0 6-8 12-8 12S4 16 4 10a8 8 0 1 1 16 0ZM12 7v5',
    arrow: 'M4 12h16m-6-6 6 6-6 6',
    check: 'm5 12 4 4L20 5',
    shield: 'm12 2 9 4v6c0 6-9 10-9 10S3 18 3 12V6Zm-5 9 4 4 6-7',
    truck: 'M1 4h13v13H1ZM14 9h5l4 5v3h-9M8 19a2 2 0 1 1-4 0 2 2 0 0 1 4 0M21 19a2 2 0 1 1-4 0 2 2 0 0 1 4 0',
    card: 'M2 5h20v14H2ZM2 9h20M5 15h5',
    share: 'm7 11 10-6M7 13l10 6M7 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0M23 4a3 3 0 1 1-6 0 3 3 0 0 1 6 0M23 20a3 3 0 1 1-6 0 3 3 0 0 1 6 0',
    filter: 'M3 4h18M6 10h12M9 16h6M11 21h2',
  };
  return <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"
    strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" {...props}>
    <path d={paths[name] || paths.leaf} />
  </svg>;
}

export function safeUrl(value) {
  if (!value) return null;
  try {
    const url = new URL(value, window.location.origin);
    return ['http:', 'https:'].includes(url.protocol) ? url.href : null;
  } catch { return null; }
}

export function ProductImage({ url, alt, ...props }) {
  const [failed, setFailed] = useState(null);
  const source = safeUrl(url);
  return source && failed !== source
    ? <img src={source} alt={alt} onError={() => setFailed(source)} {...props} />
    : <div className="mkt-image-fallback" role="img" aria-label={alt}>
      <Icon /><span>Chưa có ảnh sản phẩm</span>
    </div>;
}

export function Price({ product }) {
  return <span className="mkt-price">{money(product.price)} <small>/ {product.unit}</small></span>;
}

export function Rating({ product }) {
  return <span className="mkt-rating"><span aria-hidden="true">★</span>{' '}
    {product.rating == null ? 'Chưa có đánh giá'
      : <>{product.rating.toFixed(1)} <small>({product.reviewCount} đánh giá)</small></>}
  </span>;
}

export function ProductCard({ product, query }) {
  return <article className="mkt-product-card">
    <a className="mkt-card-photo" href={`#/marketplace/products/${product.id}${query}`}>
      <ProductImage url={product.imageUrl} alt={product.name} loading="lazy" />
      {product.certifications[0] && <span className="mkt-cert-badge">
        <Icon name="shield" />{product.certifications[0]}</span>}
    </a>
    <div className="mkt-card-content">
      <h3><a href={`#/marketplace/products/${product.id}${query}`}>{product.name}</a></h3>
      <p className="mkt-supplier">{product.supplier}</p>
      <Rating product={product} />
      {product.region && <p className="mkt-region"><Icon name="pin" />{product.region}</p>}
      <Price product={product} />
      {/* Cart lines are per harvest batch, which is chosen on the detail page. */}
      {product.availableQuantity < 1
        ? <button type="button" className="mkt-primary" disabled><Icon name="cart" />Hết hàng</button>
        : <a className="mkt-primary" href={`#/marketplace/products/${product.id}${query}`}>
          <Icon name="cart" />Chọn lô & mua</a>}
    </div>
  </article>;
}
