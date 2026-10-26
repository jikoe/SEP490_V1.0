import React, { useState, useEffect } from 'react';
import { Notice, Field, initials, VerificationBadge, Modal } from '../../components/ui/Common';

/**
 * Manage Profile Screen
 * Provides personal information management, delivery address book,
 * farmer production credentials, and account security.
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

  const [profile, setProfile] = useState({
    fullName: storedUser?.fullName || (isFarmer ? 'Nguyễn Văn Hùng' : 'Lê Thị Mai'),
    phoneNumber: storedUser?.phoneNumber || '0912345678',
    email: storedUser?.email || (isFarmer ? 'nongtrai.dalat@gmail.com' : 'khachle.hcm@gmail.com'),
    gender: 'Nam',
    birthDate: '1990-05-15',
    rewardPoints: 1250,
    // Delivery address fields
    recipientName: storedUser?.fullName || (isFarmer ? 'Nguyễn Văn Hùng' : 'Lê Thị Mai'),
    recipientPhone: storedUser?.phoneNumber || '0912345678',
    provinceCity: 'Hà Nội',
    district: 'Quận Cầu Giấy',
    ward: 'Phường Dịch Vọng Hậu',
    addressDetail: 'Số 18, Phố Duy Tân',
    isDefault: true,
    // Farmer specific fields
    farmName: isFarmer ? (storedUser?.fullName ? `Trang trại ${storedUser.fullName}` : 'Hợp tác xã Nông nghiệp Xanh') : '',
    representativeName: storedUser?.fullName || (isFarmer ? 'Nguyễn Văn Hùng' : ''),
    productionScale: isFarmer ? '5.2 ha (Đạt chuẩn VietGAP)' : '',
    cultivationZone: isFarmer ? 'Lâm Hà - Đà Lạt - Lâm Đồng' : '',
    businessLicenseNumber: isFarmer ? 'BL-2026-LD-889' : '',
    gpsCoordinates: isFarmer ? '11.9404, 108.4583' : '',
    farmAddress: isFarmer ? 'Xã Đạ Đờn, Huyện Lâm Hà, Tỉnh Lâm Đồng' : '',
    verificationStatus: 'NotSubmitted',
    reputationScore: 5.0,
    rejectReason: null,
  });

  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState(null);
  const [error, setError] = useState(null);
  const [fieldErrors, setFieldErrors] = useState({});
  const [phoneNotice, setPhoneNotice] = useState(null);
  const [recipientPhoneNotice, setRecipientPhoneNotice] = useState(null);

  // Change password modal state
  const [passwordModalOpen, setPasswordModalOpen] = useState(false);
  const [passwordForm, setPasswordForm] = useState({
    currentPassword: '',
    newPassword: '',
    confirmNewPassword: '',
  });
  const [passwordError, setPasswordError] = useState(null);
  const [passwordSuccess, setPasswordSuccess] = useState(null);

  function handleChange(field, value) {
    setProfile((prev) => ({ ...prev, [field]: value }));
    setFieldErrors((prev) => ({ ...prev, [field]: '' }));
    setNotice(null);
    setError(null);
  }

  function handlePhoneChange(field, e) {
    const raw = e.target.value;
    const digitsOnly = raw.replace(/\D/g, '');
    const setNoticeFn = field === 'phoneNumber' ? setPhoneNotice : setRecipientPhoneNotice;
    
    if (digitsOnly.length > 10) {
      const truncated = digitsOnly.slice(0, 10);
      setProfile((prev) => ({ ...prev, [field]: truncated }));
      setNoticeFn('Số điện thoại chỉ lấy 10 số đầu. Các số sau tự động bị xóa.');
    } else {
      setProfile((prev) => ({ ...prev, [field]: digitsOnly }));
      if (digitsOnly.length === 10) {
        setNoticeFn('Đã nhập đủ 10 chữ số hợp lệ.');
      } else {
        setNoticeFn(null);
      }
    }
    setFieldErrors((prev) => ({ ...prev, [field]: '' }));
    setError(null);
  }

  useEffect(() => {
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
              birthDate: data.birthDate ? String(data.birthDate) : prev.birthDate,
              rewardPoints: data.rewardPoints ?? prev.rewardPoints,
              // Address
              recipientName: data.deliveryAddress?.recipientName || data.fullName || prev.recipientName,
              recipientPhone: data.deliveryAddress?.recipientPhone || data.phoneNumber || prev.recipientPhone,
              provinceCity: data.deliveryAddress?.provinceCity || prev.provinceCity,
              district: data.deliveryAddress?.district || prev.district,
              ward: data.deliveryAddress?.ward || prev.ward,
              addressDetail: data.deliveryAddress?.addressDetail || prev.addressDetail,
              isDefault: data.deliveryAddress?.isDefault ?? prev.isDefault,
              // Farmer
              farmName: data.farmerProfile?.farmName || prev.farmName,
              representativeName: data.farmerProfile?.representativeName || prev.representativeName,
              productionScale: data.farmerProfile?.productionScale || prev.productionScale,
              cultivationZone: data.farmerProfile?.cultivationZone || prev.cultivationZone,
              businessLicenseNumber: data.farmerProfile?.businessLicenseNumber || prev.businessLicenseNumber,
              gpsCoordinates: data.farmerProfile?.gpsCoordinates || data.farmerProfile?.gpscoordinates || prev.gpsCoordinates,
              farmAddress: data.farmerProfile?.address || prev.farmAddress,
              verificationStatus: data.farmerProfile?.verificationStatus || prev.verificationStatus,
              reputationScore: data.farmerProfile?.reputationScore ?? prev.reputationScore,
              rejectReason: data.farmerProfile?.rejectReason || null,
            }));

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

  function validateProfile() {
    const errors = {};
    if (!profile.fullName.trim()) {
      errors.fullName = 'Họ và tên không được để trống.';
    }
    if (!profile.phoneNumber.trim()) {
      errors.phoneNumber = 'Số điện thoại không được để trống.';
    } else if (!/^0\d{9}$/.test(profile.phoneNumber.trim())) {
      errors.phoneNumber = 'Số điện thoại không hợp lệ (cần đúng 10 chữ số, bắt đầu bằng số 0).';
    }

    if (!profile.addressDetail.trim()) {
      errors.addressDetail = 'Địa chỉ chi tiết không được để trống.';
    }

    if (isFarmer) {
      if (!profile.farmName.trim()) {
        errors.farmName = 'Tên nông trại / hợp tác xã không được để trống.';
      }
      if (!profile.representativeName.trim()) {
        errors.representativeName = 'Tên người đại diện không được để trống.';
      }
    }

    return errors;
  }

  async function handleSave(e) {
    e.preventDefault();
    const errors = validateProfile();
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      setError('Vui lòng kiểm tra lại các trường thông tin bị lỗi bên dưới.');
      return;
    }

    setSaving(true);
    setError(null);
    setFieldErrors({});

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
        addressDetail: profile.addressDetail.trim(),
        provinceCode: '01',
        wardCode: '001',
      };

      if (isFarmer) {
        endpoint = '/api/profile/farmer';
        payload = {
          fullName: profile.fullName.trim(),
          phoneNumber: profile.phoneNumber.trim(),
          farmName: profile.farmName.trim(),
          representativeName: profile.representativeName.trim(),
          productionScale: profile.productionScale || '5.0 ha',
          cultivationZone: profile.cultivationZone || 'Vùng trồng nông sản VietGAP',
          businessLicenseNumber: profile.businessLicenseNumber || 'DKKD-12345',
          address: profile.farmAddress || `${profile.addressDetail}, ${profile.ward}, ${profile.provinceCity}`,
          provinceCode: '01',
          gpsCoordinates: profile.gpsCoordinates || '11.9404, 108.4583',
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
        if (data.errors && typeof data.errors === 'object') {
          const sErrors = {};
          for (const [k, v] of Object.entries(data.errors)) {
            sErrors[k] = Array.isArray(v) ? v.join(' ') : String(v);
          }
          setFieldErrors(sErrors);
        }
        throw new Error(data.title || data.message || 'Cập nhật hồ sơ thất bại.');
      }

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
      setNotice('Cập nhật thông tin hồ sơ và địa chỉ thành công vào cơ sở dữ liệu!');
    } catch (err) {
      setSaving(false);
      setError(err.message || 'Lỗi khi lưu dữ liệu.');
    }
  }

  async function handleSubmitKyc() {
    if (!confirm('Bạn có chắc chắn muốn nộp hồ sơ để thẩm định KYC không?')) return;
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
          notes: 'Nông hộ gửi hồ sơ thẩm định giấy phép và quy chuẩn canh tác VietGAP.',
        }),
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error(data.title || data.message || 'Nộp hồ sơ KYC thất bại.');
      }

      setSaving(false);
      setProfile((prev) => ({ ...prev, verificationStatus: 'Pending' }));
      setNotice('Hồ sơ của bạn đã được chuyển vào hàng đợi duyệt KYC của Nhân viên xác minh.');
    } catch (err) {
      setSaving(false);
      setError(err.message || 'Lỗi khi nộp KYC.');
    }
  }

  function handleOpenPasswordModal() {
    setPasswordForm({ currentPassword: '', newPassword: '', confirmNewPassword: '' });
    setPasswordError(null);
    setPasswordSuccess(null);
    setPasswordModalOpen(true);
  }

  function handleChangePasswordSubmit(e) {
    e.preventDefault();
    if (!passwordForm.currentPassword) {
      setPasswordError('Vui lòng nhập mật khẩu hiện tại.');
      return;
    }
    if (passwordForm.newPassword.length < 8) {
      setPasswordError('Mật khẩu mới phải có ít nhất 8 ký tự.');
      return;
    }
    const hasUpper = /[A-Z]/.test(passwordForm.newPassword);
    const hasLower = /[a-z]/.test(passwordForm.newPassword);
    if (!hasUpper && !hasLower) {
      setPasswordError('Mật khẩu phải có ít nhất 1 chữ hoa. Mật khẩu phải có ít nhất 1 chữ thường.');
      return;
    } else if (!hasUpper) {
      setPasswordError('Mật khẩu phải có ít nhất 1 chữ hoa.');
      return;
    } else if (!hasLower) {
      setPasswordError('Mật khẩu phải có ít nhất 1 chữ thường.');
      return;
    }
    if (!/[0-9]/.test(passwordForm.newPassword)) {
      setPasswordError('Mật khẩu phải có ít nhất 1 chữ số.');
      return;
    }
    if (passwordForm.newPassword !== passwordForm.confirmNewPassword) {
      setPasswordError('Mật khẩu xác nhận không khớp.');
      return;
    }

    setPasswordError(null);
    setPasswordSuccess('Đổi mật khẩu thành công! Mật khẩu mới đã được kích hoạt.');
    setTimeout(() => {
      setPasswordModalOpen(false);
    }, 1500);
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
              {profile.email} • SĐT: {profile.phoneNumber}
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

      {/* Reject Reason Warning banner if KYC is Rejected */}
      {profile.rejectReason && (
        <div style={{ background: '#fef2f2', border: '1px solid #fecaca', borderRadius: 12, padding: '14px 20px', marginBottom: 20, color: '#991b1b' }}>
          <strong>Lý do từ chối thẩm định trước đó:</strong> {profile.rejectReason}.
          <div style={{ fontSize: 13, marginTop: 4 }}>Vui lòng kiểm tra lại thông tin nông trại và nộp lại hồ sơ thẩm định.</div>
        </div>
      )}

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
                cursor: 'pointer',
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
                cursor: 'pointer',
              }}
              onClick={() => {
                if (navigate) navigate('/orders');
                else window.location.hash = '#/orders';
              }}
            >
              📦 Lịch sử đơn mua hàng
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
                cursor: 'pointer',
              }}
              onClick={handleOpenPasswordModal}
            >
              🔑 Đổi mật khẩu & Bảo mật
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

            {(profile.verificationStatus === 'NotSubmitted' || profile.verificationStatus === 'Rejected') && (
              <button
                type="button"
                className="primary"
                style={{ width: '100%', marginTop: 8, background: '#1e6546', borderRadius: 8, padding: '10px 14px', fontWeight: 600 }}
                onClick={handleSubmitKyc}
                disabled={saving}
              >
                Gửi thẩm định KYC
              </button>
            )}
          </div>
        </aside>

        {/* Right Form Card */}
        <main className="panel padded" style={{ minHeight: 'auto', padding: 32 }}>
          <form onSubmit={handleSave} noValidate>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
              <div>
                <h2 style={{ margin: 0, fontSize: 18, color: '#1b4332' }}>1. Thông tin cá nhân cơ bản</h2>
                <p className="muted" style={{ fontSize: 12 }}>
                  Thông tin dùng để định danh và liên hệ giao nhận hàng trên hệ thống.
                </p>
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
              <Field
                label="Họ và tên người dùng *"
                error={fieldErrors.fullName}
              >
                <input
                  type="text"
                  value={profile.fullName}
                  onChange={(e) => handleChange('fullName', e.target.value)}
                  disabled={saving}
                  maxLength={150}
                  required
                />
              </Field>

              <Field
                label="Số điện thoại di động *"
                error={fieldErrors.phoneNumber}
                hint={phoneNotice}
              >
                <input
                  type="tel"
                  value={profile.phoneNumber}
                  onChange={(e) => handlePhoneChange('phoneNumber', e)}
                  disabled={saving}
                  maxLength={11}
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
              <h2 style={{ margin: '0 0 6px', fontSize: 18, color: '#1b4332' }}>
                2. Sổ địa chỉ nhận hàng mặc định
              </h2>
              <p className="muted" style={{ fontSize: 12, marginBottom: 16 }}>
                Địa chỉ này sẽ được ưu tiên tự động điền khi thanh toán đơn hàng nông sản.
              </p>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16, marginBottom: 16 }}>
                <Field label="Họ tên người nhận">
                  <input
                    type="text"
                    value={profile.recipientName}
                    onChange={(e) => handleChange('recipientName', e.target.value)}
                    placeholder="Mặc định theo họ tên tài khoản"
                    disabled={saving}
                    maxLength={150}
                  />
                </Field>

                <Field
                  label="Số điện thoại người nhận"
                  hint={recipientPhoneNotice}
                >
                  <input
                    type="tel"
                    value={profile.recipientPhone}
                    onChange={(e) => handlePhoneChange('recipientPhone', e)}
                    placeholder="Mặc định theo SĐT tài khoản"
                    disabled={saving}
                    maxLength={11}
                  />
                </Field>
              </div>

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
                    <option value="Cần Thơ">Cần Thơ</option>
                    <option value="Đồng Tháp">Đồng Tháp</option>
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
                    <option value="TP. Đà Lạt">TP. Đà Lạt</option>
                    <option value="Quận 1">Quận 1</option>
                    <option value="Quận 7">Quận 7</option>
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
                    <option value="Phường Bến Nghé">Phường Bến Nghé</option>
                    <option value="Phường Tân Phong">Phường Tân Phong</option>
                  </select>
                </Field>
              </div>

              <Field
                label="Địa chỉ chi tiết (Số nhà, tên ngõ, phố) *"
                error={fieldErrors.addressDetail}
              >
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

              <div style={{ marginTop: 10 }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: 8, cursor: 'pointer', fontSize: 13, color: '#2d4d3c' }}>
                  <input
                    type="checkbox"
                    checked={profile.isDefault}
                    onChange={(e) => handleChange('isDefault', e.target.checked)}
                    style={{ accentColor: '#1e6546' }}
                    disabled={saving}
                  />
                  <span>Đặt làm địa chỉ nhận hàng mặc định</span>
                </label>
              </div>
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
                  <Field
                    label="Tên Nông trại / Hợp tác xã *"
                    error={fieldErrors.farmName}
                  >
                    <input
                      type="text"
                      value={profile.farmName}
                      onChange={(e) => handleChange('farmName', e.target.value)}
                      disabled={saving}
                      placeholder="Ví dụ: Hợp tác xã Nông nghiệp Xanh Đà Lạt"
                    />
                  </Field>

                  <Field
                    label="Người đại diện pháp lý *"
                    error={fieldErrors.representativeName}
                  >
                    <input
                      type="text"
                      value={profile.representativeName}
                      onChange={(e) => handleChange('representativeName', e.target.value)}
                      disabled={saving}
                      placeholder="Ví dụ: Nguyễn Văn Hùng"
                    />
                  </Field>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                  <Field label="Quy mô sản xuất / Diện tích">
                    <input
                      type="text"
                      value={profile.productionScale}
                      onChange={(e) => handleChange('productionScale', e.target.value)}
                      disabled={saving}
                      placeholder="Ví dụ: 5.2 ha (Đạt chuẩn VietGAP)"
                    />
                  </Field>

                  <Field label="Vùng canh tác nông nghiệp">
                    <input
                      type="text"
                      value={profile.cultivationZone}
                      onChange={(e) => handleChange('cultivationZone', e.target.value)}
                      disabled={saving}
                      placeholder="Ví dụ: Lâm Hà - Đà Lạt - Lâm Đồng"
                    />
                  </Field>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                  <Field label="Số ĐKKD / Giấy chứng nhận HTX">
                    <input
                      type="text"
                      value={profile.businessLicenseNumber}
                      onChange={(e) => handleChange('businessLicenseNumber', e.target.value)}
                      disabled={saving}
                      placeholder="Ví dụ: BL-2026-LD-889"
                    />
                  </Field>

                  <Field label="Tọa độ GPS cơ sở (Kinh độ, Vĩ độ)">
                    <input
                      type="text"
                      value={profile.gpsCoordinates}
                      onChange={(e) => handleChange('gpsCoordinates', e.target.value)}
                      disabled={saving}
                      placeholder="Ví dụ: 11.9404, 108.4583"
                    />
                  </Field>
                </div>

                <Field label="Địa chỉ cơ sở / nông trại">
                  <input
                    type="text"
                    value={profile.farmAddress}
                    onChange={(e) => handleChange('farmAddress', e.target.value)}
                    disabled={saving}
                    placeholder="Ví dụ: Thôn 3, Xã Đạ Đờn, Huyện Lâm Hà, Tỉnh Lâm Đồng"
                  />
                </Field>
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
                {saving ? 'Đang lưu...' : 'Lưu Thay Đổi Hồ Sơ'}
              </button>
            </div>
          </form>
        </main>
      </div>

      {/* Modal Đổi mật khẩu & Bảo mật */}
      {passwordModalOpen && (
        <Modal
          onClose={() => setPasswordModalOpen(false)}
          title="Đổi mật khẩu tài khoản"
        >
          <div style={{ padding: '8px 0 16px' }}>
            <p style={{ margin: '0 0 16px', fontSize: 13, color: '#557262' }}>
              Để bảo vệ tài khoản, mật khẩu mới cần có tối thiểu 8 ký tự gồm cả chữ hoa, chữ thường và chữ số.
            </p>

            {passwordError && <Notice error>{passwordError}</Notice>}
            {passwordSuccess && <Notice>{passwordSuccess}</Notice>}

            <form onSubmit={handleChangePasswordSubmit}>
              <div style={{ display: 'grid', gap: 14 }}>
                <Field label="Mật khẩu hiện tại *">
                  <input
                    type="password"
                    placeholder="Nhập mật khẩu hiện tại"
                    value={passwordForm.currentPassword}
                    onChange={(e) => setPasswordForm((p) => ({ ...p, currentPassword: e.target.value }))}
                    required
                  />
                </Field>

                <Field
                  label="Mật khẩu mới *"
                  hint="Ít nhất 8 ký tự, gồm ít nhất 1 chữ hoa, 1 chữ thường và 1 chữ số"
                >
                  <input
                    type="password"
                    placeholder="Nhập mật khẩu mới"
                    value={passwordForm.newPassword}
                    onChange={(e) => setPasswordForm((p) => ({ ...p, newPassword: e.target.value }))}
                    required
                  />
                </Field>

                <Field label="Xác nhận mật khẩu mới *">
                  <input
                    type="password"
                    placeholder="Nhập lại mật khẩu mới"
                    value={passwordForm.confirmNewPassword}
                    onChange={(e) => setPasswordForm((p) => ({ ...p, confirmNewPassword: e.target.value }))}
                    required
                  />
                </Field>
              </div>

              <div className="modal-actions" style={{ marginTop: 24 }}>
                <button
                  type="button"
                  onClick={() => setPasswordModalOpen(false)}
                  style={{ padding: '10px 18px', borderRadius: 8 }}
                >
                  Hủy bỏ
                </button>
                <button
                  type="submit"
                  className="primary"
                  style={{ background: '#1e6546', padding: '10px 22px', borderRadius: 8, fontWeight: 700 }}
                >
                  Cập nhật mật khẩu
                </button>
              </div>
            </form>
          </div>
        </Modal>
      )}
    </div>
  );
}
