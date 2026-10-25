import React, { useEffect, useState } from 'react';
import { request } from '../../api/client';
import { Notice, Field, initials, VerificationBadge } from '../../components/ui/Common';

const sectionStyle = { marginTop: 28, paddingTop: 20, borderTop: '1px solid #e2eae4' };
const twoColumns = { display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 };
const sideLink = {
  textAlign: 'left', border: 'none', background: 'transparent', color: '#52695c',
  padding: '10px 14px', borderRadius: 8,
};

function toForm(profile) {
  const address = profile.deliveryAddress;
  const farm = profile.farmerProfile;
  return {
    fullName: profile.fullName || '',
    phoneNumber: profile.phoneNumber || '',
    provinceCode: address?.provinceCode || '',
    districtCode: address?.districtCode || '',
    wardCode: address?.wardCode || '',
    addressDetail: address?.addressDetail || '',
    farmName: farm?.farmName || '',
    representativeName: farm?.representativeName || '',
    farmAddress: farm?.address || '',
    productionScale: farm?.productionScale || '',
    cultivationZone: farm?.cultivationZone || '',
    businessLicenseNumber: farm?.businessLicenseNumber || '',
    gpsCoordinates: farm?.gpsCoordinates || '',
  };
}

/**
 * UC-AUTH-03: Manage Profile Screen (SCR-11). Reads and saves the signed-in account through
 * /api/profile; the RetailCustomer delivery address uses the checkout province/district/ward catalog so it
 * can prefill UC-RC-02.
 */
