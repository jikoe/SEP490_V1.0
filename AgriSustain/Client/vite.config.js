import { resolve } from 'path';
import { defineConfig } from 'vite';

export default defineConfig({
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:5179',
        changeOrigin: true,
      },
      '/health': {
        target: 'http://127.0.0.1:5179',
        changeOrigin: true,
      },
      '/media': {
        target: 'http://127.0.0.1:5179',
        changeOrigin: true,
      },
    },
  },
  build: {
    rollupOptions: {
      input: {
        main: resolve(__dirname, 'index.html'),
        guestLogin: resolve(__dirname, 'Guest/login.html'),
        guestRegister: resolve(__dirname, 'Guest/register.html'),
        guestForgotPassword: resolve(__dirname, 'Guest/forgot-password.html'),
        retailIndex: resolve(__dirname, 'RetailCustomer/index.html'),
        retailProductDetail: resolve(__dirname, 'RetailCustomer/product-detail.html'),
        retailCart: resolve(__dirname, 'RetailCustomer/cart.html'),
        retailCheckout: resolve(__dirname, 'RetailCustomer/checkout.html'),
        retailOrders: resolve(__dirname, 'RetailCustomer/orders.html'),
        retailProfile: resolve(__dirname, 'RetailCustomer/profile.html'),
        retailFeedback: resolve(__dirname, 'RetailCustomer/feedback.html'),
        adminDashboard: resolve(__dirname, 'Administrator/dashboard.html'),
        adminUsers: resolve(__dirname, 'Administrator/users.html'),
        adminCategories: resolve(__dirname, 'Administrator/categories.html'),
        farmerProducts: resolve(__dirname, 'Farmer/products.html'),
        farmerOrders: resolve(__dirname, 'Farmer/orders.html'),
        farmerProfile: resolve(__dirname, 'Farmer/profile.html'),
        moderation: resolve(__dirname, 'ContentModerator/moderation.html'),
        supportDashboard: resolve(__dirname, 'CustomerSupport/dashboard.html'),
        supportComplaints: resolve(__dirname, 'CustomerSupport/complaints.html'),
        promotionDashboard: resolve(__dirname, 'PromotionStaff/dashboard.html'),
        promotionVouchers: resolve(__dirname, 'PromotionStaff/vouchers.html'),
        financeDashboard: resolve(__dirname, 'FinanceStaff/dashboard.html'),
        financePayments: resolve(__dirname, 'FinanceStaff/payments.html'),
        financeRefunds: resolve(__dirname, 'FinanceStaff/refunds.html'),
        verificationDashboard: resolve(__dirname, 'VerificationOfficer/dashboard.html'),
        wholesaleDashboard: resolve(__dirname, 'WholesaleBuyer/dashboard.html'),
        gatewayDashboard: resolve(__dirname, 'PaymentGateway/dashboard.html'),
        gmailDashboard: resolve(__dirname, 'GmailService/dashboard.html'),
      },
    },
  },
});
