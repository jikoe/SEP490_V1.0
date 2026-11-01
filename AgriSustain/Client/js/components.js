/* ==========================================================================
   AgriSustain - Dynamic Layout Components Engine (js/components.js)
   Renders Sidebar & Top Bar strictly matching Frontend App.jsx 1:1
   ========================================================================== */

const ROLE_CONFIGS = {
  administrator: {
    key: 'administrator',
    title: 'Administrator',
    portalName: 'ADMIN CONSOLE',
    portalCategory: 'QUẢN TRỊ HỆ THỐNG',
    avatar: 'A',
    hasSidebar: true,
    landingRoute: 'Administrator/dashboard.html',
    navItems: [
      { key: 'admin-dash', label: 'Tổng quan hệ thống', icon: '📊', path: 'Administrator/dashboard.html' },
      { key: 'users', label: 'Quản lý người dùng', icon: '♙', path: 'Administrator/users.html' },
      { key: 'categories', label: 'Danh mục sản phẩm', icon: '▦', path: 'Administrator/categories.html' },
    ],
  },
  content_moderator: {
    key: 'content_moderator',
    title: 'Content Moderator',
    portalName: 'MODERATION CONSOLE',
    portalCategory: 'KIỂM DUYỆT NỘI DUNG',
    avatar: 'M',
    hasSidebar: true,
    landingRoute: 'ContentModerator/moderation.html',
    navItems: [
      { key: 'moderation', label: 'Kiểm duyệt sản phẩm', icon: '✓', path: 'ContentModerator/moderation.html' },
    ],
  },
  farmer: {
    key: 'farmer',
    title: 'Farmer',
    portalName: 'FARMER PORTAL',
    portalCategory: 'TRANG TRẠI & NÔNG HỘ',
    avatar: 'F',
    hasSidebar: true,
    landingRoute: 'Farmer/products.html',
    navItems: [
      { key: 'farmer-products', label: 'Quản lý sản phẩm', icon: '🌾', path: 'Farmer/products.html' },
      { key: 'farmer-orders', label: 'Xử lý đơn hàng', icon: '📦', path: 'Farmer/orders.html' },
      { key: 'farmer-profile', label: 'Hồ sơ nông hộ & KYC', icon: '🌾', path: 'Farmer/profile.html' },
    ],
  },
  wholesale_buyer: {
    key: 'wholesale_buyer',
    title: 'WholesaleBuyer',
    portalName: 'B2B WHOLESALE',
    portalCategory: 'DOANH NGHIỆP MUA BUÔN',
    avatar: 'W',
    hasSidebar: true,
    landingRoute: 'WholesaleBuyer/dashboard.html',
    navItems: [
      { key: 'wholesale-dash', label: 'Tổng quan mua buôn', icon: '📊', path: 'WholesaleBuyer/dashboard.html' },
      { key: 'marketplace', label: 'Khám phá nông sản', icon: '✦', path: 'RetailCustomer/index.html' },
      { key: 'wholesale-profile', label: 'Hồ sơ & Tài khoản', icon: '👤', path: 'WholesaleBuyer/profile.html' },
    ],
  },
  retail_customer: {
    key: 'retail_customer',
    title: 'RetailCustomer',
    portalName: 'RETAIL MARKET',
    portalCategory: 'MUA SẮM TIÊU DÙNG',
    avatar: 'C',
    hasSidebar: false,
    landingRoute: 'RetailCustomer/index.html',
    navItems: [
      { key: 'retail-dash', label: 'Trang chủ mua sắm', icon: '🏠', path: 'RetailCustomer/index.html' },
      { key: 'cart', label: 'Giỏ hàng', icon: '🛒', path: 'RetailCustomer/cart.html' },
      { key: 'retail-orders', label: 'Đơn hàng của tôi', icon: '📦', path: 'RetailCustomer/orders.html' },
      { key: 'retail-profile', label: 'Hồ sơ cá nhân', icon: '👤', path: 'RetailCustomer/profile.html' },
    ],
  },
  verification_officer: {
    key: 'verification_officer',
    title: 'Verification Officer',
    portalName: 'QUALITY ASSURANCE',
    portalCategory: 'THẨM ĐỊNH CHẤT LƯỢNG',
    avatar: 'V',
    hasSidebar: true,
    landingRoute: 'VerificationOfficer/dashboard.html',
    navItems: [
      { key: 'verification-dash', label: 'Thẩm định chất lượng', icon: '📊', path: 'VerificationOfficer/dashboard.html' },
    ],
  },
  customer_support: {
    key: 'customer_support',
    title: 'Customer Support',
    portalName: 'CARE & DISPUTES',
    portalCategory: 'CHĂM SÓC KHÁCH HÀNG',
    avatar: 'S',
    hasSidebar: true,
    landingRoute: 'CustomerSupport/dashboard.html',
    navItems: [
      { key: 'support-dash', label: 'Trung tâm hỗ trợ', icon: '📊', path: 'CustomerSupport/dashboard.html' },
      { key: 'support-complaints', label: 'Xử lý khiếu nại', icon: '⚖️', path: 'CustomerSupport/complaints.html' },
    ],
  },
  promotion_staff: {
    key: 'promotion_staff',
    title: 'Promotion Staff',
    portalName: 'PROMOTIONS HUB',
    portalCategory: 'KHUYẾN MẠI & CHIẾN DỊCH',
    avatar: 'P',
    hasSidebar: true,
    landingRoute: 'PromotionStaff/dashboard.html',
    navItems: [
      { key: 'promotion-dash', label: 'Chiến dịch khuyến mại', icon: '📊', path: 'PromotionStaff/dashboard.html' },
      { key: 'promotion-vouchers', label: 'Quản lý voucher', icon: '🎟️', path: 'PromotionStaff/vouchers.html' },
    ],
  },
  finance_staff: {
    key: 'finance_staff',
    title: 'Finance Staff',
    portalName: 'QUẢN LÝ TÀI CHÍNH',
    portalCategory: 'TÀI CHÍNH',
    avatar: '💰',
    hasSidebar: true,
    landingRoute: 'FinanceStaff/dashboard.html',
    navItems: [
      { key: 'finance-dash', label: 'Tổng quan', icon: '▦', path: 'FinanceStaff/dashboard.html' },
      { key: 'finance-payments', label: 'Đối soát thanh toán', icon: '⇄', path: 'FinanceStaff/payments.html' },
      { key: 'finance-refunds', label: 'Xử lý hoàn tiền', icon: '🔄', path: 'FinanceStaff/refunds.html' },
    ],
  },
  payment_gateway: {
    key: 'payment_gateway',
    title: 'Payment Gateway',
    portalName: 'GATEWAY CONSOLE',
    portalCategory: 'CỔNG THANH TOÁN',
    avatar: '⚡',
    hasSidebar: true,
    landingRoute: 'PaymentGateway/dashboard.html',
    navItems: [
      { key: 'gateway-dash', label: 'Giám sát cổng thanh toán', icon: '📊', path: 'PaymentGateway/dashboard.html' },
    ],
  },
  gmail_service: {
    key: 'gmail_service',
    title: 'Gmail Notification Service API',
    portalName: 'NOTIFICATION ENGINE',
    portalCategory: 'DỊCH VỤ THÔNG BÁO',
    avatar: '✉️',
    hasSidebar: true,
    landingRoute: 'GmailService/dashboard.html',
    navItems: [
      { key: 'notifications-dash', label: 'Dịch vụ thông báo', icon: '📊', path: 'GmailService/dashboard.html' },
    ],
  }
};

