import { getSession } from '../session/session';

export async function login(credentials, { signal } = {}) {
  const response = await fetch('/api/auth/login', {
    method: 'POST',
    signal,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(credentials),
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    const error = new Error(data.message || 'Không thể đăng nhập. Vui lòng thử lại.');
    error.status = response.status;
    throw error;
  }
  return data;
}

/**
 * Sends JSON to the same-origin API using the existing authenticated session.
 */
export async function request(path, { signal, method = 'GET', body } = {}) {
  const session = getSession();
  const response = await fetch(`/api${path}`, {
    method,
    signal,
    headers: {
      ...(session?.accessToken ? { Authorization: `Bearer ${session.accessToken}` } : {}),
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
    },
    body: body === undefined ? undefined : body instanceof FormData ? body : JSON.stringify(body),
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    const error = new Error(data.title || data.message || (response.status === 401 ? 'Vui lòng đăng nhập bằng tài khoản thật để sử dụng chức năng.' : 'Không thể tải dữ liệu. Vui lòng thử lại.'));
    error.code = data.code;
    error.fields = data.errors || {};
    error.status = response.status;
    throw error;
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

