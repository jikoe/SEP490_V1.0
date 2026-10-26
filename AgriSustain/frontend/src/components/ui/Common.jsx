import React, { useEffect, useId, useRef, useState } from 'react';

/**
 * Formats a server-computed decimal amount for Vietnamese display. Display formatting does not
 * calculate discounts or change the order money contract.
 */
export const money = (amount) => new Intl.NumberFormat('vi-VN', {
  style: 'currency', currency: 'VND', maximumFractionDigits: 2,
}).format(amount);

/**
 * Formats an ISO timestamp in the Vietnam business timezone; absent timestamps render as a dash
 * without guessing a legacy database timezone.
 */
export function localTime(time) {
  if (!time) return '—';
  const parts = Object.fromEntries(new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit', hour12: false,
  }).formatToParts(new Date(time)).map((part) => [part.type, part.value]));
  return `${parts.day}/${parts.month}/${parts.year} ${parts.hour}:${parts.minute}`;
}

export const statusLabel = {
  Active: 'Hoạt động', Suspended: 'Tạm ngưng', Pending: 'Chờ duyệt', Rejected: 'Từ chối',
  SystemAdmin: 'Quản trị hệ thống', CustomerSupport: 'Hỗ trợ khách hàng',
  RetailCustomer: 'Người mua lẻ', Farmer: 'Nông dân / HTX',
  WholesaleBuyer: 'Người mua B2B', VerificationOfficer: 'Nhân viên xác minh',
  ContentModerator: 'Kiểm duyệt nội dung', PromotionStaff: 'Nhân viên khuyến mại',
  FinanceStaff: 'Nhân viên tài chính',
};

/**
 * Exposes user feedback as an accessible status or alert region, keeping server messages as text
 * rather than rendering untrusted HTML.
 */
export function Notice({ children, error = false }) {
  return children ? <div className={`notice ${error ? 'error' : ''}`}
    role={error ? 'alert' : 'status'}>{children}</div> : null;
}

/**
 * Computes bounded previous/next navigation from the server total and asks the parent to change
 * page; it does not filter a hidden client-side dataset.
 */
export function Pager({ total, page, pageSize = 10, onPage }) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  const first = Math.max(1, Math.min(page - 2, pages - 4));
  const numbers = Array.from({ length: Math.min(pages, 5) }, (_, index) => first + index);
  return <div className="pager"><span>{total ? (page - 1) * pageSize + 1 : 0}–{
    Math.min(page * pageSize, total)} trên {total} bản ghi</span>
    <nav className="page-buttons" aria-label="Phân trang"><button type="button"
      disabled={page <= 1} onClick={() => onPage(page - 1)} aria-label="Trang trước">‹</button>
      {first > 1 && <button type="button" onClick={() => onPage(1)}>1</button>}
      {first > 2 && <span>…</span>}{numbers.map((number) => <button type="button" key={number}
        className={number === page ? 'current' : ''} aria-label={`Trang ${number}`}
        aria-current={number === page ? 'page' : undefined} onClick={() => onPage(number)}>
        {number}</button>)}
      {numbers.at(-1) < pages - 1 && <span>…</span>}
      {numbers.at(-1) < pages && <button type="button" onClick={() => onPage(pages)}>{pages}</button>}
      <button type="button" disabled={page >= pages} onClick={() => onPage(page + 1)}
        aria-label="Trang sau">›</button></nav></div>;
}

/**
 * Uses a native modal dialog for focus trapping and restores previous focus on cleanup. Escape
 * delegates cancellation so busy operations can retain their confirmation state.
 */
export function Modal({ title, children, onClose }) {
  const ref = useRef(null);
  useEffect(() => {
    const dialog = ref.current;
    const previousFocus = document.activeElement;
    dialog.showModal();
    return () => { dialog.close(); previousFocus?.focus(); };
  }, []);
  return <dialog ref={ref} onCancel={(event) => { event.preventDefault(); onClose(); }}>
    <div className="modal-heading"><h2>{title}</h2><button aria-label="Đóng"
      onClick={onClose}>×</button></div>{children}</dialog>;
}

/**
 * Links a stable generated label ID to its single child control, preserving an explicit child ID
 * when supplied so forms and accessibility locators remain consistent.
 */
export function Field({ label, children, error, hint, className = '', ...props }) {
  const generatedId = useId();
  const id = children?.props?.id || generatedId;
  return (
    <div className={`field ${error ? 'has-error' : ''} ${className}`.trim()} {...props}>
      {label && <label htmlFor={id}>{label}</label>}
      {children && React.cloneElement(children, { id, 'aria-invalid': Boolean(error) })}
      {error && (
        <span
          className="field-error"
          role="alert"
          style={{
            color: '#dc2626',
            fontSize: '12px',
            marginTop: '5px',
            display: 'block',
            fontWeight: 500,
            lineHeight: 1.4,
          }}
        >
          {error}
        </span>
      )}
      {hint && !error && (
        <span
          className="field-hint"
          style={{
            color: '#557262',
            fontSize: '11.5px',
            marginTop: '4px',
            display: 'block',
            lineHeight: 1.4,
          }}
        >
          {hint}
        </span>
      )}
    </div>
  );
}

/** Returns readable initials for the read-only avatar without requiring a new database field. */
export function initials(name = '') {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  return parts.length ? `${parts[0][0]}${parts.length > 1 ? parts.at(-1)[0] : ''}`.toUpperCase() : '?';
}

/** Renders persisted KYC state; absent verification is never presented as verified. */
export function VerificationBadge({ value }) {
  const labels = { Approved: 'Đã xác minh', Verified: 'Đã xác minh', Pending: 'Chờ xác minh',
    Rejected: 'Từ chối', NotVerified: 'Chưa xác minh', Unverified: 'Chưa xác minh',
    Expired: 'Hết hạn', Review: 'Đang xét duyệt' };
  return <span className={`badge ${value === 'Approved' || value === 'Verified' ? 'Active'
    : value === 'Rejected' || value === 'Expired' ? 'Suspended' : 'neutral'}`}>
    {value ? labels[value] || value : 'Không áp dụng'}</span>;
}

/** Displays the real product image when supplied; missing/broken images use an honest placeholder. */
export function ProductThumbnail({ src, name }) {
  const [broken, setBroken] = useState(false);
  useEffect(() => setBroken(false), [src]);
  const allowed = typeof src === 'string' && /^(https?:\/\/|\/(?!\/))/.test(src);
  return <div className="product-thumbnail">{allowed && !broken
    ? <img src={src} alt={name} onError={() => setBroken(true)} />
    : <span aria-label={`Chưa có ảnh ${name}`}>Chưa có<br />ảnh</span>}</div>;
}
