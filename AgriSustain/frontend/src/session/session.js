let currentSession = null;

export function getSession() {
  return currentSession;
}

export function setSession(value) {
  currentSession = value;
}

export function clearSession() {
  currentSession = null;
}
