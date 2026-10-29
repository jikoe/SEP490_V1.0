import React, { useCallback, useEffect, useState } from 'react';
import { requestUseCase as request } from '../../api/client';
import { Field, Modal, Notice, Pager, VerificationBadge, initials, localTime, statusLabel } from '../../components/ui/Common';
import { getSession } from '../../session/session';

const APPLICANT_ROLES = ['Farmer', 'WholesaleBuyer', 'RetailCustomer'];
const VERIFICATION_STATUSES = ['Pending', 'Approved', 'Rejected', 'AdditionalInfoRequired'];
const PAGE_SIZE = 10;
const DEMO_STORAGE_KEY = 'agrisustain:kyc-demo-store-v1';

export const DEMO_KYC_APPLICATIONS = [
  {
    userId: 201,
    username: 'farmer_mangden',
    fullName: 'Trần Minh Khôi',
    email: 'khoi.mangden@agrisustain.vn',
    phoneNumber: '0912345678',
    role: 'Farmer',
    accountStatus: 'Pending',
    entityName: 'Nông trại Xanh Măng Đen',
    organizationName: 'Nông trại Xanh Măng Đen',
    representativeName: 'Trần Minh Khôi',
    identificationNumber: 'GC-VG-2026-8812',
    businessType: 'Nông trại hữu cơ & Hợp tác xã',
    productionScale: '15 ha rau củ ôn đới & cà phê Arabica',
    cultivationZone: 'Thị trấn Măng Đen, Huyện Kon Plông, Kon Tum',
    gpsCoordinates: '14.6025, 108.2914',
    address: 'Thôn Kon Tu Rằng, Măng Đen, Kon Plông, Kon Tum',
    provinceCode: 'KT',
    verificationStatus: 'Pending',
    submittedAt: '2026-10-28T08:30:00Z',
    createdAt: '2026-10-27T14:00:00Z',
    approvedAt: null,
    reviewedAt: null,
    reviewedBy: null,
    rejectReason: null,
    documentCount: 2,
    documents: [
      {
        certificateId: 501,
        certificateType: 'Chứng nhận Hữu cơ (Organic)',
        certificateNumber: 'GC-VG-2026-8812',
        issuedBy: 'Control Union Vietnam',
        issueDate: '2026-01-15T00:00:00Z',
        expiryDate: '2028-01-15T00:00:00Z',
        documentUrl: 'https://agrisustain.vn/docs/kyc/mangden-organic-cert.pdf',
        status: 'Pending',
      },
      {
        certificateId: 502,
        certificateType: 'Giấy chứng nhận VietGAP Trồng trọt',
        certificateNumber: 'VG-KT-2025-104',
        issuedBy: 'Trung tâm Chất lượng NLTS Vùng 3',
        issueDate: '2025-06-10T00:00:00Z',
        expiryDate: '2028-06-10T00:00:00Z',
        documentUrl: 'https://agrisustain.vn/docs/kyc/mangden-vietgap.pdf',
        status: 'Pending',
      },
    ],
  },
  {
    userId: 202,
    username: 'b2b_greenmart',
    fullName: 'Lê Hoàng Bách',
    email: 'procurement@greenmart.vn',
    phoneNumber: '0987654321',
    role: 'WholesaleBuyer',
    accountStatus: 'Pending',
    entityName: 'Công ty Cổ phần Thực phẩm Sạch GreenMart',
    organizationName: 'Công ty Cổ phần Thực phẩm Sạch GreenMart',
    representativeName: 'Lê Hoàng Bách',
    identificationNumber: 'GPKD: 0316894210 | MST: 0316894210',
    businessType: 'Hệ thống siêu thị & Phân phối B2B',
    productionScale: null,
    cultivationZone: null,
    gpsCoordinates: null,
    address: '245 Nguyễn Thị Minh Khai, Quận 1, TP. Hồ Chí Minh',
    provinceCode: 'HCM',
    verificationStatus: 'Pending',
    submittedAt: '2026-10-28T10:15:00Z',
    createdAt: '2026-10-28T09:00:00Z',
    approvedAt: null,
    reviewedAt: null,
    reviewedBy: null,
    rejectReason: null,
    documentCount: 1,
    documents: [
      {
        certificateId: 503,
        certificateType: 'Giấy chứng nhận đăng ký doanh nghiệp / Mã số thuế',
        certificateNumber: '0316894210 / MST: 0316894210',
        issuedBy: 'Sở Kế hoạch & Đầu tư TP.HCM',
        issueDate: '2026-10-18T10:15:00Z',
        expiryDate: null,
        documentUrl: null,
        status: 'Pending',
      },
    ],
  },
  {
    userId: 203,
    username: 'retail_thuha',
    fullName: 'Nguyễn Thu Hà',
    email: 'thuha.nguyen@gmail.com',
    phoneNumber: '0905112233',
    role: 'RetailCustomer',
    accountStatus: 'Active',
    entityName: 'Khách hàng Nguyễn Thu Hà',
    organizationName: 'Khách hàng Nguyễn Thu Hà',
    representativeName: 'Nguyễn Thu Hà',
    identificationNumber: 'CCCD/SĐT: 0905112233',
    businessType: 'Khách hàng cá nhân',
    productionScale: null,
    cultivationZone: null,
    gpsCoordinates: null,
    address: 'Số 18 Ngõ 42 Trần Duy Hưng, Cầu Giấy, Hà Nội',
    provinceCode: 'HN',
    verificationStatus: 'AdditionalInfoRequired',
    submittedAt: '2026-10-26T16:40:00Z',
    createdAt: '2026-10-25T11:20:00Z',
    approvedAt: null,
    reviewedAt: '2026-10-27T09:10:00Z',
    reviewedBy: 'Chuyên viên Thẩm định KYC',
    rejectReason: 'Vui lòng cập nhật ảnh chụp mặt sau CCCD rõ nét và địa chỉ nhận hàng chính xác.',
    documentCount: 1,
    documents: [
      {
        certificateId: 504,
        certificateType: 'Hồ sơ định danh khách hàng cá nhân (CCCD / Hộ chiếu)',
        certificateNumber: '0905112233',
        issuedBy: 'Cục CS QLHC về TTXH',
        issueDate: '2026-10-15T16:40:00Z',
        expiryDate: null,
        documentUrl: null,
        status: 'AdditionalInfoRequired',
      },
    ],
  },
  {
    userId: 204,
    username: 'farmer_bentre',
    fullName: 'Đặng Văn Thành',
    email: 'thanh.buoidaxanh@agrisustain.vn',
    phoneNumber: '0939887766',
    role: 'Farmer',
    accountStatus: 'Active',
    entityName: 'Vườn Bưởi Da Xanh Châu Thành',
    organizationName: 'Vườn Bưởi Da Xanh Châu Thành',
    representativeName: 'Đặng Văn Thành',
    identificationNumber: 'VG-BT-2025-990',
    businessType: 'Hộ kinh doanh cá thể',
    productionScale: '6.5 ha bưởi da xanh xuất khẩu',
    cultivationZone: 'Xã Giao Long, Huyện Châu Thành, Bến Tre',
    gpsCoordinates: '10.2891, 106.3812',
    address: 'Ấp Phú Lợi, Xã Giao Long, Châu Thành, Bến Tre',
    provinceCode: 'BT',
    verificationStatus: 'Approved',
    submittedAt: '2026-10-20T08:00:00Z',
    createdAt: '2026-10-19T15:30:00Z',
    approvedAt: '2026-10-21T14:20:00Z',
    reviewedAt: '2026-10-21T14:20:00Z',
    reviewedBy: 'Chuyên viên Thẩm định KYC',
    rejectReason: null,
    documentCount: 1,
    documents: [
      {
        certificateId: 505,
        certificateType: 'VietGAP Trồng trọt',
        certificateNumber: 'VG-BT-2025-990',
        issuedBy: 'Trung tâm Chất lượng NLTS Nam Bộ',
        issueDate: '2025-04-01T00:00:00Z',
        expiryDate: '2028-04-01T00:00:00Z',
        documentUrl: 'https://agrisustain.vn/docs/kyc/bentre-vietgap.pdf',
        status: 'Approved',
      },
    ],
  },
];

