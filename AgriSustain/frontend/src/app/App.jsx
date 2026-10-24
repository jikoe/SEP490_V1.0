import '../styles/customer-portal.css';
import React, { useEffect, useState } from 'react';
import UsersScreen from '../features/Administrator/UsersScreen';
import CategoriesScreen from '../features/Administrator/CategoriesScreen';
import AdminDashboard from '../features/Administrator/AdminDashboard';
import NotificationServiceDashboard from '../features/Gmail Notification Service API/NotificationServiceDashboard';
import ProductListingModerationScreen from '../features/Content Moderator/ProductListingModerationScreen';
import ProductsScreen from '../features/Farmer/ProductsScreen';
import OrdersScreen from '../features/Farmer/OrdersScreen';
import CartScreen from '../features/RetailCustomer/CartScreen';
import WholesaleDashboard from '../features/WholesaleBuyer/WholesaleDashboard';
import Marketplace from '../features/Marketplace/Marketplace';
import CheckoutScreen from '../features/RetailCustomer/CheckoutScreen';
import RegisterScreen from '../features/Guest/RegisterScreen';
import ProfileScreen from '../features/RetailCustomer/ProfileScreen';
import OrderTrackingScreen from '../features/RetailCustomer/OrderTrackingScreen';
import ChangePasswordScreen from '../features/authentication/ChangePasswordScreen';
import CustomerPortalApp from './CustomerPortalApp';

import VerificationDashboard from '../features/Verification Officer/VerificationDashboard';
import SupportDashboard from '../features/Customer Support/SupportDashboard';
import PromotionsDashboard from '../features/Promotion Staff/PromotionsDashboard';
import FinanceDashboard from '../features/Finance Staff/FinanceDashboard';
import GatewayDashboard from '../features/Payment Gateway/GatewayDashboard';
import LoginScreen from '../features/Guest/LoginScreen';
import { clearSession, getSession, setSession } from '../session/session';

export function normalizeRole(role) {
  if (!role) return 'guest';
  const clean = String(role).replace(/[^a-z0-9]/gi, '').toLowerCase();
  if (clean.includes('admin')) return 'administrator';
  if (clean.includes('moderator')) return 'content_moderator';
  if (clean.includes('support')) return 'customer_support';
  if (clean.includes('farmer')) return 'farmer';
  if (clean.includes('finance')) return 'finance_staff';
  if (clean.includes('gmail') || clean.includes('notification')) return 'gmail_service';
  if (clean.includes('gateway') || clean.includes('payment')) return 'payment_gateway';
  if (clean.includes('promotion') || clean.includes('promo')) return 'promotion_staff';
  if (clean.includes('retail') || clean.includes('customer')) return 'retail_customer';
  if (clean.includes('verification') || clean.includes('verify')) return 'verification_officer';
  if (clean.includes('wholesale') || clean.includes('buyer')) return 'wholesale_buyer';
  if (clean.includes('guest')) return 'guest';
  return 'guest';
}

