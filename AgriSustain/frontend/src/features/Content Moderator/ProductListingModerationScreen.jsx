import React, { useEffect, useState } from 'react';
import { request, queryString } from '../../api/client';
import { Modal, Notice, ProductThumbnail } from '../../components/ui/Common';

const listRoute = '/moderation/products';
const pageSize = 10;
const currency = new Intl.NumberFormat('vi-VN', {
  style: 'currency', currency: 'VND', maximumFractionDigits: 0,
});
const date = (value) => value ? new Intl.DateTimeFormat('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'medium',
}).format(new Date(value)) : '—';
const dateTime = (value) => value ? new Intl.DateTimeFormat('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'medium', timeStyle: 'short',
}).format(new Date(value)) : '—';
const todayInVietnam = () => {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(new Date());
  const values = Object.fromEntries(parts.map(({ type, value }) => [type, value]));
  return `${values.year}-${values.month}-${values.day}`;
};
const safeUrl = (value) => typeof value === 'string'
  && /^(https?:\/\/|\/(?!\/))/.test(value) ? value : '';

function statusLabel(status) {
  if (status === 'Approved') return 'Đã duyệt';
  if (status === 'Rejected') return 'Đã từ chối';
  return 'Chờ duyệt';
}

function farmVerificationLabel(status) {
  if (status === 'Approved') return 'Đã xác minh';
  if (status === 'Rejected') return 'Bị từ chối xác minh';
  if (status === 'PendingVerification') return 'Đang chờ xác minh';
  return 'Chưa xác minh';
}

function certificatePresentation(certificate) {
  if (typeof certificate === 'string') {
    return { label: `${certificate} · Chưa xác minh`, verified: false };
  }

  const expired = certificate.expiryDate && certificate.expiryDate < todayInVietnam();
  const verified = certificate.verificationStatus === 'Approved' && !expired;
  const verificationLabel = certificate.verificationStatus === 'Approved'
    ? 'Đã xác minh'
    : certificate.verificationStatus === 'Rejected'
      ? 'Bị từ chối'
      : certificate.verificationStatus === 'Pending'
        ? 'Đang chờ xác minh'
        : 'Chưa xác minh';

  return {
    label: expired ? `${verificationLabel} · Đã hết hạn` : verificationLabel,
    verified,
  };
}

function readProductList(result) {
  if (!result || !Array.isArray(result.items) || !Number.isInteger(result.total)) {
    throw new Error('Dữ liệu danh sách sản phẩm từ API không đúng định dạng.');
  }
  return result;
}

function SectionTitle({ children }) {
  return <h3 className="moderation-section-title">{children}</h3>;
}

