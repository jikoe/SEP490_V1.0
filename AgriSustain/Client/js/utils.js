/* ==========================================================================
   AgriSustain - Utilities & UI Helpers (js/utils.js)
   ========================================================================== */

const Utils = {
  /**
   * Determine relative path prefix back to the Client/ root folder
   */
  getRootPath() {
    const p = window.location.pathname.replace(/\\/g, '/');
    const roleDirs = [
      '/Administrator/', '/Farmer/', '/RetailCustomer/',
      '/ContentModerator/', '/CustomerSupport/', '/PromotionStaff/',
      '/FinanceStaff/', '/VerificationOfficer/', '/WholesaleBuyer/',
      '/GmailService/', '/PaymentGateway/', '/Guest/'
    ];
    for (const dir of roleDirs) {
      if (p.includes(dir)) return '../';
    }
    return '';
  },

  /**
   * Display floating Toast Notification matching Frontend 1:1
   */
  showToast(message, type = 'info') {
    if (!message) return;

    let container = document.getElementById('toast-container');
    if (!container) {
      container = document.createElement('div');
      container.id = 'toast-container';
      document.body.appendChild(container);
    }

    const toast = document.createElement('div');
    const isMkt = Boolean(document.querySelector('.marketplace, .mkt-header, .co-page'));
    toast.className = `toast toast-${type} ${isMkt ? 'mkt-cart-toast' : ''}`;
    toast.setAttribute('role', type === 'error' ? 'alert' : 'status');

    let iconSvg = '';
    if (type === 'success') {
      iconSvg = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" width="20" height="20" style="flex-shrink:0;color:#1e6546;"><polyline points="20 6 9 17 4 12"></polyline></svg>';
    } else if (type === 'error') {
      iconSvg = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" width="20" height="20" style="flex-shrink:0;color:#a0362c;"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line></svg>';
    } else if (type === 'warning') {
      iconSvg = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" width="20" height="20" style="flex-shrink:0;color:#b45309;"><path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"></path><line x1="12" y1="9" x2="12" y2="13"></line><line x1="12" y1="17" x2="12.01" y2="17"></line></svg>';
    } else {
      iconSvg = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" width="20" height="20" style="flex-shrink:0;color:#1e40af;"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>';
    }

    toast.innerHTML = `
      ${iconSvg}
      <span>${this.escapeHtml(message)}</span>
      <button type="button" aria-label="Đóng thông báo" onclick="this.parentElement.remove()">&times;</button>
    `;

    container.appendChild(toast);

    setTimeout(() => {
      if (toast.parentElement) {
        toast.style.transition = 'opacity 0.3s, transform 0.3s';
        toast.style.opacity = '0';
        toast.style.transform = 'translateX(20px)';
        setTimeout(() => toast.remove(), 300);
      }
    }, 4000);
  },

  /**
   * Open Modal Dialog
   */
  openModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) modal.classList.add('active');
  },

  /**
   * Close Modal Dialog
   */
  closeModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) modal.classList.remove('active');
  },

  /**
   * Format Currency as Vietnam Dong (VND)
   */
  formatVND(amount) {
    if (amount === null || amount === undefined || isNaN(amount)) return '0 ₫';
    return Number(amount).toLocaleString('vi-VN') + ' ₫';
  },

  /**
   * Format Date string (DD/MM/YYYY)
   */
  formatDate(dateString) {
    if (!dateString) return '---';
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return dateString;
    return date.toLocaleDateString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric'
    });
  },

  /**
   * Format Date & Time string (DD/MM/YYYY HH:mm)
   */
  formatDateTime(dateString) {
    if (!dateString) return '---';
    const date = new Date(dateString);
    if (isNaN(date.getTime())) return dateString;
    return date.toLocaleString('vi-VN', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  },

  /**
   * Format date for <input type="date"> (YYYY-MM-DD)
   */
  toInputDateFormat(date) {
    const d = date ? new Date(date) : new Date();
    return d.toISOString().split('T')[0];
  },

  /**
   * Escape HTML to prevent XSS attacks
   */
  escapeHtml(str) {
    if (str === null || str === undefined) return '';
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  },

  /**
   * Filter out technical report reference markers (e.g. (MSG71)) and double dots
   * matching Frontend displayMessage.js 1:1
   */
  displayMessage(value) {
    if (typeof window !== 'undefined' && window.Messages && window.Messages.displayMessage) {
      return window.Messages.displayMessage(value);
    }
    return String(value ?? '').replace(/\s*\(MSG\d+\)/g, '').replace(/([.!?])\.+(?=\s|$)/g, '$1');
  },

  /**
   * Render Badge for generic status
   */
  getStatusBadge(status, configDict) {
    if (!status) return `<span class="badge badge-grey">Không xác định</span>`;
    const conf = configDict ? configDict[status] : null;
    if (conf) {
      return `<span class="badge ${conf.badge}">${conf.label}</span>`;
    }
    return `<span class="badge badge-grey">${this.escapeHtml(status)}</span>`;
  }
};
