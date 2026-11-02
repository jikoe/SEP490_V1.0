import React, { useEffect, useState } from 'react';
import { request, queryString } from '../../api/client';
import { Notice, ProductThumbnail } from '../../components/ui/Common';

const listRoute = '/moderation/products';
const pageSize = 10;
const currency = new Intl.NumberFormat('vi-VN', {
  style: 'currency', currency: 'VND', maximumFractionDigits: 0,
});
const date = (value) => value ? new Intl.DateTimeFormat('vi-VN', {
  timeZone: 'Asia/Ho_Chi_Minh', dateStyle: 'medium',
}).format(new Date(value)) : '—';
const safeUrl = (value) => typeof value === 'string'
  && /^(https?:\/\/|\/(?!\/))/.test(value) ? value : '';

const demoProducts = [
  {
    id: 90001, name: 'Hữu cơ Đà Lạt', category: 'Rau củ hữu cơ', emoji: '🥬',
    submittedAt: '2026-10-20T01:42:00Z', retailPrice: 45000, unit: 'kg', quantity: 120,
    description: 'Rau củ tươi được canh tác theo hướng hữu cơ, thu hoạch và đóng gói trong ngày.',
    farmer: { fullName: 'Nguyễn Thị Mai', email: 'mai.nguyen@example.test', phoneNumber: '+84 912 345 678' },
    farm: { farmName: 'Nông trại Xanh Đà Lạt', address: 'Xuân Thọ, Đà Lạt, Lâm Đồng',
      productionScale: '2,5 ha', verificationStatus: 'Approved' },
    harvest: [{ batchCode: 'DL-VEG-261018-A', estimatedHarvestDate: '2026-10-18',
      season: 'Vụ thu đông 2026', cropVariety: 'Rau củ hữu cơ', harvestYield: 120,
      qualityNotes: 'Thu hoạch vào sáng sớm, đóng gói trong ngày.' }],
    traceability: [{ batchCode: 'DL-VEG-261018-A', plantingDate: '20/08/2026',
      growingPractices: 'Canh tác theo hướng hữu cơ, không sử dụng thuốc trừ sâu tổng hợp.',
      certificates: ['VietGAP', 'Chứng nhận cơ sở hữu cơ'] }],
    certificates: ['VietGAP', 'Chứng nhận cơ sở hữu cơ'],
  },
  {
    id: 90002, name: 'Cam sành Vĩnh Long', category: 'Trái cây', emoji: '🍊',
    submittedAt: '2026-10-20T00:15:00Z', retailPrice: 38000, unit: 'kg', quantity: 300,
    description: 'Cam sành tuyển chọn từ vườn Tân Phú, phân loại theo kích cỡ trước khi đóng thùng.',
    farmer: { fullName: 'Trần Văn Hùng', email: 'hung.tran@example.test', phoneNumber: '+84 938 111 222' },
    farm: { farmName: 'Vườn cam Tân Phú', address: 'Tân Phú, Tam Bình, Vĩnh Long',
      productionScale: '1,8 ha', verificationStatus: 'Approved' },
    harvest: [{ batchCode: 'VL-ORG-261022-B', estimatedHarvestDate: '2026-10-22',
      season: 'Vụ cam 2026', cropVariety: 'Cam sành', harvestYield: 300,
      qualityNotes: 'Phân loại theo kích cỡ trước khi đóng thùng.' }],
    traceability: [{ batchCode: 'VL-ORG-261022-B', plantingDate: '15/03/2023',
      growingPractices: 'Sử dụng phân bón hữu cơ và quy trình quản lý dịch hại tổng hợp.',
      certificates: ['VietGAP'] }],
    certificates: ['VietGAP'],
  },
  {
    id: 90003, name: 'Gạo ST25 mùa mới', category: 'Ngũ cốc', emoji: '🌾',
    submittedAt: '2026-10-19T09:30:00Z', retailPrice: 32000, unit: 'kg', quantity: 800,
    description: 'Gạo thơm ST25 từ vùng trồng lúa Sóc Trăng, ghi nhật ký canh tác theo mùa vụ.',
    farmer: { fullName: 'Lê Minh Phúc', email: 'phuc.le@example.test', phoneNumber: '+84 979 555 010' },
    farm: { farmName: 'Hợp tác xã Lúa thơm Sóc Trăng', address: 'Mỹ Xuyên, Sóc Trăng',
      productionScale: '12 ha', verificationStatus: 'Approved' },
    harvest: [{ batchCode: 'ST25-SOC-261015-C', estimatedHarvestDate: '2026-10-15',
      season: 'Vụ mùa 2026', cropVariety: 'ST25', harvestYield: 800,
      qualityNotes: 'Lúa được sấy trong vòng 12 giờ sau thu hoạch.' }],
    traceability: [{ batchCode: 'ST25-SOC-261015-C', plantingDate: '10/06/2026',
      growingPractices: 'Sản xuất theo quy trình giảm phát thải, ghi nhật ký canh tác.',
      certificates: [] }],
    certificates: [],
  },
];

