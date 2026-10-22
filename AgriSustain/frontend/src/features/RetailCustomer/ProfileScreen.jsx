import React, { useState } from 'react';
import { Notice, Field, initials, VerificationBadge } from '../../components/ui/Common';

/**
 * UC-AUTH-03: Manage Profile Screen (SCR-11)
 * Styled with agricultural design language matching Figma SCR-TRC-01
 */
export default function ProfileScreen({ fixture = 'retail-customer', navigate }) {
  const storedUser = (() => {
    try {
      const raw = sessionStorage.getItem('agrisustain:current-user');
      return raw ? JSON.parse(raw) : null;
    } catch { return null; }
  })();

  const authIdentifier = storedUser?.email || fixture;
  const isFarmer = storedUser ? storedUser.role === 'Farmer' : fixture === 'farmer';
  const roleName = isFarmer ? 'Farmer' : 'RetailCustomer';

  // State loaded according to SRS specs
  const [profile, setProfile] = useState({
    fullName: storedUser?.fullName || (isFarmer ? 'Nguyễn Văn Hùng' : 'Lê Thị Mai'),
    phoneNumber: storedUser?.phoneNumber || '0912345678',
    email: storedUser?.email || (isFarmer ? 'nongtrai.dalat@gmail.com' : 'khachle.hcm@gmail.com'),
    gender: 'Nam',
    birthDate: '1990-05-15',
    // Delivery address (for RetailCustomer)
    provinceCity: 'Hà Nội',
    district: 'Quận Cầu Giấy',
    ward: 'Phường Dịch Vọng Hậu',
    addressDetail: 'Số 18, Phố Duy Tân',
    rewardPoints: 1250,
    // Farmer specific fields
    farmName: isFarmer ? (storedUser?.fullName ? `Trang trại ${storedUser.fullName}` : 'Hợp tác xã Nông nghiệp Xanh') : '',
    representativeName: storedUser?.fullName || (isFarmer ? 'Nguyễn Văn Hùng' : ''),
    productionScale: isFarmer ? '5.2 ha (Đạt chuẩn VietGAP)' : '',
    cultivationZone: isFarmer ? 'Lâm Hà - Đà Lạt - Lâm Đồng' : '',
    businessLicenseNumber: isFarmer ? 'BL-2026-LD-889' : '',
    verificationStatus: 'NotSubmitted',
    reputationScore: 5.0,
  });

  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState(null);
  const [error, setError] = useState(null);

  function handleChange(field, value) {
    setProfile((prev) => ({ ...prev, [field]: value }));
    setNotice(null);
    setError(null);
  }

  React.useEffect(() => {
    let cancelled = false;
    async function loadProfile() {
      try {
        const res = await fetch('/api/profile', {
          headers: { Authorization: `Demo ${authIdentifier}` },
        });
        if (res.ok) {
          const data = await res.json();
          if (!cancelled && data) {
            setProfile((prev) => ({
              ...prev,
              fullName: data.fullName || prev.fullName,
              phoneNumber: data.phoneNumber || prev.phoneNumber,
              email: data.email || prev.email,
              gender: data.gender || prev.gender,
              birthDate: data.birthDate || prev.birthDate,
              rewardPoints: data.rewardPoints ?? prev.rewardPoints,
              // Address
              provinceCity: data.deliveryAddress?.provinceCity || prev.provinceCity,
              district: data.deliveryAddress?.district || prev.district,
              ward: data.deliveryAddress?.ward || prev.ward,
              addressDetail: data.deliveryAddress?.addressDetail || prev.addressDetail,
              // Farmer
              farmName: data.farmerProfile?.farmName || prev.farmName,
              representativeName: data.farmerProfile?.representativeName || prev.representativeName,
              productionScale: data.farmerProfile?.productionScale || prev.productionScale,
              cultivationZone: data.farmerProfile?.cultivationZone || prev.cultivationZone,
              businessLicenseNumber: data.farmerProfile?.businessLicenseNumber || prev.businessLicenseNumber,
              verificationStatus: data.farmerProfile?.verificationStatus || prev.verificationStatus,
              reputationScore: data.farmerProfile?.reputationScore ?? prev.reputationScore,
            }));

            // Sync loaded name to sessionStorage so Header is always in sync with DB
            try {
              const raw = sessionStorage.getItem('agrisustain:current-user');
              if (raw) {
                const u = JSON.parse(raw);
                if (data.fullName && u.fullName !== data.fullName) {
                  u.fullName = data.fullName;
                  sessionStorage.setItem('agrisustain:current-user', JSON.stringify(u));
                  window.dispatchEvent(new Event('agrisustain:user-changed'));
                }
              }
            } catch {}
          }
        }
      } catch (err) {
        console.error('Failed to load profile', err);
      }
    }
    loadProfile();
    return () => { cancelled = true; };
  }, [authIdentifier, fixture]);

  async function handleSave(e) {
    e.preventDefault();
    if (!profile.fullName.trim()) {
      setError('Họ và tên không được để trống.');
      return;
    }
    if (!profile.phoneNumber.trim()) {
      setError('Số điện thoại không được để trống.');
      return;
    }

    setSaving(true);
    setError(null);
    try {
      let endpoint = '/api/profile/consumer';
      let payload = {
        fullName: profile.fullName.trim(),
        phoneNumber: profile.phoneNumber.trim(),
        gender: profile.gender || 'Khác',
        birthDate: profile.birthDate || null,
        provinceCity: profile.provinceCity || 'Hà Nội',
        district: profile.district || 'Cầu Giấy',
        ward: profile.ward || 'Dịch Vọng Hậu',
        addressDetail: profile.addressDetail || '123 Đường Mẫu',
        provinceCode: '01',
        wardCode: '001',
      };

      if (isFarmer) {
        endpoint = '/api/profile/farmer';
        payload = {
          fullName: profile.fullName.trim(),
          phoneNumber: profile.phoneNumber.trim(),
          farmName: profile.farmName || profile.fullName,
          representativeName: profile.representativeName || profile.fullName,
          productionScale: profile.productionScale || '5.0 ha',
          cultivationZone: profile.cultivationZone || 'Vùng trồng mẫu',
          businessLicenseNumber: profile.businessLicenseNumber || 'DKKD-12345',
          address: `${profile.addressDetail || ''}, ${profile.ward || ''}, ${profile.provinceCity || ''}`,
          provinceCode: '01',
        };
      }

      const res = await fetch(endpoint, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Demo ${authIdentifier}`,
        },
        body: JSON.stringify(payload),
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error(data.title || data.message || 'Cập nhật hồ sơ thất bại.');
      }

      // Sync updated name to sessionStorage and notify Header
      try {
        const raw = sessionStorage.getItem('agrisustain:current-user');
        if (raw) {
          const u = JSON.parse(raw);
          u.fullName = profile.fullName.trim();
          sessionStorage.setItem('agrisustain:current-user', JSON.stringify(u));
          window.dispatchEvent(new Event('agrisustain:user-changed'));
        }
      } catch {}

      setSaving(false);
      setNotice('MSG13: Cập nhật thông tin hồ sơ và địa chỉ thành công vào cơ sở dữ liệu!');
    } catch (err) {
      setSaving(false);
      setError(err.message || 'Lỗi khi lưu dữ liệu.');
    }
  }

  async function handleSubmitKyc() {
    if (!confirm('Bạn có chắc chắn muốn nộp hồ sơ để thẩm định KYC (UC-VO-01) không?')) return;
    setSaving(true);
    setError(null);
    try {
      const res = await fetch('/api/profile/farmer/submit-kyc', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Demo ${authIdentifier}`,
        },
        body: JSON.stringify({
          farmName: profile.farmName || profile.fullName,
          representativeName: profile.representativeName || profile.fullName,
          address: `${profile.addressDetail || ''}, ${profile.ward || ''}, ${profile.provinceCity || ''}`,
          provinceCode: '01',
          businessLicenseNumber: profile.businessLicenseNumber || 'BL-DEMO-2026',
          productionScale: profile.productionScale || '5.0 ha',
          cultivationZone: profile.cultivationZone || 'Khu vực thử nghiệm',
          gpsCoordinates: '10.7769, 106.7009',
          farmScale: 'Trang trại vừa',
          standardCertifications: ['VietGAP', 'GlobalGAP'],
        }),
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error(data.title || data.message || 'Nộp hồ sơ KYC thất bại.');
      }

      setSaving(false);
      setProfile((prev) => ({ ...prev, verificationStatus: 'Pending' }));
      setNotice('Hồ sơ của bạn đã được chuyển vào hàng đợi duyệt KYC của Verification Officer (UC-VO-01).');
    } catch (err) {
      setSaving(false);
      setError(err.message || 'Lỗi khi nộp KYC.');
    }
  }

  return (
    <div style={{ maxWidth: 1120, margin: '0 auto' }}>
      {/* Top Banner Hero */}
      <div
        style={{
          background: 'linear-gradient(135deg, #1e6546 0%, #28744f 100%)',
          borderRadius: 16,
          padding: '28px 36px',
          color: '#ffffff',
          marginBottom: 24,
          boxShadow: '0 4px 20px rgba(30, 101, 70, 0.12)',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: 20,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 20 }}>
          <div
            style={{
              width: 72,
              height: 72,
              borderRadius: '50%',
              background: '#ffffff',
              color: '#1e6546',
              fontSize: 26,
              fontWeight: 800,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
              border: '3px solid #b7e4c7',
            }}
          >
            {initials(profile.fullName)}
          </div>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <h1 style={{ color: '#ffffff', margin: 0, fontSize: 24, fontWeight: 800 }}>
                {profile.fullName}
              </h1>
              <span
                style={{
                  background: '#d8f3dc',
                  color: '#1b4332',
                  fontSize: 11,
                  fontWeight: 700,
                  padding: '3px 10px',
                  borderRadius: 12,
                }}
              >
                {roleName}
              </span>
            </div>
            <p style={{ margin: '6px 0 0', fontSize: 13, color: '#d8f3dc' }}>
              {profile.email} · SĐT: {profile.phoneNumber}
            </p>
          </div>
        </div>

        <div style={{ display: 'flex', gap: 14, alignItems: 'center' }}>
          <div
            style={{
              background: 'rgba(255,255,255,0.15)',
              padding: '10px 16px',
              borderRadius: 12,
              textAlign: 'center',
            }}
          >
            <div style={{ fontSize: 11, color: '#b7e4c7' }}>ĐIỂM TÍCH LŨY</div>
            <div style={{ fontSize: 18, fontWeight: 800 }}>⭐ {profile.rewardPoints}</div>
          </div>

          <div
            style={{
              background: 'rgba(255,255,255,0.15)',
              padding: '10px 16px',
              borderRadius: 12,
              textAlign: 'center',
            }}
          >
            <div style={{ fontSize: 11, color: '#b7e4c7' }}>TRẠNG THÁI KYC</div>
            <div style={{ marginTop: 2 }}>
              <VerificationBadge value={profile.verificationStatus} />
            </div>
          </div>
        </div>
      </div>

      <Notice error={Boolean(error)}>{error}</Notice>
      {notice && <Notice>{notice}</Notice>}

      <div style={{ display: 'grid', gridTemplateColumns: '280px 1fr', gap: 24 }}>
        {/* Navigation / Profile Summary Card */}
        <aside className="panel padded" style={{ height: 'fit-content' }}>
          <h3 style={{ margin: '0 0 16px', fontSize: 14, color: '#1e4834', fontWeight: 700 }}>
            QUẢN LÝ TÀI KHOẢN
          </h3>
          <div style={{ display: 'grid', gap: 8 }}>
            <button
              type="button"
              style={{
                textAlign: 'left',
                border: 'none',
                background: '#edf6f0',
                color: '#1e6546',
                fontWeight: 700,
                padding: '10px 14px',
                borderRadius: 8,
              }}
            >
              👤 Thông tin cá nhân & Địa chỉ
            </button>
            <button
              type="button"
              style={{
                textAlign: 'left',
                border: 'none',
                background: 'transparent',
                color: '#52695c',
                padding: '10px 14px',
                borderRadius: 8,
              }}
              onClick={() => navigate && navigate('/orders')}
            >
              📦 Lịch sử đơn mua hàng (SCR-16)
            </button>
            <button
              type="button"
              style={{
                textAlign: 'left',
                border: 'none',
                background: 'transparent',
                color: '#52695c',
                padding: '10px 14px',
                borderRadius: 8,
              }}
              onClick={() => alert('Chức năng bảo mật đang kích hoạt xác thực hai lớp (2FA).')}
            >
              🔒 Đổi mật khẩu & Bảo mật
            </button>
          </div>

          <div style={{ borderTop: '1px solid #e0e7df', marginTop: 24, paddingTop: 16 }}>
            <div style={{ fontSize: 12, color: '#668071', marginBottom: 8 }}>
              <strong>Xác minh KYC:</strong> <VerificationBadge value={profile.verificationStatus} />
            </div>
            {isFarmer && (
              <div style={{ fontSize: 12, color: '#668071', marginBottom: 12 }}>
                <strong>Điểm uy tín nhà vườn:</strong> ⭐ {profile.reputationScore} / 5.0
              </div>
            )}

            {profile.verificationStatus === 'NotVerified' && (
              <button
                type="button"
                className="primary"
                style={{ width: '100%', marginTop: 8, background: '#1e6546' }}
                onClick={handleSubmitKyc}
                disabled={saving}
              >
                Gửi thẩm định KYC (UC-VO-01)
              </button>
            )}
          </div>
        </aside>

        {/* Right Form Card */}
        <main className="panel padded" style={{ minHeight: 'auto', padding: 32 }}>
          <form onSubmit={handleSave}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
              <div>
                <h2 style={{ margin: 0, fontSize: 18, color: '#1b4332' }}>1. Thông tin cá nhân cơ bản</h2>
                <p className="muted" style={{ fontSize: 12 }}>
                  Thông tin dùng để định danh và liên hệ giao nhận hàng trên hệ thống.
                </p>
              </div>
              <span className="count">Mã thành viên: #MB-{isFarmer ? '8892' : '1024'}</span>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
              <Field label="Họ và tên *">
                <input
                  type="text"
                  value={profile.fullName}
                  onChange={(e) => handleChange('fullName', e.target.value)}
                  disabled={saving}
                  maxLength={50}
                  required
                />
              </Field>

              <Field label="Số điện thoại di động *">
                <input
                  type="tel"
                  value={profile.phoneNumber}
                  onChange={(e) => handleChange('phoneNumber', e.target.value)}
                  disabled={saving}
                  maxLength={10}
                  required
                />
              </Field>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 16 }}>
              <Field label="Địa chỉ Email (Cố định)">
                <input
                  type="email"
                  value={profile.email}
                  disabled
                  style={{ background: '#f5f7f5', color: '#6b7d72', cursor: 'not-allowed' }}
                />
              </Field>

              <Field label="Giới tính">
                <select
                  value={profile.gender}
                  onChange={(e) => handleChange('gender', e.target.value)}
                  disabled={saving}
                >
                  <option value="Nam">Nam</option>
                  <option value="Nữ">Nữ</option>
                  <option value="Khác">Khác</option>
                </select>
              </Field>

              <Field label="Ngày sinh">
                <input
                  type="date"
                  value={profile.birthDate}
                  onChange={(e) => handleChange('birthDate', e.target.value)}
                  disabled={saving}
                />
              </Field>
            </div>

            {/* Sổ địa chỉ giao hàng */}
            <div style={{ marginTop: 28, paddingTop: 20, borderTop: '1px solid #e2eae4' }}>
              <h2 style={{ margin: '0 0 6px', fontSize: 18, color: '#1b4332' }}>2. Sổ địa chỉ nhận hàng mặc định</h2>
              <p className="muted" style={{ fontSize: 12, marginBottom: 16 }}>
                Địa chỉ này sẽ được ưu tiên tự động điền khi thanh toán đơn hàng nông sản.
              </p>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: 16 }}>
                <Field label="Tỉnh / Thành phố *">
                  <select
                    value={profile.provinceCity}
                    onChange={(e) => handleChange('provinceCity', e.target.value)}
                    disabled={saving}
                  >
                    <option value="Hà Nội">Hà Nội</option>
                    <option value="TP. Hồ Chí Minh">TP. Hồ Chí Minh</option>
                    <option value="Đà Nẵng">Đà Nẵng</option>
                    <option value="Lâm Đồng">Lâm Đồng</option>
                  </select>
                </Field>

                <Field label="Quận / Huyện *">
                  <select
                    value={profile.district}
                    onChange={(e) => handleChange('district', e.target.value)}
                    disabled={saving}
                  >
                    <option value="Quận Cầu Giấy">Quận Cầu Giấy</option>
                    <option value="Quận Đống Đa">Quận Đống Đa</option>
                    <option value="Quận Ba Đình">Quận Ba Đình</option>
                    <option value="Huyện Lâm Hà">Huyện Lâm Hà</option>
                  </select>
                </Field>

                <Field label="Phường / Xã *">
                  <select
                    value={profile.ward}
                    onChange={(e) => handleChange('ward', e.target.value)}
                    disabled={saving}
                  >
                    <option value="Phường Dịch Vọng Hậu">Phường Dịch Vọng Hậu</option>
                    <option value="Phường Yên Hòa">Phường Yên Hòa</option>
                    <option value="Xã Đạ Đờn">Xã Đạ Đờn</option>
                  </select>
                </Field>
              </div>

              <Field label="Địa chỉ chi tiết (Số nhà, tên ngõ, phố) *">
                <input
                  type="text"
                  value={profile.addressDetail}
                  onChange={(e) => handleChange('addressDetail', e.target.value)}
                  placeholder="Ví dụ: Số 18, Phố Duy Tân"
                  disabled={saving}
                  maxLength={150}
                  required
                />
              </Field>
            </div>

            {/* Thông tin dành riêng cho Farmer */}
            {isFarmer && (
              <div style={{ marginTop: 28, paddingTop: 20, borderTop: '1px solid #e2eae4' }}>
                <h2 style={{ margin: '0 0 6px', fontSize: 18, color: '#1b4332' }}>
                  3. Thông tin Nông trại & Hợp tác xã (Dành cho Farmer)
                </h2>
                <p className="muted" style={{ fontSize: 12, marginBottom: 16 }}>
                  Dữ liệu dùng để cấp mã truy xuất nguồn gốc nông sản theo quy chuẩn VietGAP.
                </p>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                  <Field label="Tên Nông trại / Hợp tác xã *">
                    <input
                      type="text"
                      value={profile.farmName}
                      onChange={(e) => handleChange('farmName', e.target.value)}
                      disabled={saving}
                    />
                  </Field>

                  <Field label="Quy mô sản xuất / Diện tích *">
                    <input
                      type="text"
                      value={profile.productionScale}
                      onChange={(e) => handleChange('productionScale', e.target.value)}
                      disabled={saving}
                    />
                  </Field>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                  <Field label="Vùng canh tác nông nghiệp">
                    <input
                      type="text"
                      value={profile.cultivationZone}
                      onChange={(e) => handleChange('cultivationZone', e.target.value)}
                      disabled={saving}
                    />
                  </Field>

                  <Field label="Số ĐKKD / Giấy chứng nhận HTX">
                    <input
                      type="text"
                      value={profile.businessLicenseNumber}
                      onChange={(e) => handleChange('businessLicenseNumber', e.target.value)}
                      disabled={saving}
                    />
                  </Field>
                </div>
              </div>
            )}

            <div style={{ marginTop: 32, display: 'flex', justifyContent: 'flex-end', gap: 12 }}>
              <button
                type="submit"
                className="primary"
                style={{
                  padding: '12px 28px',
                  borderRadius: 10,
                  fontSize: 14,
                  fontWeight: 700,
                  background: '#1e6546',
                  boxShadow: '0 4px 12px rgba(30, 101, 70, 0.2)',
                }}
                disabled={saving}
              >
                {saving ? 'Đang lưu...' : 'Lưu Thay Đổi Hồ Sơ (SCR-11)'}
              </button>
            </div>
          </form>
        </main>
      </div>
    </div>
  );
}
