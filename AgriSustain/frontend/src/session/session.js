// The login is kept in sessionStorage so a page reload stays signed in; it is dropped when the
// tab closes, on logout, or once the JWT has expired.
const storageKey = 'agrisustain:session';

let currentSession = restore();

function restore() {
  try {
    const saved = JSON.parse(sessionStorage.getItem(storageKey) || 'null');
    if (!saved?.accessToken) return null;
    if (saved.expiresAtUtc && Date.parse(saved.expiresAtUtc) <= Date.now()) {
      sessionStorage.removeItem(storageKey);
      return null;
    }
    return saved;
  } catch {
    return null;
  }
}

export function getSession() {
  return currentSession;
}

export function setSession(value) {
  currentSession = value;
  try {
    if (value) sessionStorage.setItem(storageKey, JSON.stringify(value));
    else sessionStorage.removeItem(storageKey);
  } catch {
    // Storage can be blocked (private mode); the session then lasts until reload as before.
  }
}

export function clearSession() {
  setSession(null);
}
