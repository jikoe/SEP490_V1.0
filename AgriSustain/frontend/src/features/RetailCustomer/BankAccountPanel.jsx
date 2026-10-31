import React, { useEffect, useRef, useState } from 'react';
import { request } from '../../api/client';
import { Field, Modal, Notice, Toast, VerificationBadge } from '../../components/ui/Common';

/**
 * UC-AUTH-03 Manage Profile – "Tài khoản ngân hàng" tab (BR-BA-01..06).
 * Farmer: account that receives payouts. RetailCustomer / WholesaleBuyer: default refund account.
 * Flow: choose bank → type number → lookup after 600 ms → tick confirmation → OTP by e-mail → save.
 */
const LOOKUP_DELAY_MS = 600;
const RESEND_SECONDS = 60;
const EMPTY_FORM = { bankCode: '', accountNumber: '', accountHolder: '', confirmRecipient: false };

// MSG-BA-04
const NOT_FOUND = 'Không tìm thấy tài khoản này tại ngân hàng đã chọn. Vui lòng kiểm tra lại ngân hàng và số tài khoản.';

function formatTime(value) {
  if (!value) return '';
  const text = String(value);
  return new Date(/[zZ]|[+-]\d\d:\d\d$/.test(text) ? text : `${text}Z`).toLocaleString('vi-VN');
}