export const ROLE_CONFIGS = {
  administrator: {
    key: 'administrator',
    title: 'Administrator',
    portalName: 'ADMIN CONSOLE',
    portalCategory: 'QUẢN TRỊ HỆ THỐNG',
    avatar: 'A',
    hasSidebar: true,
    landingRoute: '/administration/dashboard',
    navItems: [
      { key: 'admin-dash', label: 'Tổng quan hệ thống', icon: '📊', path: '/administration/dashboard' },
      { key: 'users', label: 'Quản lý người dùng', icon: '♙', path: '/administration/users' },
      { key: 'categories', label: 'Danh mục sản phẩm', icon: '▦', path: '/administration/categories' },
    ],
  },
  content_moderator: {
    key: 'content_moderator',
    title: 'Content Moderator',
    portalName: 'MODERATION CONSOLE',
    portalCategory: 'KIỂM DUYỆT NỘI DUNG',
    avatar: 'M',
    hasSidebar: true,
    landingRoute: '/moderation/products',
    navItems: [
      { key: 'moderation', label: 'Kiểm duyệt sản phẩm', icon: '✓', path: '/moderation/products' },
    ],
  },
  farmer: {
    key: 'farmer',
    title: 'Farmer',
    portalName: 'FARMER PORTAL',
    portalCategory: 'TRANG TRẠI & NÔNG HỘ',
    avatar: 'F',
    hasSidebar: true,
    landingRoute: '/farmer/products',
    navItems: [
      { key: 'farmer-products', label: 'Quản lý sản phẩm', icon: '🌾', path: '/farmer/products' },
      { key: 'farmer-orders', label: 'Xử lý đơn hàng', icon: '📦', path: '/farmer/orders' },
      { key: 'farmer-profile', label: 'Hồ sơ nông hộ & KYC', icon: '🌾', path: '/farmer/profile' },
    ],
  },
  wholesale_buyer: {
    key: 'wholesale_buyer',
    title: 'WholesaleBuyer',
    portalName: 'B2B WHOLESALE',
    portalCategory: 'DOANH NGHIỆP MUA BUÔN',
    avatar: 'W',
    hasSidebar: true,
    landingRoute: '/wholesale/dashboard',
    navItems: [
      { key: 'wholesale-dash', label: 'Tổng quan mua buôn', icon: '📊', path: '/wholesale/dashboard' },
      { key: 'marketplace', label: 'Khám phá nông sản', icon: '✦', path: '/marketplace' },
    ],
  },
  retail_customer: {
    key: 'retail_customer',
    title: 'RetailCustomer',
    portalName: 'RETAIL MARKET',
    portalCategory: 'MUA SẮM TIÊU DÙNG',
    avatar: 'C',
    hasSidebar: false, // Riêng RetailCustomer là không có thanh bên
    landingRoute: '/marketplace',
    navItems: [
      { key: 'retail-dash', label: 'Trang chủ mua sắm', icon: '🏠', path: '/retail/dashboard' },
      { key: 'cart', label: 'Giỏ hàng', icon: '🛒', path: '/cart' },
      { key: 'retail-orders', label: 'Đơn hàng của tôi', icon: '📦', path: '/retail/orders' },
      { key: 'retail-profile', label: 'Hồ sơ cá nhân', icon: '👤', path: '/retail/profile' },
    ],
  },
  verification_officer: {
    key: 'verification_officer',
    title: 'Verification Officer',
    portalName: 'QUALITY ASSURANCE',
    portalCategory: 'THẨM ĐỊNH CHẤT LƯỢNG',
    avatar: 'V',
    hasSidebar: true,
    landingRoute: '/verification/dashboard',
    navItems: [
      { key: 'verification-dash', label: 'Thẩm định chất lượng', icon: '📊', path: '/verification/dashboard' },
    ],
  },
  customer_support: {
    key: 'customer_support',
    title: 'Customer Support',
    portalName: 'CARE & DISPUTES',
    portalCategory: 'CHĂM SÓC KHÁCH HÀNG',
    avatar: 'S',
    hasSidebar: true,
    landingRoute: '/support/dashboard',
    navItems: [
      { key: 'support-dash', label: 'Trung tâm hỗ trợ', icon: '📊', path: '/support/dashboard' },
    ],
  },
  promotion_staff: {
    key: 'promotion_staff',
    title: 'Promotion Staff',
    portalName: 'PROMOTIONS HUB',
    portalCategory: 'KHUYẾN MẠI & CHIẾN DỊCH',
    avatar: 'P',
    hasSidebar: true,
    landingRoute: '/promotions/dashboard',
    navItems: [
      { key: 'promotion-dash', label: 'Chiến dịch khuyến mại', icon: '📊', path: '/promotions/dashboard' },
    ],
  },
  finance_staff: {
    key: 'finance_staff',
    title: 'Finance Staff',
    portalName: 'FINANCE & PAYOUT',
    portalCategory: 'TÀI CHÍNH & KẾ TOÁN',
    avatar: '💰',
    hasSidebar: true,
    landingRoute: '/finance/dashboard',
    navItems: [
      { key: 'finance-dash', label: 'Quản lý tài chính', icon: '📊', path: '/finance/dashboard' },
    ],
  },
  payment_gateway: {
    key: 'payment_gateway',
    title: 'Payment Gateway',
    portalName: 'GATEWAY CONSOLE',
    portalCategory: 'CỔNG THANH TOÁN',
    avatar: '⚡',
    hasSidebar: true,
    landingRoute: '/gateway/dashboard',
    navItems: [
      { key: 'gateway-dash', label: 'Giám sát cổng thanh toán', icon: '📊', path: '/gateway/dashboard' },
    ],
  },
  gmail_service: {
    key: 'gmail_service',
    title: 'Gmail Notification Service API',
    portalName: 'NOTIFICATION ENGINE',
    portalCategory: 'DỊCH VỤ THÔNG BÁO',
    avatar: '✉️',
    hasSidebar: true,
    landingRoute: '/notifications/dashboard',
    navItems: [
      { key: 'notifications-dash', label: 'Dịch vụ thông báo', icon: '📊', path: '/notifications/dashboard' },
    ],
  },
  guest: {
    key: 'guest',
    title: 'Guest',
    portalName: 'GUEST PORTAL',
    portalCategory: 'KHÁCH VÃNG LAI',
    avatar: 'G',
    hasSidebar: true,
    landingRoute: '/marketplace',
    navItems: [
      { key: 'guest-dash', label: 'Khám phá Nông Sản Việt', icon: '✦', path: '/marketplace' },
    ],
  },
};

