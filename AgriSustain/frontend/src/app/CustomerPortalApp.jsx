import React, { useEffect, useState } from 'react';
import RegisterScreen from '../features/Guest/RegisterScreen';
import ProfileScreen from '../features/RetailCustomer/ProfileScreen';
import OrderTrackingScreen from '../features/RetailCustomer/OrderTrackingScreen';
import CustomerHeader from '../layouts/CustomerHeader';
import CustomerFooter from '../layouts/CustomerFooter';
import { getSession } from '../session/session';
import '../styles/customer-portal.css';

/**
 * CustomerPortalApp (Sàn Nông Sản Việt - AgriSustain)
 * Thiết kế giao diện chuẩn Figma SCR-TRC-01 cho Iteration 1 của Dự (DuND):
 * - SCR-07: Đăng ký tài khoản (UC-AUTH-01 / UC-AUTH-08)
 * - SCR-11: Quản lý hồ sơ & địa chỉ (UC-AUTH-03)
 * - SCR-16: Lịch sử & Theo dõi đơn hàng (UC-RC-04)
 */

function readPortalRoute() {
  const path = window.location.hash.slice(1);
  return /^\/(register|profile|orders)$/.test(path) ? path : '/orders';
}

function readPortalFixture() {
  const selected = sessionStorage.getItem('agrisustain:demo-fixture');
  return selected || 'retail-customer';
}

function readStoredUser() {
  try {
    const raw = sessionStorage.getItem('agrisustain:current-user');
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

function navigate(path) {
  window.location.hash = path;
}

export default function CustomerPortalApp() {
  const [fixture, setFixture] = useState(readPortalFixture);
  const [currentUser, setCurrentUser] = useState(readStoredUser);
  const [route, setRoute] = useState(readPortalRoute);

  const isFarmer = currentUser ? currentUser.role === 'Farmer' : fixture === 'farmer';
  const roleName = currentUser ? currentUser.role : (isFarmer ? 'Farmer' : 'RetailCustomer');
  const userName = currentUser ? currentUser.fullName : (isFarmer ? 'Nguyễn Văn Hùng (Farmer)' : 'Lê Thị Mai (RetailBuyer)');
  const userEmail = currentUser ? currentUser.email : (isFarmer ? 'nongtrai.dalat@gmail.com' : 'khachle.hcm@gmail.com');

  const screen = route === '/register' ? 'register'
    : route === '/profile' ? 'profile'
    : 'orders';

  useEffect(() => {
    const handleRoute = () => setRoute(readPortalRoute());
    const handleUser = () => {
      setCurrentUser(readStoredUser());
      setFixture(readPortalFixture());
    };
    window.addEventListener('hashchange', handleRoute);
    window.addEventListener('agrisustain:user-changed', handleUser);
    return () => {
      window.removeEventListener('hashchange', handleRoute);
      window.removeEventListener('agrisustain:user-changed', handleUser);
    };
  }, []);

  function handleSwitchRole(role) {
    const newFixture = role === 'Farmer' ? 'farmer' : 'retail-customer';
    sessionStorage.removeItem('agrisustain:current-user');
    sessionStorage.setItem('agrisustain:demo-fixture', newFixture);
    setCurrentUser(null);
    setFixture(newFixture);
  }

  const breadcrumbLabels = {
    register: 'Đăng ký tài khoản mới',
    profile: 'Hồ sơ cá nhân & Sổ địa chỉ',
    orders: 'Lịch sử & Theo dõi đơn hàng',
  };

  return (
    <div className="customer-portal-shell">
      {/* 1. Header ngang đầy đủ danh mục nông sản, logo và tài khoản theo SCR-TRC-01 */}
      <CustomerHeader
        currentRoute={route}
        navigate={navigate}
        user={{
          fullName: userName,
          email: userEmail,
          role: roleName,
        }}
        onSwitchRole={handleSwitchRole}
      />

      {/* 2. Sub-header Slogan & Breadcrumb Bar */}
      <div className="portal-subbar">
        <div className="portal-subbar-container">
          <div className="portal-slogan">
            <span>🌿</span>
            <span>MINH BẠCH NGUỒN GỐC - AN TÂM LỰA CHỌN · HỆ THỐNG TRUY XUẤT NÔNG SẢN AGRISUSTAIN</span>
          </div>
          <div className="portal-breadcrumb">
            <a href="#/orders" onClick={(e) => { e.preventDefault(); navigate('/orders'); }}>Trang chủ</a>
            <span>›</span>
            <span>{breadcrumbLabels[screen]}</span>
          </div>
        </div>
      </div>

      {/* 3. Vùng nội dung chính của màn hình */}
      <main className="customer-portal-main">
        {screen === 'register' && <RegisterScreen navigate={navigate} />}
        {screen === 'profile' && <ProfileScreen session={getSession()} navigate={navigate} />}
        {screen === 'orders' && <OrderTrackingScreen navigate={navigate} />}
      </main>

      {/* 4. Footer phong cách thương mại nông sản chuẩn */}
      <CustomerFooter />
    </div>
  );
}
