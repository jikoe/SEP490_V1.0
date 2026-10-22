import '../styles/customer-portal.css';
import React, { useEffect, useState } from 'react';
import UsersScreen from '../features/Administrator/UsersScreen';
import CategoriesScreen from '../features/Administrator/CategoriesScreen';
import AdminDashboard from '../features/Administrator/AdminDashboard';
import NotificationServiceDashboard from '../features/Gmail Notification Service API/NotificationServiceDashboard';
import ProductListingModerationScreen from '../features/Content Moderator/ProductListingModerationScreen';
import FarmerDashboard from '../features/Farmer/FarmerDashboard';
import WholesaleDashboard from '../features/WholesaleBuyer/WholesaleDashboard';
import RetailCustomerDashboard from '../features/RetailCustomer/RetailCustomerDashboard';
import CheckoutScreen from '../features/RetailCustomer/CheckoutScreen';
import RegisterScreen from '../features/Guest/RegisterScreen';
import ProfileScreen from '../features/RetailCustomer/ProfileScreen';
import OrderTrackingScreen from '../features/RetailCustomer/OrderTrackingScreen';
import CustomerPortalApp from './CustomerPortalApp';

import VerificationDashboard from '../features/Verification Officer/VerificationDashboard';
import SupportDashboard from '../features/Customer Support/SupportDashboard';
import PromotionsDashboard from '../features/Promotion Staff/PromotionsDashboard';
import FinanceDashboard from '../features/Finance Staff/FinanceDashboard';
import GatewayDashboard from '../features/Payment Gateway/GatewayDashboard';
import GuestDashboard from '../features/Guest/GuestDashboard';
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
    portalCategory: 'QUáº¢N TRá»Š Há»† THá»NG',
    avatar: 'A',
    hasSidebar: true,
    landingRoute: '/administration/dashboard',
    navItems: [
      { key: 'admin-dash', label: 'Tá»•ng quan há»‡ thá»‘ng', icon: 'ðŸ“Š', path: '/administration/dashboard' },
      { key: 'users', label: 'Quáº£n lÃ½ ngÆ°á»i dÃ¹ng', icon: 'â™™', path: '/administration/users' },
      { key: 'categories', label: 'Danh má»¥c sáº£n pháº©m', icon: 'â–¦', path: '/administration/categories' },
    ],
  },
  content_moderator: {
    key: 'content_moderator',
    title: 'Content Moderator',
    portalName: 'MODERATION CONSOLE',
    portalCategory: 'KIá»‚M DUYá»†T Ná»˜I DUNG',
    avatar: 'M',
    hasSidebar: true,
    landingRoute: '/moderation/products',
    navItems: [
      { key: 'moderation', label: 'Kiá»ƒm duyá»‡t sáº£n pháº©m', icon: 'âœ“', path: '/moderation/products' },
    ],
  },
  farmer: {
    key: 'farmer',
    title: 'Farmer',
    portalName: 'FARMER PORTAL',
    portalCategory: 'TRANG TRáº I & NÃ”NG Há»˜',
    avatar: 'F',
    hasSidebar: true,
    landingRoute: '/farmer/dashboard',
    navItems: [
      { key: 'farmer-dash', label: 'Tá»•ng quan nÃ´ng tráº¡i', icon: 'ðŸ“Š', path: '/farmer/dashboard' },
      { key: 'farmer-profile', label: 'Há»“ sÆ¡ nÃ´ng há»™ & KYC', icon: 'ðŸŒ¾', path: '/farmer/profile' },
    ],
  },
  wholesale_buyer: {
    key: 'wholesale_buyer',
    title: 'WholesaleBuyer',
    portalName: 'B2B WHOLESALE',
    portalCategory: 'DOANH NGHIá»†P MUA BUÃ”N',
    avatar: 'W',
    hasSidebar: true,
    landingRoute: '/wholesale/dashboard',
    navItems: [
      { key: 'wholesale-dash', label: 'Tá»•ng quan mua buÃ´n', icon: 'ðŸ“Š', path: '/wholesale/dashboard' },
    ],
  },
  retail_customer: {
    key: 'retail_customer',
    title: 'RetailCustomer',
    portalName: 'RETAIL MARKET',
    portalCategory: 'MUA Sáº®M TIÃŠU DÃ™NG',
    avatar: 'C',
    hasSidebar: false, // RiÃªng RetailCustomer lÃ  khÃ´ng cÃ³ thanh bÃªn
    landingRoute: '/retail/dashboard',
    navItems: [
      { key: 'retail-dash', label: 'Trang chá»§ mua sáº¯m', icon: 'ðŸ ', path: '/retail/dashboard' },
      { key: 'checkout', label: 'Thanh toÃ¡n', icon: 'ðŸ›’', path: '/checkout' },
      { key: 'retail-orders', label: 'ÄÆ¡n hÃ ng cá»§a tÃ´i', icon: 'ðŸ“¦', path: '/retail/orders' },
      { key: 'retail-profile', label: 'Há»“ sÆ¡ cÃ¡ nhÃ¢n', icon: 'ðŸ‘¤', path: '/retail/profile' },
    ],
  },
  verification_officer: {
    key: 'verification_officer',
    title: 'Verification Officer',
    portalName: 'QUALITY ASSURANCE',
    portalCategory: 'THáº¨M Äá»ŠNH CHáº¤T LÆ¯á»¢NG',
    avatar: 'V',
    hasSidebar: true,
    landingRoute: '/verification/dashboard',
    navItems: [
      { key: 'verification-dash', label: 'Tháº©m Ä‘á»‹nh cháº¥t lÆ°á»£ng', icon: 'ðŸ“Š', path: '/verification/dashboard' },
    ],
  },
  customer_support: {
    key: 'customer_support',
    title: 'Customer Support',
    portalName: 'CARE & DISPUTES',
    portalCategory: 'CHÄ‚M SÃ“C KHÃCH HÃ€NG',
    avatar: 'S',
    hasSidebar: true,
    landingRoute: '/support/dashboard',
    navItems: [
      { key: 'support-dash', label: 'Trung tÃ¢m há»— trá»£', icon: 'ðŸ“Š', path: '/support/dashboard' },
    ],
  },
  promotion_staff: {
    key: 'promotion_staff',
    title: 'Promotion Staff',
    portalName: 'PROMOTIONS HUB',
    portalCategory: 'KHUYáº¾N Máº I & CHIáº¾N Dá»ŠCH',
    avatar: 'P',
    hasSidebar: true,
    landingRoute: '/promotions/dashboard',
    navItems: [
      { key: 'promotion-dash', label: 'Chiáº¿n dá»‹ch khuyáº¿n máº¡i', icon: 'ðŸ“Š', path: '/promotions/dashboard' },
    ],
  },
  finance_staff: {
    key: 'finance_staff',
    title: 'Finance Staff',
    portalName: 'FINANCE & PAYOUT',
    portalCategory: 'TÃ€I CHÃNH & Káº¾ TOÃN',
    avatar: 'ðŸ’°',
    hasSidebar: true,
    landingRoute: '/finance/dashboard',
    navItems: [
      { key: 'finance-dash', label: 'Quáº£n lÃ½ tÃ i chÃ­nh', icon: 'ðŸ“Š', path: '/finance/dashboard' },
    ],
  },
  payment_gateway: {
    key: 'payment_gateway',
    title: 'Payment Gateway',
    portalName: 'GATEWAY CONSOLE',
    portalCategory: 'Cá»”NG THANH TOÃN',
    avatar: 'âš¡',
    hasSidebar: true,
    landingRoute: '/gateway/dashboard',
    navItems: [
      { key: 'gateway-dash', label: 'GiÃ¡m sÃ¡t cá»•ng thanh toÃ¡n', icon: 'ðŸ“Š', path: '/gateway/dashboard' },
    ],
  },
  gmail_service: {
    key: 'gmail_service',
    title: 'Gmail Notification Service API',
    portalName: 'NOTIFICATION ENGINE',
    portalCategory: 'Dá»ŠCH Vá»¤ THÃ”NG BÃO',
    avatar: 'âœ‰ï¸',
    hasSidebar: true,
    landingRoute: '/notifications/dashboard',
    navItems: [
      { key: 'notifications-dash', label: 'Dá»‹ch vá»¥ thÃ´ng bÃ¡o', icon: 'ðŸ“Š', path: '/notifications/dashboard' },
    ],
  },
  guest: {
    key: 'guest',
    title: 'Guest',
    portalName: 'GUEST PORTAL',
    portalCategory: 'KHÃCH VÃƒNG LAI',
    avatar: 'G',
    hasSidebar: true,
    landingRoute: '/guest/dashboard',
    navItems: [
      { key: 'guest-dash', label: 'KhÃ¡m phÃ¡ NÃ´ng Sáº£n Viá»‡t', icon: 'âœ¦', path: '/guest/dashboard' },
    ],
  },
};