export function readDemoKycStore() {
  try {
    const raw = sessionStorage.getItem(DEMO_STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw);
      if (Array.isArray(parsed) && parsed.length > 0) {
        return parsed;
      }
    }
  } catch {
    // ignore storage parse failure
  }
  return DEMO_KYC_APPLICATIONS;
}

export function writeDemoKycStore(nextItems) {
  try {
    sessionStorage.setItem(DEMO_STORAGE_KEY, JSON.stringify(nextItems));
  } catch {
    // ignore storage quota failure
  }
  return nextItems;
}

const DECISION_CONFIG = {
  approve: {
    title: 'Phê duyệt hồ sơ KYC',
    submitLabel: 'Xác nhận phê duyệt',
    requireReason: false,
    reasonLabel: 'Ghi chú thẩm định (Không bắt buộc)',
    placeholder: 'Nhập ghi chú xác nhận hồ sơ hợp lệ (tùy chọn)...',
    description:
      'Hồ sơ KYC sẽ chuyển sang trạng thái Đã xác minh. Tài khoản đang Chờ duyệt sẽ được kích hoạt sử dụng các chức năng tương ứng.',
  },
  reject: {
    title: 'Từ chối hồ sơ KYC',
    submitLabel: 'Xác nhận từ chối',
    requireReason: true,
    reasonLabel: 'Lý do từ chối hồ sơ *',
    placeholder: 'Nhập rõ lý do từ chối hồ sơ KYC (ví dụ: giấy phép hết hạn, thông tin không trùng khớp)...',
    description:
      'Hồ sơ KYC sẽ chuyển sang trạng thái Từ chối và hệ thống sẽ gửi thông báo kèm lý do từ chối cho người nộp.',
  },
  'request-info': {
    title: 'Yêu cầu bổ sung thông tin KYC',
    submitLabel: 'Gửi yêu cầu bổ sung',
    requireReason: true,
    reasonLabel: 'Nội dung / tài liệu cần bổ sung *',
    placeholder: 'Nhập hướng dẫn chi tiết các thông tin hoặc tài liệu người dùng cần nộp bổ sung...',
    description:
      'Hồ sơ KYC sẽ chuyển sang trạng thái Cần bổ sung thông tin và thông báo cho người dùng cập nhật hồ sơ.',
  },
};

function InfoCard({ step, icon, title, subtitle, accentColor = '#166534', accentBg = '#ecfdf3', children }) {
  return (
    <div
      style={{
        background: '#ffffff',
        border: '1px solid #e2ebe5',
        borderRadius: 16,
        padding: '20px 22px',
        boxShadow: '0 2px 8px rgba(17, 42, 28, 0.04)',
        display: 'flex',
        flexDirection: 'column',
      }}
    >
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          gap: 12,
          paddingBottom: 14,
          marginBottom: 8,
          borderBottom: '1px solid #edf2ee',
        }}
      >
        <span
          aria-hidden="true"
          style={{
            width: 38,
            height: 38,
            borderRadius: 10,
            background: accentBg,
            color: accentColor,
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: 18,
            fontWeight: 700,
            flexShrink: 0,
          }}
        >
          {icon}
        </span>
        <div>
          <span
            style={{
              display: 'block',
              fontSize: 11,
              fontWeight: 700,
              letterSpacing: '0.06em',
              textTransform: 'uppercase',
              color: accentColor,
            }}
          >
            Mục {step} · {subtitle}
          </span>
          <h3 style={{ margin: '2px 0 0', fontSize: 16, fontWeight: 700, color: '#14281d' }}>
            {title}
          </h3>
        </div>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', flex: 1 }}>{children}</div>
    </div>
  );
}

