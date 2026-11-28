import { request, showNotice } from './page-ui.js';
import { esc, cleanMessage, money, localTime, status, dateLabel, todayVietnam } from './finance-ui.js';

let currentPeriod = { period: 'Month', date: `${todayVietnam().slice(0, 7)}-01` };
let currentMetric = 'Gmv';
let currentPage = 1;
let currentSummaryData = null;
let currentRecordsData = null;

const notice = document.getElementById('finance-notice');
const today = todayVietnam();

function initPeriodInputs() {
  const container = document.getElementById('period-dynamic-inputs');
  const modeSelect = document.getElementById('period-mode');
  
  const updateInputs = () => {
    const mode = modeSelect.value;
    if (mode === 'Day') {
      container.innerHTML = `
        <label>Ngày báo cáo
          <input id="input-day" type="date" value="${today}" min="1900-01-01" max="9998-12-31">
        </label>`;
    } else if (mode === 'Custom') {
      container.innerHTML = `
        <label>Từ ngày
          <input id="input-from" type="date" value="${today.slice(0, 7)}-01">
        </label>
        <label>Đến ngày
          <input id="input-to" type="date" value="${today}">
        </label>`;
    } else {
      // Month
      container.innerHTML = `
        <label>Tháng báo cáo
          <input id="input-month" type="month" value="${today.slice(0, 7)}" min="1900-01" max="9998-12">
        </label>`;
    }
  };

  modeSelect.addEventListener('change', updateInputs);
  updateInputs();
}

async function loadSummary() {
  try {
    const query = {
      period: currentPeriod.period,
      date: currentPeriod.date
    };
    const res = await request(`/finance/dashboard?${API.toQueryString(query)}`);
    currentSummaryData = res;

    // Render KPI Cards
    const m = res.metrics || {};
    document.getElementById('kpi-gmv').textContent = money(m.Gmv);
    document.getElementById('kpi-fees').textContent = money(m.PlatformFees);
    document.getElementById('kpi-subscriptions').textContent = money(m.Subscriptions);
    document.getElementById('kpi-tax').textContent = money(m.TaxWithheld);
    document.getElementById('kpi-refunds').textContent = money(m.Refunds);
    document.getElementById('kpi-payouts').textContent = money(m.PendingPayouts);
    document.getElementById('kpi-payouts-sub').textContent = `Đã giải ngân: ${money(m.CompletedPayouts)}`;

    document.getElementById('report-refresh-time').textContent = `Cập nhật: ${localTime(res.refreshedAt)}`;
    document.getElementById('print-period-text').textContent = `Kỳ báo cáo: ${dateLabel(res.period.start)} đến ${dateLabel(res.period.end)}`;
  } catch (err) {
    showNotice(notice, cleanMessage(err.message));
  }
}

async function loadRecords(page = 1) {
  currentPage = page;
  const tbody = document.getElementById('report-records-body');
  tbody.innerHTML = `<tr><td colspan="7" class="fin-empty-state">Đang tải danh sách chứng từ...</td></tr>`;

  try {
    const query = {
      metric: currentMetric,
      period: currentPeriod.period,
      date: currentPeriod.date,
      page: currentPage,
      pageSize: 10
    };
    const res = await request(`/finance/dashboard/records?${API.toQueryString(query)}`);
    currentRecordsData = res;

    const items = res.items || [];
    document.getElementById('table-record-count').textContent = `${res.total || 0} chứng từ`;
    
    if (items.length === 0) {
      tbody.innerHTML = `<tr><td colspan="7" class="fin-empty-state">Không có chứng từ tài chính nào trong kỳ đã chọn.</td></tr>`;
      document.getElementById('btn-page-prev').disabled = true;
      document.getElementById('btn-page-next').disabled = true;
      document.getElementById('page-indicator').textContent = 'Trang 1 / 1';
      return;
    }

    tbody.innerHTML = items.map((item, idx) => {
      const stt = (currentPage - 1) * 10 + (idx + 1);
      return `
        <tr>
          <td>${stt}</td>
          <td><strong>${esc(item.reference)}</strong></td>
          <td>${esc(item.relatedReference || '—')}</td>
          <td style="font-weight:600; color:#1c3326;">${money(item.amount)}</td>
          <td>${status(item.status)}</td>
          <td>${localTime(item.at)}</td>
          <td>
            ${item.paymentId ? `<a class="btn fin-view" href="payment-detail.html?id=${item.paymentId}">Chi tiết GD</a>` : '—'}
          </td>
        </tr>
      `;
    }).join('');

    const totalPages = Math.max(1, Math.ceil((res.total || 0) / 10));
    document.getElementById('page-indicator').textContent = `Trang ${currentPage} / ${totalPages}`;
    document.getElementById('btn-page-prev').disabled = currentPage <= 1;
    document.getElementById('btn-page-next').disabled = currentPage >= totalPages;
  } catch (err) {
    tbody.innerHTML = `<tr><td colspan="7" class="fin-empty-state" style="color:#c81e1e;">${cleanMessage(err.message)}</td></tr>`;
  }
}

function exportExcel() {
  if (!currentRecordsData || !currentRecordsData.items || currentRecordsData.items.length === 0) {
    alert('Không có dữ liệu chứng từ để xuất Excel!');
    return;
  }

  const items = currentRecordsData.items;
  let csvContent = "\uFEFF"; // UTF-8 BOM for Excel Vietnamese support
  csvContent += "STT,Ma Chung Tu,Ma Lien Ket,So Tien (VND),Trang Thai,Thoi Gian\n";

  items.forEach((item, index) => {
    const row = [
      index + 1,
      `"${item.reference}"`,
      `"${item.relatedReference || ''}"`,
      item.amount,
      `"${item.status}"`,
      `"${localTime(item.at)}"`
    ];
    csvContent += row.join(",") + "\n";
  });

  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.setAttribute("href", url);
  link.setAttribute("download", `BaoCao_TaiChinh_${currentMetric}_${today}.csv`);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
}

function initEventHandlers() {
  // Submit Filter
  document.getElementById('report-filter-form').addEventListener('submit', (e) => {
    e.preventDefault();
    const mode = document.getElementById('period-mode').value;
    if (mode === 'Day') {
      currentPeriod = { period: 'Day', date: document.getElementById('input-day').value };
    } else if (mode === 'Month') {
      currentPeriod = { period: 'Month', date: `${document.getElementById('input-month').value}-01` };
    } else {
      // Custom - default fallback to month
      currentPeriod = { period: 'Month', date: `${today.slice(0, 7)}-01` };
    }

    loadSummary();
    loadRecords(1);
  });

  // Switch Metric Tabs
  document.querySelectorAll('.report-tab-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('.report-tab-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      currentMetric = btn.dataset.metric;
      document.getElementById('table-title').textContent = `Chi tiết bảng kê: ${btn.textContent.trim()}`;
      loadRecords(1);
    });
  });

  // Pagination
  document.getElementById('btn-page-prev').addEventListener('click', () => {
    if (currentPage > 1) loadRecords(currentPage - 1);
  });
  document.getElementById('btn-page-next').addEventListener('click', () => {
    loadRecords(currentPage + 1);
  });

  // Export Buttons
  document.getElementById('btn-export-excel').addEventListener('click', exportExcel);
  document.getElementById('btn-export-pdf').addEventListener('click', () => {
    window.print();
  });
}

// Entry Point
document.addEventListener('DOMContentLoaded', () => {
  initPeriodInputs();
  initEventHandlers();
  loadSummary();
  loadRecords(1);
});