function readRoute() {
  const path = window.location.hash.slice(1);
  return path.startsWith('/') ? path : '/login';
}

function readFixture() {
  const selected = sessionStorage.getItem('agrisustain:demo-fixture');
  return selected && ROLE_CONFIGS[selected] ? selected : null;
}

function navigate(path) {
  window.location.hash = path;
}

export default function App() {
  const [session, setCurrentSession] = useState(getSession);
  const [fixture, setFixture] = useState(readFixture);
  const [route, setRoute] = useState(readRoute);
  const [persistence, setPersistence] = useState(null);
  const [databaseReady, setDatabaseReady] = useState(false);

  // XÃ¡c Ä‘á»‹nh role hiá»‡n táº¡i (Æ°u tiÃªn session tá»« DB, náº¿u khÃ´ng cÃ³ thÃ¬ láº¥y fixture demo)
  const currentRoleRaw = session ? session.user.role : (fixture ? ROLE_CONFIGS[fixture]?.title : null);
  const roleKey = normalizeRole(currentRoleRaw);
  const roleConfig = ROLE_CONFIGS[roleKey] || ROLE_CONFIGS.guest;

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
  }, []);

  function enterDemo(roleKeyName = 'administrator') {
    clearSession();
    setCurrentSession(null);
    setFixture(roleKeyName);
    sessionStorage.setItem('agrisustain:demo-fixture', roleKeyName);
    const targetConfig = ROLE_CONFIGS[roleKeyName] || ROLE_CONFIGS.administrator;
    navigate(targetConfig.landingRoute);
  }

  function completeLogin(loginData) {
    setSession(loginData);
    setCurrentSession(loginData);
    setFixture(null);
    sessionStorage.removeItem('agrisustain:demo-fixture');
    const targetKey = normalizeRole(loginData?.user?.role);
    const targetConfig = ROLE_CONFIGS[targetKey] || ROLE_CONFIGS.administrator;
    navigate(targetConfig.landingRoute);
  }

  function logout() {
    clearSession();
    setCurrentSession(null);
    setFixture(null);
    sessionStorage.removeItem('agrisustain:demo-fixture');
    navigate('/login');
  }

  // Náº¿u chÆ°a Ä‘Äƒng nháº­p vÃ  khÃ´ng cÃ³ phiÃªn demo -> Hiá»ƒn thá»‹ LoginScreen
  if (!session && !fixture) {
    // N?u route là /register -> Hi?n th? RegisterScreen cho Guest
    if (route === '/register') {
      return <RegisterScreen navigate={navigate} />;
    }
    return <LoginScreen onLogin={completeLogin} onDemo={() => enterDemo('guest')} navigate={navigate} />;
  }

  // XÃ¡c Ä‘á»‹nh tiÃªu Ä‘á» hiá»ƒn thá»‹ trÃªn thanh Header Breadcrumbs
  let currentTitle = 'Báº£ng Ä‘iá»u khiá»ƒn';
  if (route.startsWith('/administration/users')) currentTitle = 'Quáº£n lÃ½ ngÆ°á»i dÃ¹ng';
  else if (route.startsWith('/administration/categories')) currentTitle = 'Danh má»¥c sáº£n pháº©m';
  else if (route.startsWith('/administration/dashboard')) currentTitle = 'Tá»•ng quan há»‡ thá»‘ng';
  else if (route.startsWith('/moderation/products')) currentTitle = 'Kiá»ƒm duyá»‡t sáº£n pháº©m';
  else if (route.startsWith('/farmer/dashboard')) currentTitle = 'Tá»•ng quan nÃ´ng tráº¡i';
  else if (route.startsWith('/wholesale/dashboard')) currentTitle = 'Tá»•ng quan mua buÃ´n B2B';
  else if (route.startsWith('/retail/dashboard')) currentTitle = 'Cá»­a hÃ ng nÃ´ng sáº£n';
  else if (route.startsWith('/checkout')) currentTitle = 'Thanh toÃ¡n & ÄÆ¡n hÃ ng';
  else if (route.startsWith('/verification/dashboard')) currentTitle = 'Tháº©m Ä‘á»‹nh cháº¥t lÆ°á»£ng';
  else if (route.startsWith('/support/dashboard')) currentTitle = 'Trung tÃ¢m khiáº¿u náº¡i & Tranh cháº¥p';
  else if (route.startsWith('/promotions/dashboard')) currentTitle = 'Chiáº¿n dá»‹ch khuyáº¿n máº¡i';
  else if (route.startsWith('/finance/dashboard')) currentTitle = 'Quáº£n lÃ½ tÃ i chÃ­nh & Chi tráº£';
  else if (route.startsWith('/gateway/dashboard')) currentTitle = 'GiÃ¡m sÃ¡t cá»•ng thanh toÃ¡n';
  else if (route.startsWith('/notifications/dashboard')) currentTitle = 'Dá»‹ch vá»¥ gá»­i thÃ´ng bÃ¡o';
  else if (route.startsWith('/guest/dashboard')) currentTitle = 'KhÃ¡m phÃ¡ NÃ´ng Sáº£n Viá»‡t';

  const userDisplayName = session ? session.user.fullName : (fixture ? `TÃ i khoáº£n ${roleConfig.title}` : 'KhÃ¡ch truy cáº­p');
  const userDisplayRole = session ? session.user.role : (fixture ? `PhiÃªn demo (${roleConfig.title})` : 'Demo');
  const userAvatarInitial = session ? session.user.fullName?.charAt(0)?.toUpperCase() : roleConfig.avatar;

  return (
    <div className="app-shell">
      {/* THANH BÃŠN (SIDEBAR) - Hiá»ƒn thá»‹ cho táº¥t cáº£ role NGOáº I TRá»ª RetailCustomer */}
      {roleConfig.hasSidebar && (
        <aside className="sidebar">
          <a className="brand" href={`#${roleConfig.landingRoute}`}>
            <span className="brand-icon">âœ¦</span>
            <span>
              AgriSustain
              <small>{roleConfig.portalName}</small>
            </span>
          </a>

          <p className="nav-label">{roleConfig.portalCategory}</p>

          <nav className="main-navigation" aria-label="Chá»©c nÄƒng Ä‘iá»u hÆ°á»›ng">
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
                ? 'ÄÃ£ káº¿t ná»‘i SQL Â· EF Core'
                : persistence === 'SqlServer'
                  ? 'ChÆ°a xÃ¡c nháº­n káº¿t ná»‘i SQL'
                  : 'Sáºµn sÃ ng hoáº¡t Ä‘á»™ng'}
            </p>
          </div>
        </aside>
      )}

      {/* VÃ™NG LÃ€M VIá»†C CHÃNH (WORKSPACE) - KhÃ´ng cÃ³ sidebar sáº½ full-width */}
      <div className={`workspace ${!roleConfig.hasSidebar ? 'no-sidebar' : ''}`}>
        <header>
          {roleConfig.hasSidebar ? (
            <span className="header-path">
              {roleConfig.portalCategory}
              <span> / {currentTitle}</span>
            </span>
          ) : (
            /* Header riÃªng cho RetailCustomer (khÃ´ng cÃ³ sidebar) */
            <div className="retail-brand-area">
              <a className="brand" href={`#${roleConfig.landingRoute}`}>
                <span className="brand-icon">âœ¦</span>
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
                <button type="button" onClick={logout} style={{ marginLeft: 10 }}>ÄÄƒng xuáº¥t</button>
              </>
            ) : (
              <>
                <label className="session-selector">
                  Vai trÃ² demo:
                  <select
                    aria-label="Chá»n vai trÃ² giáº£ láº­p"
                    value={fixture || 'guest'}
                    onChange={(event) => enterDemo(event.target.value)}
                  >
                    <option value="administrator">1. Administrator (Quáº£n trá»‹ viÃªn)</option>
                    <option value="content_moderator">2. Content Moderator (Kiá»ƒm duyá»‡t viÃªn)</option>
                    <option value="farmer">3. Farmer (NÃ´ng dÃ¢n / Trang tráº¡i)</option>
                    <option value="wholesale_buyer">4. WholesaleBuyer (KhÃ¡ch mua buÃ´n B2B)</option>
                    <option value="retail_customer">5. RetailCustomer (NgÆ°á»i mua láº» - KhÃ´ng sidebar)</option>
                    <option value="verification_officer">6. Verification Officer (Tháº©m Ä‘á»‹nh viÃªn)</option>
                    <option value="customer_support">7. Customer Support (Há»— trá»£ khÃ¡ch hÃ ng)</option>
                    <option value="promotion_staff">8. Promotion Staff (NhÃ¢n viÃªn khuyáº¿n máº¡i)</option>
                    <option value="finance_staff">9. Finance Staff (Káº¿ toÃ¡n & TÃ i chÃ­nh)</option>
                    <option value="payment_gateway">10. Payment Gateway (Cá»•ng thanh toÃ¡n)</option>
                    <option value="gmail_service">11. Gmail Notification Service (ThÃ´ng bÃ¡o)</option>
                    <option value="guest">12. Guest (KhÃ¡ch vÃ£ng lai)</option>
                  </select>
                </label>
                <button type="button" onClick={logout}>ÄÄƒng nháº­p</button>
              </>
            )}
          </div>
        </header>

        <main key={`${roleKey}-${route}`}>
          {/* Äá»‹nh tuyáº¿n mÃ n hÃ¬nh tÆ°Æ¡ng á»©ng */}
          {route.startsWith('/administration/dashboard') && <AdminDashboard navigate={navigate} />}
          {route.startsWith('/administration/users') && <UsersScreen fixture={fixture} route={route} navigate={navigate} />}
          {route.startsWith('/administration/categories') && <CategoriesScreen fixture={fixture} route={route} navigate={navigate} />}
          {route.startsWith('/moderation/products') && <ProductListingModerationScreen route={route} navigate={navigate} />}
          {route.startsWith('/farmer/dashboard') && <FarmerDashboard navigate={navigate} />}
          {route.startsWith('/wholesale/dashboard') && <WholesaleDashboard navigate={navigate} />}
          {route.startsWith('/retail/dashboard') && <RetailCustomerDashboard navigate={navigate} />}
          {route.startsWith('/register') && <RegisterScreen navigate={navigate} />}
          {route.startsWith('/retail/profile') && <ProfileScreen fixture="retail-customer" navigate={navigate} />}
          {route.startsWith('/farmer/profile') && <ProfileScreen fixture="farmer" navigate={navigate} />}
          {route.startsWith('/profile') && <ProfileScreen navigate={navigate} />}
          {route.startsWith('/retail/orders') && <OrderTrackingScreen fixture="retail-customer" navigate={navigate} />}
          {route.startsWith('/orders') && <OrderTrackingScreen navigate={navigate} />}
          {route.startsWith('/customer-portal') && <CustomerPortalApp />}

          {route.startsWith('/checkout') && <CheckoutScreen fixture={fixture} />}
          {route.startsWith('/verification/dashboard') && <VerificationDashboard navigate={navigate} />}
          {route.startsWith('/support/dashboard') && <SupportDashboard navigate={navigate} />}
          {route.startsWith('/promotions/dashboard') && <PromotionsDashboard navigate={navigate} />}
          {route.startsWith('/finance/dashboard') && <FinanceDashboard navigate={navigate} />}
          {route.startsWith('/gateway/dashboard') && <GatewayDashboard navigate={navigate} />}
          {route.startsWith('/notifications/dashboard') && <NotificationServiceDashboard navigate={navigate} />}
          {route.startsWith('/guest/dashboard') && <GuestDashboard navigate={navigate} />}

          {/* Fallback náº¿u Ä‘Æ°á»ng dáº«n khÃ´ng khá»›p vá»›i mÃ n hÃ¬nh nÃ o */}
          {!['/administration/dashboard', '/administration/users', '/administration/categories',
             '/moderation/products', '/farmer/dashboard', '/wholesale/dashboard',
             '/retail/dashboard', '/checkout', '/verification/dashboard',
             '/support/dashboard', '/promotions/dashboard', '/finance/dashboard',
             '/gateway/dashboard', '/notifications/dashboard', '/guest/dashboard', '/register', '/retail/profile', '/farmer/profile', '/profile', '/retail/orders', '/orders', '/customer-portal'].some((p) => route.startsWith(p)) && (
            <div style={{ textAlign: 'center', padding: '60px 20px' }}>
              <h2>Äang chuyá»ƒn hÆ°á»›ng vá» trang lÃ m viá»‡c cá»§a báº¡n...</h2>
              <button className="primary" style={{ marginTop: 15 }} onClick={() => navigate(roleConfig.landingRoute)}>
                VÃ o {roleConfig.portalName}
              </button>
            </div>
          )}
        </main>

        <footer>
          Ná»n táº£ng AgriSustain Â· Há»‡ sinh thÃ¡i NÃ´ng Sáº£n Viá»‡t Bá»n Vá»¯ng Â· PhÃ¢n quyá»n {roleConfig.title}
        </footer>
      </div>
    </div>
  );
}