export default function ProductListingModerationScreen({ route = listRoute, navigate }) {
  const routeProductId = /^\/moderation\/products\/\d+$/.test(route)
    ? Number(route.split('/').at(-1)) : null;
  const [activeTab, setActiveTab] = useState('Pending');
  const [selectedId, setSelectedId] = useState(null);
  const [activeImage, setActiveImage] = useState(0);
  const [keywordDraft, setKeywordDraft] = useState('');
  const [keyword, setKeyword] = useState('');
  const [page, setPage] = useState(1);
  const [lists, setLists] = useState({
    Pending: { items: [], total: 0 },
    Reviewed: { items: [], total: 0 },
  });
  const [detail, setDetail] = useState(null);
  const [listLoading, setListLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [error, setError] = useState('');
  const [actionError, setActionError] = useState('');
  const [notification, setNotification] = useState('');
  const [reason, setReason] = useState('');
  const [isRejecting, setIsRejecting] = useState(false);
  const [confirmReject, setConfirmReject] = useState(false);
  const [busy, setBusy] = useState(false);
  const [refresh, setRefresh] = useState(0);

  const visibleProducts = lists[activeTab].items;
  const selectedProductId = routeProductId
    ?? (visibleProducts.some((product) => product.id === selectedId)
      ? selectedId : visibleProducts[0]?.id ?? null);
  const pendingCount = lists.Pending.total;
  const reviewedCount = lists.Reviewed.total;
  const activeTotal = lists[activeTab].total;
  const totalPages = Math.max(1, Math.ceil(activeTotal / pageSize));
  const activeStatus = detail?.moderationStatus || detail?.status || 'Pending';
  const farmVerified = detail?.farm?.verificationStatus === 'Approved';

  useEffect(() => {
    const controller = new AbortController();
    setListLoading(true);
    setError('');
    const load = (status) => request(`/moderation/products?${queryString({
      keyword, status, page, pageSize,
    })}`, { signal: controller.signal }).then(readProductList);

    Promise.allSettled([load('Pending'), load('Reviewed')]).then(([pending, reviewed]) => {
      if (controller.signal.aborted) return;

      setLists((current) => ({
        Pending: pending.status === 'fulfilled' ? pending.value : current.Pending,
        Reviewed: reviewed.status === 'fulfilled' ? reviewed.value : current.Reviewed,
      }));

      const failures = [pending, reviewed]
        .filter((result) => result.status === 'rejected')
        .map((result) => result.reason)
        .filter((failure) => failure.name !== 'AbortError');
      if (failures.length) {
        setError(failures.map((failure) => failure.message).join(' '));
      }
    }).finally(() => {
      if (!controller.signal.aborted) setListLoading(false);
    });
    return () => controller.abort();
  }, [keyword, page, refresh]);

  useEffect(() => {
    if (!listLoading && page > totalPages) {
      setPage(totalPages);
    }
  }, [listLoading, page, totalPages]);

  useEffect(() => {
    setActiveImage(0);
    setReason('');
    setIsRejecting(false);
    setActionError('');
    setConfirmReject(false);
    if (!selectedProductId) {
      setDetail(null);
      setDetailLoading(false);
      return undefined;
    }
    const controller = new AbortController();
    setDetail(null);
    setDetailLoading(true);
    request(`/moderation/products/${selectedProductId}`, { signal: controller.signal })
      .then(setDetail)
      .catch((failure) => {
        if (failure.name !== 'AbortError') setError(failure.message);
      })
      .finally(() => { if (!controller.signal.aborted) setDetailLoading(false); });
    return () => controller.abort();
  }, [selectedProductId, refresh]);

  function selectTab(tab) {
    setActiveTab(tab);
    setSelectedId(null);
    setPage(1);
    setReason('');
    setIsRejecting(false);
    setActionError('');
    setConfirmReject(false);
    if (routeProductId) navigate(listRoute);
  }

  function selectProduct(product) {
    setSelectedId(product.id);
    setReason('');
    setIsRejecting(false);
    setActionError('');
    setConfirmReject(false);
    setActiveImage(0);
    navigate(`${listRoute}/${product.id}`);
  }

  function search(event) {
    event.preventDefault();
    setKeyword(keywordDraft.trim());
    setSelectedId(null);
    setPage(1);
  }

  async function moderate(action) {
    setBusy(true);
    setActionError('');
    try {
      await request(`/moderation/products/${selectedProductId}/${action}`, {
        method: 'POST',
        ...(action === 'reject' ? { body: { reason: reason.trim() } } : {}),
      });
      setNotification(action === 'approve'
        ? `Đã duyệt "${detail.name}" và gửi thông báo cho nông dân.`
        : `Đã từ chối "${detail.name}" và gửi thông báo cho nông dân.`);
      setRefresh((value) => value + 1);
      setReason('');
      setIsRejecting(false);
      setConfirmReject(false);
      setActiveTab('Reviewed');
      setPage(1);
      if (routeProductId) navigate(listRoute);
    } catch (failure) {
      setActionError(failure.message);
    } finally {
      setBusy(false);
    }
  }

  function requestRejectConfirmation() {
    if (!reason.trim()) {
      setActionError('Vui lòng nhập lý do từ chối trước khi tiếp tục.');
      return;
    }
    setActionError('');
    setConfirmReject(true);
  }

  const images = detail?.images?.map((image) => safeUrl(image.imageUrl)).filter(Boolean) || [];
  const harvests = detail?.harvest || [];
  const traceability = detail?.traceability || [];
  const certificates = detail?.certificates || [];

  return <section className="moderation-screen">
    <div className="moderation-heading">
      <div><p className="eyebrow">QUẢN TRỊ NỘI DUNG</p>
        <h1>Duyệt tin đăng sản phẩm</h1>
        <p className="muted">Xem thông tin sản phẩm, nguồn gốc và chứng nhận trước khi đưa tin đăng lên sàn.</p>
      </div><div className="moderation-pending-count"><span>Đang chờ duyệt</span>
        <strong>{pendingCount}</strong></div>
    </div>

    {notification && <div className="moderation-notification" role="status">
      <span>{notification}</span><button type="button" aria-label="Đóng thông báo"
        onClick={() => setNotification('')}>×</button></div>}
    <Notice error>{error}</Notice>

    <div className="moderation-tabs" role="tablist" aria-label="Trạng thái hồ sơ">
      {[{ key: 'Pending', label: 'Chờ duyệt', count: pendingCount },
        { key: 'Reviewed', label: 'Đã xử lý', count: reviewedCount }].map((tab) =>
        <button key={tab.key} type="button" role="tab" aria-selected={activeTab === tab.key}
          className={activeTab === tab.key ? 'active' : ''}
          onClick={() => selectTab(tab.key)}>{tab.label}<span>{tab.count}</span></button>)}
    </div>

    <form className="moderation-filter" onSubmit={search}>
      <input aria-label="Tìm tin đăng" maxLength={100} value={keywordDraft}
        placeholder="Tìm tên sản phẩm, farmer hoặc nông trại"
        onChange={(event) => setKeywordDraft(event.target.value)} />
      <button type="submit">Tìm kiếm</button>
    </form>

    {listLoading && visibleProducts.length === 0
      ? <p className="empty" role="status">Đang tải danh sách hồ sơ…</p>
      : visibleProducts.length === 0 ? <>
        <div className="moderation-empty">
          <span>✓</span><h2>{activeTab === 'Pending'
            ? 'Không có tin đăng cần kiểm duyệt.' : 'Chưa có tin đăng đã xử lý'}</h2>
          <p>{activeTab === 'Pending' ? 'Các hồ sơ chờ duyệt sẽ xuất hiện tại đây.'
            : 'Các hồ sơ bạn đã duyệt hoặc từ chối sẽ xuất hiện tại đây.'}</p>
        </div>
        {!listLoading && <div className="moderation-queue-footer">
          <span>{activeTotal} hồ sơ</span>
          <div><button type="button" aria-label="Trang trước" disabled={page <= 1}
            onClick={() => setPage((value) => value - 1)}>‹</button>
            <span>{page}</span><button type="button" aria-label="Trang sau"
              disabled={page >= totalPages}
              onClick={() => setPage((value) => value + 1)}>›</button></div>
        </div>}
      </> : <div className="moderation-workspace">
        <aside className="moderation-queue" aria-label="Danh sách tin đăng">
          {visibleProducts.map((product) => <button key={product.id}
            type="button" onClick={() => selectProduct(product)}
            aria-current={product.id === selectedProductId ? 'true' : undefined}
            className={`moderation-queue-card ${product.id === selectedProductId ? 'selected' : ''}`}>
            <div className="moderation-queue-image">
              <ProductThumbnail src={product.imageUrl} name={product.name} />
            </div>
            <div className="moderation-queue-copy">
              <span className={`moderation-status ${product.status}`}>
                {statusLabel(product.status)}
              </span>
              <strong>{product.name}</strong>
              <span>{product.farmerName}</span>
              <small>{activeTab === 'Reviewed' && product.reviewedAt
                ? `Xử lý ${dateTime(product.reviewedAt)}`
                : `Gửi duyệt ${date(product.submittedAt)}`}</small>
              {activeTab === 'Reviewed' && <small>
                {product.reviewedBy ? `Người xử lý: ${product.reviewedBy}`
                  : 'Chưa có lịch sử người xử lý'}
              </small>}
            </div>
          </button>)}
          {!listLoading && <div className="moderation-queue-footer">
            <span>{activeTab === 'Pending' ? pendingCount : reviewedCount} hồ sơ</span>
            <div><button type="button" aria-label="Trang trước" disabled={page <= 1}
              onClick={() => setPage((value) => value - 1)}>‹</button>
              <span>{page}</span><button type="button" aria-label="Trang sau"
                disabled={page >= totalPages}
                onClick={() => setPage((value) => value + 1)}>›</button></div>
          </div>}
        </aside>

        <div className="moderation-detail-column">
          {detailLoading && <p className="empty" role="status">Đang tải chi tiết hồ sơ…</p>}
          {detail && <article className="moderation-detail">
            <div className="moderation-detail-heading">
              <div><p>{`MÃ SẢN PHẨM #${detail.id}`}</p>
                <h2>{detail.name}</h2><span>Gửi duyệt {date(detail.submittedAt)}
                  {' · '}{detail.category}</span></div>
              <span className={`moderation-status ${activeStatus}`}>
                {statusLabel(activeStatus)}
              </span>
            </div>
            {(activeStatus === 'Approved' || activeStatus === 'Rejected') && <p className="moderation-review-meta">
              {detail.reviewedAt
                ? `${activeStatus === 'Rejected' ? 'Từ chối' : 'Duyệt'} bởi ${detail.reviewedBy || 'không rõ'} · ${dateTime(detail.reviewedAt)}`
                : 'Chưa có lịch sử người xử lý và thời điểm quyết định.'}
            </p>}
            <div className="moderation-detail-body">
              <section><SectionTitle>Hình ảnh sản phẩm</SectionTitle>
                <div className="moderation-gallery">
                  <div className="moderation-gallery-main">{images.length
                    ? <img src={images[activeImage] || images[0]} alt={`${detail.name} - ảnh ${activeImage + 1}`} />
                    : <span className="moderation-gallery-placeholder">Chưa có ảnh sản phẩm</span>}
                  </div>
                  {images.length > 1 && <div className="moderation-gallery-thumbs">
                    {images.map((image, index) => <button type="button" key={image}
                      aria-label={`Xem ảnh ${index + 1}`}
                      className={activeImage === index ? 'selected' : ''}
                      onClick={() => setActiveImage(index)}><img src={image} alt="" /></button>)}
                  </div>}
                </div>
              </section>

              <section><SectionTitle>Thông tin sản phẩm</SectionTitle>
                <div className="moderation-facts">
                  <div><span>Danh mục</span><strong>{detail.category}</strong></div>
                  <div><span>Giá bán</span><strong className="price">
                    {currency.format(detail.retailPrice)} / {detail.unit}</strong></div>
                  {detail.wholesalePrice != null && <div><span>Giá bán sỉ</span><strong className="price">
                    {currency.format(detail.wholesalePrice)} / {detail.unit}
                    {detail.wholesaleMinQuantity != null
                      && ` · Tối thiểu ${Number(detail.wholesaleMinQuantity).toLocaleString('vi-VN')} ${detail.unit}`}</strong></div>}
                  <div><span>Sản lượng</span><strong>{Number(detail.quantity || 0).toLocaleString('vi-VN')}
                    {' '}{detail.unit}</strong></div>
                </div>
                {detail.description && <p className="moderation-description">{detail.description}</p>}
              </section>

              <section><SectionTitle>Thông tin farmer và nông trại</SectionTitle>
                <div className="moderation-person-grid">
                  <div><span>FARMER</span><strong>{detail.farmer?.fullName}</strong>
                    <p>{detail.farmer?.email}</p><p>{detail.farmer?.phoneNumber}</p></div>
                  <div><span>NÔNG TRẠI</span><strong>{detail.farm?.farmName}</strong>
                    <p>{detail.farm?.address || detail.farm?.provinceCode}</p>
                    <p>Diện tích: {detail.farm?.productionScale || '—'}</p>
                    <p>Trạng thái xác minh: <strong>{farmVerificationLabel(
                      detail.farm?.verificationStatus,
                    )}</strong></p></div>
                </div>
              </section>

              <section><SectionTitle>Thông tin thu hoạch</SectionTitle>
                {harvests.length ? harvests.map((batch) => <div className="moderation-facts" key={batch.batchCode}>
                  <div><span>Mã lô</span><strong>{batch.batchCode}</strong></div>
                  <div><span>Ngày gieo trồng</span><strong>{date(batch.plantingDate)}</strong></div>
                  <div><span>Ngày thu hoạch dự kiến</span><strong>
                    {date(batch.actualHarvestDate || batch.estimatedHarvestDate)}</strong></div>
                  <div><span>Phương thức / mùa vụ</span><strong>
                    {batch.season || batch.cropVariety || '—'}</strong></div>
                  <div><span>Trạng thái lô</span><strong>{batch.status || '—'}</strong></div>
                  <div><span>Ghi chú</span><strong>{batch.qualityNotes || '—'}</strong></div>
                  {batch.growingPractices && <div><span>Quy trình canh tác</span>
                    <strong>{batch.growingPractices}</strong></div>}
                </div>) : <p className="moderation-muted">Chưa cung cấp thông tin thu hoạch.</p>}
              </section>

              <section><SectionTitle>Truy xuất nguồn gốc & chứng nhận</SectionTitle>
                <div className="moderation-traceability">
                  {traceability.map((batch) => <div className="moderation-trace-item" key={batch.batchCode}>
                    <span>Mã lô</span><strong>{batch.batchCode}</strong>
                    <span>Trạng thái mã truy xuất</span>
                    <strong>{batch.isRevoked ? 'Đã thu hồi' : 'Còn hiệu lực'}</strong>
                    {safeUrl(batch.targetUrl) && <a href={safeUrl(batch.targetUrl)}
                      target="_blank" rel="noreferrer">Xem thông tin truy xuất</a>}
                  </div>)}
                  <div><span>Chứng nhận</span>
                    {certificates.length ? <div className="moderation-certificates">
                      {certificates.map((certificate) => {
                        const name = typeof certificate === 'string'
                          ? certificate : certificate.certificateType;
                        const presentation = certificatePresentation(certificate);
                        return <span key={name} className={presentation.verified ? 'verified' : 'unverified'}>
                          <span aria-hidden="true">{presentation.verified ? '✓' : '!'}</span>
                          {name} · {presentation.label}
                        </span>;
                      })}
                    </div> : <p className="moderation-muted">Chưa cung cấp chứng nhận.</p>}
                  </div>
                </div>
              </section>

              {activeStatus === 'Rejected' && detail.rejectionReason && <div className="moderation-rejection">
                <strong>Lý do từ chối</strong><p>{detail.rejectionReason}</p>
              </div>}
              {activeStatus === 'Pending' && <section className="moderation-decision">
                <SectionTitle>Quyết định kiểm duyệt</SectionTitle>
                {!farmVerified && <Notice error>
                  Chưa thể duyệt sản phẩm vì nông trại chưa được xác minh.
                </Notice>}
                {!isRejecting ? <>
                  <Notice error>{actionError}</Notice>
                  <div className="moderation-decision-actions">
                    <button type="button" className="reject" disabled={busy}
                      onClick={() => { setActionError(''); setIsRejecting(true); }}>
                      Từ chối sản phẩm</button>
                    <button type="button" className="approve" disabled={busy || !farmVerified}
                      onClick={() => moderate('approve')}>{busy ? 'Đang xử lý…' : 'Duyệt sản phẩm'}</button>
                  </div>
                </> : <>
                  <label htmlFor="moderation-reason">Lý do từ chối <span>(bắt buộc)</span></label>
                  <textarea id="moderation-reason" rows="3" maxLength={500} value={reason}
                    placeholder="Nêu rõ thông tin cần farmer bổ sung hoặc chỉnh sửa..."
                    onChange={(event) => { setReason(event.target.value); setActionError(''); }} />
                  <Notice error>{actionError}</Notice>
                  <div className="moderation-decision-actions">
                    <button type="button" disabled={busy} onClick={() => {
                      setIsRejecting(false);
                      setReason('');
                      setActionError('');
                    }}>Hủy</button>
                    <button type="button" className="reject" disabled={busy}
                      onClick={requestRejectConfirmation}>Xem lại và xác nhận</button>
                  </div>
                  <p className="moderation-character-count">{reason.length}/500 ký tự · Farmer sẽ được thông báo sau khi có quyết định.</p>
                </>}
              </section>}
            </div>
          </article>}
        </div>
      </div>}
      {confirmReject && <Modal title="Xác nhận từ chối sản phẩm"
        onClose={() => { if (!busy) setConfirmReject(false); }}>
        <div className="moderation-confirmation">
          <p>Bạn có chắc chắn muốn từ chối <strong>{detail?.name}</strong> không?</p>
          <p className="moderation-confirmation-reason"><strong>Lý do từ chối:</strong> {reason.trim()}</p>
          <p>Hệ thống sẽ lưu lý do và gửi thông báo cho Farmer sau khi xác nhận.</p>
          <div className="moderation-decision-actions">
            <button type="button" disabled={busy}
              onClick={() => setConfirmReject(false)}>Hủy</button>
            <button type="button" className="reject" disabled={busy}
              onClick={() => moderate('reject')}>{busy ? 'Đang xử lý…' : 'Xác nhận từ chối'}</button>
          </div>
        </div>
      </Modal>}
  </section>;
}
