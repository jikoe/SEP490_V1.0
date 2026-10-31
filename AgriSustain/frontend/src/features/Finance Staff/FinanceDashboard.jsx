import React, { useState } from 'react';
import { Modal } from '../../components/ui/Common';
import {
  dateLabel, financeApi, financeUrl, FinanceHeader, FinanceSource, LoadState, localTime,
  money, Pager, PaymentTable, Status, todayVietnam, useFinanceData, useFinanceSource,
} from './FinanceUi';

const metrics = [
  ['Gmv', 'GMV', 'Đã loại đơn hủy và khoản hoàn tiền hoàn tất'],
  ['Commission', 'Doanh thu hoa hồng', 'Hoa hồng của các kỳ quyết toán đã chi trả'],
  ['Subscriptions', 'Doanh thu gói thành viên', 'Khoản thu gói B2B đã xác minh'],
  ['Refunds', 'Hoàn tiền đã xác minh', 'Chỉ tính giao dịch hoàn tiền thành công'],
  ['PendingPayouts', 'Chờ thanh toán nông hộ', 'Tách riêng khoản đang chờ / đang xử lý'],
];

function SupportingRecords({ metric, period, sample, onClose, navigate }) {
  const [page, setPage] = useState(1);
  const result = useFinanceData(financeApi('dashboard/records', sample, {
    metric, period: period.type, date: period.start, page, pageSize: 10,
  }));
  return <Modal title={`Chứng từ · ${metrics.find(([key]) => key === metric)?.[1] || 'Chi trả đã hoàn tất'}`}
    onClose={onClose}>
    <p className="muted">{dateLabel(period.start)} – {dateLabel(period.end)} · {sample ? 'Dữ liệu mẫu' : 'Cơ sở dữ liệu'}</p>
    <LoadState {...result} retry={result.refresh} hasData={Boolean(result.data)} />
    {result.data && <><div className="fin-table-wrap"><table className="fin-table"><thead><tr>
      <th>Mã chứng từ</th><th>Liên quan</th><th>Số tiền</th><th>Trạng thái</th><th>Thời gian</th><th /></tr></thead>
      <tbody>{result.data.items.map(row => <tr key={row.id}><td><strong>{row.reference}</strong></td>
        <td>{row.relatedReference || '—'}</td><td>{money(row.amount)}</td><td><Status value={row.status}
          label={metric === 'PendingPayouts' && row.status === 'Pending' ? 'Chờ chi trả'
            : ['CompletedPayouts', 'Commission'].includes(metric) && row.status === 'Paid' ? 'Đã chi trả' : undefined} /></td>
        <td>{localTime(row.at)}</td><td>{row.paymentId && <button type="button"
          onClick={() => navigate(`/finance/payments/${row.paymentId}`)}>Xem giao dịch</button>}</td></tr>)}</tbody>
    </table></div>{!result.data.total && <p className="fin-empty">Không có chứng từ trong kỳ này.</p>}
      <Pager total={result.data.total} page={page} pageSize={10} onPage={setPage} /></>}
  </Modal>;
}

