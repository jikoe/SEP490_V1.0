import React, { useEffect, useState } from 'react';
import UsersScreen from '../features/administration/UsersScreen';
import CategoriesScreen from '../features/administration/CategoriesScreen';
import CheckoutScreen from '../features/orders/CheckoutScreen';
import LoginScreen from '../features/authentication/LoginScreen';
import { clearSession, getSession, setSession } from '../session/session';

/** Reads only the assigned screen paths; unknown hashes return to the login screen. */
function readRoute() {
  const path = window.location.hash.slice(1);
  return /^\/(login|access-denied|administration\/(users|categories)(\/(new|\d+))?|checkout)$/.test(path)
    ? path : '/login';
}

function readFixture() {
  const selected = sessionStorage.getItem('agrisustain:demo-fixture');
  return ['admin', 'retail-customer', 'other-retail-customer'].includes(selected) ? selected : null;
}

function hasAdminRole(role) { return ['Admin', 'SystemAdmin'].includes(role); }

function landingRoute(role) {
  if (hasAdminRole(role)) return '/administration/users';
  if (role === 'RetailCustomer') return '/checkout';
  return '/access-denied';
}

function navigate(path) { window.location.hash = path; }

export default function App() {
  const [session, setCurrentSession] = useState(getSession);
  const [fixture, setFixture] = useState(readFixture);
  const [route, setRoute] = useState(readRoute);
  const [persistence, setPersistence] = useState(null);
  const [databaseReady, setDatabaseReady] = useState(false);
  const admin = session ? hasAdminRole(session.user.role) : fixture === 'admin';
  const activeFixture = session ? (admin ? 'admin' : 'retail-customer') : fixture;
  const screen = route.startsWith('/administration/categories') ? 'categories'
    : route === '/checkout' ? 'checkout' : 'users';

  useEffect(() => {
    const handleRoute = () => setRoute(readRoute());
    window.addEventListener('hashchange', handleRoute);
    return () => window.removeEventListener('hashchange', handleRoute);
  }, []);

  useEffect(() => {
    if (session && route === '/login')
      navigate(landingRoute(session.user.role));
  }, [session, route]);

  useEffect(() => {
    const controller = new AbortController();
    fetch('/health', { signal: controller.signal }).then((response) => response.json())
      .then(async (health) => {
        setPersistence(health.persistence);
        if (health.persistence === 'SqlServer') {
          const response = await fetch('/health/database', { signal: controller.signal });
          const readiness = await response.json();
          setDatabaseReady(response.ok && readiness.connected === true);
        }
      }).catch(() => setDatabaseReady(false));
    return () => controller.abort();
  }, []);

  function enterDemo(value = 'admin') {
    clearSession();
    setCurrentSession(null);
    setFixture(value);
    sessionStorage.setItem('agrisustain:demo-fixture', value);
    navigate(value === 'admin' ? '/administration/users' : '/checkout');
  }

  function completeLogin(value) {
    setSession(value);
    setCurrentSession(value);
    setFixture(null);
    sessionStorage.removeItem('agrisustain:demo-fixture');
    navigate(landingRoute(value.user.role));
  }

  function logout() {
    clearSession();
    setCurrentSession(null);
    setFixture(null);
    sessionStorage.removeItem('agrisustain:demo-fixture');
    navigate('/login');
  }

  if (!session && !fixture) {
    return <LoginScreen onLogin={completeLogin} onDemo={() => enterDemo()} />;
  }

  const unsupportedRole = session && !hasAdminRole(session.user.role)
    && session.user.role !== 'RetailCustomer';

  return <div className="app-shell"><aside className="sidebar">
    <a className="brand" href={admin ? '#/administration/users' : '#/checkout'}>
      <span className="brand-icon">✦</span><span>AgriSustain
        <small>{admin ? 'ADMIN CONSOLE' : 'RETAIL MARKET'}</small></span></a>
    <p className="nav-label">{admin ? 'QUẢN TRỊ HỆ THỐNG' : 'MUA SẮM'}</p>
    <nav className="main-navigation" aria-label="Chức năng">
      {admin ? <><button className={screen === 'users' ? 'selected' : ''}
        onClick={() => navigate('/administration/users')}><span>♙</span>Quản lý người dùng</button>
        <button className={screen === 'categories' ? 'selected' : ''}
          onClick={() => navigate('/administration/categories')}><span>▦</span>Danh mục sản phẩm</button></>
        : <button className="selected" onClick={() => navigate('/checkout')}>
          <span>▣</span>Thanh toán đơn bán lẻ</button>}
    </nav><div className="sidebar-bottom"><div className="sidebar-profile">
      <span className="profile-avatar">{admin ? 'A' : 'K'}</span><div><strong>
        {session ? session.user.fullName : admin ? 'SystemAdmin' : 'RetailCustomer'}
      </strong><small>{session ? session.user.role : 'Phiên demo'}</small></div>
    </div><p><span className="status-dot" />{databaseReady ? 'Đã kết nối SQL · EF Core'
      : persistence === 'SqlServer' ? 'Chưa xác nhận kết nối SQL'
        : persistence === 'InMemory' ? 'Dữ liệu trong bộ nhớ' : 'Đang kết nối API'}</p></div>
    </aside><div className="workspace"><header><span className="header-path">
      {admin ? 'Administration' : 'Consumer Ordering'}<span> / {screen === 'users'
        ? 'Người dùng' : screen === 'categories' ? 'Danh mục sản phẩm' : 'Checkout'}</span></span>
      {session ? <div className="account-actions"><span>{session.user.fullName}</span>
        <button type="button" onClick={logout}>Đăng xuất</button></div>
        : <div className="account-actions"><label className="session-selector">Phiên giả lập
          <select aria-label="Phiên giả lập" value={fixture}
            onChange={(event) => enterDemo(event.target.value)}>
            <option value="admin">SystemAdmin demo</option>
            <option value="retail-customer">RetailCustomer demo</option>
            <option value="other-retail-customer">RetailCustomer đối chứng</option>
          </select></label><button type="button" onClick={logout}>Đăng nhập</button></div>}
    </header><main key={`${activeFixture}-${screen}`}>
      {unsupportedRole ? <section className="panel padded access-denied">
        <h1>Chức năng chưa khả dụng</h1>
        <p className="muted">Tài khoản {session.user.role} đã đăng nhập nhưng chưa có màn hình
          được tích hợp trong ứng dụng này.</p>
      </section> : <>
        {screen === 'users' && <UsersScreen fixture={activeFixture} route={route} navigate={navigate} />}
        {screen === 'categories' && <CategoriesScreen fixture={activeFixture} route={route} navigate={navigate} />}
        {screen === 'checkout' && <CheckoutScreen fixture={activeFixture} />}
      </>}
    </main><footer>{databaseReady ? 'SQL Server · EF Core' : 'Môi trường phát triển'}
      {' · '}Xác thực đăng nhập dùng JWT; các chức năng nghiệp vụ khác vẫn đang tích hợp.</footer>
    </div></div>;
}