export default function ProfileScreen({ session, navigate, onProfileSaved }) {
  const [profile, setProfile] = useState(null);
  const [form, setForm] = useState(null);
  const [provinces, setProvinces] = useState([]);
  const [districts, setDistricts] = useState([]);
  const [wards, setWards] = useState([]);
  const [loading, setLoading] = useState(Boolean(session));
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState('');
  const [error, setError] = useState('');

  const role = profile?.role || session?.user.role;
  const isFarmer = role === 'Farmer';
  const isRetail = role === 'RetailCustomer';
  const farm = profile?.farmerProfile;

  useEffect(() => {
    if (!session) return undefined;
    const controller = new AbortController();
    // A catalog failure only disables the address picker; the profile itself still loads.
    const catalog = session.user.role === 'RetailCustomer'
      ? request('/checkout/provinces', { signal: controller.signal }).catch((failure) => {
        if (failure.name !== 'AbortError') setError(failure.message);
        return [];
      }) : Promise.resolve([]);
    Promise.all([request('/profile', { signal: controller.signal }), catalog])
      .then(([data, provinceOptions]) => {
        setProfile(data); setForm(toForm(data)); setProvinces(provinceOptions);
      })
      .catch((failure) => { if (failure.name !== 'AbortError') setError(failure.message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
    // Reload only for a different login, not when the saved name is copied into the session.
  }, [session?.accessToken]);

  useEffect(() => {
    const provinceCode = form?.provinceCode;
    setDistricts([]);
    if (!isRetail || !provinceCode) return undefined;
    const controller = new AbortController();
    request(`/checkout/provinces/${encodeURIComponent(provinceCode)}/districts`, { signal: controller.signal })
      .then(setDistricts)
      .catch((failure) => { if (failure.name !== 'AbortError') setError(failure.message); });
    return () => controller.abort();
  }, [isRetail, form?.provinceCode]);

  useEffect(() => {
    const districtCode = form?.districtCode;
    setWards([]);
    if (!isRetail || !districtCode) return undefined;
    const controller = new AbortController();
    request(`/checkout/districts/${encodeURIComponent(districtCode)}/wards`, { signal: controller.signal })
      .then(setWards)
      .catch((failure) => { if (failure.name !== 'AbortError') setError(failure.message); });
    return () => controller.abort();
  }, [isRetail, form?.districtCode]);

  function handleChange(field, value) {
    const reset = field === 'provinceCode' ? { districtCode: '', wardCode: '' }
      : field === 'districtCode' ? { wardCode: '' } : {};
    setForm((prev) => ({ ...prev, [field]: value, ...reset }));
    setNotice('');
    setError('');
  }

  async function handleSave(event) {
    event.preventDefault();
    if (!form.fullName.trim()) { setError('Họ và tên không được để trống.'); return; }
    if (!/^0\d{9}$/.test(form.phoneNumber.trim())) {
      setError('Số điện thoại phải có 10 chữ số, bắt đầu bằng 0.'); return;
    }
    const hasAddress = form.provinceCode || form.districtCode || form.wardCode || form.addressDetail.trim();
    if (isRetail && hasAddress && (!form.provinceCode || !form.districtCode || !form.wardCode
      || !form.addressDetail.trim())) {
      setError('Vui lòng nhập đầy đủ tỉnh / thành, quận / huyện, phường / xã và địa chỉ chi tiết.'); return;
    }

    setSaving(true);
    setError('');
    try {
      const updated = isFarmer
        ? await request('/profile/farmer', { method: 'PUT', body: {
          fullName: form.fullName.trim(),
          phoneNumber: form.phoneNumber.trim(),
          farmName: form.farmName.trim(),
          representativeName: form.representativeName.trim(),
          address: form.farmAddress.trim(),
          productionScale: form.productionScale.trim() || null,
          cultivationZone: form.cultivationZone.trim() || null,
          businessLicenseNumber: form.businessLicenseNumber.trim() || null,
          gpsCoordinates: form.gpsCoordinates.trim() || null,
        } })
        : await request('/profile/consumer', { method: 'PUT', body: {
          fullName: form.fullName.trim(),
          phoneNumber: form.phoneNumber.trim(),
          ...(isRetail && hasAddress ? {
            provinceCode: form.provinceCode,
            districtCode: form.districtCode,
            wardCode: form.wardCode,
            addressDetail: form.addressDetail.trim(),
          } : {}),
        } });
      setProfile(updated);
      setForm(toForm(updated));
      onProfileSaved?.(updated);
      setNotice('MSG13: Cập nhật thông tin hồ sơ thành công.');
    } catch (failure) {
      setError(failure.message || 'Cập nhật hồ sơ thất bại.');
    } finally {
      setSaving(false);
    }
  }

  async function handleSubmitKyc() {
    if (!confirm('Bạn có chắc chắn muốn nộp hồ sơ để thẩm định KYC (UC-VO-01) không?')) return;
    setSaving(true);
    setError('');
    try {
      const updated = await request('/profile/farmer/submit-kyc', { method: 'POST', body: { notes: null } });
      setProfile(updated);
      setNotice('Hồ sơ của bạn đã được chuyển vào hàng đợi duyệt KYC của Verification Officer (UC-VO-01).');
    } catch (failure) {
      setError(failure.message || 'Lỗi khi nộp KYC.');
    } finally {
      setSaving(false);
    }
  }

  if (!session) {
    return <div className="panel padded" style={{ maxWidth: 560, margin: '0 auto' }}>
      <h1>Hồ sơ cá nhân</h1>
      <Notice error>Vui lòng đăng nhập bằng tài khoản thật để xem và cập nhật hồ sơ.</Notice>
      <button type="button" className="primary" onClick={() => navigate('/login')}>Đăng nhập</button>
    </div>;
  }
  if (loading) return <p role="status">Đang tải hồ sơ…</p>;
  if (!profile) return <Notice error>{error || 'Không tải được hồ sơ.'}</Notice>;

  const canSubmitKyc = isFarmer && farm
    && !['Approved', 'Verified', 'Pending', 'PendingVerification'].includes(farm.verificationStatus);

  return (
    <div style={{ maxWidth: 1120, margin: '0 auto' }}>
      <div
        style={{
          background: 'linear-gradient(135deg, #1e6546 0%, #28744f 100%)',
          borderRadius: 16, padding: '28px 36px', color: '#ffffff', marginBottom: 24,
          boxShadow: '0 4px 20px rgba(30, 101, 70, 0.12)', display: 'flex',
          justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 20,
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: 20 }}>
          <div
            style={{
              width: 72, height: 72, borderRadius: '50%', background: '#ffffff', color: '#1e6546',
              fontSize: 26, fontWeight: 800, display: 'flex', alignItems: 'center',
              justifyContent: 'center', border: '3px solid #b7e4c7',
            }}
          >
            {initials(profile.fullName)}
          </div>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
              <h1 style={{ color: '#ffffff', margin: 0, fontSize: 24, fontWeight: 800 }}>{profile.fullName}</h1>
              <span style={{ background: '#d8f3dc', color: '#1b4332', fontSize: 11, fontWeight: 700,
                padding: '3px 10px', borderRadius: 12 }}>{profile.role}</span>
            </div>
            <p style={{ margin: '6px 0 0', fontSize: 13, color: '#d8f3dc' }}>
              {profile.email}{profile.phoneNumber && ` · SĐT: ${profile.phoneNumber}`}
            </p>
          </div>
        </div>

        <div style={{ display: 'flex', gap: 14, alignItems: 'center' }}>
          {isRetail && <div style={{ background: 'rgba(255,255,255,0.15)', padding: '10px 16px',
            borderRadius: 12, textAlign: 'center' }}>
            <div style={{ fontSize: 11, color: '#b7e4c7' }}>ĐIỂM TÍCH LŨY</div>
            <div style={{ fontSize: 18, fontWeight: 800 }}>⭐ {profile.rewardPoints}</div>
          </div>}
          {isFarmer && farm && <div style={{ background: 'rgba(255,255,255,0.15)', padding: '10px 16px',
            borderRadius: 12, textAlign: 'center' }}>
            <div style={{ fontSize: 11, color: '#b7e4c7' }}>TRẠNG THÁI KYC</div>
            <div style={{ marginTop: 2 }}><VerificationBadge value={farm.verificationStatus} /></div>
          </div>}
        </div>
      </div>

      <Notice error>{error}</Notice>
      <Notice>{notice}</Notice>

      <div style={{ display: 'grid', gridTemplateColumns: '280px 1fr', gap: 24 }}>
        <aside className="panel padded" style={{ height: 'fit-content' }}>
          <h3 style={{ margin: '0 0 16px', fontSize: 14, color: '#1e4834', fontWeight: 700 }}>QUẢN LÝ TÀI KHOẢN</h3>
          <div style={{ display: 'grid', gap: 8 }}>
            <button type="button" style={{ ...sideLink, background: '#edf6f0', color: '#1e6546', fontWeight: 700 }}>
              👤 Thông tin cá nhân{isFarmer ? ' & Nông trại' : ' & Địa chỉ'}
            </button>
            {(isRetail || isFarmer) && <button type="button" style={sideLink}
              onClick={() => navigate(isFarmer ? '/farmer/orders' : '/retail/orders')}>
              📦 {isFarmer ? 'Đơn hàng cần xử lý' : 'Lịch sử đơn mua hàng (SCR-16)'}
            </button>}
            <button type="button" style={sideLink} onClick={() => navigate('/account/password')}>
              🔒 Đổi mật khẩu (UC-AUTH-06)
            </button>
          </div>

          {isFarmer && farm && <div style={{ borderTop: '1px solid #e0e7df', marginTop: 24, paddingTop: 16 }}>
            <div style={{ fontSize: 12, color: '#668071', marginBottom: 8 }}>
              <strong>Xác minh KYC:</strong> <VerificationBadge value={farm.verificationStatus} />
            </div>
            <div style={{ fontSize: 12, color: '#668071', marginBottom: 12 }}>
              <strong>Điểm uy tín nhà vườn:</strong> ⭐ {farm.reputationScore} / 5.0
            </div>
            {farm.verificationStatus === 'Rejected' && farm.rejectReason && (
              <Notice error>Lý do từ chối: {farm.rejectReason}</Notice>
            )}
            {canSubmitKyc && <button type="button" className="primary"
              style={{ width: '100%', marginTop: 8, background: '#1e6546' }}
              onClick={handleSubmitKyc} disabled={saving}>Gửi thẩm định KYC (UC-VO-01)</button>}
          </div>}
        </aside>

        <main className="panel padded" style={{ minHeight: 'auto', padding: 32 }}>
          <form onSubmit={handleSave}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
              <div>
                <h2 style={{ margin: 0, fontSize: 18, color: '#1b4332' }}>1. Thông tin cá nhân cơ bản</h2>
                <p className="muted" style={{ fontSize: 12 }}>Thông tin dùng để định danh và liên hệ giao nhận hàng trên hệ thống.</p>
              </div>
              <span className="count">Mã thành viên: #{profile.userId}</span>
            </div>

            <div style={twoColumns}>
              <Field label="Họ và tên *">
                <input type="text" value={form.fullName} maxLength={150} required disabled={saving}
                  onChange={(e) => handleChange('fullName', e.target.value)} />
              </Field>
              <Field label="Số điện thoại di động *">
                <input type="tel" value={form.phoneNumber} maxLength={10} required disabled={saving}
                  onChange={(e) => handleChange('phoneNumber', e.target.value)} />
              </Field>
            </div>
            <Field label="Địa chỉ Email (Cố định)">
              <input type="email" value={profile.email} disabled
                style={{ background: '#f5f7f5', color: '#6b7d72', cursor: 'not-allowed' }} />
            </Field>

            {isRetail && <div style={sectionStyle}>
              <h2 style={{ margin: '0 0 6px', fontSize: 18, color: '#1b4332' }}>2. Địa chỉ nhận hàng mặc định</h2>
              <p className="muted" style={{ fontSize: 12, marginBottom: 16 }}>
                Địa chỉ này được tự động điền khi thanh toán đơn hàng nông sản (UC-RC-02).
              </p>
              <div style={twoColumns}>
                <Field label="Tỉnh / Thành phố">
                  <select value={form.provinceCode} disabled={saving}
                    onChange={(e) => handleChange('provinceCode', e.target.value)}>
                    <option value="">Chọn tỉnh / thành phố</option>
                    {provinces.map((item) => <option key={item.code} value={item.code}>{item.name}</option>)}
                  </select>
                </Field>
                <Field label="Quận / Huyện">
                  <select value={form.districtCode} disabled={saving || !form.provinceCode || !districts.length}
                    onChange={(e) => handleChange('districtCode', e.target.value)}>
                    <option value="">{form.provinceCode ? 'Chọn quận / huyện' : 'Chọn tỉnh / thành phố trước'}</option>
                    {districts.map((item) => <option key={item.code} value={item.code}>{item.name}</option>)}
                  </select>
                </Field>
                <Field label="Phường / Xã">
                  <select value={form.wardCode} disabled={saving || !form.districtCode || !wards.length}
                    onChange={(e) => handleChange('wardCode', e.target.value)}>
                    <option value="">{form.districtCode ? 'Chọn phường / xã' : 'Chọn quận / huyện trước'}</option>
                    {wards.map((item) => <option key={item.code} value={item.code}>{item.name}</option>)}
                  </select>
                </Field>
              </div>
              <Field label="Địa chỉ chi tiết (Số nhà, tên ngõ, phố)">
                <input type="text" value={form.addressDetail} placeholder="Ví dụ: Số 18, Phố Duy Tân"
                  maxLength={255} disabled={saving} onChange={(e) => handleChange('addressDetail', e.target.value)} />
              </Field>
            </div>}

            {isFarmer && <div style={sectionStyle}>
              <h2 style={{ margin: '0 0 6px', fontSize: 18, color: '#1b4332' }}>2. Thông tin Nông trại & Hợp tác xã</h2>
              <p className="muted" style={{ fontSize: 12, marginBottom: 16 }}>
                Dữ liệu dùng để cấp mã truy xuất nguồn gốc nông sản theo quy chuẩn VietGAP.
              </p>
              <div style={twoColumns}>
                <Field label="Tên Nông trại / Hợp tác xã *">
                  <input type="text" value={form.farmName} maxLength={200} required disabled={saving}
                    onChange={(e) => handleChange('farmName', e.target.value)} />
                </Field>
                <Field label="Người đại diện *">
                  <input type="text" value={form.representativeName} maxLength={150} required disabled={saving}
                    onChange={(e) => handleChange('representativeName', e.target.value)} />
                </Field>
              </div>
              <Field label="Địa chỉ nông trại *">
                <input type="text" value={form.farmAddress} maxLength={500} required disabled={saving}
                  onChange={(e) => handleChange('farmAddress', e.target.value)} />
              </Field>
              <div style={twoColumns}>
                <Field label="Quy mô sản xuất / Diện tích">
                  <input type="text" value={form.productionScale} disabled={saving}
                    onChange={(e) => handleChange('productionScale', e.target.value)} />
                </Field>
                <Field label="Vùng canh tác nông nghiệp">
                  <input type="text" value={form.cultivationZone} disabled={saving}
                    onChange={(e) => handleChange('cultivationZone', e.target.value)} />
                </Field>
                <Field label="Số ĐKKD / Giấy chứng nhận HTX">
                  <input type="text" value={form.businessLicenseNumber} disabled={saving}
                    onChange={(e) => handleChange('businessLicenseNumber', e.target.value)} />
                </Field>
                <Field label="Tọa độ GPS">
                  <input type="text" value={form.gpsCoordinates} placeholder="11.9404,108.4583" disabled={saving}
                    onChange={(e) => handleChange('gpsCoordinates', e.target.value)} />
                </Field>
              </div>
            </div>}

            <div style={{ marginTop: 32, display: 'flex', justifyContent: 'flex-end', gap: 12 }}>
              <button type="submit" className="primary" disabled={saving}
                style={{ padding: '12px 28px', borderRadius: 10, fontSize: 14, fontWeight: 700, background: '#1e6546' }}>
                {saving ? 'Đang lưu...' : 'Lưu Thay Đổi Hồ Sơ (SCR-11)'}
              </button>
            </div>
          </form>
        </main>
      </div>
    </div>
  );
}
