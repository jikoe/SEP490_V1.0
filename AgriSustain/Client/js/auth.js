/* ==========================================================================
   AgriSustain - Authentication Guard & Token Storage (js/auth.js)
   Tab-isolated authentication session using sessionStorage to support
   multiple concurrent accounts across different tabs in the same browser.
   ========================================================================== */

// One-time cleanup/migration of legacy localStorage tokens to prevent cross-tab contamination
try {
  const legacyToken = localStorage.getItem(CONFIG.STORAGE_KEYS.ACCESS_TOKEN);
  if (legacyToken) {
    if (!sessionStorage.getItem(CONFIG.STORAGE_KEYS.ACCESS_TOKEN)) {
      sessionStorage.setItem(CONFIG.STORAGE_KEYS.ACCESS_TOKEN, legacyToken);
      const legacyRefresh = localStorage.getItem(CONFIG.STORAGE_KEYS.REFRESH_TOKEN);
      if (legacyRefresh) sessionStorage.setItem(CONFIG.STORAGE_KEYS.REFRESH_TOKEN, legacyRefresh);
      const legacyUser = localStorage.getItem(CONFIG.STORAGE_KEYS.USER_INFO);
      if (legacyUser) sessionStorage.setItem(CONFIG.STORAGE_KEYS.USER_INFO, legacyUser);
    }
    localStorage.removeItem(CONFIG.STORAGE_KEYS.ACCESS_TOKEN);
    localStorage.removeItem(CONFIG.STORAGE_KEYS.REFRESH_TOKEN);
    localStorage.removeItem(CONFIG.STORAGE_KEYS.USER_INFO);
  }
} catch {}

