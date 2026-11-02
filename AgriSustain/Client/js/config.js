/* ==========================================================================
   AgriSustain - Config Constants (js/config.js)
   ========================================================================== */

// Detect if running through Vite dev proxy (port 5173)
const isViteProxy = (window.location.port === '5173' || window.location.hostname === '127.0.0.1');

const CONFIG = {
  // If served via Vite proxy at 5173, use '/api' to proxy cleanly to backend 5179; otherwise direct backend URL
  API_BASE_URL: isViteProxy ? '/api' : 'http://localhost:5179/api',
  API_BASE_URL_FALLBACK: 'https://localhost:7055/api',

  STORAGE_KEYS: {
    ACCESS_TOKEN: 'agri_access_token',
    REFRESH_TOKEN: 'agri_refresh_token',
    USER_INFO: 'agri_user_info'
  },

  ROLES: {
    ADMINISTRATOR: 'Administrator',
    FARMER: 'Farmer',
    RETAIL_CUSTOMER: 'RetailCustomer',
    WHOLESALE_BUYER: 'WholesaleBuyer',
    CONTENT_MODERATOR: 'ContentModerator',
    CUSTOMER_SUPPORT: 'CustomerSupport',
    PROMOTION_STAFF: 'PromotionStaff',
    FINANCE_STAFF: 'FinanceStaff',
    VERIFICATION_OFFICER: 'VerificationOfficer',
    GUEST: 'Guest'
  },

  PRODUCT_STATUS: {
    Draft: { label: 'Bản nháp', badge: 'badge-grey' },
    Pending: { label: 'Chờ duyệt', badge: 'badge-yellow' },
    Approved: { label: 'Đã duyệt', badge: 'badge-green' },
    Rejected: { label: 'Bị từ chối', badge: 'badge-red' },
    Published: { label: 'Đang bán', badge: 'badge-blue' },
    Hidden: { label: 'Tạm ẩn', badge: 'badge-grey' }
  },

  ORDER_STATUS: {
    Pending: { label: 'Chờ xác nhận', badge: 'badge-yellow' },
    Confirmed: { label: 'Đã xác nhận', badge: 'badge-blue' },
    ReadyForPickup: { label: 'Sẵn sàng giao', badge: 'badge-purple' },
    Dispatched: { label: 'Đang vận chuyển', badge: 'badge-blue' },
    Delivered: { label: 'Giao thành công', badge: 'badge-green' },
    Rejected: { label: 'Đã từ chối', badge: 'badge-red' },
    Cancelled: { label: 'Đã hủy', badge: 'badge-red' }
  },

  USER_STATUS: {
    Active: { label: 'Hoạt động', badge: 'badge-green' },
    Locked: { label: 'Đang khóa', badge: 'badge-yellow' },
    Suspended: { label: 'Đình chỉ', badge: 'badge-red' }
  },

  COMPLAINT_STATUS: {
    Pending: { label: 'Chờ xử lý', badge: 'badge-yellow' },
    Investigating: { label: 'Đang xác minh', badge: 'badge-blue' },
    Resolved: { label: 'Đã giải quyết', badge: 'badge-green' },
    Rejected: { label: 'Bác bỏ', badge: 'badge-red' }
  }
};