export default function FinanceDashboard({ navigate }) {
  const today = todayVietnam();
  const [sample, setSample] = useFinanceSource();
  const [type, setType] = useState('Month');
  const [date, setDate] = useState(today);
  const [month, setMonth] = useState(today.slice(0, 7));
  const [year, setYear] = useState(today.slice(0, 4));
  const [quarter, setQuarter] = useState(String(Math.ceil(Number(today.slice(5, 7)) / 3)));
  const [applied, setApplied] = useState({ period: 'Month', date: `${today.slice(0, 7)}-01` });
  const [metric, setMetric] = useState(null);
  const result = useFinanceData(financeApi('dashboard', sample, applied));
  const data = result.data;
  function apply(event) {
    event.preventDefault();
    setApplied({ period: type, date: type === 'Day' ? date : type === 'Month'
      ? `${month}-01` : `${year}-${String((Number(quarter) - 1) * 3 + 1).padStart(2, '0')}-01` });
    result.refresh();
  }
  function source(value) { setMetric(null); setSample(value); result.setData(null); }
  function openPayment(id) { navigate(`/finance/payments/${id}`); }
  function allPayments() {
    navigate(financeUrl('/finance/payments', { needsAttention: true,
      from: data?.period.start, to: data?.period.end }));
  }
  return <section className="finance-page">
    <FinanceHeader sample={sample} title="Tổng quan tài chính" description="Theo dõi số liệu tài chính và các giao dịch cần xử lý."
      onRefresh={result.refresh} busy={result.loading} />
    <form className="fin-panel fin-period" onSubmit={apply}>
      <div className="fin-period-fields"><label>Kỳ báo cáo<select value={type}
        onChange={event => setType(event.target.value)}><option value="Day">Theo ngày</option>
        <option value="Month">Theo tháng</option><option value="Quarter">Theo quý</option></select></label>
        {type === 'Day' && <label>Ngày<input type="date" value={date} required min="1900-01-01"
          max="9998-12-31" onChange={event => setDate(event.target.value)} /></label>}
        {type === 'Month' && <label>Tháng<input type="month" value={month} required min="1900-01"
          max="9998-12" onChange={event => setMonth(event.target.value)} /></label>}
        {type === 'Quarter' && <div className="fin-quarter"><label>Quý<select value={quarter}
          onChange={event => setQuarter(event.target.value)}>{[1, 2, 3, 4].map(value =>
            <option key={value} value={value}>Quý {value}</option>)}</select></label>
          <label>Năm<input type="number" min="1900" max="9998" value={year} required
            onChange={event => setYear(event.target.value)} /></label></div>}
        <button className="fin-primary" type="submit" disabled={result.loading}>Áp dụng</button></div>
      <div className="fin-period-caption"><span>Đang hiển thị: {data
        ? `${dateLabel(data.period.start)} – ${dateLabel(data.period.end)}` : 'Chưa có kết quả'}</span>
        <span>Cập nhật: {data ? localTime(data.refreshedAt) : 'Chưa tải'}</span></div>
    </form>
    <LoadState {...result} retry={result.refresh} hasData={Boolean(data)} />
    {data && <><div className="fin-metrics">{metrics.map(([key, label, caption]) =>
      <button className={`fin-metric ${key === 'Gmv' ? 'featured' : ''}`} type="button" key={key}
        aria-label={`Xem chứng từ ${label}`} onClick={() => setMetric(key)}>
        <span>{label}<span aria-hidden="true">↗</span></span>
        <strong>{data.metrics[key] == null ? 'Không khả dụng' : money(data.metrics[key])}</strong>
        <small>{caption}</small>{key === 'PendingPayouts' && <small>Đã chi trả: {
          data.metrics.CompletedPayouts == null ? 'Không khả dụng' : money(data.metrics.CompletedPayouts)}</small>}
      </button>)}<button className="fin-metric" type="button" onClick={allPayments}>
        <span>Giao dịch cần xử lý<span aria-hidden="true">◷</span></span>
        <strong>{data.transactionsToProcess == null ? 'Không khả dụng' : data.transactionsToProcess}
          {data.transactionsToProcess != null && <small> giao dịch</small>}</strong>
        <small>Thanh toán chưa xác minh hoặc có sai lệch</small></button></div>
      <button type="button" className="fin-text" onClick={() => setMetric('CompletedPayouts')}>
        Xem các khoản chi trả nông hộ đã hoàn tất →</button>
      {data.unavailable.length > 0 && <div className="fin-warning" role="alert">
        Một số số liệu chưa tải được. Giá trị không khả dụng chưa được tính là 0.
        <button type="button" disabled={result.loading} onClick={result.refresh}>Thử lại</button></div>}
      <section className="fin-panel"><div className="fin-panel-heading"><h2>Giao dịch cần xử lý</h2>
        <button type="button" className="fin-text" onClick={allPayments}>Xem tất cả →</button></div>
        {data.pendingTransactions == null ? <p className="fin-empty">Chưa tải được danh sách giao dịch.</p>
          : data.pendingTransactions.length === 0 ? <p className="fin-empty">Không có giao dịch cần xử lý trong kỳ này.</p>
            : <PaymentTable items={data.pendingTransactions} onOpen={openPayment} compact />}
        <div className="fin-table-caption"><span>Hiển thị {data.pendingTransactions?.length ?? '—'} / {
          data.transactionsToProcess ?? '—'} giao dịch cần xử lý trong kỳ</span><span>Chỉ xem dữ liệu</span></div>
      </section>
      <p className="fin-calculation">GMV loại trừ đơn đã hủy và khoản hoàn tất hoàn tiền.
        Khoản thu, hóa đơn và tiền cọc được áp dụng chỉ tính một lần.</p></>}
    <FinanceSource sample={sample} onChange={source} />
    {metric && data && <SupportingRecords key={`${metric}-${sample}`} metric={metric} period={data.period}
      sample={sample} navigate={navigate} onClose={() => setMetric(null)} />}
  </section>;
}