function statusLabel(status) {
  if (status === 'Approved') return 'Đã duyệt';
  if (status === 'Rejected') return 'Đã từ chối';
  return 'Chờ duyệt';
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
  const [busy, setBusy] = useState(false);
  const [refresh, setRefresh] = useState(0);
  const [demoDecisions, setDemoDecisions] = useState({});

  const query = keyword.toLocaleLowerCase('vi');
  const filteredDemoProducts = demoProducts.filter((product) =>
    !query || [product.name, product.category, product.farmer.fullName, product.farm.farmName]
      .some((value) => value.toLocaleLowerCase('vi').includes(query)));
  const visibleDemoProducts = filteredDemoProducts.filter((product) => {
    const status = demoDecisions[product.id]?.status || 'Pending';
    return activeTab === 'Pending' ? status === 'Pending' : status !== 'Pending';
  }).map((product) => ({
    ...product,
    status: demoDecisions[product.id]?.status || 'Pending',
    demo: true,
  }));
  const visibleProducts = [...lists[activeTab].items, ...visibleDemoProducts];
  const selectedProductId = routeProductId
    ?? (visibleProducts.some((product) => product.id === selectedId)
      ? selectedId : visibleProducts[0]?.id ?? null);
  const selectedIsDemo = demoProducts.some((product) => product.id === selectedProductId);
  const pendingCount = lists.Pending.total + filteredDemoProducts.filter((product) =>
    !demoDecisions[product.id] || demoDecisions[product.id].status === 'Pending').length;
  const reviewedCount = lists.Reviewed.total + filteredDemoProducts.filter((product) =>
    ['Approved', 'Rejected'].includes(demoDecisions[product.id]?.status)).length;
  const activeStatus = detail?.moderationStatus || detail?.status || 'Pending';

  useEffect(() => {
    const controller = new AbortController();
    setListLoading(true);
    setError('');
    const load = (status) => request(`/moderation/products?${queryString({
      keyword, status, page, pageSize,
    })}`, { signal: controller.signal });
    Promise.all([load('Pending'), load('Reviewed')])
      .then(([pending, reviewed]) => setLists({ Pending: pending, Reviewed: reviewed }))
      .catch((failure) => {
        if (failure.name !== 'AbortError') setError(failure.message);
      })
      .finally(() => { if (!controller.signal.aborted) setListLoading(false); });
    return () => controller.abort();
  }, [keyword, page, refresh]);

  useEffect(() => {
    setActiveImage(0);
    setReason('');
    setActionError('');
    if (!selectedProductId) {
      setDetail(null);
      setDetailLoading(false);
      return undefined;
    }
    const demoProduct = demoProducts.find((product) => product.id === selectedProductId);
    if (demoProduct) {
      const decision = demoDecisions[selectedProductId];
      setDetail({
        ...demoProduct,
        images: [],
        demo: true,
        moderationStatus: decision?.status || 'Pending',
        rejectionReason: decision?.reason || '',
      });
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
  }, [selectedProductId, refresh, demoDecisions]);

  function selectTab(tab) {
    setActiveTab(tab);
    setSelectedId(null);
    setPage(1);
    setReason('');
    setActionError('');
    if (routeProductId) navigate(listRoute);
  }

  function selectProduct(product) {
    setSelectedId(product.id);
    setReason('');
    setActionError('');
    setActiveImage(0);
    if (!product.demo) navigate(`${listRoute}/${product.id}`);
    else if (routeProductId) navigate(`${listRoute}/${product.id}`);
  }

  function search(event) {
    event.preventDefault();
    setKeyword(keywordDraft.trim());
    setSelectedId(null);
    setPage(1);
  }

  async function moderate(action) {
    if (action === 'reject' && !reason.trim()) {
      setActionError('Vui lòng nhập lý do từ chối trước khi tiếp tục.');
      return;
    }
    setBusy(true);
    setActionError('');
    try {
      if (selectedIsDemo) {
        setDemoDecisions((current) => ({
          ...current,
          [selectedProductId]: {
            status: action === 'approve' ? 'Approved' : 'Rejected',
            reason: action === 'reject' ? reason.trim() : '',
          },
        }));
        setNotification(action === 'approve'
          ? `Đã duyệt "${detail.name}". (Demo) Thông báo kết quả được mô phỏng cho nông dân ${detail.farmer.fullName}.`
          : `Đã từ chối "${detail.name}". (Demo) Thông báo kết quả được mô phỏng cho nông dân ${detail.farmer.fullName}.`);
      } else {
        await request(`/moderation/products/${selectedProductId}/${action}`, {
          method: 'POST',
          ...(action === 'reject' ? { body: { reason: reason.trim() } } : {}),
        });
        setNotification(action === 'approve'
          ? `Đã duyệt "${detail.name}" và gửi thông báo cho nông dân.`
          : `Đã từ chối "${detail.name}" và gửi thông báo cho nông dân.`);
        setRefresh((value) => value + 1);
      }
      setReason('');
      setActiveTab('Reviewed');
      if (routeProductId) navigate(listRoute);
    } catch (failure) {
      setActionError(failure.message);
    } finally {
      setBusy(false);
    }
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
      : visibleProducts.length === 0 ? <div className="moderation-empty">
        <span>✓</span><h2>{activeTab === 'Pending'
          ? 'Không có tin đăng cần xử lý' : 'Chưa có tin đăng đã xử lý'}</h2>
        <p>{activeTab === 'Pending' ? 'Các hồ sơ chờ duyệt sẽ xuất hiện tại đây.'
          : 'Các hồ sơ bạn đã duyệt hoặc từ chối sẽ xuất hiện tại đây.'}</p>
      </div> : <div className="moderation-workspace">
        <aside className="moderation-queue" aria-label="Danh sách tin đăng">
          {visibleProducts.map((product) => <button key={`${product.demo ? 'demo-' : ''}${product.id}`}
            type="button" onClick={() => selectProduct(product)}
            aria-current={product.id === selectedProductId ? 'true' : undefined}
            className={`moderation-queue-card ${product.id === selectedProductId ? 'selected' : ''}`}>
            <div className="moderation-queue-image">
              {product.demo ? <span>{product.emoji}</span>
                : <ProductThumbnail src={product.imageUrl} name={product.name} />}
            </div>
            <div className="moderation-queue-copy">
              <span className={`moderation-status ${product.status}`}>
                {statusLabel(product.status)}{product.demo ? ' · Demo' : ''}
              </span>
              <strong>{product.name}</strong>
              <span>{product.demo ? product.farmer.fullName : product.farmerName}</span>
              <small>{date(product.submittedAt)}</small>
            </div>
          </button>)}
          {!listLoading && <div className="moderation-queue-footer">
            <span>{activeTab === 'Pending' ? pendingCount : reviewedCount} hồ sơ</span>
            <div><button type="button" aria-label="Trang trước" disabled={page <= 1}
              onClick={() => setPage((value) => value - 1)}>‹</button>
              <span>{page}</span><button type="button" aria-label="Trang sau"
                disabled={lists[activeTab].items.length < pageSize}
                onClick={() => setPage((value) => value + 1)}>›</button></div>
          </div>}
        </aside>

        <div className="moderation-detail-column">
          {detailLoading && <p className="empty" role="status">Đang tải chi tiết hồ sơ…</p>}
          {detail && <article className="moderation-detail">
            <div className="moderation-detail-heading">
              <div><p>{detail.demo ? `PRD-DEMO-${detail.id}` : `MÃ SẢN PHẨM #${detail.id}`}</p>
                <h2>{detail.name}</h2><span>Gửi duyệt {date(detail.submittedAt)}
                  {' · '}{detail.category}</span></div>
              <span className={`moderation-status ${activeStatus}`}>
                {statusLabel(activeStatus)}{detail.demo ? ' · Demo' : ''}
              </span>
            </div>
            <div className="moderation-detail-body">
              <section><SectionTitle>Hình ảnh sản phẩm</SectionTitle>
                <div className="moderation-gallery">
                  <div className="moderation-gallery-main">{images.length
                    ? <img src={images[activeImage] || images[0]} alt={`${detail.name} - ảnh ${activeImage + 1}`} />
                    : detail.demo
                      ? <span className="moderation-gallery-emoji" aria-label={detail.name}>{detail.emoji}</span>
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
                    <p>Diện tích: {detail.farm?.productionScale || '—'}</p></div>
                </div>
              </section>

              <section><SectionTitle>Thông tin thu hoạch</SectionTitle>
                {harvests.length ? harvests.map((batch) => <div className="moderation-facts" key={batch.batchCode}>
                  <div><span>Ngày thu hoạch dự kiến</span><strong>
                    {date(batch.actualHarvestDate || batch.estimatedHarvestDate)}</strong></div>
                  <div><span>Phương thức / mùa vụ</span><strong>
                    {batch.season || batch.cropVariety || batch.status || '—'}</strong></div>
                  <div><span>Ghi chú</span><strong>{batch.qualityNotes || '—'}</strong></div>
                </div>) : <p className="moderation-muted">Chưa cung cấp thông tin thu hoạch.</p>}
              </section>

              <section><SectionTitle>Truy xuất nguồn gốc & chứng nhận</SectionTitle>
                <div className="moderation-traceability">
                  {traceability.map((batch) => <div className="moderation-trace-item" key={batch.batchCode}>
                    <span>Mã lô</span><strong>{batch.batchCode}</strong>
                    {batch.plantingDate && <><span>Ngày gieo trồng</span><strong>{batch.plantingDate}</strong></>}
                    {batch.growingPractices && <><span>Quy trình canh tác</span>
                      <p>{batch.growingPractices}</p></>}
                    {safeUrl(batch.targetUrl) && <a href={safeUrl(batch.targetUrl)}
                      target="_blank" rel="noreferrer">Xem thông tin truy xuất</a>}
                  </div>)}
                  <div><span>Chứng nhận</span>
                    {certificates.length ? <div className="moderation-certificates">
                      {certificates.map((certificate) => {
                        const label = typeof certificate === 'string'
                          ? certificate : certificate.certificateType;
                        return <span key={label}>✓ {label}</span>;
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
                <label htmlFor="moderation-reason">Lý do từ chối
                  <span> (bắt buộc nếu từ chối)</span></label>
                <textarea id="moderation-reason" rows="3" maxLength={500} value={reason}
                  placeholder="Nêu rõ thông tin cần farmer bổ sung hoặc chỉnh sửa..."
                  onChange={(event) => { setReason(event.target.value); setActionError(''); }} />
                <Notice error>{actionError}</Notice>
                <div className="moderation-decision-actions">
                  <button type="button" className="reject" disabled={busy}
                    onClick={() => moderate('reject')}>{busy ? 'Đang xử lý…' : 'Từ chối sản phẩm'}</button>
                  <button type="button" className="approve" disabled={busy}
                    onClick={() => moderate('approve')}>{busy ? 'Đang xử lý…' : 'Duyệt sản phẩm'}</button>
                </div>
                <p className="moderation-character-count">{reason.length}/500 ký tự · Farmer sẽ được thông báo sau khi có quyết định.</p>
              </section>}
            </div>
          </article>}
        </div>
      </div>}
    <p className="moderation-demo-note">Sản phẩm có nhãn Demo và quyết định demo chỉ tồn tại trong phiên hiện tại.</p>
  </section>;
}
