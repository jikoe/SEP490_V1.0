import React, { useEffect, useState } from 'react';
import { requestUseCase as request } from '../../api/client';
import { VerificationBadge, localTime, statusLabel } from '../../components/ui/Common';
import { getSession } from '../../session/session';
import { readDemoKycStore } from './KycVerificationReviewScreen';

export default function VerificationDashboard({ navigate }) {
  const [applications, setApplications] = useState(readDemoKycStore);

  useEffect(() => {
    const demoItems = readDemoKycStore();
    if (!getSession()?.accessToken) {
      setApplications(demoItems);
      return;
    }

    let cancelled = false;
    request('/api/kyc-applications?page=1&pageSize=10')
      .then((res) => {
        if (!cancelled && Array.isArray(res?.items) && res.items.length > 0) {
          setApplications(res.items);
        } else if (!cancelled) {
          setApplications(demoItems);
        }
      })
      .catch(() => {
        if (!cancelled) setApplications(demoItems);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const pendingCount = applications.filter(
    (item) => item.verificationStatus === 'Pending' || item.verificationStatus === 'PendingApproval',
  ).length;
  const approvedCount = applications.filter((item) => item.verificationStatus === 'Approved').length;
  const infoRequiredCount = applications.filter(
    (item) => item.verificationStatus === 'AdditionalInfoRequired',
  ).length;

  const metrics = [
    {
      label: 'Hồ sơ KYC chờ duyệt',
      value: `${pendingCount} hồ sơ`,
      sub: 'Farmer, WholesaleBuyer & RetailCustomer',
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
      value: `${applications.length} hồ sơ`,
      sub: 'Cập nhật theo thời gian thực',
      icon: '📍',
      color: 'purple',
    },
  ];

  return (
    <div className="verification-dashboard">
      <div className="section-heading">
        <div>
          <p className="eyebrow">CHUYÊN VIÊN THẨM ĐỊNH CHẤT LƯỢNG</p>
          <h1>Bảng Thẩm Định & Xác Minh Nông Trại</h1>
          <p className="muted">
            Kiểm tra tính hợp lệ của chứng nhận VietGAP, GlobalGAP, Organic và xác minh pháp lý KYC của
            người dùng trước khi kích hoạt.
          </p>
        </div>
        {navigate && (
          <div className="action-row">
            <button
              type="button"
              className="primary"
              onClick={() => navigate('/verification/kyc')}
            >
              Mở chức năng Xét duyệt hồ sơ KYC
            </button>
          </div>
        )}
      </div>

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

      <div className="panel padded">
        <div className="record-heading" style={{ padding: '0 0 16px' }}>
          <h2>Hồ sơ KYC & Chứng chỉ đang chờ xử lý</h2>
          <span className="badge Pending">{applications.length} hồ sơ</span>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Đơn vị / Người nộp</th>
                <th>Vai trò</th>
                <th>Số định danh / Chứng nhận</th>
                <th>Ngày nộp</th>
                <th>Trạng thái</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {applications.map((item) => (
                <tr key={item.userId}>
                  <td>
                    <strong>{item.entityName || item.organizationName || item.fullName}</strong>
                    <div className="sub">{item.fullName}</div>
                  </td>
                  <td>{statusLabel[item.role] || item.role}</td>
                  <td>{item.identificationNumber || '—'}</td>
                  <td>{localTime(item.submittedAt)}</td>
                  <td>
                    <VerificationBadge value={item.verificationStatus} />
                  </td>
                  <td>
                    <button
                      type="button"
                      className="primary"
                      style={{ padding: '6px 12px', fontSize: 12 }}
                      onClick={() => navigate?.(`/verification/kyc/${item.userId}`)}
                    >
                      Thẩm định KYC
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