const Components = {
  getCurrentRoleKey() {
    const raw = Auth.getUserRole();
    if (!raw) {
      const fixture = sessionStorage.getItem('agrisustain:demo-fixture');
      if (fixture && ROLE_CONFIGS[fixture]) return fixture;
      // Default to administrator if on admin pages, farmer on farmer pages, etc.
      const p = window.location.pathname.toLowerCase();
      if (p.includes('/administrator/')) return 'administrator';
      if (p.includes('/farmer/')) return 'farmer';
      if (p.includes('/retailcustomer/')) return 'retail_customer';
      if (p.includes('/contentmoderator/')) return 'content_moderator';
      if (p.includes('/customersupport/')) return 'customer_support';
      if (p.includes('/promotionstaff/')) return 'promotion_staff';
      if (p.includes('/financestaff/')) return 'finance_staff';
      if (p.includes('/verificationofficer/')) return 'verification_officer';
      if (p.includes('/wholesalebuyer/')) return 'wholesale_buyer';
      if (p.includes('/gmailservice/')) return 'gmail_service';
      if (p.includes('/paymentgateway/')) return 'payment_gateway';
      return 'administrator';
    }

    const clean = String(raw).replace(/[^a-z0-9]/gi, '').toLowerCase();
    if (clean.includes('admin')) return 'administrator';
    if (clean.includes('moderator')) return 'content_moderator';
    if (clean.includes('support')) return 'customer_support';
    if (clean.includes('farmer')) return 'farmer';
    if (clean.includes('finance')) return 'finance_staff';
    if (clean.includes('gmail') || clean.includes('notif')) return 'gmail_service';
    if (clean.includes('gateway') || clean.includes('payment')) return 'payment_gateway';
    if (clean.includes('promotion') || clean.includes('promo')) return 'promotion_staff';
    if (clean.includes('retail') || clean.includes('customer')) return 'retail_customer';
    if (clean.includes('verification') || clean.includes('verify')) return 'verification_officer';
    if (clean.includes('wholesale') || clean.includes('buyer')) return 'wholesale_buyer';
    return 'administrator';
  },

  renderSidebar() {
    const sidebarEl = document.getElementById('app-sidebar');
    if (!sidebarEl) return;

    const root = Utils.getRootPath();
    const roleKey = this.getCurrentRoleKey();
    const config = ROLE_CONFIGS[roleKey] || ROLE_CONFIGS.administrator;

    if (!config.hasSidebar) {
      sidebarEl.style.display = 'none';
      const ws = document.querySelector('.workspace');
      if (ws) ws.classList.add('no-sidebar');
      return;
    } else {
      sidebarEl.style.display = '';
      const ws = document.querySelector('.workspace');
      if (ws) ws.classList.remove('no-sidebar');
    }

    const currentPath = window.location.pathname.replace(/\\/g, '/').toLowerCase();

    const user = Auth.getUserInfo();
    const displayName = user ? (user.fullName || user.username) : `Tài khoản ${config.title}`;
    const displayRole = user ? (user.role || config.title) : `Phiên demo (${config.title})`;
    const avatar = user ? (user.fullName || 'U').charAt(0).toUpperCase() : config.avatar;

    const navButtons = config.navItems.map(item => {
      const fullLink = `${root}${item.path}`;
      const isSelected = currentPath.endsWith(item.path.split('/').pop().toLowerCase());

      return `
        <button type="button" class="${isSelected ? 'selected' : ''}" onclick="window.location.href='${fullLink}'">
          <span>${item.icon}</span>
          ${item.label}
        </button>
      `;
    }).join('');

    sidebarEl.className = 'sidebar';
    sidebarEl.innerHTML = `
      <a class="brand" href="${root}${config.landingRoute}">
        <span class="brand-icon">✦</span>
        <span>
          AgriSustain
          <small>${config.portalName}</small>
        </span>
      </a>

      <p class="nav-label">${config.portalCategory}</p>

      <nav class="main-navigation" aria-label="Chức năng điều hướng">
        ${navButtons}
      </nav>

      <div class="sidebar-bottom">
        <div class="sidebar-profile">
          <span class="profile-avatar">${avatar}</span>
          <div>
            <strong>${Utils.escapeHtml(displayName)}</strong>
            <small>${Utils.escapeHtml(displayRole)}</small>
          </div>
        </div>
        <p>
          <span class="status-dot"></span>
          Đã kết nối SQL · EF Core
        </p>
      </div>
    `;
  },

  renderHeader() {
    const headerEl = document.getElementById('app-header');
    if (!headerEl) return;

    const root = Utils.getRootPath();
    const roleKey = this.getCurrentRoleKey();
    const config = ROLE_CONFIGS[roleKey] || ROLE_CONFIGS.administrator;
    const isAuth = Auth.isAuthenticated();
    const user = Auth.getUserInfo();

    // Determine current page title
    const currentTitle = document.title.split('-')[0].trim() || 'Bảng điều khiển';

    let leftArea = '';
    if (config.hasSidebar) {
      leftArea = `
        <span class="header-path">
          ${config.portalCategory}
          <span> / ${currentTitle}</span>
        </span>
      `;
    } else {
      const currentPath = window.location.pathname.replace(/\\/g, '/').toLowerCase();
      const navButtons = config.navItems.map(item => {
        const fullLink = `${root}${item.path}`;
        const itemFilename = item.path.split('/').pop().toLowerCase();
        const isSelected = currentPath.endsWith(itemFilename)
          || (itemFilename === 'cart.html' && currentPath.endsWith('checkout.html'));

        return `
          <button type="button" class="${isSelected ? 'selected' : ''}" onclick="window.location.href='${fullLink}'">
            <span>${item.icon}</span>
            ${item.label}
          </button>
        `;
      }).join('');

      leftArea = `
        <div class="retail-brand-area">
          <a class="brand" href="${root}${config.landingRoute}">
            <span class="brand-icon">✦</span>
            <span>
              AgriSustain
              <small>${config.portalName}</small>
            </span>
          </a>
          <nav class="retail-nav-links">
            ${navButtons}
          </nav>
        </div>
      `;
    }

    let rightActions = '';
    if (isAuth && user) {
      const avatar = (user.fullName || user.username || 'U').charAt(0).toUpperCase();
      rightActions = `
        <div class="account-actions">
          <details class="account-menu">
            <summary>
              <span class="account-avatar">${avatar}</span>
              <span>${Utils.escapeHtml(user.fullName || user.username)}</span>
              <span aria-hidden="true">▾</span>
            </summary>
            <div class="account-menu-panel">
              <strong>${Utils.escapeHtml(user.fullName || user.username)}</strong>
              <span>${Utils.escapeHtml(user.role || config.title)}</span>
              <button type="button" onclick="window.location.href='${root}Guest/change-password.html'">Đổi mật khẩu</button>
              <button type="button" onclick="API.logout()">Đăng xuất</button>
            </div>
          </details>
        </div>
      `;
    } else {
      rightActions = `
        <div class="account-actions">
          <label class="session-selector">
            Vai trò demo:
            <select onchange="Components.switchDemo(this.value)">
              <option value="administrator" ${roleKey === 'administrator' ? 'selected' : ''}>1. Administrator (Quản trị viên)</option>
              <option value="content_moderator" ${roleKey === 'content_moderator' ? 'selected' : ''}>2. Content Moderator (Kiểm duyệt viên)</option>
              <option value="farmer" ${roleKey === 'farmer' ? 'selected' : ''}>3. Farmer (Nông dân / Trang trại)</option>
              <option value="wholesale_buyer" ${roleKey === 'wholesale_buyer' ? 'selected' : ''}>4. WholesaleBuyer (Khách mua buôn B2B)</option>
              <option value="retail_customer" ${roleKey === 'retail_customer' ? 'selected' : ''}>5. RetailCustomer (Người mua lẻ)</option>
              <option value="verification_officer" ${roleKey === 'verification_officer' ? 'selected' : ''}>6. Verification Officer (Thẩm định viên)</option>
              <option value="customer_support" ${roleKey === 'customer_support' ? 'selected' : ''}>7. Customer Support (Hỗ trợ khách hàng)</option>
              <option value="promotion_staff" ${roleKey === 'promotion_staff' ? 'selected' : ''}>8. Promotion Staff (Nhân viên khuyến mại)</option>
              <option value="finance_staff" ${roleKey === 'finance_staff' ? 'selected' : ''}>9. Finance Staff (Kế toán &amp; Tài chính)</option>
              <option value="payment_gateway" ${roleKey === 'payment_gateway' ? 'selected' : ''}>10. Payment Gateway (Cổng thanh toán)</option>
              <option value="gmail_service" ${roleKey === 'gmail_service' ? 'selected' : ''}>11. Gmail Notification Service (Thông báo)</option>
            </select>
          </label>
          <a href="${root}Guest/login.html" class="primary" style="padding:7px 15px; font-size:12px; text-decoration:none; display:inline-block; border-radius:7px;">Đăng nhập</a>
        </div>
      `;
    }

    headerEl.innerHTML = `
      ${leftArea}
      ${rightActions}
    `;
  },

  switchDemo(roleKey) {
    sessionStorage.setItem('agrisustain:demo-fixture', roleKey);
    const targetConfig = ROLE_CONFIGS[roleKey];
    if (targetConfig) {
      window.location.href = `${Utils.getRootPath()}${targetConfig.landingRoute}`;
    }
  },

  init() {
    this.renderSidebar();
    this.renderHeader();
  }
};

document.addEventListener('DOMContentLoaded', () => {
  Components.init();
});
