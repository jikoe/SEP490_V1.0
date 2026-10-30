import { getSession } from '../session/session';

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL || '').replace(/\/+$/, '');

export function backendUrl(path) {
  return `${API_BASE_URL}${path.startsWith('/') ? path : `/${path}`}`;
}

export async function login(credentials, { signal } = {}) {
  const response = await fetch(backendUrl('/api/auth/login'), {
    method: 'POST',
    signal,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(credentials),
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    const error = new Error(data.detail || data.title || data.message
      || `Đăng nhập thất bại (HTTP ${response.status}).`);
    error.status = response.status;
    throw error;
  }
  return data;
}

export async function logout() {
  const session = getSession();
  if (!session?.accessToken) {
    throw new Error('Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.');
  }

  const response = await fetch(backendUrl('/api/auth/logout'), {
    method: 'POST',
    headers: { Authorization: `Bearer ${session.accessToken}` },
  });
  if (!response.ok) {
    const data = await response.json().catch(() => ({}));
    const error = new Error(data.detail || data.title || data.message
      || `Đăng xuất thất bại (HTTP ${response.status}).`);
    error.status = response.status;
    throw error;
  }
}

/** Sends a request to the same-origin API with the session token, if any. */
async function send(path, { signal, method = 'GET', body } = {}) {
  const session = getSession();
  const cleanPath = path.startsWith('/api') ? path : `/api${path.startsWith('/') ? path : `/${path}`}`;
  const response = await fetch(backendUrl(cleanPath), {
    method,
    signal,
    headers: {
      ...(session?.accessToken ? { Authorization: `Bearer ${session.accessToken}` } : {}),
      // Uploads let the browser set the multipart boundary.
      ...(body === undefined || body instanceof FormData ? {} : { 'Content-Type': 'application/json' }),
    },
    body: body === undefined ? undefined : (body instanceof FormData || typeof body === 'string') ? body : JSON.stringify(body),
  });
  const data = await response.json().catch(() => ({}));
  return { response, data };
}

/**
 * Sends JSON to the same-origin API using the existing authenticated session. Field errors stay
 * in `error.fields` for screens that list them separately.
 */
export async function request(path, options) {
  const { response, data } = await send(path, options);
  if (!response.ok) {
    const error = new Error(data.detail || data.title || data.message || (response.status === 401 ? 'Vui lòng đăng nhập bằng tài khoản thật để sử dụng chức năng.' : 'Không thể tải dữ liệu. Vui lòng thử lại.'));
    error.code = data.code;
    error.fields = data.errors || {};
    error.status = response.status;
    throw error;
  }
  return data;
}

/**
 * Request contract of UC-ADM-03, UC-ADM-05 and UC-RC-02: requires a signed-in session (a demo
 * selector never grants access) and folds field errors into the message.
 */
export async function requestUseCase(path, options) {
  if (!getSession()?.accessToken) throw new Error('Vui lòng đăng nhập để sử dụng chức năng này.');
  const { response, data } = await send(path, options);
  if (!response.ok) {
    const fieldMessages = data.errors ? Object.values(data.errors).flat().join(' ') : '';
    const failure = new Error(fieldMessages || data.title || (response.status === 401
      ? 'Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.'
      : response.status === 403 ? 'Tài khoản không có quyền hoặc đang bị hạn chế.'
        : 'Không thể hoàn tất thao tác. Vui lòng thử lại.'));
    failure.code = data.code; failure.status = response.status;
    throw failure;
  }
  return data;
}

/**
 * Serializes supplied filters while omitting empty/unset values, preserving explicit false and
 * zero rather than changing their meaning.
 */
export function queryString(values) {
  return new URLSearchParams(Object.entries(values).filter(([, value]) =>
    value !== '' && value !== null && value !== undefined)).toString();
}
