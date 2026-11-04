/* ==========================================================================
   AgriSustain - Universal API Service Wrapper (js/api.js)
   Features: Bearer Token Injection, Standardized Response Handling, Error Interceptor
   ========================================================================== */

const API = {
  /**
   * Helper to format query parameters like frontend client.js queryString:
   * Omits undefined, null, empty strings, and internal options.
   */
  toQueryString(params = {}) {
    if (!params) return '';
    const searchParams = new URLSearchParams();
    for (const [key, value] of Object.entries(params)) {
      if (value === undefined || value === null || value === '') continue;
      if (key === 'suppressErrorToast' || key === 'signal') continue;
      if (Array.isArray(value)) {
        value.forEach(item => {
          if (item !== undefined && item !== null && item !== '') {
            searchParams.append(key, item);
          }
        });
      } else {
        searchParams.append(key, value);
      }
    }
    return searchParams.toString();
  },

  /**
   * Core HTTP Request handler
   */
  async request(endpoint, options = {}) {
    let url = endpoint.startsWith('http') ? endpoint : `${CONFIG.API_BASE_URL}${endpoint}`;
    const token = Auth.getAccessToken();

    const headers = {
      'Accept': 'application/json',
      ...(options.headers || {})
    };

    if (!(options.body instanceof FormData)) {
      headers['Content-Type'] = 'application/json';
    }

    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const fetchConfig = {
      method: options.method || 'GET',
      headers,
      signal: options.signal,
      body: (options.body && !(options.body instanceof FormData))
        ? JSON.stringify(options.body)
        : options.body
    };

    try {
      let response;
      try {
        response = await fetch(url, fetchConfig);
      } catch (networkError) {
        if (networkError.name === 'AbortError') throw networkError;
        // Fallback to secondary base URL if primary localhost port fails
        if (CONFIG.API_BASE_URL_FALLBACK && url.includes(CONFIG.API_BASE_URL)) {
          url = url.replace(CONFIG.API_BASE_URL, CONFIG.API_BASE_URL_FALLBACK);
          response = await fetch(url, fetchConfig);
        } else {
          throw networkError;
        }
      }

      // 401 means "session expired" only when a token was sent. A failed login (wrong
      // password) also returns 401 and must show the server message instead.
      if (response.status === 401 && token) {
        Auth.clearAuth();
        if (!options.suppressErrorToast) {
          const msg = (typeof window !== 'undefined' && window.Messages?.common?.sessionExpired)
            || 'Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.';
          Utils.showToast(msg, 'warning');
        }
        setTimeout(() => {
          window.location.href = `${Utils.getRootPath()}Guest/login.html`;
        }, 1200);
        return { success: false, status: 401, error: 'Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.' };
      }

      const contentType = response.headers.get('content-type') || '';
      let data = null;
      if (/[/+]json/i.test(contentType)) {
        data = await response.json();
      } else {
        data = await response.text();
      }

      if (!response.ok) {
        const errorMsg = (data && (data.detail || data.title || data.message))
          || (typeof data === 'string' && data)
          || `Yêu cầu thất bại (Mã lỗi: ${response.status})`;
        const errorCode = (data && (data.code || data.title)) || '';
        if (!options.suppressErrorToast) {
          Utils.showToast(errorMsg, 'error');
        }
        return { success: false, status: response.status, error: errorMsg, code: errorCode, data };
      }

      // Smart unwrap for Result<T> / ApiResult envelope:
      // If the backend wraps payload in { success: true, data: ... }, unwrap it into payload
      // while keeping payload.data reference so both res.data.items and res.data.data.items succeed.
      let payload = data;
      if (data && typeof data === 'object' && !Array.isArray(data)) {
        if ('data' in data && (data.success !== undefined || data.isSuccess !== undefined)) {
          payload = data.data !== undefined ? data.data : data;
          if (payload && typeof payload === 'object' && !Array.isArray(payload) && !('data' in payload)) {
            payload.data = payload;
          }
        }
      }

      const spreadable = (typeof payload === 'object' && payload !== null && !Array.isArray(payload)) ? payload : {};
      return { success: true, status: response.status, data: payload, raw: data, ...spreadable };
    } catch (err) {
      if (err.name === 'AbortError') throw err;
      console.error('API Client Network Error:', err);
      if (!options.suppressErrorToast) {
        const msg = (typeof window !== 'undefined' && window.Messages?.common?.networkError)
          || 'Không thể kết nối đến máy chủ Backend AgriSustain. Hãy kiểm tra API đang chạy.';
        Utils.showToast(msg, 'error');
      }
      return { success: false, error: err.message };
    }
  },

  /* ---------------- CONVENIENCE HTTP METHODS ---------------- */
  async get(endpoint, options = {}) {
    return this.request(endpoint, { method: 'GET', ...options });
  },

  async post(endpoint, body = null, options = {}) {
    return this.request(endpoint, { method: 'POST', body, ...options });
  },

  async put(endpoint, body = null, options = {}) {
    return this.request(endpoint, { method: 'PUT', body, ...options });
  },

  async delete(endpoint, options = {}) {
    return this.request(endpoint, { method: 'DELETE', ...options });
  },

  async patch(endpoint, body = null, options = {}) {
    return this.request(endpoint, { method: 'PATCH', body, ...options });
  },

  /* ---------------- AUTHENTICATION MODULE ---------------- */
  async login(credentials) {
    const res = await this.request('/auth/login', {
      method: 'POST',
      body: credentials
    });
    if (res.success && res.data) {
      Auth.setTokens(res.data.accessToken || res.data.token, res.data.refreshToken);
      Auth.setUserInfo(res.data.user);
    }
    return res;
  },

  async logout() {
    try {
      await this.request('/auth/logout', { method: 'POST', suppressErrorToast: true });
    } finally {
      Auth.clearAuth();
      window.location.href = `${Utils.getRootPath()}Guest/login.html`;
    }
  },

  async register(data) {
    return this.request('/auth/register', { method: 'POST', body: data });
  },

  async verifyOtp(data) {
    return this.request('/auth/verify-otp', { method: 'POST', body: data });
  },

  async resendOtp(data) {
    return this.request('/auth/resend-otp', { method: 'POST', body: data });
  },

  async forgotPassword(data) {
    return this.request('/auth/forgot-password', { method: 'POST', body: data });
  },

  async verifyForgotPasswordOtp(data) {
    return this.request('/auth/forgot-password/verify-otp', { method: 'POST', body: data });
  },

  async resendForgotPasswordOtp(data) {
    return this.request('/auth/forgot-password/resend-otp', { method: 'POST', body: data });
  },

  async resetPassword(data) {
    return this.request('/auth/reset-password', {
      method: 'POST', body: data, skipAuthRedirect: true, suppressErrorToast: true
    });
  },

  async uploadImage(file) {
    const formData = new FormData();
    formData.append('file', file);
    return this.request('/files/images', {
      method: 'POST',
      body: formData
    });
  },

  getMarketplaceProducts(params = {}) {
    return this.browseMarketplace(params);
  },

  /* ---------------- ADMINISTRATOR MODULE ---------------- */
  async getUsers(params = {}, options = {}) {
    const query = this.toQueryString(params);
    return this.request(`/users${query ? `?${query}` : ''}`, options);
  },

  async getUserDetail(id) {
    return this.request(`/users/${id}`);
  },

  async changeUserStatus(id, actionData) {
    return this.request(`/users/${id}/status-actions`, {
      method: 'POST',
      body: actionData
    });
  },

  async getCategories(params = {}, options = {}) {
    const query = this.toQueryString(params);
    return this.request(`/categories${query ? `?${query}` : ''}`, options);
  },

  async getCategoryDetail(id) {
    return this.request(`/categories/${id}`);
  },

  async createCategory(data) {
    return this.request('/categories', { method: 'POST', body: data });
  },

  async updateCategory(id, data) {
    return this.request(`/categories/${id}`, { method: 'PUT', body: data });
  },

  async changeCategoryStatus(id, operation, confirmed = true) {
    return this.request(`/categories/${id}/${operation}`, {
      method: 'POST',
      body: { confirmed }
    });
  },

  /* ---------------- FARMER MODULE ---------------- */
  async getFarmerProductOptions() {
    return this.request('/farmer/products/options');
  },

  async getFarmerProducts(params = {}, options = {}) {
    const query = this.toQueryString(params);
    return this.request(`/farmer/products${query ? `?${query}` : ''}`, options);
  },

  async getFarmerProductDetail(id) {
    return this.request(`/farmer/products/${id}`);
  },

  async createFarmerProduct(data) {
    return this.request('/farmer/products', { method: 'POST', body: data });
  },

  async updateFarmerProduct(id, data) {
    return this.request(`/farmer/products/${id}`, { method: 'PUT', body: data });
  },

  async setProductVisibility(id, isPublished, version = 1) {
    return this.request(`/farmer/products/${id}/visibility`, {
      method: 'PUT',
      body: { isPublished, version }
    });
  },

  async getFarmerOrders(params = {}, options = {}) {
    const query = this.toQueryString(params);
    return this.request(`/farmer/orders${query ? `?${query}` : ''}`, options);
  },

  async getFarmerOrderDetail(id) {
    return this.request(`/farmer/orders/${id}`);
  },

  async confirmFarmerOrder(id, version = '1') {
    return this.request(`/farmer/orders/${id}/confirm`, {
      method: 'POST',
      body: {
        version: String(version || '1'),
        requestKey: (typeof crypto !== 'undefined' && crypto.randomUUID) ? crypto.randomUUID() : 'req-' + Date.now()
      }
    });
  },

  async readyFarmerOrder(id, version = '1') {
    return this.request(`/farmer/orders/${id}/ready`, {
      method: 'POST',
      body: {
        version: String(version || '1'),
        requestKey: (typeof crypto !== 'undefined' && crypto.randomUUID) ? crypto.randomUUID() : 'req-' + Date.now()
      }
    });
  },

  async dispatchFarmerOrder(id, version = '1') {
    return this.request(`/farmer/orders/${id}/start-shipping`, {
      method: 'POST',
      body: {
        version: String(version || '1'),
        requestKey: (typeof crypto !== 'undefined' && crypto.randomUUID) ? crypto.randomUUID() : 'req-' + Date.now()
      }
    });
  },

  async rejectFarmerOrder(id, reason = '', version = '1') {
    return this.request(`/farmer/orders/${id}/reject`, {
      method: 'POST',
      body: {
        version: String(version || '1'),
        requestKey: (typeof crypto !== 'undefined' && crypto.randomUUID) ? crypto.randomUUID() : 'req-' + Date.now(),
        reason: 'Quality',
        explanation: reason
      }
    });
  },

  async getPackingSlip(id) {
    return this.request(`/farmer/orders/${id}/packing-slip`);
  },

  /* ---------------- MARKETPLACE & RETAIL CUSTOMER ---------------- */
  async browseMarketplace(params = {}, options = {}) {
    const query = this.toQueryString(params);
    return this.request(`/marketplace/products${query ? `?${query}` : ''}`, options);
  },

  async getMarketplaceFilters(params = {}) {
    const query = this.toQueryString(params);
    return this.request(`/marketplace/filters${query ? `?${query}` : ''}`);
  },

  async getMarketplaceDetail(productId) {
    return this.request(`/marketplace/products/${productId}`);
  },

  async getCart(options = {}) {
    return this.request('/cart', options);
  },

  async addToCart(productId, quantity = 1, options = {}) {
    return this.request('/cart/items', {
      method: 'POST',
      body: { productId, quantity },
      ...options
    });
  },

  async updateCartItemQuantity(id, quantity, options = {}) {
    return this.request(`/cart/items/${id}/quantity`, {
      method: 'PUT',
      body: { quantity },
      ...options
    });
  },

  async selectCartItems(itemIds, isSelected, options = {}) {
    return this.request('/cart/selection', {
      method: 'PUT',
      body: { itemIds, isSelected },
      ...options
    });
  },

  async removeCartItems(itemIds, options = {}) {
    return this.request('/cart/remove-items', {
      method: 'POST',
      body: { itemIds },
      ...options
    });
  },

  async prepareCheckout(reviewToken, options = {}) {
    return this.request('/cart/prepare-checkout', {
      method: 'POST',
      body: { reviewToken },
      ...options
    });
  },

  async getCheckoutContext() {
    return this.request('/checkout');
  },

  async getProvinces() {
    return this.request('/checkout/provinces');
  },

  async getDistricts(provinceCode) {
    return this.request(`/checkout/provinces/${provinceCode}/districts`);
  },

  async getWards(districtCode) {
    return this.request(`/checkout/districts/${districtCode}/wards`);
  },

  async quoteCheckout(data, options = {}) {
    return this.request('/checkout/quotes', {
      method: 'POST',
      body: data,
      suppressErrorToast: true,
      ...options
    });
  },

  async placeOrder(data, options = {}) {
    return this.request('/checkout/orders', {
      method: 'POST',
      body: data,
      ...options
    });
  },

  /* ---------------- CONTENT MODERATOR MODULE ---------------- */
  async getModerationProducts(params = {}, options = {}) {
    const query = this.toQueryString(params);
    return this.request(`/moderation/products${query ? `?${query}` : ''}`, options);
  },

  async getModerationDetail(productId) {
    return this.request(`/moderation/products/${productId}`);
  },

  async moderateProductAction(productId, action, reason = '') {
    return this.request(`/moderation/products/${productId}/${action}`, {
      method: 'POST',
      body: { reason }
    });
  },

  /* ---------------- CUSTOMER SUPPORT MODULE ---------------- */
  async getComplaints(params = {}, options = {}) {
    const query = this.toQueryString(params);
    return this.request(`/support/complaints${query ? `?${query}` : ''}`, options);
  },

  async getComplaintDetail(ticketId) {
    return this.request(`/support/complaints/${ticketId}`);
  },

  async resolveComplaint(ticketId, resolutionData) {
    return this.request(`/support/complaints/${ticketId}/resolve`, {
      method: 'POST',
      body: resolutionData,
      suppressErrorToast: true
    });
  },

  /** UC-CS-01: party is Buyer or Seller. */
  async requestComplaintInfo(ticketId, party, instructions) {
    return this.request(`/support/complaints/${ticketId}/request-info`, {
      method: 'POST',
      body: { party, instructions },
      suppressErrorToast: true
    });
  },

  /** Evidence is served only with the reviewer's token, so it is opened from a blob. */
  async openComplaintEvidence(ticketId, evidenceId) {
    const target = window.open('', '_blank');
    try {
      const response = await fetch(`${CONFIG.API_BASE_URL}/support/complaints/${ticketId}/evidence/${evidenceId}`, {
        headers: { Authorization: `Bearer ${Auth.getAccessToken()}` }
      });
      if (!response.ok) throw new Error(String(response.status));
      const url = URL.createObjectURL(await response.blob());
      if (target) target.location.href = url; else window.open(url, '_blank');
      return { success: true };
    } catch {
      if (target) target.close();
      Utils.showToast('Không thể mở bằng chứng. Vui lòng thử lại.', 'error');
      return { success: false };
    }
  },

  /* ---------------- FINANCE · PROCESS REFUND (UC-FIN-03) ---------------- */
  async getRefunds(params = {}, options = {}) {
    const query = this.toQueryString(params);
    return this.request(`/finance/refunds${query ? `?${query}` : ''}`, options);
  },

  async getRefund(refundId) {
    return this.request(`/finance/refunds/${refundId}`);
  },

  /** Start Transfer (Pending) or Transfer Again (Failed); expectedStatus detects a stale screen. */
  async startRefundTransfer(refundId, expectedStatus) {
    return this.request(`/finance/refunds/${refundId}/start-transfer`, {
      method: 'POST', body: { expectedStatus }, suppressErrorToast: true
    });
  },

  async cancelRefundTransfer(refundId, expectedStatus) {
    return this.request(`/finance/refunds/${refundId}/cancel-transfer`, {
      method: 'POST', body: { expectedStatus }, suppressErrorToast: true
    });
  },

  async completeRefund(refundId, transferReference, note) {
    return this.request(`/finance/refunds/${refundId}/complete`, {
      method: 'POST',
      body: { refundMethod: 'BankTransfer', transferReference, note: note || null },
      suppressErrorToast: true
    });
  },

  async failRefundTransfer(refundId, reason) {
    return this.request(`/finance/refunds/${refundId}/transfer-failed`, {
      method: 'POST', body: { reason }, suppressErrorToast: true
    });
  },

  async markRefundReconciliation(refundId, note) {
    return this.request(`/finance/refunds/${refundId}/needs-reconciliation`, {
      method: 'POST', body: { note }, suppressErrorToast: true
    });
  },

  /* ---------------- PROMOTION STAFF MODULE ---------------- */
  async getVouchers(params = {}, options = {}) {
    const query = this.toQueryString(params);
    return this.request(`/promotions/vouchers${query ? `?${query}` : ''}`, options);
  },

  async createVoucher(data) {
    return this.request('/promotions/vouchers', {
      method: 'POST',
      body: data,
      suppressErrorToast: true
    });
  },

  async getVoucher(id) {
    return this.request(`/promotions/vouchers/${id}`);
  },

  async updateVoucher(id, data) {
    return this.request(`/promotions/vouchers/${id}`, {
      method: 'PUT',
      body: data,
      suppressErrorToast: true
    });
  },

  // UC-PS-01: activate | deactivate; version rejects a change made on stale data (MSG-PV-15).
  async setVoucherActive(id, active, version) {
    const query = this.toQueryString({ version });
    return this.request(`/promotions/vouchers/${id}/${active ? 'activate' : 'deactivate'}${query ? `?${query}` : ''}`, {
      method: 'POST',
      suppressErrorToast: true
    });
  },

  async deleteVoucher(id, version) {
    const query = this.toQueryString({ version });
    return this.request(`/promotions/vouchers/${id}${query ? `?${query}` : ''}`, {
      method: 'DELETE',
      suppressErrorToast: true
    });
  },

  /* ---------------- FINANCE STAFF MODULE ---------------- */
  async getFinanceDashboard(params = {}, options = {}) {
    const query = this.toQueryString(params);
    return this.request(`/finance/dashboard${query ? `?${query}` : ''}`, options);
  },

  async getFinancePayments(params = {}, options = {}) {
    const query = this.toQueryString(params);
    return this.request(`/finance/payments${query ? `?${query}` : ''}`, options);
  },

  async reconcilePayment(id, data = {}) {
    return this.request(`/finance/payments/${id}/reconcile`, {
      method: 'POST',
      body: data
    });
  },

  async updateUserStatus(id, actionData) {
    return this.changeUserStatus(id, actionData);
  },

  async deleteCategory(id) {
    return this.changeCategoryStatus(id, 'delete');
  },

  async updateProductVisibility(id, isPublished) {
    return this.setProductVisibility(id, isPublished);
  },

  async updateFarmerOrderStatus(id, status) {
    if (status === 'Preparing') return this.confirmFarmerOrder(id);
    if (status === 'ReadyToShip') return this.readyFarmerOrder(id);
    if (status === 'Shipping') return this.dispatchFarmerOrder(id);
    return this.request(`/farmer/orders/${id}/status`, { method: 'PUT', body: { status } });
  },

  async approveProductListing(id) {
    return this.moderateProductAction(id, 'approve');
  },

  async rejectProductListing(id, reasonData = {}) {
    const reason = typeof reasonData === 'string' ? reasonData : (reasonData.reason || reasonData.note || 'Không đạt tiêu chuẩn');
    return this.moderateProductAction(id, 'reject', reason);
  },

  async getRetailOrders(params = {}, options = {}) {
    const reqOptions = { ...options };
    if (params && params.suppressErrorToast !== undefined) {
      reqOptions.suppressErrorToast = params.suppressErrorToast;
    }
    const query = this.toQueryString(params);
    return this.request(`/orders${query ? `?${query}` : ''}`, reqOptions);
  },

  async getOrderDetail(id) {
    return this.request(`/orders/${id}`);
  },

  async checkout(data) {
    return this.placeOrder(data);
  },

  async changePassword(data) {
    return this.request('/auth/change-password', { method: 'POST', body: data, suppressErrorToast: true });
  },

  /* ---------------- PROFILE MODULE ---------------- */
  async getProfile() {
    return this.request('/profile');
  },

  async updateConsumerProfile(data) {
    return this.request('/profile/consumer', { method: 'PUT', body: data });
  },

  async updateFarmerProfile(data) {
    return this.request('/profile/farmer', { method: 'PUT', body: data });
  },

  async submitFarmerKyc(data = { notes: 'Nông hộ gửi hồ sơ thẩm định giấy phép và quy chuẩn canh tác VietGAP.' }) {
    return this.request('/profile/farmer/submit-kyc', { method: 'POST', body: data });
  },

  async uploadAvatar(file) {
    const formData = new FormData();
    formData.append('file', file);
    return this.request('/profile/avatar', {
      method: 'POST',
      body: formData
    });
  }
};

