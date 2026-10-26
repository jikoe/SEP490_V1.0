import React, { useState, useEffect } from 'react';
import { Notice, Field, initials, VerificationBadge, Modal } from '../../components/ui/Common';

/**
 * UC-AUTH-03: Manage Profile Screen (SCR-11)
 * Provides personal information management, delivery address book,
 * farmer production credentials, and account security.
 */
export default function ProfileScreen({ fixture = 'retail-customer', session, navigate, onProfileSaved }) {
  // 1. Synchronize user from props, sessionStorage (active auth session) or signup cache
  const activeUser = session?.user || (() => {
    try {
      const fromSession = sessionStorage.getItem('agrisustain:session');
      if (fromSession) {
        const parsed = JSON.parse(fromSession);
        if (parsed?.user) return parsed.user;
      }
      const raw = sessionStorage.getItem('agrisustain:current-user');
      return raw ? JSON.parse(raw) : null;
    } catch { return null; }
  })();

  const token = session?.accessToken || (() => {
    try {
      const s = JSON.parse(sessionStorage.getItem('agrisustain:session') || 'null');
      return s?.accessToken;
    } catch { return null; }
  })();

  const authIdentifier = activeUser?.email || fixture;
  const isFarmer = activeUser
    ? (String(activeUser.role).toLowerCase() === 'farmer')
    : (window.location.hash.includes('/farmer') || fixture === 'farmer');
  const roleName = isFarmer ? 'Farmer' : (activeUser?.role || 'RetailCustomer');

  // 2. Initial state: Read saved cache from localStorage for the active user if exists
  const [profile, setProfile] = useState(() => {
    let cached = null;
    try {
      const email = activeUser?.email;
      if (email) {
        const raw = localStorage.getItem('agrisustain:profile:' + email);
        if (raw) cached = JSON.parse(raw);
      }
    } catch {}

    return {
      fullName: cached?.fullName || activeUser?.fullName || '',
      phoneNumber: cached?.phoneNumber || activeUser?.phoneNumber || '',
      email: activeUser?.email || '',
      gender: cached?.gender || '',
      birthDate: cached?.birthDate || '',
      rewardPoints: 0,
      // Delivery address fields
      recipientName: cached?.recipientName || '',
      recipientPhone: cached?.recipientPhone || '',
      provinceCity: cached?.provinceCity || '',
      district: cached?.district || '',
      ward: cached?.ward || '',
      addressDetail: cached?.addressDetail || '',
      provinceCode: '',
      districtCode: '',
      wardCode: '',
      isDefault: Boolean(cached?.isDefault),
      // Farmer specific fields
      farmName: cached?.farmName || (isFarmer ? (activeUser?.fullName ? `Trang trại ${activeUser.fullName}` : '') : ''),
      representativeName: cached?.representativeName || activeUser?.fullName || '',
      productionScale: cached?.productionScale || '',
      cultivationZone: cached?.cultivationZone || '',
      businessLicenseNumber: cached?.businessLicenseNumber || '',
      gpsCoordinates: cached?.gpsCoordinates || '',
      farmAddress: cached?.farmAddress || '',
      verificationStatus: 'NotSubmitted',
      reputationScore: 5.0,
      rejectReason: null,
    };
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

  // Province / district / ward catalog (same source as checkout) so the saved address has real codes.
  const [provinces, setProvinces] = useState([]);
  const [districts, setDistricts] = useState([]);
  const [wards, setWards] = useState([]);

  function authHeaders() {
    return { Authorization: `Bearer ${token}` };
  }

  async function loadCatalog(path, setter) {
    try {
      const res = await fetch(path, { headers: authHeaders() });
      setter(res.ok ? await res.json() : []);
    } catch {
      setter([]);
    }
  }

  useEffect(() => {
    if (token && !isFarmer) loadCatalog('/api/profile/address/provinces', setProvinces);
  }, [token, isFarmer]);

  useEffect(() => {
    if (!profile.provinceCode) { setDistricts([]); return; }
    loadCatalog(`/api/profile/address/provinces/${encodeURIComponent(profile.provinceCode)}/districts`, setDistricts);
  }, [profile.provinceCode]);

  useEffect(() => {
    if (!profile.districtCode) { setWards([]); return; }
    loadCatalog(`/api/profile/address/districts/${encodeURIComponent(profile.districtCode)}/wards`, setWards);
  }, [profile.districtCode]);

  function selectProvince(code) {
    const item = provinces.find((x) => x.code === code);
    setProfile((prev) => ({ ...prev, provinceCode: code, provinceCity: item?.name || '',
      districtCode: '', district: '', wardCode: '', ward: '' }));
    setFieldErrors((prev) => ({ ...prev, provinceCode: '' }));
  }

  function selectDistrict(code) {
    const item = districts.find((x) => x.code === code);
    setProfile((prev) => ({ ...prev, districtCode: code, district: item?.name || '', wardCode: '', ward: '' }));
    setFieldErrors((prev) => ({ ...prev, districtCode: '' }));
  }

  function selectWard(code) {
    const item = wards.find((x) => x.code === code);
    setProfile((prev) => ({ ...prev, wardCode: code, ward: item?.name || '' }));
    setFieldErrors((prev) => ({ ...prev, wardCode: '' }));
  }

  // Server field names -> form field names.
  function mapServerErrors(errors) {
    const alias = isFarmer
      ? { address: 'farmAddress', gPSCoordinates: 'gpsCoordinates' }
      : { address: 'addressDetail' };
    const mapped = {};
    for (const [k, v] of Object.entries(errors)) {
      mapped[alias[k] || k] = Array.isArray(v) ? v.join(' ') : String(v);
    }
    return mapped;
  }

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

  // Load real user profile from backend database
  useEffect(() => {
    let cancelled = false;
    async function loadProfile() {
      try {
        const authHeader = token ? `Bearer ${token}` : `Demo ${authIdentifier}`;
        const res = await fetch('/api/profile', {
          headers: { Authorization: authHeader },
        });
        // Read cache to fill fields that backend does not store (like gender, birthDate)
        const emailKey = activeUser?.email || '';
        let cached = null;
        try {
          const raw = localStorage.getItem('agrisustain:profile:' + emailKey);
          if (raw) cached = JSON.parse(raw);
        } catch {}

        if (res.ok) {
          const data = await res.json();
          if (!cancelled && data) {
            setProfile((prev) => ({
              ...prev,
              fullName: data.fullName || cached?.fullName || activeUser?.fullName || prev.fullName,
              phoneNumber: data.phoneNumber || cached?.phoneNumber || activeUser?.phoneNumber || prev.phoneNumber,
              email: data.email || activeUser?.email || prev.email,
              gender: data.gender || cached?.gender || prev.gender || '',
              birthDate: data.birthDate ? String(data.birthDate).slice(0, 10) : (cached?.birthDate || prev.birthDate || ''),
              rewardPoints: data.rewardPoints ?? 0,
              // Address
              recipientName: data.deliveryAddress?.recipientName || cached?.recipientName || '',
              recipientPhone: data.deliveryAddress?.recipientPhone || cached?.recipientPhone || '',
              provinceCity: data.deliveryAddress?.provinceCity || cached?.provinceCity || '',
              district: data.deliveryAddress?.district || cached?.district || '',
              ward: data.deliveryAddress?.ward || cached?.ward || '',
              addressDetail: data.deliveryAddress?.addressDetail || cached?.addressDetail || '',
              provinceCode: data.deliveryAddress?.provinceCode || '',
              districtCode: data.deliveryAddress?.districtCode || '',
              wardCode: data.deliveryAddress?.wardCode || '',
              isDefault: data.deliveryAddress ? Boolean(data.deliveryAddress.isDefault) : Boolean(cached?.isDefault),
              // Farmer specific data
              farmName: data.farmerProfile?.farmName || cached?.farmName || prev.farmName,
              representativeName: data.farmerProfile?.representativeName || cached?.representativeName || prev.representativeName,
              productionScale: data.farmerProfile?.productionScale || cached?.productionScale || '',
              cultivationZone: data.farmerProfile?.cultivationZone || cached?.cultivationZone || '',
              businessLicenseNumber: data.farmerProfile?.businessLicenseNumber || cached?.businessLicenseNumber || '',
              gpsCoordinates: data.farmerProfile?.gpsCoordinates || data.farmerProfile?.gpscoordinates || cached?.gpsCoordinates || '',
              farmAddress: data.farmerProfile?.address || cached?.farmAddress || '',
              verificationStatus: data.farmerProfile?.verificationStatus || cached?.verificationStatus || 'NotSubmitted',
              reputationScore: data.farmerProfile?.reputationScore ?? 5.0,
              rejectReason: data.farmerProfile?.rejectReason || null,
            }));

            // Sync loaded name to storage so top header stays updated
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
  }, [authIdentifier, token]);

  function validateProfile() {
    const errors = {};
    if (!profile.fullName.trim()) {
      errors.fullName = 'Họ và tên không được để trống.';
    } else if (profile.fullName.trim().length < 2 || profile.fullName.trim().length > 50) {
      errors.fullName = 'Họ và tên phải từ 2 đến 50 ký tự.';
    }
    if (!profile.phoneNumber.trim()) {
      errors.phoneNumber = 'Số điện thoại không được để trống.';
    } else if (!/^0\d{9}$/.test(profile.phoneNumber.trim())) {
      errors.phoneNumber = 'Số điện thoại không hợp lệ (cần đúng 10 chữ số, bắt đầu bằng số 0).';
    }

    if (isFarmer) {
      if (!profile.farmName.trim()) {
        errors.farmName = 'Tên nông trại / hợp tác xã không được để trống.';
      }
      if (!profile.representativeName.trim()) {
        errors.representativeName = 'Tên người đại diện không được để trống.';
      }
      if (!profile.farmAddress.trim()) {
        errors.farmAddress = 'Địa chỉ nông trại không được để trống.';
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
      const authHeader = token ? `Bearer ${token}` : `Demo ${authIdentifier}`;
      let endpoint = '/api/profile/consumer';
      let payload = {
        fullName: profile.fullName.trim(),
        phoneNumber: profile.phoneNumber.trim(),
        gender: profile.gender || null,
        birthDate: profile.birthDate || null,
        provinceCity: profile.provinceCity || null,
        district: profile.district || null,
        ward: profile.ward || null,
        addressDetail: profile.addressDetail.trim() || null,
        provinceCode: profile.provinceCode || null,
        districtCode: profile.districtCode || null,
        wardCode: profile.wardCode || null,
        recipientName: profile.recipientName?.trim() || null,
        recipientPhone: profile.recipientPhone?.trim() || null,
      };

      if (isFarmer) {
        endpoint = '/api/profile/farmer';
        payload = {
          fullName: profile.fullName.trim(),
          phoneNumber: profile.phoneNumber.trim(),
          farmName: profile.farmName.trim(),
          representativeName: profile.representativeName.trim(),
          productionScale: profile.productionScale || '',
          cultivatedArea: null,
          cultivationZone: profile.cultivationZone || '',
          businessLicenseNumber: profile.businessLicenseNumber || '',
          address: profile.farmAddress.trim(),
          provinceCode: null,
          gpsCoordinates: profile.gpsCoordinates || '',
        };
      }

      const res = await fetch(endpoint, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
          Authorization: authHeader,
        },
        body: JSON.stringify(payload),
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        if (data.errors && typeof data.errors === 'object') {
          setFieldErrors(mapServerErrors(data.errors));
        }
        throw new Error(data.title || data.message || 'Cập nhật hồ sơ thất bại.');
      }

      const saved = await res.json().catch(() => null);
      const previousStatus = profile.verificationStatus;
      const newStatus = saved?.farmerProfile?.verificationStatus;
      if (newStatus) {
        setProfile((prev) => ({ ...prev, verificationStatus: newStatus }));
      }

      // Persist full profile to localStorage by email so it survives reloads (F5)
      try {
        const emailKey = profile.email || activeUser?.email;
        if (emailKey) {
          localStorage.setItem('agrisustain:profile:' + emailKey, JSON.stringify({
            fullName: profile.fullName.trim(),
            phoneNumber: profile.phoneNumber.trim(),
            gender: profile.gender || '',
            birthDate: profile.birthDate || '',
            recipientName: profile.recipientName || '',
            recipientPhone: profile.recipientPhone || '',
            provinceCity: profile.provinceCity || '',
            district: profile.district || '',
            ward: profile.ward || '',
            addressDetail: profile.addressDetail || '',
            isDefault: Boolean(profile.isDefault),
            farmName: profile.farmName || '',
            representativeName: profile.representativeName || '',
            productionScale: profile.productionScale || '',
            cultivationZone: profile.cultivationZone || '',
            businessLicenseNumber: profile.businessLicenseNumber || '',
            gpsCoordinates: profile.gpsCoordinates || '',
            farmAddress: profile.farmAddress || '',
          }));
        }
      } catch {}

      // Sync to parent session and storage
      if (onProfileSaved) {
        onProfileSaved({ fullName: profile.fullName.trim() });
      }
      try {
        const raw = sessionStorage.getItem('agrisustain:session');
        if (raw) {
          const s = JSON.parse(raw);
          if (s?.user) {
            s.user.fullName = profile.fullName.trim();
            s.user.phoneNumber = profile.phoneNumber.trim();
            sessionStorage.setItem('agrisustain:session', JSON.stringify(s));
          }
        }
        const rawUser = sessionStorage.getItem('agrisustain:current-user');
        if (rawUser) {
          const u = JSON.parse(rawUser);
          u.fullName = profile.fullName.trim();
          u.phoneNumber = profile.phoneNumber.trim();
          sessionStorage.setItem('agrisustain:current-user', JSON.stringify(u));
        }
        window.dispatchEvent(new Event('agrisustain:user-changed'));
      } catch {}

      setSaving(false);
      // MSG13, plus the re-verification notice of SRS UC-AUTH-03 AF Step 6.
      setNotice(previousStatus === 'Approved' && newStatus === 'PendingApproval'
        ? 'Cập nhật hồ sơ thành công! Bạn đã thay đổi thông tin pháp lý nên hồ sơ cần được Nhân viên xác minh duyệt lại.'
        : 'Cập nhật hồ sơ thành công!');
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
      const authHeader = token ? `Bearer ${token}` : `Demo ${authIdentifier}`;
      const res = await fetch('/api/profile/farmer/submit-kyc', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: authHeader,
        },
        body: JSON.stringify({
          notes: 'Nông hộ gửi hồ sơ thẩm định giấy phép và quy chuẩn canh tác VietGAP.',
        }),
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        if (data.errors && typeof data.errors === 'object') {
          setFieldErrors(mapServerErrors(data.errors));
        }
        throw new Error(data.title || data.message || 'Nộp hồ sơ KYC thất bại.');
      }

      const submitted = await res.json().catch(() => null);
      setSaving(false);
      setProfile((prev) => ({
        ...prev,
        verificationStatus: submitted?.farmerProfile?.verificationStatus || 'PendingApproval',
      }));
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

  async function handleChangePasswordSubmit(e) {
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
    if (!hasUpper || !hasLower) {
      setPasswordError('Mật khẩu mới phải có ít nhất 1 chữ hoa và 1 chữ thường.');
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

    try {
      const authHeader = token ? `Bearer ${token}` : `Demo ${authIdentifier}`;
      const res = await fetch('/api/auth/change-password', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: authHeader,
        },
        body: JSON.stringify({
          currentPassword: passwordForm.currentPassword,
          newPassword: passwordForm.newPassword,
          confirmPassword: passwordForm.confirmNewPassword,
        }),
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error(data.detail || data.title || data.message || 'Đổi mật khẩu thất bại.');
      }

      setPasswordError(null);
      setPasswordSuccess('Đổi mật khẩu thành công! Mật khẩu mới đã được cập nhật.');
      setTimeout(() => {
        setPasswordModalOpen(false);
      }, 1500);
    } catch (err) {
      setPasswordError(err.message || 'Lỗi khi đổi mật khẩu.');
    }
  }

  const avatarInitial = initials(profile.fullName || activeUser?.fullName || (isFarmer ? 'Farmer' : 'User'));

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
            {avatarInitial}
          </div>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <h1 style={{ color: '#ffffff', margin: 0, fontSize: 24, fontWeight: 800 }}>
                {profile.fullName || (isFarmer ? 'Tài khoản Nông hộ' : 'Khách hàng')}
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
              {profile.email || 'Chưa cập nhật email'} • SĐT: {profile.phoneNumber || 'Chưa cập nhật'}
            </p>
          </div>
        </div>

        <div style={{ display: 'flex', gap: 14, alignItems: 'center' }}>
          {isFarmer ? (
            <>
              <div
                style={{
                  background: 'rgba(255,255,255,0.15)',
                  padding: '10px 16px',
                  borderRadius: 12,
                  textAlign: 'center',
                }}
              >
                <div style={{ fontSize: 11, color: '#b7e4c7' }}>ĐIỂM UY TÍN NHÀ VƯỜN</div>
                <div style={{ fontSize: 18, fontWeight: 800 }}>⭐ {profile.reputationScore || 5.0} / 5.0</div>
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
            </>
          ) : (
            <>
              <div
                style={{
                  background: 'rgba(255,255,255,0.15)',
                  padding: '10px 16px',
                  borderRadius: 12,
                  textAlign: 'center',
                }}
              >
                <div style={{ fontSize: 11, color: '#b7e4c7' }}>ĐIỂM TÍCH LŨY</div>
                <div style={{ fontSize: 18, fontWeight: 800 }}>⭐ {profile.rewardPoints || 0}</div>
              </div>

              <div
                style={{
                  background: 'rgba(255,255,255,0.15)',
                  padding: '10px 16px',
                  borderRadius: 12,
                  textAlign: 'center',
                }}
              >
                <div style={{ fontSize: 11, color: '#b7e4c7' }}>TRẠNG THÁI TÀI KHOẢN</div>
                <div style={{ marginTop: 4, fontSize: 13, fontWeight: 700, color: '#ffffff' }}>
                  ✓ Đã kích hoạt
                </div>
              </div>
            </>
          )}
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
              👤 {isFarmer ? 'Hồ sơ nông hộ & KYC' : 'Thông tin cá nhân & Địa chỉ'}
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
                const target = isFarmer ? '/farmer/orders' : '/orders';
                if (navigate) navigate(target);
                else window.location.hash = '#' + target;
              }}
            >
              📦 {isFarmer ? 'Đơn hàng của nông trại' : 'Lịch sử đơn mua hàng'}
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

          {isFarmer && (
            <div style={{ borderTop: '1px solid #e0e7df', marginTop: 24, paddingTop: 16 }}>
              <div style={{ fontSize: 12, color: '#668071', marginBottom: 8 }}>
                <strong>Xác minh KYC:</strong> <VerificationBadge value={profile.verificationStatus} />
              </div>
              <div style={{ fontSize: 12, color: '#668071', marginBottom: 12 }}>
                <strong>Điểm uy tín nhà vườn:</strong> ⭐ {profile.reputationScore || 5.0} / 5.0
              </div>

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
          )}
        </aside>

        {/* Right Form Card */}
        <main className="panel padded" style={{ minHeight: 'auto', padding: 32 }}>
          <form onSubmit={handleSave} noValidate>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
              <div>
                <h2 style={{ margin: 0, fontSize: 18, color: '#1b4332' }}>1. Thông tin cá nhân cơ bản</h2>
                <p className="muted" style={{ fontSize: 12 }}>
                  Thông tin dùng để định danh và liên hệ trên hệ thống.
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
                  placeholder="Nhập họ và tên"
                  disabled={saving}
                  maxLength={50}
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
                  placeholder="Ví dụ: 0912345678"
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
                  <option value="">-- Chọn giới tính --</option>
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

            {/* Sổ địa chỉ giao hàng (Hiển thị cho RetailCustomer, mặc định để trống cho người dùng mới) */}
            {!isFarmer && (
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
                      maxLength={50}
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
                  <Field label="Tỉnh / Thành phố" error={fieldErrors.provinceCode}>
                    <select
                      value={profile.provinceCode}
                      onChange={(e) => selectProvince(e.target.value)}
                      disabled={saving || provinces.length === 0}
                    >
                      <option value="">-- Chọn Tỉnh / Thành phố --</option>
                      {provinces.map((item) => <option key={item.code} value={item.code}>{item.name}</option>)}
                    </select>
                  </Field>

                  <Field label="Quận / Huyện" error={fieldErrors.districtCode}>
                    <select
                      value={profile.districtCode}
                      onChange={(e) => selectDistrict(e.target.value)}
                      disabled={saving || !profile.provinceCode || districts.length === 0}
                    >
                      <option value="">-- Chọn Quận / Huyện --</option>
                      {districts.map((item) => <option key={item.code} value={item.code}>{item.name}</option>)}
                    </select>
                  </Field>

                  <Field label="Phường / Xã" error={fieldErrors.wardCode}>
                    <select
                      value={profile.wardCode}
                      onChange={(e) => selectWard(e.target.value)}
                      disabled={saving || !profile.districtCode || wards.length === 0}
                    >
                      <option value="">-- Chọn Phường / Xã --</option>
                      {wards.map((item) => <option key={item.code} value={item.code}>{item.name}</option>)}
                    </select>
                  </Field>
                </div>

                <Field
                  label="Địa chỉ chi tiết (Số nhà, tên ngõ, phố)"
                  error={fieldErrors.addressDetail}
                >
                  <input
                    type="text"
                    value={profile.addressDetail}
                    onChange={(e) => handleChange('addressDetail', e.target.value)}
                    placeholder="Ví dụ: Số 18, Phố Duy Tân"
                    disabled={saving}
                    maxLength={150}
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
            )}

            {/* Thông tin dành riêng cho Farmer (Hồ sơ nông hộ & KYC) */}
            {isFarmer && (
              <div style={{ marginTop: 28, paddingTop: 20, borderTop: '1px solid #e2eae4' }}>
                <h2 style={{ margin: '0 0 6px', fontSize: 18, color: '#1b4332' }}>
                  2. Thông tin Nông trại & Hợp tác xã (Hồ sơ nông hộ)
                </h2>
                <p className="muted" style={{ fontSize: 12, marginBottom: 16 }}>
                  Dữ liệu dùng để cấp mã truy xuất nguồn gốc nông sản và thẩm định hồ sơ VietGAP.
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
                      placeholder="Ví dụ: Trang trại Nông sản Đà Lạt"
                      disabled={saving}
                      maxLength={100}
                      required
                    />
                  </Field>

                  <Field
                    label="Tên người đại diện pháp luật *"
                    error={fieldErrors.representativeName}
                  >
                    <input
                      type="text"
                      value={profile.representativeName}
                      onChange={(e) => handleChange('representativeName', e.target.value)}
                      placeholder="Họ và tên chủ hộ / người đại diện"
                      disabled={saving}
                      maxLength={50}
                      required
                    />
                  </Field>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                  <Field label="Quy mô canh tác">
                    <input
                      type="text"
                      value={profile.productionScale}
                      onChange={(e) => handleChange('productionScale', e.target.value)}
                      placeholder="Ví dụ: 3.5 ha canh tác nhà kính"
                      disabled={saving}
                      maxLength={100}
                    />
                  </Field>

                  <Field label="Vùng canh tác trọng điểm *" error={fieldErrors.cultivationZone}>
                    <input
                      type="text"
                      value={profile.cultivationZone}
                      onChange={(e) => handleChange('cultivationZone', e.target.value)}
                      placeholder="Ví dụ: Lâm Hà - Đà Lạt - Lâm Đồng"
                      disabled={saving}
                      maxLength={150}
                    />
                  </Field>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
                  <Field label="Số ĐKKD / Mã số nông hộ *" error={fieldErrors.businessLicenseNumber}>
                    <input
                      type="text"
                      value={profile.businessLicenseNumber}
                      onChange={(e) => handleChange('businessLicenseNumber', e.target.value)}
                      placeholder="Ví dụ: BL-2026-LD-889"
                      disabled={saving}
                      maxLength={100}
                    />
                  </Field>

                  <Field label="Tọa độ GPS vườn (Vĩ độ, Kinh độ)" error={fieldErrors.gpsCoordinates}>
                    <input
                      type="text"
                      value={profile.gpsCoordinates}
                      onChange={(e) => handleChange('gpsCoordinates', e.target.value)}
                      placeholder="Ví dụ: 11.9404, 108.4583"
                      disabled={saving}
                      maxLength={100}
                    />
                  </Field>
                </div>

                <Field label="Địa chỉ nông trại canh tác *" error={fieldErrors.farmAddress}>
                  <input
                    type="text"
                    value={profile.farmAddress}
                    onChange={(e) => handleChange('farmAddress', e.target.value)}
                    placeholder="Ví dụ: Thôn 4, Xã Đạ Đờn, Huyện Lâm Hà, Tỉnh Lâm Đồng"
                    disabled={saving}
                    maxLength={200}
                  />
                </Field>
              </div>
            )}

            <div style={{ marginTop: 28, display: 'flex', justifyContent: 'flex-end', gap: 12 }}>
              <button
                type="submit"
                className="primary"
                style={{
                  background: '#1e6546',
                  color: '#ffffff',
                  padding: '12px 28px',
                  borderRadius: 8,
                  fontWeight: 700,
                  fontSize: 14,
                  border: 'none',
                  cursor: 'pointer',
                  boxShadow: '0 4px 12px rgba(30,101,70,0.2)',
                }}
                disabled={saving}
              >
                {saving ? 'Đang lưu...' : 'Lưu Thay Đổi Hồ Sơ'}
              </button>
            </div>
          </form>
        </main>
      </div>

      {/* Modal Change Password */}
      {passwordModalOpen && (
      <Modal
        onClose={() => setPasswordModalOpen(false)}
        title="Đổi Mật Khẩu & Bảo Mật"
      >
        <form onSubmit={handleChangePasswordSubmit}>
          <Notice error={Boolean(passwordError)}>{passwordError}</Notice>
          {passwordSuccess && <Notice>{passwordSuccess}</Notice>}

          <Field label="Mật khẩu hiện tại *">
            <input
              type="password"
              value={passwordForm.currentPassword}
              onChange={(e) => setPasswordForm((prev) => ({ ...prev, currentPassword: e.target.value }))}
              placeholder="Nhập mật khẩu hiện tại"
              required
            />
          </Field>

          <Field
            label="Mật khẩu mới *"
            hint="Tối thiểu 8 ký tự, gồm ít nhất 1 chữ hoa, 1 chữ thường và 1 chữ số."
          >
            <input
              type="password"
              value={passwordForm.newPassword}
              onChange={(e) => setPasswordForm((prev) => ({ ...prev, newPassword: e.target.value }))}
              placeholder="Nhập mật khẩu mới"
              required
            />
          </Field>

          <Field label="Xác nhận mật khẩu mới *">
            <input
              type="password"
              value={passwordForm.confirmNewPassword}
              onChange={(e) => setPasswordForm((prev) => ({ ...prev, confirmNewPassword: e.target.value }))}
              placeholder="Nhập lại mật khẩu mới chính xác"
              required
            />
          </Field>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10, marginTop: 20 }}>
            <button
              type="button"
              onClick={() => setPasswordModalOpen(false)}
              style={{
                background: '#e0e7df',
                border: 'none',
                padding: '10px 18px',
                borderRadius: 8,
                fontWeight: 600,
                cursor: 'pointer',
              }}
            >
              Hủy
            </button>
            <button
              type="submit"
              className="primary"
              style={{
                background: '#1e6546',
                color: '#ffffff',
                border: 'none',
                padding: '10px 20px',
                borderRadius: 8,
                fontWeight: 700,
                cursor: 'pointer',
              }}
            >
              Cập Nhật Mật Khẩu
            </button>
          </div>
        </form>
      </Modal>
      )}
    </div>
  );
}