function readRoute() {
  const path = window.location.hash.slice(1);
  return path.startsWith('/') ? path : '/marketplace';
}

function readFixture() {
  const selected = sessionStorage.getItem('agrisustain:demo-fixture');
  return selected && ROLE_CONFIGS[selected] ? selected : null;
}

function navigate(path) {
  window.location.hash = path;
}

const marketplacePaths = ['/marketplace', '/retail/dashboard', '/guest/dashboard'];

export default function App() {
  const [session, setCurrentSession] = useState(getSession);
  const [fixture, setFixture] = useState(readFixture);
  const [route, setRoute] = useState(readRoute);
  const [persistence, setPersistence] = useState(null);
  const [databaseReady, setDatabaseReady] = useState(false);

  // Xác định role hiện tại (ưu tiên session từ DB, nếu không có thì lấy fixture demo)
  const isPublicMarketplace = !session && !fixture && route === '/retail/dashboard';
  const currentRoleRaw = session ? session.user.role : (fixture ? ROLE_CONFIGS[fixture]?.title : (isPublicMarketplace ? ROLE_CONFIGS.retail_customer.title : null));
  const roleKey = normalizeRole(currentRoleRaw);
  const roleConfig = ROLE_CONFIGS[roleKey] || ROLE_CONFIGS.guest;
  const isMarketplace = route === '/' || route.split('?')[0] === '/marketplace'
    || route.startsWith('/marketplace/products/')
    || route.startsWith('/guest/dashboard') || route.startsWith('/retail/dashboard');

  useEffect(() => {
    const handleRoute = () => setRoute(readRoute());
    window.addEventListener('hashchange', handleRoute);
    return () => window.removeEventListener('hashchange', handleRoute);
  }, []);

  useEffect(() => {
    if (session && route === '/login') {
      navigate(roleConfig.landingRoute);
    }
  }, [session, route, roleConfig]);

  useEffect(() => {
    if (isMarketplace) return;
    const controller = new AbortController();
    fetch('/health', { signal: controller.signal })
      .then((response) => response.json())
      .then(async (health) => {
        setPersistence(health.persistence);
        if (health.persistence === 'SqlServer') {
          const response = await fetch('/health/database', { signal: controller.signal });
          const readiness = await response.json().catch(() => ({}));
          setDatabaseReady(response.ok && readiness.connected === true);
        }
      })
      .catch(() => setDatabaseReady(false));
    return () => controller.abort();
  }, [isMarketplace]);

  useEffect(() => {
    if (route === '/farmer/dashboard') navigate('/farmer/products');
    else if (route === '/cart/checkout') navigate('/checkout');
  }, [route]);

  function enterDemo(roleKeyName = 'administrator') {
    clearSession();
    setCurrentSession(null);
    setFixture(roleKeyName);
    sessionStorage.setItem('agrisustain:demo-fixture', roleKeyName);
    const targetConfig = ROLE_CONFIGS[roleKeyName] || ROLE_CONFIGS.administrator;
    navigate(targetConfig.landingRoute);
  }

  function completeLogin(loginData) {
    const roleValue = loginData?.user?.role;
    const targetKey = normalizeRole(roleValue);
    const targetConfig = ROLE_CONFIGS[targetKey];
    if (!roleValue || !targetConfig || targetKey === 'guest') {
      throw new Error('Tài khoản chưa có vai trò hợp lệ để mở đúng khu vực làm việc.');
    }
    setSession(loginData);
    setCurrentSession(loginData);
    setFixture(null);
    sessionStorage.removeItem('agrisustain:demo-fixture');
    navigate(targetConfig.landingRoute);
  }

  function logout() {
    clearSession();
    setCurrentSession(null);
    setFixture(null);
    sessionStorage.removeItem('agrisustain:demo-fixture');
    navigate('/login');
  }

  /** Keeps the header in sync after UC-AUTH-03 saves a new display name. */
  function updateSessionUser(profile) {
    if (!session) return;
    const next = { ...session, user: { ...session.user, fullName: profile.fullName } };
    setSession(next);
    setCurrentSession(next);
  }

  if (isMarketplace) {
    return <Marketplace route={route} navigate={navigate} session={session} onLogout={logout}
      accountLinks={roleConfig.navItems.filter((item) => !marketplacePaths.includes(item.path))} />;
  }

  // Nếu chưa đăng nhập và không có phiên demo -> Hiển thị LoginScreen
  if (!session && !fixture && route !== '/register' && route !== '/retail/dashboard') {
    return <LoginScreen onLogin={completeLogin} onDemo={() => enterDemo('guest')} />;
  }

  // Xác định tiêu đề hiển thị trên thanh Header Breadcrumbs
  let currentTitle = 'Bảng điều khiển';
  if (route.startsWith('/administration/users')) currentTitle = 'Quản lý người dùng';
  else if (route.startsWith('/administration/categories')) currentTitle = 'Danh mục sản phẩm';
  else if (route.startsWith('/administration/dashboard')) currentTitle = 'Tổng quan hệ thống';
  else if (route.startsWith('/moderation/products')) currentTitle = 'Kiểm duyệt sản phẩm';
  else if (route.startsWith('/farmer/products')) currentTitle = 'Quản lý sản phẩm';
  else if (route.startsWith('/farmer/orders')) currentTitle = 'Xử lý đơn hàng';
  else if (route.startsWith('/cart')) currentTitle = 'Giỏ hàng';
  else if (route.startsWith('/wholesale/dashboard')) currentTitle = 'Tổng quan mua buôn B2B';
  else if (route.startsWith('/retail/dashboard')) currentTitle = 'Cửa hàng nông sản';
  else if (route.startsWith('/checkout')) currentTitle = 'Thanh toán & Đơn hàng';
  else if (route.startsWith('/retail/orders') || route.startsWith('/orders')) currentTitle = 'Đơn hàng của tôi';
  else if (route.startsWith('/farmer/profile')) currentTitle = 'Hồ sơ nông hộ & KYC';
  else if (route.startsWith('/retail/profile') || route.startsWith('/profile')) currentTitle = 'Hồ sơ cá nhân';
  else if (route.startsWith('/account/password')) currentTitle = 'Đổi mật khẩu';
  else if (route.startsWith('/verification/dashboard')) currentTitle = 'Thẩm định chất lượng';
  else if (route.startsWith('/support/dashboard')) currentTitle = 'Trung tâm khiếu nại & Tranh chấp';
  else if (route.startsWith('/promotions/dashboard')) currentTitle = 'Chiến dịch khuyến mại';
  else if (route.startsWith('/finance/dashboard')) currentTitle = 'Quản lý tài chính & Chi trả';
  else if (route.startsWith('/gateway/dashboard')) currentTitle = 'Giám sát cổng thanh toán';
  else if (route.startsWith('/notifications/dashboard')) currentTitle = 'Dịch vụ gửi thông báo';
  else if (route.startsWith('/guest/dashboard')) currentTitle = 'Khám phá Nông Sản Việt';

  const userDisplayName = session ? session.user.fullName : (fixture ? `Tài khoản ${roleConfig.title}` : 'Khách truy cập');
  const userDisplayRole = session ? session.user.role : (fixture ? `Phiên demo (${roleConfig.title})` : 'Demo');
  const userAvatarInitial = session ? session.user.fullName?.charAt(0)?.toUpperCase() : roleConfig.avatar;

  return (
    <div className="app-shell">
      {/* THANH BÊN (SIDEBAR) - Hiển thị cho tất cả role NGOẠI TRỪ RetailCustomer */}
      {roleConfig.hasSidebar && (
        <aside className="sidebar">
          <a className="brand" href={`#${roleConfig.landingRoute}`}>
            <span className="brand-icon">✦</span>
            <span>
              AgriSustain
              <small>{roleConfig.portalName}</small>
            </span>
          </a>

          <p className="nav-label">{roleConfig.portalCategory}</p>

          <nav className="main-navigation" aria-label="Chức năng điều hướng">
            {roleConfig.navItems.map((item) => {
              const isSelected = route === item.path || (item.path !== '/' && route.startsWith(item.path));
              return (
                <button
                  key={item.key}
                  type="button"
                  className={isSelected ? 'selected' : ''}
                  onClick={() => navigate(item.path)}
                >
                  <span>{item.icon}</span>
                  {item.label}
                </button>
              );
            })}
          </nav>

          <div className="sidebar-bottom">
            <div className="sidebar-profile">
              <span className="profile-avatar">{userAvatarInitial}</span>
              <div>
                <strong>{userDisplayName}</strong>
                <small>{userDisplayRole}</small>
              </div>
            </div>
            <p>
              <span className="status-dot" />
              {databaseReady
                ? 'Đã kết nối SQL · EF Core'
                : persistence === 'SqlServer'
                  ? 'Chưa xác nhận kết nối SQL'
                  : 'Sẵn sàng hoạt động'}
            </p>
          </div>
        </aside>
      )}

      {/* VÙNG LÀM VIỆC CHÍNH (WORKSPACE) - Không có sidebar sẽ full-width */}
      <div className={`workspace ${!roleConfig.hasSidebar ? 'no-sidebar' : ''}`}>
        <header>
          {roleConfig.hasSidebar ? (
            <span className="header-path">
              {roleConfig.portalCategory}
              <span> / {currentTitle}</span>
            </span>
          ) : (
            /* Header riêng cho RetailCustomer (không có sidebar) */
            <div className="retail-brand-area">
              <a className="brand" href={`#${roleConfig.landingRoute}`}>
                <span className="brand-icon">✦</span>
                <span>
                  AgriSustain
                  <small>RETAIL MARKET</small>
                </span>
              </a>
              <nav className="retail-nav-links">
                {roleConfig.navItems.map((item) => (
                  <button
                    key={item.key}
                    type="button"
                    className={route === item.path ? 'selected' : ''}
                    onClick={() => navigate(item.path)}
                  >
                    <span>{item.icon}</span>
                    {item.label}
                  </button>
                ))}
              </nav>
            </div>
          )}

          <div className="account-actions">
            {session ? (
              <>
                <span style={{ fontSize: 13, color: '#294334', fontWeight: 600 }}>{session.user.fullName}</span>
                <span className="badge role-badge" style={{ marginLeft: 6 }}>{session.user.role}</span>
                <button type="button" onClick={() => navigate('/account/password')} style={{ marginLeft: 10 }}>Đổi mật khẩu</button>
                <button type="button" onClick={logout} style={{ marginLeft: 6 }}>Đăng xuất</button>
              </>
            ) : (
              <>
                <label className="session-selector">
                  Vai trò demo:
                  <select
                    aria-label="Chọn vai trò giả lập"
                    value={fixture || 'guest'}
                    onChange={(event) => enterDemo(event.target.value)}
                  >
                    <option value="administrator">1. Administrator (Quản trị viên)</option>
                    <option value="content_moderator">2. Content Moderator (Kiểm duyệt viên)</option>
                    <option value="farmer">3. Farmer (Nông dân / Trang trại)</option>
                    <option value="wholesale_buyer">4. WholesaleBuyer (Khách mua buôn B2B)</option>
                    <option value="retail_customer">5. RetailCustomer (Người mua lẻ - Không sidebar)</option>
                    <option value="verification_officer">6. Verification Officer (Thẩm định viên)</option>
                    <option value="customer_support">7. Customer Support (Hỗ trợ khách hàng)</option>
                    <option value="promotion_staff">8. Promotion Staff (Nhân viên khuyến mại)</option>
                    <option value="finance_staff">9. Finance Staff (Kế toán & Tài chính)</option>
                    <option value="payment_gateway">10. Payment Gateway (Cổng thanh toán)</option>
                    <option value="gmail_service">11. Gmail Notification Service (Thông báo)</option>
                    <option value="guest">12. Guest (Khách vãng lai)</option>
                  </select>
                </label>
                <button type="button" onClick={logout}>Đăng nhập</button>
              </>
            )}
          </div>
        </header>

        <main key={`${roleKey}-${route}`}>
          {/* Định tuyến màn hình tương ứng */}
          {route.startsWith('/administration/dashboard') && <AdminDashboard navigate={navigate} />}
          {route.startsWith('/administration/users') && <UsersScreen fixture={fixture} route={route} navigate={navigate} />}
          {route.startsWith('/administration/categories') && <CategoriesScreen fixture={fixture} route={route} navigate={navigate} />}
          {route.startsWith('/moderation/products') && <ProductListingModerationScreen route={route} navigate={navigate} />}
          {route.startsWith('/farmer/products') && <ProductsScreen route={route} navigate={navigate} />}
          {route.startsWith('/farmer/orders') && <OrdersScreen route={route} navigate={navigate} />}
          {route.startsWith('/cart') && <CartScreen navigate={navigate} />}
          {route.startsWith('/wholesale/dashboard') && <WholesaleDashboard navigate={navigate} />}
          {route.startsWith('/register') && <RegisterScreen navigate={navigate} />}
          {['/retail/profile', '/farmer/profile', '/profile'].some((p) => route.startsWith(p))
            && <ProfileScreen session={session} navigate={navigate} onProfileSaved={updateSessionUser} />}
          {(route.startsWith('/retail/orders') || route.startsWith('/orders'))
            && <OrderTrackingScreen navigate={navigate} />}
          {route.startsWith('/account/password') && <ChangePasswordScreen session={session}
            navigate={navigate} backRoute={roleConfig.landingRoute} />}
          {route.startsWith('/customer-portal') && <CustomerPortalApp />}

          {route.startsWith('/checkout') && <CheckoutScreen fixture={fixture} navigate={navigate} />}
          {route.startsWith('/verification/dashboard') && <VerificationDashboard navigate={navigate} />}
          {route.startsWith('/support/dashboard') && <SupportDashboard navigate={navigate} />}
          {route.startsWith('/promotions/dashboard') && <PromotionsDashboard navigate={navigate} />}
          {route.startsWith('/finance/dashboard') && <FinanceDashboard navigate={navigate} />}
          {route.startsWith('/gateway/dashboard') && <GatewayDashboard navigate={navigate} />}
          {route.startsWith('/notifications/dashboard') && <NotificationServiceDashboard navigate={navigate} />}

          {/* Fallback nếu đường dẫn không khớp với màn hình nào */}
          {!['/farmer/products', '/farmer/orders', '/cart', '/administration/dashboard', '/administration/users', '/administration/categories',
             '/moderation/products', '/farmer/dashboard', '/wholesale/dashboard',
             '/retail/dashboard', '/checkout', '/verification/dashboard',
             '/support/dashboard', '/promotions/dashboard', '/finance/dashboard',
             '/gateway/dashboard', '/notifications/dashboard', '/guest/dashboard', '/register', '/retail/profile', '/farmer/profile', '/profile', '/retail/orders', '/orders', '/account/password', '/customer-portal'].some((p) => route.startsWith(p)) && (
            <div style={{ textAlign: 'center', padding: '60px 20px' }}>
              <h2>Đang chuyển hướng về trang làm việc của bạn...</h2>
              <button className="primary" style={{ marginTop: 15 }} onClick={() => navigate(roleConfig.landingRoute)}>
                Vào {roleConfig.portalName}
              </button>
            </div>
          )}
        </main>

        <footer>
          Nền tảng AgriSustain · Hệ sinh thái Nông Sản Việt Bền Vững · Phân quyền {roleConfig.title}
        </footer>
      </div>
    </div>
  );
}
