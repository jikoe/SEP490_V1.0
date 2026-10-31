import React, { useState } from 'react';
import { Notice } from '../../components/ui/Common';
import PaymentReconciliationDetail from './PaymentReconciliationDetail';
import {
  financeApi, financeUrl, FinanceHeader, FinanceSource, LoadState, Pager,
  PaymentTable, useFinanceData, useFinanceSource,
} from './FinanceUi';

function PaymentList({ route, navigate, sample, setSample }) {
  const params = new URLSearchParams(route.split('?')[1] || '');
  const applied = {
    search: params.get('search') || '', category: params.get('category') || '',
    provider: params.get('provider') || '', status: params.get('status') || '',
    from: params.get('from') || '', to: params.get('to') || '',
    needsAttention: params.get('needsAttention') === 'true',
    page: Number(params.get('page') || 1), pageSize: 10,
  };
  const [filters, setFilters] = useState(applied);
  const [validation, setValidation] = useState('');
  const result = useFinanceData(financeApi('payments', sample, applied));
  function update(key, value) { setFilters(current => ({ ...current, [key]: value })); }
  function search(event) {
    event.preventDefault();
    if (filters.from && filters.to && filters.from > filters.to) {
      setValidation('Ngày bắt đầu phải không sau ngày kết thúc.'); return;
    }
    setValidation('');
    navigate(financeUrl('/finance/payments', { ...filters, search: filters.search.trim(), page: 1 }));
    result.refresh();
  }
  function clear() {
    setFilters({ search: '', category: '', provider: '', status: '', from: '', to: '', needsAttention: false });
    setValidation(''); navigate('/finance/payments'); result.refresh();
  }
  function open(id) { navigate(financeUrl(`/finance/payments/${id}`, applied)); }
  function source(value) { setSample(value); result.setData(null); }
  return <section className="finance-page">
    <FinanceHeader sample={sample} title="Đối soát thanh toán"
      description="Kiểm tra thanh toán đơn hàng và gói thành viên với PayOS / VietQR."
      onRefresh={result.refresh} busy={result.loading} />
    <form className="fin-panel fin-filters" onSubmit={search}>
      <label className="fin-filter-search">Tìm kiếm<input value={filters.search} maxLength={150}
        onChange={event => update('search', event.target.value)}
        placeholder="Mã giao dịch, tham chiếu hoặc nghĩa vụ" /></label>
      <label>Loại thanh toán<select value={filters.category} onChange={event => update('category', event.target.value)}>
        <option value="">Tất cả</option><option value="Order">Đơn hàng</option>
        <option value="Subscription">Gói thành viên</option></select></label>
      <label>Cổng thanh toán<select value={filters.provider} onChange={event => update('provider', event.target.value)}>
        <option value="">Tất cả</option>{['PayOS', 'VietQR', ...(!sample ? ['VNPay', 'MoMo'] : [])].map(provider =>
          <option key={provider} value={provider}>{provider}</option>)}</select></label>
      <label>Trạng thái<select value={filters.status} onChange={event => update('status', event.target.value)}>
        <option value="">Tất cả</option><option value="Pending">Chờ xác minh</option>
        <option value="Succeeded">Thành công</option><option value="Failed">Thất bại</option>
        <option value="Expired">Hết hạn</option></select></label>
      <label className="fin-filter-date">Từ ngày tạo<input type="date" value={filters.from} min="1900-01-01"
        max="9998-12-31" onChange={event => update('from', event.target.value)} /></label>
      <label className="fin-filter-date">Đến ngày tạo<input type="date" value={filters.to} min="1900-01-01"
        max="9998-12-31" onChange={event => update('to', event.target.value)} /></label>
      <div className="fin-filter-actions"><button className="fin-primary" type="submit" disabled={result.loading}>
        Tìm kiếm</button><button type="button" onClick={clear} disabled={result.loading}>Xóa bộ lọc</button></div>
      {applied.needsAttention && <p className="fin-filter-notice">Đang lọc giao dịch cần xử lý.
        <button className="fin-text" type="button" onClick={clear}>Bỏ bộ lọc</button></p>}
    </form>
    <Notice error>{validation}</Notice>
    <LoadState {...result} retry={result.refresh} hasData={Boolean(result.data)} />
    {result.data && <section className="fin-panel"><div className="fin-panel-heading">
      <h2>Danh sách giao dịch · {result.data.total} kết quả</h2><span>Đơn vị: VND</span></div>
      {result.data.total ? <PaymentTable items={result.data.items} onOpen={open} />
        : <p className="fin-empty">Không tìm thấy giao dịch phù hợp với bộ lọc.</p>}
      <div className="fin-pagination"><Pager total={result.data.total} page={result.data.page}
        pageSize={10} onPage={page => navigate(financeUrl('/finance/payments', { ...applied, page }))} /></div>
    </section>}
    <p className="fin-calculation">Chọn một giao dịch để xem nghĩa vụ liên kết, bằng chứng từ cổng và lịch sử đối soát.
      Chỉ ghi nhận thành công khi tham chiếu, số tiền và kết quả được xác minh.</p>
    <FinanceSource sample={sample} onChange={source} />
  </section>;
}

export default function PaymentReconciliationScreen({ route, navigate }) {
  const [sample, setSample] = useFinanceSource();
  const id = route.split('?')[0].match(/^\/finance\/payments\/(\d+)$/)?.[1];
  return id ? <PaymentReconciliationDetail key={`${id}-${sample}`} id={id} route={route}
    sample={sample} setSample={setSample} navigate={navigate} />
    : <PaymentList route={route} navigate={navigate} sample={sample} setSample={setSample} />;
}
