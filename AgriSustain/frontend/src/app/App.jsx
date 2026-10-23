import React, { useEffect, useState } from 'react';
import UsersScreen from '../features/administration/UsersScreen';
import CategoriesScreen from '../features/administration/CategoriesScreen';
import CheckoutScreen from '../features/orders/CheckoutScreen';

/** Reads only the assigned screen paths; unknown hashes return to User Management. */
function readRoute() {
  const path = window.location.hash.slice(1);
  return /^\/(administration\/(users|categories)(\/(new|\d+))?|checkout)$/.test(path)
    ? path : '/administration/users';
}

/** Restores only the public development fixture selector; no credential or token is stored. */
function readFixture() {
  const selected = sessionStorage.getItem('agrisustain:demo-fixture');
  return ['admin', 'retail-customer', 'other-retail-customer'].includes(selected) ? selected
    : window.location.hash === '#/checkout' ? 'retail-customer' : 'admin';
}

/** Navigates between the assigned screens using browser history without adding another UC. */
function navigate(path) { window.location.hash = path; }

/** Renders the SRS administration/checkout shell while keeping the authorized fixture selector explicit. */
export default function App() {
  const [fixture, setFixture] = useState(readFixture);
  const [route, setRoute] = useState(readRoute);
  const [persistence, setPersistence] = useState(null);
  const [databaseReady, setDatabaseReady] = useState(false);
  const admin = fixture === 'admin';
  const screen = route.startsWith('/administration/categories') ? 'categories'
    : route === '/checkout' ? 'checkout' : 'users';
  useEffect(() => {
    /** Synchronizes browser Back/Forward with the assigned screen path. */
    const handleRoute = () => setRoute(readRoute());
    window.addEventListener('hashchange', handleRoute);
    return () => window.removeEventListener('hashchange', handleRoute);
  }, []);
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
  /** Switches only the local identity fixture and returns to the corresponding assigned screen. */
  function selectFixture(value) {
    sessionStorage.setItem('agrisustain:demo-fixture', value);
    setFixture(value);
    navigate(value === 'admin' ? '/administration/users' : '/checkout');
  }
  return <div className="app-shell"><aside className="sidebar">
    <a className="brand" href={admin ? '#/administration/users' : '#/checkout'}><span className="brand-icon">✦</span>
      <span>AgriSustain<small>{admin ? 'ADMIN CONSOLE' : 'RETAIL MARKET'}</small></span></a>
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
        {admin ? 'SystemAdmin' : 'RetailCustomer'}</strong><small>Phiên phát triển</small></div>
    </div><p><span className="status-dot" />{databaseReady ? 'Đã kết nối SQL · EF Core'
      : persistence === 'SqlServer' ? 'Chưa xác nhận kết nối SQL'
        : persistence === 'InMemory' ? 'Dữ liệu trong bộ nhớ' : 'Đang kết nối API'}</p></div>
    </aside><div className="workspace"><header><span className="header-path">
      {admin ? 'Administration' : 'Consumer Ordering'}<span> / {screen === 'users'
        ? 'Người dùng' : screen === 'categories' ? 'Danh mục sản phẩm' : 'Checkout'}</span></span>
      <label className="session-selector">Phiên giả lập<select aria-label="Phiên giả lập"
        value={fixture} onChange={(event) => selectFixture(event.target.value)}>
        <option value="admin">SystemAdmin demo</option>
        <option value="retail-customer">RetailCustomer demo</option>
        <option value="other-retail-customer">RetailCustomer đối chứng</option></select></label>
    </header><main key={`${fixture}-${screen}`}>
      {screen === 'users' && <UsersScreen fixture={fixture} route={route} navigate={navigate} />}
      {screen === 'categories' && <CategoriesScreen fixture={fixture} route={route} navigate={navigate} />}
      {screen === 'checkout' && <CheckoutScreen fixture={fixture} />}
    </main><footer>{databaseReady ? 'SQL Server · EF Core' : 'Môi trường phát triển'}
      {' · '}Đăng nhập, điểm thưởng, chính sách giao hàng và liên kết thanh toán còn giả lập.</footer>
    </div></div>;
}
