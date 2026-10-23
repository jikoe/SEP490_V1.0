// DEMO-AUTH: fixture is a local testing selector, not a login or access token.
// Authentication team: replace this header with the in-memory Bearer token supplied
// by your session provider. Do not retain fixture selection in production builds.
/**
 * Sends JSON to the same-origin API with the authorized local fixture selector and optional
 * cancellation. Non-success responses retain the stable error code/status for screen recovery;
 * replace the fixture header with the team session adapter.
 */
export async function request(path, { fixture, signal, method = 'GET', body } = {}) {
  const response = await fetch(`/api${path}`, {
    method,
    signal,
    headers: {
      Authorization: `Demo ${fixture}`,
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const data = await response.json().catch(() => ({}));
  if (!response.ok) {
    const error = new Error(data.title || 'Không thể tải dữ liệu. Vui lòng thử lại.');
    error.code = data.code;
    error.status = response.status;
    throw error;
  }
  if (data && typeof data === 'object' && !Array.isArray(data)) {
    data.currentActorId = Number(response.headers.get('X-Demo-Actor-Id')) || null;
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