export default function BankAccountPanel() {
  const [overview, setOverview] = useState(null);
  const [loadError, setLoadError] = useState(null);
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState(EMPTY_FORM);
  const [lookup, setLookup] = useState({ status: 'idle', holder: '' });
  const [fieldErrors, setFieldErrors] = useState({});
  const [otpFor, setOtpFor] = useState(null); // 'save' | 'remove' | null
  const [otp, setOtp] = useState('');
  const [otpError, setOtpError] = useState('');
  const [otpInfo, setOtpInfo] = useState('');
  const [resendIn, setResendIn] = useState(0);
  const [busy, setBusy] = useState(false);
  const [toast, setToast] = useState(null);
  const [toastError, setToastError] = useState(null);
  const [confirmRemove, setConfirmRemove] = useState(false);
  const lookupSeq = useRef(0);

  async function load() {
    try {
      setLoadError(null);
      setOverview(await request('/profile/bank-account'));
    } catch (failure) {
      setLoadError(failure.message);
    }
  }

  useEffect(() => { load(); }, []);

  // Countdown for "Gửi lại mã" (60 s, UC-AUTH-08).
  useEffect(() => {
    if (resendIn <= 0) return undefined;
    const timer = setTimeout(() => setResendIn((value) => value - 1), 1000);
    return () => clearTimeout(timer);
  }, [resendIn]);

  // Step 3: look the account up about 600 ms after the user stops typing.
  useEffect(() => {
    if (!editing) return undefined;
    const { bankCode, accountNumber } = form;
    setForm((prev) => (prev.confirmRecipient ? { ...prev, confirmRecipient: false } : prev));
    if (!bankCode || !/^\d{6,19}$/.test(accountNumber)) {
      setLookup({ status: 'idle', holder: '' });
      return undefined;
    }
    const seq = ++lookupSeq.current;
    setLookup({ status: 'checking', holder: '' });
    const timer = setTimeout(async () => {
      try {
        const result = await request('/profile/bank-account/lookup', {
          method: 'POST', body: { bankCode, accountNumber },
        });
        if (seq !== lookupSeq.current) return;
        setLookup({ status: result.status, holder: result.accountHolder || '' });
      } catch (failure) {
        if (seq !== lookupSeq.current) return;
        setLookup({ status: 'error', holder: '' });
        setFieldErrors(failure.fields || {});
      }
    }, LOOKUP_DELAY_MS);
    return () => clearTimeout(timer);
  }, [editing, form.bankCode, form.accountNumber]);

  function change(field, value) {
    setForm((prev) => ({ ...prev, [field]: value }));
    setFieldErrors((prev) => ({ ...prev, [field]: undefined }));
  }

  function startEdit() {
    setForm({ ...EMPTY_FORM, bankCode: overview?.account?.bankCode || '' });
    setLookup({ status: 'idle', holder: '' });
    setFieldErrors({});
    setEditing(true);
  }

  function cancelEdit() {
    setEditing(false);
    setForm(EMPTY_FORM);
    setFieldErrors({});
  }

  const unavailable = lookup.status === 'Unavailable';
  const canSubmit = editing && form.confirmRecipient && !busy
    && (lookup.status === 'Found' || (unavailable && form.accountHolder.trim().length > 0));

  function firstError(fields, key) {
    const value = fields?.[key];
    return Array.isArray(value) ? value[0] : value;
  }

  async function sendOtp(purpose) {
    setBusy(true);
    setOtpError('');
    try {
      const message = await request('/profile/bank-account/otp', { method: 'POST' });
      setOtpInfo(typeof message === 'string' ? message : 'Mã OTP đã được gửi tới email của bạn.');
      setOtpFor(purpose);
      setOtp('');
      setResendIn(RESEND_SECONDS);
    } catch (failure) {
      setToastError(failure.message);
    } finally {
      setBusy(false);
    }
  }

  // Step 5: validate locally, then send the OTP.
  async function handleSave(event) {
    event.preventDefault();
    const errors = {};
    if (!form.bankCode) errors.bankCode = 'Trường này là bắt buộc.';
    if (!form.accountNumber) errors.accountNumber = 'Trường này là bắt buộc.';
    else if (!/^\d{6,19}$/.test(form.accountNumber)) {
      errors.accountNumber = 'Vui lòng sửa Số tài khoản: số tài khoản chỉ gồm chữ số, dài từ 6 đến 19 ký tự.';
    }
    if (unavailable && !form.accountHolder.trim()) errors.accountHolder = 'Trường này là bắt buộc.';
    if (!form.confirmRecipient) errors.confirmRecipient = 'Vui lòng xác nhận đây là đúng tài khoản nhận tiền.';
    setFieldErrors(errors);
    if (Object.keys(errors).length || !canSubmit) return;
    await sendOtp('save');
  }

  async function confirmOtp(event) {
    event.preventDefault();
    if (!/^\d{6}$/.test(otp)) {
      setOtpError('Mã OTP gồm 6 chữ số.');
      return;
    }
    setBusy(true);
    setOtpError('');
    try {
      const result = otpFor === 'remove'
        ? await request('/profile/bank-account/remove', { method: 'POST', body: { otpCode: otp } })
        : await request('/profile/bank-account', {
          method: 'PUT',
          body: {
            bankCode: form.bankCode,
            accountNumber: form.accountNumber,
            accountHolder: unavailable ? form.accountHolder.trim() : null,
            confirmRecipient: form.confirmRecipient,
            otpCode: otp,
          },
        });
      setOverview(result.overview);
      setToast(result.message);
      setOtpFor(null);
      setEditing(false);
      setForm(EMPTY_FORM);
    } catch (failure) {
      const fields = failure.fields || {};
      if (firstError(fields, 'otpCode') || /OTP/i.test(failure.code || '')) {
        setOtpError(firstError(fields, 'otpCode') || failure.message);
      } else {
        // The account itself was rejected: close the OTP box and show it on the form.
        setOtpFor(null);
        setFieldErrors(Object.fromEntries(Object.entries(fields).map(([key, value]) => [key, firstError(fields, key)])));
        setToastError(failure.message);
      }
    } finally {
      setBusy(false);
    }
  }

  if (loadError) {
    return <div><Notice error>{loadError}</Notice>
      <button type="button" onClick={load}>Thử lại</button></div>;
  }
  if (!overview) return <p style={{ color: '#557262' }}>Đang tải tài khoản ngân hàng…</p>;

  const account = overview.account;
  const holdActive = account?.payoutHoldUntil && new Date(`${account.payoutHoldUntil}`.endsWith('Z')
    ? account.payoutHoldUntil : `${account.payoutHoldUntil}Z`) > new Date();

  return (
    <div>
      <Toast message={toast} onClose={() => setToast(null)} />
      <Toast error message={toastError} onClose={() => setToastError(null)} />

      <h2 style={{ margin: '0 0 6px', fontSize: 18, color: '#1b4332' }}>Tài khoản ngân hàng</h2>
      <p style={{ margin: '0 0 20px', color: '#557262', fontSize: 14 }}>{overview.purpose}</p>

      {account ? (
        <div style={{ border: '1px solid #d7e5dc', borderRadius: 12, padding: 20, background: '#f7fbf8', marginBottom: 20 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
            <div>
              <div style={{ fontWeight: 700, color: '#1b4332', fontSize: 16 }}>
                {account.bankShortName} <span style={{ letterSpacing: 2 }}>{account.maskedAccountNumber}</span>
              </div>
              <div style={{ color: '#52695c', fontSize: 13, marginTop: 4 }}>{account.bankFullName}</div>
              <div style={{ marginTop: 8, fontSize: 14 }}>Chủ tài khoản: <strong>{account.accountHolder}</strong></div>
            </div>
            <VerificationBadge value={account.verificationStatus} />
          </div>
          {account.verificationStatus === 'Unverified' && (
            <div style={{ marginTop: 12, fontSize: 13, color: '#92400e' }}>
              Tài khoản chưa được ngân hàng xác minh{overview.role === 'Farmer'
                ? ' nên chưa thể nhận tiền thanh toán. Vui lòng đổi lại tài khoản khi dịch vụ tra cứu hoạt động.' : '.'}
            </div>
          )}
          {holdActive && (
            <div style={{ marginTop: 12, fontSize: 13, color: '#92400e' }}>
              Thanh toán đang tạm giữ đến {formatTime(account.payoutHoldUntil)} do vừa đổi tài khoản (48 giờ).
            </div>
          )}
          {account.updatedAt && (
            <div style={{ marginTop: 8, fontSize: 12, color: '#7a8f83' }}>Cập nhật: {formatTime(account.updatedAt)}</div>
          )}
        </div>
      ) : (
        <Notice>
          {overview.role === 'Farmer'
            ? 'Bạn chưa có tài khoản nhận tiền. Nông trại cần có tài khoản này trước khi gửi thẩm định KYC.'
            : 'Bạn chưa lưu tài khoản ngân hàng nào.'}
        </Notice>
      )}

      {!editing && (
        <div style={{ display: 'flex', gap: 12, marginTop: 12 }}>
          <button type="button" className="primary" onClick={startEdit}
            style={{ background: '#1e6546', color: '#fff', border: 'none', borderRadius: 8, padding: '10px 20px', fontWeight: 700 }}>
            {account ? 'Đổi tài khoản' : 'Thêm tài khoản'}
          </button>
          {account && overview.canRemove && (
            <button type="button" onClick={() => setConfirmRemove(true)} disabled={busy}
              style={{ background: '#fff', color: '#b91c1c', border: '1px solid #fca5a5', borderRadius: 8, padding: '10px 20px', fontWeight: 600 }}>
              Xóa tài khoản
            </button>
          )}
        </div>
      )}

      {editing && (
        <form onSubmit={handleSave} noValidate style={{ marginTop: 8, display: 'grid', gap: 14, maxWidth: 520 }}>
          <Field label="Ngân hàng *" error={fieldErrors.bankCode}>
            <select value={form.bankCode} onChange={(event) => change('bankCode', event.target.value)} disabled={busy}>
              <option value="">-- Chọn ngân hàng --</option>
              {overview.banks.map((bank) => (
                <option key={bank.bankCode} value={bank.bankCode}>{bank.shortName} – {bank.fullName}</option>
              ))}
            </select>
          </Field>

          <Field label="Số tài khoản *" error={fieldErrors.accountNumber
            || (lookup.status === 'NotFound' ? NOT_FOUND : undefined)}
            hint={lookup.status === 'checking' ? 'Đang tra cứu tài khoản tại ngân hàng…' : '6–19 chữ số'}>
            <input inputMode="numeric" autoComplete="off" maxLength={19} value={form.accountNumber}
              onChange={(event) => change('accountNumber', event.target.value.replace(/\D/g, ''))}
              placeholder="Nhập số tài khoản" disabled={busy} />
          </Field>

          {lookup.status === 'Found' && (
            <Field label="Chủ tài khoản" hint="Tên do ngân hàng trả về, không thể sửa.">
              <input value={lookup.holder} readOnly style={{ background: '#f1f5f2', fontWeight: 700 }} />
            </Field>
          )}

          {unavailable && (
            <>
              <Notice>Hiện không tra cứu được ngân hàng. Bạn vẫn có thể lưu tài khoản ở trạng thái
                <strong> Chưa xác minh</strong>{overview.role === 'Farmer' ? '; tài khoản này chưa nhận được tiền thanh toán cho đến khi được xác minh.' : '.'}</Notice>
              <Field label="Tên chủ tài khoản *" error={fieldErrors.accountHolder}>
                <input maxLength={100} value={form.accountHolder}
                  onChange={(event) => change('accountHolder', event.target.value)}
                  placeholder="VD: NGUYEN VAN A" disabled={busy} />
              </Field>
            </>
          )}

          {(lookup.status === 'Found' || unavailable) && (
            <Field error={fieldErrors.confirmRecipient}>
              <label style={{ display: 'flex', gap: 8, alignItems: 'flex-start', fontSize: 14, cursor: 'pointer' }}>
                <input type="checkbox" checked={form.confirmRecipient} style={{ width: 'auto', marginTop: 3 }}
                  onChange={(event) => change('confirmRecipient', event.target.checked)} disabled={busy} />
                Tôi xác nhận đây là đúng tài khoản nhận tiền của tôi.
              </label>
            </Field>
          )}

          <div style={{ display: 'flex', gap: 12 }}>
            <button type="submit" className="primary" disabled={!canSubmit}
              style={{ background: '#1e6546', color: '#fff', border: 'none', borderRadius: 8, padding: '10px 20px', fontWeight: 700 }}>
              {busy ? 'Đang gửi mã…' : 'Lưu tài khoản'}
            </button>
            <button type="button" onClick={cancelEdit} disabled={busy}
              style={{ background: '#fff', border: '1px solid #cbd5d0', borderRadius: 8, padding: '10px 20px' }}>
              Hủy
            </button>
          </div>
        </form>
      )}

      {confirmRemove && (
        <Modal title="Xóa tài khoản ngân hàng" onClose={() => setConfirmRemove(false)}>
          <p>Xóa tài khoản {account?.bankShortName} {account?.maskedAccountNumber}? Các yêu cầu hoàn tiền
            đã gửi vẫn giữ tài khoản bạn đã xác nhận.</p>
          <div style={{ display: 'flex', gap: 12, justifyContent: 'flex-end' }}>
            <button type="button" onClick={() => setConfirmRemove(false)}>Không</button>
            <button type="button" className="primary" disabled={busy}
              onClick={async () => { setConfirmRemove(false); await sendOtp('remove'); }}>
              Gửi mã OTP để xóa
            </button>
          </div>
        </Modal>
      )}

      {otpFor && (
        <Modal title="Xác nhận bằng mã OTP" onClose={() => !busy && setOtpFor(null)}>
          <form onSubmit={confirmOtp} noValidate style={{ display: 'grid', gap: 12, minWidth: 320 }}>
            <p style={{ margin: 0, fontSize: 14, color: '#3f5a4b' }}>{otpInfo}</p>
            <Field label="Mã OTP (6 chữ số)" error={otpError}>
              <input inputMode="numeric" autoComplete="one-time-code" maxLength={6} value={otp}
                onChange={(event) => { setOtp(event.target.value.replace(/\D/g, '')); setOtpError(''); }}
                autoFocus disabled={busy} />
            </Field>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 12 }}>
              <button type="button" disabled={busy || resendIn > 0} onClick={() => sendOtp(otpFor)}
                style={{ background: 'transparent', border: 'none', color: '#1e6546', cursor: 'pointer', padding: 0 }}>
                {resendIn > 0 ? `Gửi lại mã sau ${resendIn}s` : 'Gửi lại mã'}
              </button>
              <button type="submit" className="primary" disabled={busy}>
                {busy ? 'Đang xác nhận…' : otpFor === 'remove' ? 'Xác nhận xóa' : 'Xác nhận lưu'}
              </button>
            </div>
          </form>
        </Modal>
      )}
    </div>
  );
}
