import React, { useEffect, useState } from 'react';
import { request, queryString } from '../../api/client';
import { Field, Modal, Notice, Pager, VerificationBadge, initials, localTime,
  statusLabel } from '../../components/ui/Common';

const labels = { suspend: 'Tạm ngưng tài khoản', activate: 'Kích hoạt tài khoản', unlock: 'Mở khóa tài khoản' };
const roles = ['RetailCustomer', 'Farmer', 'WholesaleBuyer', 'SystemAdmin', 'VerificationOfficer',
  'ContentModerator', 'CustomerSupport', 'PromotionStaff', 'FinanceStaff'];
const listRoute = '/administration/users';

/** Implements the separate User List and User Details screens described in SRS III.4.1. */
export default function UsersScreen({ fixture, route = listRoute, navigate }) {
  const [draft, setDraft] = useState({ keyword: '', role: '', status: '' });
  const [filters, setFilters] = useState({ ...draft, page: 1 });
  const [result, setResult] = useState({ items: [], total: 0 });
  const [loading, setLoading] = useState(true);
  const [detail, setDetail] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [refresh, setRefresh] = useState(0);
  const [pending, setPending] = useState(null);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const userId = /^\/administration\/users\/\d+$/.test(route) ? Number(route.split('/').at(-1)) : null;
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError('');
    request(`/users?${queryString(filters)}`, { fixture, signal: controller.signal })
      .then(setResult).catch((failure) => {
        if (failure.name !== 'AbortError') setError(failure.message);
      }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [fixture, filters, refresh]);
  useEffect(() => {
    if (!userId) { setDetail(null); return; }
    const controller = new AbortController();
    setDetail(null); setDetailLoading(true); setError('');
    request(`/users/${userId}`, { fixture, signal: controller.signal }).then(setDetail)
      .catch((failure) => { if (failure.name !== 'AbortError') setError(failure.message); })
      .finally(() => { if (!controller.signal.aborted) setDetailLoading(false); });
    return () => controller.abort();
  }, [fixture, userId, refresh]);
  /** Applies the explicit Search action and preserves filters when returning from account details. */
  function search(event) {
    event.preventDefault(); setFilters({ ...draft, page: 1 }); setMessage('');
  }
  /** Opens the existing UC action confirmation without writing or fabricating audit information. */
  function prepare(user, action) { setReason(''); setPending({ user, action }); setError(''); }
  /** Submits a confirmed status/unlock action and refreshes the list and dedicated detail read model. */
  async function change(event) {
    event.preventDefault(); setBusy(true); setError('');
    try {
      await request(`/users/${pending.user.id}/status-actions`, { fixture, method: 'POST',
        body: { action: pending.action, reason, confirmed: true } });
      setMessage(`${labels[pending.action]} thành công.`);
      setPending(null); setRefresh((value) => value + 1);
    } catch (failure) { setError(failure.message); }
    finally { setBusy(false); }
  }
  /** Renders only state-eligible actions; verified actor identity hides self-action controls. */
  function buttons(user) {
    const actorId = detail?.currentActorId ?? result.currentActorId;
    return <div className="actions">{user.id !== actorId && <>
      {user.status === 'Active' && <button className="danger-link" type="button"
        onClick={() => prepare(user, 'suspend')}>Tạm ngưng</button>}
      {user.status === 'Suspended' && <button type="button"
        onClick={() => prepare(user, 'activate')}>Kích hoạt</button>}
      {user.isLocked && <button type="button" onClick={() => prepare(user, 'unlock')}>Mở khóa</button>}
    </>}</div>;
  }
  const confirmation = pending && <Modal title={labels[pending.action]}
    onClose={() => { if (!busy) setPending(null); }}><form onSubmit={change}>
    <p>Xác nhận {labels[pending.action].toLowerCase()} <strong>{pending.user.organizationName
      || pending.user.name}</strong>?</p>
    {pending.action === 'suspend' && <p className="muted">Tài khoản sẽ mất quyền truy cập cho đến khi được kích hoạt lại.</p>}
    <Field label={pending.action === 'unlock' ? 'Ghi chú (không bắt buộc)' : 'Lý do *'}>
      <textarea autoFocus required={pending.action !== 'unlock'} maxLength={500} value={reason}
        onChange={(event) => setReason(event.target.value)} /></Field>
    <Notice error>{error}</Notice><div className="modal-actions"><button type="button"
      disabled={busy} onClick={() => setPending(null)}>Hủy</button><button
      className={pending.action === 'suspend' ? 'danger' : 'primary'} disabled={busy}>
      {busy ? 'Đang xử lý…' : `Xác nhận ${pending.action === 'suspend' ? 'tạm ngưng'
        : pending.action === 'unlock' ? 'mở khóa' : 'kích hoạt'}`}</button></div>
  </form></Modal>;
  if (userId) return <section><div className="breadcrumbs"><button className="text-button"
    onClick={() => navigate(listRoute)}>Quản lý người dùng</button><span>/</span><span>Chi tiết người dùng</span></div>
    <div className="section-heading"><div><h1>Chi tiết người dùng</h1>
      <p className="muted">Thông tin tài khoản và quyền truy cập nền tảng.</p></div></div>
    <Notice>{message}</Notice><Notice error>{!pending && error}</Notice>
    {detailLoading && <p className="empty" role="status">Đang tải thông tin người dùng…</p>}
    {detail && <><article className="panel user-detail-card"><div className="identity-heading">
      <div className="identity-avatar">{initials(detail.organizationName || detail.name)}</div><div>
        <h2>{detail.organizationName || detail.name}</h2><p className="muted">Mã tài khoản: #{detail.id}</p>
        {detail.organizationName && <p className="muted">Người đại diện: {detail.name}</p>}
        <span className="badge role-badge">{statusLabel[detail.role] || detail.role}</span>
        <p className="muted">Ngày tham gia: {localTime(detail.createdAt).slice(0, 10)}</p>
      </div></div><div className="identity-information"><div><span>Email</span><p>{detail.email}</p>
        <span>Trạng thái tài khoản</span><p><span className={`badge ${detail.status}`}>
          {statusLabel[detail.status] || detail.status}</span></p>
      </div><div><span>Số điện thoại</span><p>{detail.phone || 'Chưa có số điện thoại'}</p>
        <span>Trạng thái xác minh / KYC</span><p><VerificationBadge value={detail.verificationStatus} /></p>
      </div></div>{detail.isLocked && <div className="security-lock"><strong>Đang khóa tạm</strong>
        <span>Khóa đến {localTime(detail.lockedUntil)} · {detail.failedLoginCount} lần đăng nhập lỗi</span>
      </div>}</article><div className="detail-footer"><button onClick={() => navigate(listRoute)}>
        Quay lại danh sách</button>{buttons(detail)}</div></>}
    {!detailLoading && !detail && <button onClick={() => navigate(listRoute)}>Quay lại danh sách</button>}
    {confirmation}</section>;
  return <section><div className="section-heading"><div><h1>Quản lý người dùng</h1>
    <p className="muted">Tìm kiếm tài khoản, xem xác minh và quản lý quyền truy cập nền tảng.</p>
  </div></div><Notice>{message}</Notice><Notice error>{!pending && error}</Notice>
    <form className="panel search-panel" onSubmit={search}>
      <Field label="Từ khóa tìm kiếm"><input maxLength={100} value={draft.keyword}
        placeholder="Mã, tên, nông trại, công ty, email hoặc điện thoại"
        onChange={(event) => setDraft((old) => ({ ...old, keyword: event.target.value }))} /></Field>
      <Field label="Vai trò"><select value={draft.role}
        onChange={(event) => setDraft((old) => ({ ...old, role: event.target.value }))}>
        <option value="">Tất cả vai trò</option>{roles.map((role) => <option value={role} key={role}>
          {statusLabel[role] || role}</option>)}</select></Field>
      <Field label="Trạng thái tài khoản"><select value={draft.status}
        onChange={(event) => setDraft((old) => ({ ...old, status: event.target.value }))}>
        <option value="">Tất cả trạng thái</option>{['Active', 'Pending', 'Rejected', 'Suspended'].map((status) =>
          <option value={status} key={status}>{statusLabel[status]}</option>)}</select></Field>
      <button className="primary" type="submit">Tìm kiếm</button></form>
    <div className="panel record-panel"><div className="record-heading"><h2>Danh sách người dùng</h2>
      <span className="count">{result.total} tài khoản</span></div><div className="table-wrap">
      <table className="user-table"><thead><tr><th>Người dùng / Đơn vị</th><th>Email</th>
        <th>Số điện thoại</th><th>Vai trò</th><th>Tài khoản</th><th>KYC</th><th>Ngày tạo</th><th>Thao tác</th>
      </tr></thead><tbody>{!loading && result.items.map((user) => <tr key={user.id}>
        <td><button className="text-button name-link" onClick={() => navigate(`${listRoute}/${user.id}`)}>
          {user.organizationName || user.name}</button><small>#{user.id}</small></td>
        <td>{user.email}</td><td>{user.phone || '—'}</td><td><span className="badge role-badge">
          {statusLabel[user.role] || user.role}</span></td><td><span className={`badge ${user.status}`}>
          {statusLabel[user.status] || user.status}</span>{user.isLocked && <small>Khóa tạm</small>}</td>
        <td><VerificationBadge value={user.verificationStatus} /></td><td>{localTime(user.createdAt)}</td>
        <td><div className="row-actions"><button className="text-button" type="button"
          onClick={() => navigate(`${listRoute}/${user.id}`)}>Xem chi tiết</button>{buttons(user)}</div></td>
      </tr>)}</tbody></table>{loading && <p className="empty" role="status">Đang tải tài khoản…</p>}
      {!loading && !result.items.length && !error && <p className="empty">Không tìm thấy kết quả phù hợp.
        Hãy điều chỉnh từ khóa hoặc bộ lọc.</p>}</div><Pager total={result.total} page={filters.page}
        onPage={(page) => setFilters((old) => ({ ...old, page }))} /></div>{confirmation}
  </section>;
}