const Auth = {
  getAccessToken() {
    const token = sessionStorage.getItem(CONFIG.STORAGE_KEYS.ACCESS_TOKEN);
    if (token) return token;
    try {
      const sess = JSON.parse(sessionStorage.getItem('agrisustain:session') || 'null');
      return sess?.accessToken || null;
    } catch {
      return null;
    }
  },

  getRefreshToken() {
    const token = sessionStorage.getItem(CONFIG.STORAGE_KEYS.REFRESH_TOKEN);
    if (token) return token;
    try {
      const sess = JSON.parse(sessionStorage.getItem('agrisustain:session') || 'null');
      return sess?.refreshToken || null;
    } catch {
      return null;
    }
  },

  setTokens(accessToken, refreshToken) {
    if (accessToken) sessionStorage.setItem(CONFIG.STORAGE_KEYS.ACCESS_TOKEN, accessToken);
    if (refreshToken) sessionStorage.setItem(CONFIG.STORAGE_KEYS.REFRESH_TOKEN, refreshToken);
    try {
      const existing = JSON.parse(sessionStorage.getItem('agrisustain:session') || '{}');
      sessionStorage.setItem('agrisustain:session', JSON.stringify({
        ...existing,
        accessToken,
        refreshToken
      }));
    } catch {}
    // Ensure localStorage is cleared so new tabs do not inherit this account
    try {
      localStorage.removeItem(CONFIG.STORAGE_KEYS.ACCESS_TOKEN);
      localStorage.removeItem(CONFIG.STORAGE_KEYS.REFRESH_TOKEN);
    } catch {}
  },

  getUser() {
    return this.getUserInfo();
  },

  getCurrentUser() {
    return this.getUserInfo();
  },

  getUserInfo() {
    const raw = sessionStorage.getItem(CONFIG.STORAGE_KEYS.USER_INFO);
    try {
      if (raw) return JSON.parse(raw);
    } catch (e) {}
    try {
      const sess = JSON.parse(sessionStorage.getItem('agrisustain:session') || 'null');
      if (sess?.user) return sess.user;
      const rawUser = sessionStorage.getItem('agrisustain:current-user');
      if (rawUser) return JSON.parse(rawUser);
    } catch {}
    return null;
  },

  setUserInfo(user) {
    if (user) {
      sessionStorage.setItem(CONFIG.STORAGE_KEYS.USER_INFO, JSON.stringify(user));
      try {
        const sess = JSON.parse(sessionStorage.getItem('agrisustain:session') || '{}');
        sess.user = user;
        sessionStorage.setItem('agrisustain:session', JSON.stringify(sess));
        sessionStorage.setItem('agrisustain:current-user', JSON.stringify(user));
      } catch {}
      try {
        localStorage.removeItem(CONFIG.STORAGE_KEYS.USER_INFO);
      } catch {}
    }
  },

  clearAuth() {
    try {
      sessionStorage.removeItem(CONFIG.STORAGE_KEYS.ACCESS_TOKEN);
      sessionStorage.removeItem(CONFIG.STORAGE_KEYS.REFRESH_TOKEN);
      sessionStorage.removeItem(CONFIG.STORAGE_KEYS.USER_INFO);
      sessionStorage.removeItem('agrisustain:session');
      sessionStorage.removeItem('agrisustain:current-user');
      sessionStorage.removeItem('agrisustain:demo-fixture');
    } catch {}
    try {
      localStorage.removeItem(CONFIG.STORAGE_KEYS.ACCESS_TOKEN);
      localStorage.removeItem(CONFIG.STORAGE_KEYS.REFRESH_TOKEN);
      localStorage.removeItem(CONFIG.STORAGE_KEYS.USER_INFO);
    } catch {}
  },

  isAuthenticated() {
    return !!this.getAccessToken();
  },

  requireAuth(requiredRole = null) {
    if (!this.isAuthenticated()) {
      window.location.href = `${Utils.getRootPath()}Guest/login.html`;
      return false;
    }
    if (requiredRole) {
      const userRole = this.getUserRole();
      if (!userRole || !userRole.toLowerCase().includes(requiredRole.toLowerCase())) {
        window.location.href = this.getLandingPageForRole(userRole);
        return false;
      }
    }
    return true;
  },

  getUserRole() {
    const user = this.getUserInfo();
    if (!user) return null;
    const r = user.role || user.Role || 'Guest';
    return String(r).trim();
  },

  getLandingPageForRole(role) {
    const root = Utils.getRootPath();
    const r = String(role || '').toLowerCase();
    if (r.includes('admin')) return `${root}Administrator/dashboard.html`;
    if (r.includes('farmer')) return `${root}Farmer/products.html`;
    if (r.includes('moderator')) return `${root}ContentModerator/moderation.html`;
    if (r.includes('support') || r.includes('dispute')) return `${root}CustomerSupport/dashboard.html`;
    if (r.includes('promotion')) return `${root}PromotionStaff/dashboard.html`;
    if (r.includes('finance')) return `${root}FinanceStaff/dashboard.html`;
    if (r.includes('verify') || r.includes('verification')) return `${root}VerificationOfficer/dashboard.html`;
    if (r.includes('wholesale') || r.includes('buyer')) return `${root}WholesaleBuyer/dashboard.html`;
    if (r.includes('gmail') || r.includes('notif')) return `${root}GmailService/dashboard.html`;
    if (r.includes('gateway') || r.includes('payment')) return `${root}PaymentGateway/dashboard.html`;
    if (r.includes('retail') || r.includes('customer')) return `${root}RetailCustomer/index.html`;
    return `${root}RetailCustomer/index.html`;
  },

  /**
   * Route Guard: enforce access permissions
   * @param {Array<string>} allowedRoles List of roles permitted
   */
  guard(allowedRoles = []) {
    const isAuth = this.isAuthenticated();
    const currentPath = window.location.pathname.replace(/\\/g, '/').toLowerCase();
    const root = Utils.getRootPath();

    const isPublic = currentPath.includes('/guest/') ||
                     currentPath.endsWith('login.html') ||
                     currentPath.endsWith('register.html') ||
                     currentPath.endsWith('forgot-password.html') ||
                     currentPath.endsWith('product-detail.html') ||
                     currentPath.endsWith('index.html') ||
                     currentPath.endsWith('/') ||
                     currentPath.endsWith('/marketplace.html');

    if (isPublic) {
      if (isAuth && (currentPath.endsWith('login.html') || currentPath.endsWith('register.html') || currentPath.endsWith('forgot-password.html'))) {
        const userRole = this.getUserRole();
        window.location.href = this.getLandingPageForRole(userRole);
      }
      return;
    }

    if (!isAuth) {
      const msg = (typeof window !== 'undefined' && window.Messages?.common?.loginRequired)
        || 'Vui lòng đăng nhập để tiếp tục truy cập.';
      Utils.showToast(msg, 'warning');
      setTimeout(() => {
        window.location.href = `${root}Guest/login.html`;
      }, 700);
      return;
    }

    if (allowedRoles && allowedRoles.length > 0) {
      const userRole = this.getUserRole();
      const hasPerm = allowedRoles.some(ar => (userRole || '').toLowerCase().includes(ar.toLowerCase()));
      if (!hasPerm) {
        const msg = (typeof window !== 'undefined' && window.Messages?.common?.accessDenied?.(userRole))
          || `Bạn không có quyền truy cập trang này (Role: ${userRole}).`;
        Utils.showToast(msg, 'error');
        setTimeout(() => {
          window.location.href = this.getLandingPageForRole(userRole);
        }, 1200);
      }
    }
  }
};