function InfoRow({ label, children, mono = false, last = false }) {
  return (
    <div
      style={{
        display: 'grid',
        gridTemplateColumns: 'minmax(145px, 42%) 1fr',
        alignItems: 'center',
        gap: 12,
        padding: '10px 0',
        borderBottom: last ? 'none' : '1px dashed #e6eee8',
      }}
    >
      <span style={{ fontSize: 13, color: '#5c7265', fontWeight: 500 }}>{label}</span>
      <div
        style={{
          fontSize: 13.5,
          color: '#132a1c',
          fontWeight: 600,
          textAlign: 'right',
          wordBreak: 'break-word',
          fontFamily: mono ? 'ui-monospace, SFMono-Regular, Menlo, monospace' : 'inherit',
        }}
      >
        {children}
      </div>
    </div>
  );
}

function parseKycDetailId(route = '') {
  const match = /^\/verification\/kyc\/(\d+)$/.exec(route.split('?')[0]);
  return match ? Number(match[1]) : null;
}

function filterDemoList(source, filters) {
  const keyword = filters.search.trim().toLowerCase();
  const filtered = source.filter((item) => {
    if (filters.role && item.role !== filters.role) return false;
    if (filters.verificationStatus && item.verificationStatus !== filters.verificationStatus) return false;
    if (keyword) {
      const haystack = [
        item.fullName,
        item.username,
        item.email,
        item.phoneNumber,
        item.entityName,
        item.organizationName,
        item.identificationNumber,
        String(item.userId),
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();
      if (!haystack.includes(keyword)) return false;
    }
    return true;
  });

  const total = filtered.length;
  const start = (filters.page - 1) * PAGE_SIZE;
  const items = filtered.slice(start, start + PAGE_SIZE);
  return {
    items,
    total,
    page: filters.page,
    pageSize: PAGE_SIZE,
    message: total === 0 ? 'Không tìm thấy hồ sơ KYC nào phù hợp với tiêu chí tìm kiếm/lọc.' : null,
  };
}

export default function KycVerificationReviewScreen({ route = '/verification/kyc', navigate }) {
  const isDemoMode = !getSession()?.accessToken;
  const detailUserId = parseKycDetailId(route);

  const [demoStore, setDemoStore] = useState(readDemoKycStore);
  const [usingFallbackDemo, setUsingFallbackDemo] = useState(isDemoMode);
  const [data, setData] = useState({ items: [], total: 0, page: 1, pageSize: PAGE_SIZE, message: null });
  const [searchInput, setSearchInput] = useState('');
  const [filters, setFilters] = useState({
    search: '',
    role: '',
    verificationStatus: '',
    page: 1,
  });

  const [selectedDetail, setSelectedDetail] = useState(null);
  const [loadingList, setLoadingList] = useState(false);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');

  const [decisionModal, setDecisionModal] = useState(null); // 'approve' | 'reject' | 'request-info' | null
  const [decisionReason, setDecisionReason] = useState('');
  const [decisionError, setDecisionError] = useState('');
  const [submittingDecision, setSubmittingDecision] = useState(false);

  const loadList = useCallback(async () => {
    setLoadingList(true);
    setError('');

    const currentDemo = readDemoKycStore();

    if (isDemoMode) {
      setUsingFallbackDemo(true);
      setData(filterDemoList(currentDemo, filters));
      setLoadingList(false);
      return;
    }

    try {
      const params = new URLSearchParams({
        page: String(filters.page),
        pageSize: String(PAGE_SIZE),
      });
      if (filters.search.trim()) {
        params.set('keyword', filters.search.trim());
        params.set('search', filters.search.trim());
      }
      if (filters.role) params.set('role', filters.role);
      if (filters.verificationStatus) {
        params.set('status', filters.verificationStatus);
        params.set('verificationStatus', filters.verificationStatus);
      }

      const response = await request(`/api/kyc-applications?${params}`);
      setUsingFallbackDemo(false);
      setData({
        ...response,
        items: response.items || [],
        total: response.total ?? 0,
        message:
          (response.total ?? 0) === 0
            ? 'Không tìm thấy hồ sơ KYC nào phù hợp với tiêu chí tìm kiếm/lọc.'
            : null,
      });
    } catch {
      setUsingFallbackDemo(true);
      setData(filterDemoList(currentDemo, filters));
    } finally {
      setLoadingList(false);
    }
  }, [filters, isDemoMode]);

  useEffect(() => {
    loadList();
  }, [loadList, demoStore]);

  const loadDetail = useCallback(
    async (userId) => {
      if (!userId) {
        setSelectedDetail(null);
        return;
      }

      setLoadingDetail(true);
      setError('');

      const currentDemo = readDemoKycStore();
      const demoCandidate = currentDemo.find((item) => item.userId === Number(userId)) || null;

      if (isDemoMode) {
        setSelectedDetail(demoCandidate);
        if (!demoCandidate) {
          setError('Không tìm thấy hồ sơ KYC đã chọn.');
        }
        setLoadingDetail(false);
        return;
      }

      try {
        const detail = await request(`/api/kyc-applications/${userId}`);
        setUsingFallbackDemo(false);
        setSelectedDetail(detail);
      } catch (err) {
        if (demoCandidate) {
          setUsingFallbackDemo(true);
          setSelectedDetail(demoCandidate);
        } else {
          setSelectedDetail(null);
          setError(err.message || 'Không tìm thấy hoặc không thể tải chi tiết hồ sơ KYC.');
        }
      } finally {
        setLoadingDetail(false);
      }
    },
    [isDemoMode],
  );

  useEffect(() => {
    if (detailUserId) {
      loadDetail(detailUserId);
    } else {
      setSelectedDetail(null);
    }
  }, [detailUserId, loadDetail]);

  function openApplicationDetail(userId) {
    setNotice('');
    setError('');
    if (navigate) {
      navigate(`/verification/kyc/${userId}`);
    } else {
      loadDetail(userId);
    }
  }

  function backToList() {
    setDecisionModal(null);
    setDecisionReason('');
    setDecisionError('');
    if (navigate && detailUserId) {
      navigate('/verification/kyc');
    } else {
      setSelectedDetail(null);
    }
  }

  function handleSearchSubmit(event) {
    event.preventDefault();
    setNotice('');
    setFilters((prev) => ({ ...prev, search: searchInput, page: 1 }));
  }

  function openDecisionDialog(actionKey) {
    setDecisionModal(actionKey);
    setDecisionReason('');
    setDecisionError('');
  }

  async function submitDecision(event) {
    event.preventDefault();
    if (!selectedDetail || !decisionModal) return;

    const config = DECISION_CONFIG[decisionModal];
    const trimmedReason = decisionReason.trim();

    if (config.requireReason && !trimmedReason) {
      setDecisionError(
        decisionModal === 'reject'
          ? 'Vui lòng nhập lý do từ chối hồ sơ KYC.'
          : 'Vui lòng nhập nội dung thông tin/tài liệu yêu cầu bổ sung.',
      );
      return;
    }

    if (trimmedReason.length > 500) {
      setDecisionError('Nội dung lý do/ghi chú không được vượt quá 500 ký tự.');
      return;
    }

    setSubmittingDecision(true);
    setDecisionError('');
    setError('');

    const isDemoRecord =
      isDemoMode ||
      usingFallbackDemo ||
      demoStore.some((item) => item.userId === Number(selectedDetail.userId));

    try {
      if (!isDemoMode && !usingFallbackDemo && !demoStore.some((item) => item.userId === Number(selectedDetail.userId))) {
        const result = await request(`/api/kyc-applications/${selectedDetail.userId}/review`, {
          method: 'POST',
          body: JSON.stringify({
            action: decisionModal,
            reason: trimmedReason || null,
          }),
        });

        setNotice(result?.message || 'Cập nhật trạng thái xác minh KYC thành công.');
        setDecisionModal(null);
        setDecisionReason('');
        await Promise.all([loadList(), loadDetail(selectedDetail.userId)]);
        return;
      }

      if (!isDemoMode && !usingFallbackDemo) {
        try {
          const result = await request(`/api/kyc-applications/${selectedDetail.userId}/review`, {
            method: 'POST',
            body: JSON.stringify({
              action: decisionModal,
              reason: trimmedReason || null,
            }),
          });
          setNotice(result?.message || 'Cập nhật trạng thái xác minh KYC thành công.');
          setDecisionModal(null);
          setDecisionReason('');
          await Promise.all([loadList(), loadDetail(selectedDetail.userId)]);
          return;
        } catch {
          // Nếu ID là dữ liệu mẫu (201-204), tiếp tục cập nhật vào bộ nhớ phiên bên dưới
        }
      }

      if (isDemoRecord) {
        const nowIso = new Date().toISOString();
        const nextStatus =
          decisionModal === 'approve'
            ? 'Approved'
            : decisionModal === 'reject'
              ? 'Rejected'
              : 'AdditionalInfoRequired';

        const nextAccountStatus =
          selectedDetail.accountStatus === 'Suspended'
            ? 'Suspended'
            : decisionModal === 'approve'
              ? 'Active'
              : decisionModal === 'reject' &&
                  (selectedDetail.role === 'Farmer' || selectedDetail.role === 'WholesaleBuyer')
                ? 'Rejected'
                : selectedDetail.accountStatus;

        const message =
          decisionModal === 'approve'
            ? 'Phê duyệt hồ sơ KYC thành công. Trạng thái xác minh đã được cập nhật.'
            : decisionModal === 'reject'
              ? 'Đã từ chối hồ sơ KYC và gửi thông báo cho người dùng.'
              : 'Đã gửi yêu cầu bổ sung thông tin KYC cho người dùng.';

        const reviewerName = getSession()?.user?.fullName || 'Chuyên viên Thẩm định KYC';

        const updatedStore = readDemoKycStore().map((item) =>
          item.userId === selectedDetail.userId
            ? {
                ...item,
                verificationStatus: nextStatus,
                accountStatus: nextAccountStatus,
                approvedAt: decisionModal === 'approve' ? nowIso : null,
                reviewedAt: nowIso,
                reviewedBy: reviewerName,
                rejectReason: decisionModal === 'approve' ? null : trimmedReason,
                documents: (item.documents || []).map((doc) => ({
                  ...doc,
                  status: nextStatus,
                  verificationStatus: nextStatus,
                })),
              }
            : item,
        );

        writeDemoKycStore(updatedStore);
        setDemoStore(updatedStore);

        const updatedRecord = updatedStore.find((item) => item.userId === selectedDetail.userId) || null;
        setSelectedDetail(updatedRecord);
        setData(filterDemoList(updatedStore, filters));

        setNotice(message);
        setDecisionModal(null);
        setDecisionReason('');
      }
    } catch (err) {
      setDecisionError(err.message || 'Không thể lưu kết quả thẩm định KYC.');
    } finally {
      setSubmittingDecision(false);
    }
  }

  const metricSource = usingFallbackDemo ? demoStore : data.items;
  const pendingCount = metricSource.filter(
    (item) => item.verificationStatus === 'Pending' || item.verificationStatus === 'PendingApproval',
  ).length;
  const approvedCount = metricSource.filter((item) => item.verificationStatus === 'Approved').length;
  const infoRequiredCount = metricSource.filter(
    (item) => item.verificationStatus === 'AdditionalInfoRequired',
  ).length;
  const totalTracked = usingFallbackDemo ? demoStore.length : data.total;

  const metrics = [
    {
      label: 'Hồ sơ KYC chờ duyệt',
      value: `${pendingCount} hồ sơ`,
      sub: 'Nông dân, Mua buôn B2B & Khách lẻ',
      icon: '📑',
      color: 'gold',
    },
    {
      label: 'Cần bổ sung thông tin',
      value: `${infoRequiredCount} hồ sơ`,
      sub: 'Đang chờ người dùng cập nhật',
      icon: '🏡',
      color: 'blue',
    },
    {
      label: 'Đã xác minh KYC',
      value: `${approvedCount} hồ sơ`,
      sub: 'Đủ điều kiện giao dịch trên hệ thống',
      icon: '✓',
      color: 'green',
    },
    {
      label: 'Tổng hồ sơ theo dõi',
      value: `${totalTracked} hồ sơ`,
      sub: 'Cập nhật theo thời gian thực',
      icon: '📍',
      color: 'purple',
    },
  ];

  return (
    <section className="verification-dashboard" aria-labelledby="kyc-review-heading">
      <div className="section-heading">
        <div>
          <p className="eyebrow">CHUYÊN VIÊN THẨM ĐỊNH CHẤT LƯỢNG</p>
          <h1 id="kyc-review-heading">Xét Duyệt Hồ Sơ KYC & Xác Minh Pháp Lý</h1>
          <p className="muted">
            Kiểm tra và xác minh thông tin pháp lý, CCCD/Hộ chiếu, đăng ký kinh doanh và chứng nhận của
            Nông dân, Doanh nghiệp mua buôn và Khách mua lẻ.
          </p>
        </div>
      </div>

      {/* 4 Thẻ thống kê tổng quan KYC */}
      <div className="dashboard-grid">
        {metrics.map((item, idx) => (
          <div key={idx} className="metric-card">
            <div className={`metric-icon ${item.color}`}>{item.icon}</div>
            <div className="metric-info">
              <p className="metric-label">{item.label}</p>
              <h3 className="metric-value">{item.value}</h3>
              <p className="metric-sub">{item.sub}</p>
            </div>
          </div>
        ))}
      </div>

      <Notice>{notice}</Notice>
      <Notice error>{error}</Notice>

      {selectedDetail ? (
        <div className="panel padded" aria-label="Chi tiết hồ sơ KYC">
          {/* Header tóm tắt hồ sơ đang xem */}
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              flexWrap: 'wrap',
              gap: 16,
              padding: '18px 22px',
              marginBottom: 20,
              borderRadius: 16,
              background: 'linear-gradient(135deg, #f3faf5 0%, #ffffff 100%)',
              border: '1px solid #d5e6dc',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
              <span
                aria-hidden="true"
                style={{
                  width: 54,
                  height: 54,
                  borderRadius: 14,
                  background: '#166534',
                  color: '#ffffff',
                  display: 'inline-flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: 20,
                  fontWeight: 700,
                  boxShadow: '0 4px 12px rgba(22, 101, 52, 0.2)',
                }}
              >
                {initials(selectedDetail.fullName)}
              </span>
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 4 }}>
                  <span
                    style={{
                      fontSize: 11,
                      fontWeight: 700,
                      letterSpacing: '0.06em',
                      textTransform: 'uppercase',
                      color: '#166534',
                      background: '#dcfce7',
                      padding: '3px 8px',
                      borderRadius: 6,
                    }}
                  >
                    Hồ sơ KYC #{selectedDetail.userId}
                  </span>
                  <span className="pill" style={{ fontSize: 12 }}>
                    {statusLabel[selectedDetail.role] || selectedDetail.role}
                  </span>
                </div>
                <h2 style={{ margin: 0, fontSize: 20, color: '#11261a' }}>
                  {selectedDetail.entityName ||
                    selectedDetail.organizationName ||
                    selectedDetail.fullName}
                </h2>
                <span className="muted" style={{ fontSize: 13 }}>
                  Người đăng ký: <strong>{selectedDetail.fullName}</strong>
                  {selectedDetail.username ? ` (@${selectedDetail.username})` : ''}
                </span>
              </div>
            </div>
            <div className="action-row">
              <VerificationBadge value={selectedDetail.verificationStatus} />
              <button type="button" onClick={backToList}>
                ← Quay lại danh sách hồ sơ
              </button>
            </div>
          </div>

          {/* Lưới 2x2 hiển thị 4 nhóm thông tin KYC cân đối, rõ ràng */}
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(420px, 1fr))',
              gap: 18,
              marginBottom: 20,
            }}
          >
            {/* 1. Thông tin tài khoản */}
            <InfoCard
              step="01"
              icon="👤"
              title="Thông tin tài khoản"
              subtitle="Định danh hệ thống"
              accentColor="#166534"
              accentBg="#dcfce7"
            >
              <InfoRow label="Mã tài khoản" mono>
                <span
                  style={{
                    background: '#f1f5f3',
                    padding: '3px 8px',
                    borderRadius: 6,
                    border: '1px solid #dce6e0',
                  }}
                >
                  #{selectedDetail.userId}
                </span>
              </InfoRow>
              <InfoRow label="Tên đăng nhập" mono>
                {selectedDetail.username ? `@${selectedDetail.username}` : '—'}
              </InfoRow>
              <InfoRow label="Phân nhóm đối tượng">
                {statusLabel[selectedDetail.role] || selectedDetail.role}
              </InfoRow>
              <InfoRow label="Trạng thái tài khoản">
                <span className={`badge ${selectedDetail.accountStatus}`}>
                  {statusLabel[selectedDetail.accountStatus] || selectedDetail.accountStatus}
                </span>
              </InfoRow>
              <InfoRow label="Ngày đăng ký tài khoản" last>
                {localTime(selectedDetail.createdAt || selectedDetail.submittedAt)}
              </InfoRow>
            </InfoCard>

            {/* 2. Họ tên / Pháp nhân */}
            <InfoCard
              step="02"
              icon="🏡"
              title="Họ tên & Pháp nhân"
              subtitle="Thông tin chủ thể"
              accentColor="#0369a1"
              accentBg="#e0f2fe"
            >
              <InfoRow label="Họ và tên người đăng ký">{selectedDetail.fullName || '—'}</InfoRow>
              <InfoRow label="Tên Nông trại / Doanh nghiệp">
                <span style={{ color: '#166534', fontWeight: 700 }}>
                  {selectedDetail.entityName ||
                    selectedDetail.organizationName ||
                    selectedDetail.fullName ||
                    '—'}
                </span>
              </InfoRow>
              <InfoRow label="Người đại diện pháp luật">
                {selectedDetail.representativeName || selectedDetail.fullName || '—'}
              </InfoRow>
              <InfoRow label="Loại hình hoạt động" last={selectedDetail.role !== 'Farmer'}>
                {selectedDetail.businessType || '—'}
              </InfoRow>
              {selectedDetail.role === 'Farmer' && (
                <>
                  <InfoRow label="Quy mô sản xuất">
                    {selectedDetail.productionScale || '—'}
                  </InfoRow>
                  <InfoRow label="Vùng canh tác & Tọa độ GPS" last>
                    <div>{selectedDetail.cultivationZone || '—'}</div>
                    {selectedDetail.gpsCoordinates && (
                      <small
                        style={{
                          display: 'inline-block',
                          marginTop: 4,
                          padding: '2px 8px',
                          borderRadius: 6,
                          background: '#eff6ff',
                          color: '#1d4ed8',
                          fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
                          fontSize: 12,
                        }}
                      >
                        📍 GPS: {selectedDetail.gpsCoordinates}
                      </small>
                    )}
                  </InfoRow>
                </>
              )}
            </InfoCard>

            {/* 3. Thông tin liên hệ */}
            <InfoCard
              step="03"
              icon="📞"
              title="Thông tin liên hệ"
              subtitle="Địa chỉ & Liên lạc"
              accentColor="#b45309"
              accentBg="#fef3c7"
            >
              <InfoRow label="Số điện thoại liên hệ" mono>
                {selectedDetail.phoneNumber || '—'}
              </InfoRow>
              <InfoRow label="Địa chỉ Email">{selectedDetail.email || '—'}</InfoRow>
              <InfoRow label="Địa chỉ hoạt động / Cư trú">
                {selectedDetail.address || 'Chưa cập nhật'}
              </InfoRow>
              <InfoRow label="Mã tỉnh / Khu vực" last>
                <span
                  style={{
                    background: '#f8faf9',
                    padding: '3px 10px',
                    borderRadius: 6,
                    border: '1px solid #d5e2da',
                  }}
                >
                  {selectedDetail.provinceCode || '—'}
                </span>
              </InfoRow>
            </InfoCard>

            {/* 4. Định danh pháp lý & Trạng thái xác minh */}
            <InfoCard
              step="04"
              icon="🛡️"
              title="Định danh pháp lý & Trạng thái"
              subtitle="Kết quả thẩm định KYC"
              accentColor="#6d28d9"
              accentBg="#ede9fe"
            >
              <InfoRow label="Số CCCD / GPKD / Mã số thuế" mono>
                <span
                  style={{
                    background: '#f5f3ff',
                    color: '#5b21b6',
                    padding: '4px 10px',
                    borderRadius: 6,
                    border: '1px solid #ddd6fe',
                    fontWeight: 700,
                  }}
                >
                  {selectedDetail.identificationNumber || 'Chưa cung cấp'}
                </span>
              </InfoRow>
              <InfoRow label="Trạng thái xác minh KYC">
                <VerificationBadge value={selectedDetail.verificationStatus} />
              </InfoRow>
              <InfoRow label="Thời điểm nộp hồ sơ">
                {localTime(selectedDetail.submittedAt || selectedDetail.createdAt)}
              </InfoRow>
              <InfoRow label="Thời điểm thẩm định gần nhất">
                {localTime(selectedDetail.reviewedAt || selectedDetail.approvedAt)}
              </InfoRow>
              <InfoRow label="Chuyên viên thẩm định" last={!selectedDetail.rejectReason}>
                {selectedDetail.reviewedBy || '—'}
              </InfoRow>
              {selectedDetail.rejectReason && (
                <div
                  style={{
                    marginTop: 10,
                    padding: '10px 12px',
                    borderRadius: 10,
                    background:
                      selectedDetail.verificationStatus === 'AdditionalInfoRequired'
                        ? '#fffbeb'
                        : '#fef2f2',
                    border:
                      selectedDetail.verificationStatus === 'AdditionalInfoRequired'
                        ? '1px solid #fde68a'
                        : '1px solid #fecaca',
                  }}
                >
                  <div
                    style={{
                      fontSize: 11.5,
                      fontWeight: 700,
                      textTransform: 'uppercase',
                      color:
                        selectedDetail.verificationStatus === 'AdditionalInfoRequired'
                          ? '#b45309'
                          : '#b91c1c',
                      marginBottom: 4,
                    }}
                  >
                    {selectedDetail.verificationStatus === 'AdditionalInfoRequired'
                      ? 'Nội dung yêu cầu bổ sung thông tin'
                      : 'Lý do từ chối hồ sơ'}
                  </div>
                  <div style={{ fontSize: 13.5, color: '#7f1d1d', fontWeight: 600 }}>
                    {selectedDetail.rejectReason}
                  </div>
                </div>
              )}
            </InfoCard>
          </div>

          {/* 5. Submitted KYC documents/images */}
          <div className="panel padded" style={{ marginBottom: 20 }}>
            <div className="record-heading" style={{ padding: '0 0 12px' }}>
              <h3>5. Tài liệu & Hình ảnh chứng nhận KYC đã nộp</h3>
              <span className="pill">{selectedDetail.documents?.length || 0} tài liệu</span>
            </div>

            {selectedDetail.documents && selectedDetail.documents.length > 0 ? (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Loại giấy tờ / Chứng nhận</th>
                      <th>Số hiệu / Mã đăng ký</th>
                      <th>Cơ quan cấp / Xác thực</th>
                      <th>Ngày cấp</th>
                      <th>Ngày hết hạn</th>
                      <th>Trạng thái</th>
                      <th>Tệp đính kèm / Hình ảnh</th>
                    </tr>
                  </thead>
                  <tbody>
                    {selectedDetail.documents.map((doc, idx) => (
                      <tr key={doc.certificateId || doc.id || idx}>
                        <td>
                          <strong>{doc.certificateType || doc.documentType}</strong>
                        </td>
                        <td>{doc.certificateNumber || doc.documentNumber || '—'}</td>
                        <td>{doc.issuedBy || '—'}</td>
                        <td>{localTime(doc.issueDate)}</td>
                        <td>{localTime(doc.expiryDate)}</td>
                        <td>
                          <VerificationBadge
                            value={doc.status || doc.verificationStatus || selectedDetail.verificationStatus}
                          />
                        </td>
                        <td>
                          {doc.documentUrl ? (
                            <a
                              href={doc.documentUrl}
                              target="_blank"
                              rel="noreferrer"
                              style={{ color: '#166534', fontWeight: 600, textDecoration: 'underline' }}
                            >
                              Xem tài liệu / Ảnh KYC ↗
                            </a>
                          ) : (
                            <span className="muted">Đã đối chiếu dữ liệu hồ sơ</span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="muted">Chưa có tệp tài liệu đính kèm riêng biệt cho hồ sơ này.</p>
            )}
          </div>

          {/* Step 7 & 8: Verification Decision Controls (Approve / Reject / Request Additional Info) */}
          <div
            className="panel padded"
            style={{
              background: '#f8faf9',
              border: '1px solid #d5e2da',
              display: 'flex',
              justifyContent: 'space-between',
              alignItems: 'center',
              flexWrap: 'wrap',
              gap: 12,
            }}
          >
            <div>
              <h3 style={{ margin: '0 0 4px' }}>Quyết định thẩm định hồ sơ KYC</h3>
              <p className="muted" style={{ margin: 0 }}>
                Chọn thao tác tương ứng sau khi đã kiểm tra đầy đủ tính hợp lệ của thông tin và tài liệu KYC.
              </p>
            </div>
            <div className="action-row">
              <button
                type="button"
                className="primary"
                onClick={() => openDecisionDialog('approve')}
              >
                ✓ Phê duyệt KYC
              </button>
              <button
                type="button"
                onClick={() => openDecisionDialog('request-info')}
              >
                ✎ Yêu cầu bổ sung thông tin
              </button>
              <button
                type="button"
                className="danger"
                onClick={() => openDecisionDialog('reject')}
              >
                ✕ Từ chối KYC
              </button>
            </div>
          </div>
        </div>
      ) : (
        <div className="panel">
          <form className="toolbar" onSubmit={handleSearchSubmit} aria-label="Bộ lọc hồ sơ KYC">
            <Field label="Tìm kiếm hồ sơ">
              <input
                type="search"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                placeholder="Tên cá nhân, nông trại, doanh nghiệp, email, SĐT, mã giấy phép..."
              />
            </Field>
            <Field label="Nhóm đối tượng nộp">
              <select
                value={filters.role}
                onChange={(e) =>
                  setFilters((prev) => ({ ...prev, role: e.target.value, page: 1 }))
                }
              >
                <option value="">Tất cả đối tượng KYC</option>
                {APPLICANT_ROLES.map((role) => (
                  <option key={role} value={role}>
                    {statusLabel[role] || role}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Trạng thái xác minh">
              <select
                value={filters.verificationStatus}
                onChange={(e) =>
                  setFilters((prev) => ({ ...prev, verificationStatus: e.target.value, page: 1 }))
                }
              >
                <option value="">Tất cả trạng thái</option>
                {VERIFICATION_STATUSES.map((status) => (
                  <option key={status} value={status}>
                    {status === 'Pending'
                      ? 'Chờ xác minh'
                      : status === 'Approved'
                        ? 'Đã xác minh'
                        : status === 'Rejected'
                          ? 'Từ chối'
                          : 'Cần bổ sung thông tin'}
                  </option>
                ))}
              </select>
            </Field>
            <div className="action-row" style={{ alignSelf: 'end' }}>
              <button type="submit" className="primary">
                Tìm kiếm
              </button>
              {(filters.search || filters.role || filters.verificationStatus) && (
                <button
                  type="button"
                  onClick={() => {
                    setSearchInput('');
                    setFilters({ search: '', role: '', verificationStatus: '', page: 1 });
                  }}
                >
                  Đặt lại bộ lọc
                </button>
              )}
            </div>
          </form>

          {!loadingList && data.total === 0 && (
            <div style={{ padding: '24px 20px' }}>
              <Notice>
                {data.message || 'Không tìm thấy hồ sơ KYC nào phù hợp với tiêu chí tìm kiếm/lọc.'}
              </Notice>
            </div>
          )}

          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Mã HS</th>
                  <th>Người nộp / Đơn vị</th>
                  <th>Vai trò</th>
                  <th>Thông tin liên hệ</th>
                  <th>Số định danh / GPKD / Chứng nhận</th>
                  <th>Tài liệu</th>
                  <th>Ngày nộp</th>
                  <th>Trạng thái KYC</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {loadingList ? (
                  <tr>
                    <td colSpan={9} className="empty">
                      Đang tải danh sách hồ sơ KYC...
                    </td>
                  </tr>
                ) : data.items.length === 0 ? (
                  <tr>
                    <td colSpan={9} className="empty">
                      {data.message || 'Không tìm thấy hồ sơ KYC nào phù hợp với tiêu chí tìm kiếm/lọc.'}
                    </td>
                  </tr>
                ) : (
                  data.items.map((item) => (
                    <tr key={item.userId}>
                      <td>
                        <strong>#{item.userId}</strong>
                      </td>
                      <td>
                        <div className="user-cell">
                          <span className="avatar" aria-hidden="true">
                            {initials(item.fullName)}
                          </span>
                          <div>
                            <strong>
                              {item.entityName || item.organizationName || item.fullName}
                            </strong>
                            <span className="sub">
                              {item.fullName}
                              {item.username ? ` (@${item.username})` : ''}
                            </span>
                          </div>
                        </div>
                      </td>
                      <td>{statusLabel[item.role] || item.role}</td>
                      <td>
                        <div>{item.phoneNumber || '—'}</div>
                        <small className="muted">{item.email}</small>
                      </td>
                      <td>
                        <strong>{item.identificationNumber || '—'}</strong>
                        {item.address && (
                          <div className="sub" style={{ maxWidth: 220 }}>
                            {item.address}
                          </div>
                        )}
                      </td>
                      <td>
                        <span className="pill">{item.documentCount || 1} tài liệu</span>
                      </td>
                      <td>{localTime(item.submittedAt)}</td>
                      <td>
                        <VerificationBadge value={item.verificationStatus} />
                      </td>
                      <td>
                        <button
                          type="button"
                          className="primary"
                          onClick={() => openApplicationDetail(item.userId)}
                        >
                          Thẩm định KYC
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <Pager
            total={data.total}
            page={filters.page}
            pageSize={PAGE_SIZE}
            onPage={(nextPage) => setFilters((prev) => ({ ...prev, page: nextPage }))}
          />
        </div>
      )}

      {loadingDetail && (
        <div className="panel padded">
          <p className="muted">Đang tải chi tiết hồ sơ KYC...</p>
        </div>
      )}

      {/* Modal xác nhận quyết định thẩm định */}
      {decisionModal && selectedDetail && (
        <Modal
          title={DECISION_CONFIG[decisionModal].title}
          onClose={() => !submittingDecision && setDecisionModal(null)}
        >
          <form className="modal-body" onSubmit={submitDecision}>
            <p className="muted">{DECISION_CONFIG[decisionModal].description}</p>
            <div className="panel padded" style={{ margin: '12px 0' }}>
              <div>
                <strong>Đơn vị / Cá nhân:</strong>{' '}
                {selectedDetail.entityName ||
                  selectedDetail.organizationName ||
                  selectedDetail.fullName}{' '}
                (#{selectedDetail.userId})
              </div>
              <div>
                <strong>Vai trò:</strong> {statusLabel[selectedDetail.role] || selectedDetail.role}
              </div>
              <div>
                <strong>Định danh / Giấy phép:</strong> {selectedDetail.identificationNumber || '—'}
              </div>
            </div>

            <Field
              label={DECISION_CONFIG[decisionModal].reasonLabel}
              error={decisionError}
              hint="Tối đa 500 ký tự."
            >
              <textarea
                rows={4}
                maxLength={500}
                value={decisionReason}
                onChange={(e) => {
                  setDecisionReason(e.target.value);
                  if (decisionError) setDecisionError('');
                }}
                placeholder={DECISION_CONFIG[decisionModal].placeholder}
              />
            </Field>

            <div className="modal-actions">
              <button
                type="button"
                disabled={submittingDecision}
                onClick={() => setDecisionModal(null)}
              >
                Hủy bỏ
              </button>
              <button
                type="submit"
                className={decisionModal === 'reject' ? 'danger' : 'primary'}
                disabled={submittingDecision}
              >
                {submittingDecision ? 'Đang xử lý...' : DECISION_CONFIG[decisionModal].submitLabel}
              </button>
            </div>
          </form>
        </Modal>
      )}
    </section>
  );
}
