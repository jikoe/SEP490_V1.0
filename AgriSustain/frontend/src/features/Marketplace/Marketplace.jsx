import React, { useEffect, useRef, useState } from 'react';
import { request } from '../../api/client';
import ProductDetail from './ProductDetail';
import { Icon, ProductCard } from './MarketplaceUi';
import './marketplace.css';

const priceRanges = [
  ['under50', 'Dưới 50.000đ'], ['50to100', '50.000đ – 100.000đ'],
  ['100to200', '100.000đ – 200.000đ'], ['over200', 'Trên 200.000đ'],
];

export default function Marketplace({ route, navigate, session, onLogout, accountLinks = [] }) {
  const canBuy = session?.user.role === 'RetailCustomer';
  const query = route.includes('?') ? `?${route.split('?')[1]}` : '';
  const params = new URLSearchParams(query);
  const productId = route.split('?')[0].match(/^\/marketplace\/products\/(.+)$/)?.[1];
  const queryKeyword = params.get('keyword') || '';
  const [keyword, setKeyword] = useState(queryKeyword);
  const [isComposing, setIsComposing] = useState(false);
  const searchInput = useRef(null);
  const [filters, setFilters] = useState(null);
  const [catalogue, setCatalogue] = useState(null);
  const [error, setError] = useState('');
  const [filtersError, setFiltersError] = useState('');
  const [reload, setReload] = useState(0);

  useEffect(() => { setKeyword(queryKeyword); }, [queryKeyword]);
  useEffect(() => {
    if (isComposing || keyword.trim() === queryKeyword) return;
    const timer = setTimeout(() => {
      const next = new URLSearchParams(query);
      next.delete('page');
      next.delete('keyword');
      if (keyword.trim()) next.set('keyword', keyword.trim());
      navigate(`/marketplace${next.size ? `?${next}` : ''}`);
    }, 300);
    return () => clearTimeout(timer);
  }, [keyword, queryKeyword, query, isComposing, navigate]);
  useEffect(() => {
    const controller = new AbortController();
    setFiltersError('');
    request('/marketplace/filters', { signal: controller.signal })
      .then(result => setFilters(result.data))
      .catch(err => { if (err.name !== 'AbortError') setFiltersError(err.message); });
    return () => controller.abort();
  }, [reload]);
  useEffect(() => {
    if (productId) return;
    const controller = new AbortController();
    setCatalogue(null);
    setError('');
    request(`/marketplace/products${query}`, { signal: controller.signal })
      .then(result => { if (!controller.signal.aborted) setCatalogue(result.data); })
      .catch(err => { if (err.name !== 'AbortError') setError(err.message); });
    return () => controller.abort();
  }, [query, productId, reload]);

  function updateFilters(values) {
    const next = new URLSearchParams(query);
    next.delete('page');
    for (const [key, value] of Object.entries(values)) {
      next.delete(key);
      for (const item of Array.isArray(value) ? value : [value]) {
        if (item !== '' && item != null) next.append(key, item);
      }
    }
    navigate(`/marketplace${next.size ? `?${next}` : ''}`);
  }

  function toggleFilter(key, value) {
    const selected = params.getAll(key);
    updateFilters({ [key]: selected.includes(value)
      ? selected.filter(item => item !== value) : [...selected, value] });
  }

  const totalProducts = filters?.categories.reduce((total, item) => total + item.count, 0) || 0;
  const pages = catalogue ? Math.max(1, Math.ceil(catalogue.total / catalogue.pageSize)) : 1;
  const currentPage = catalogue?.page || 1;
  const pageNumbers = [...new Set([1,
    ...Array.from({ length: 5 }, (_, i) => currentPage + i - 2)
      .filter(page => page > 1 && page < pages), pages])];

  return <div className="marketplace">
    <header className="mkt-header">
      <a className="mkt-brand" href="#/marketplace"><Icon /><span>Nông Sản Việt</span></a>
      <p className="mkt-slogan">Nền tảng nông sản Việt – Kết nối nông dân, bền vững tương lai</p>
      <form className="mkt-search" role="search" onSubmit={event => {
        event.preventDefault();
        if (!isComposing) updateFilters({ keyword: keyword.trim() });
      }}>
        <button aria-label="Tìm kiếm" type="submit"><Icon name="search" /></button>
        <input ref={searchInput} aria-label="Tìm kiếm sản phẩm" type="search" maxLength={150}
          value={keyword} autoComplete="off" spellCheck={false}
          placeholder="Tìm kiếm sản phẩm, danh mục…" onChange={event => setKeyword(event.target.value)}
          onCompositionStart={() => setIsComposing(true)}
          onCompositionEnd={event => { setIsComposing(false); setKeyword(event.currentTarget.value); }} />
        {keyword && <button type="button" className="mkt-search-clear" aria-label="Xóa tìm kiếm"
          onClick={() => { setKeyword(''); searchInput.current?.focus(); }}><span aria-hidden="true">×</span></button>}
      </form>
      {canBuy ? <button type="button" className="mkt-cart-button" aria-label="Giỏ hàng" title="Giỏ hàng"
        onClick={() => navigate('/cart')}><Icon name="cart" /></button>
        : !session && <a className="mkt-cart-button" href="#/login" aria-label="Đăng nhập để mua hàng"
          title="Đăng nhập để mua hàng"><Icon name="cart" /></a>}
      {session ? <details className="mkt-account">
        <summary><Icon name="user" /><span>{session.user.fullName}</span><span>⌄</span></summary>
        <div>{accountLinks.map(link => <a key={link.path} href={`#${link.path}`}>{link.label}</a>)}
          <a href="#/account/password">Đổi mật khẩu</a>
          <button onClick={onLogout}>Đăng xuất</button></div>
      </details> : <a className="mkt-login" href="#/login"><Icon name="user" />Đăng nhập</a>}
    </header>
    {productId ? <ProductDetail key={productId} productId={productId} query={query}
      reload={reload} session={session} navigate={navigate} />
      : <main className="mkt-layout">
        <aside className="mkt-sidebar" aria-label="Bộ lọc sản phẩm">
          <h2><Icon name="grid" />Danh mục sản phẩm</h2>
          {filtersError && <p role="alert">{filtersError}
            <button onClick={() => setReload(value => value + 1)}>Thử lại</button></p>}
          <button className={`mkt-category ${!params.get('categoryId') ? 'active' : ''}`}
            onClick={() => updateFilters({ categoryId: '' })}>
            <Icon />Tất cả sản phẩm <span>{totalProducts.toLocaleString('vi-VN')}</span></button>
          {filters?.categories.map(category => <button key={category.id}
            className={`mkt-category ${params.get('categoryId') === String(category.id) ? 'active' : ''}`}
            onClick={() => updateFilters({ categoryId: category.id })}>
            <Icon />{category.name}<span>{category.count}</span></button>)}
          <div className="mkt-filter-heading"><h2><Icon name="filter" />Bộ lọc</h2>
            <button onClick={() => navigate('/marketplace')}>Xóa bộ lọc</button></div>
          <fieldset><legend>Khoảng giá</legend>{priceRanges.map(([value, label]) =>
            <label className="mkt-checkbox" key={value}><input type="checkbox"
              checked={params.getAll('priceRanges').includes(value)}
              onChange={() => toggleFilter('priceRanges', value)} />{label}</label>)}</fieldset>
          <label className="mkt-field">Địa phương<select value={params.get('region') || ''}
            onChange={event => updateFilters({ region: event.target.value })}>
            <option value="">Tất cả tỉnh/thành</option>
            {filters?.regions.map(region => <option key={region}>{region}</option>)}</select></label>
          <fieldset><legend>Chứng nhận</legend>{filters?.certifications.map(type =>
            <label className="mkt-checkbox" key={type}><input type="checkbox"
              checked={params.getAll('certifications').includes(type)}
              onChange={() => toggleFilter('certifications', type)} />{type}</label>)}</fieldset>
          <label className="mkt-field">Nhà cung cấp<select value={params.get('supplierId') || ''}
            onChange={event => updateFilters({ supplierId: event.target.value })}>
            <option value="">Tất cả nhà cung cấp</option>{filters?.suppliers.map(item =>
              <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
          <label className="mkt-field">Mùa vụ<select value={params.get('cropSeasonId') || ''}
            onChange={event => updateFilters({ cropSeasonId: event.target.value })}>
            <option value="">Tất cả mùa vụ</option>{filters?.seasons.map(item =>
              <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
          <label className="mkt-field">Đánh giá tối thiểu<select value={params.get('minRating') || ''}
            onChange={event => updateFilters({ minRating: event.target.value })}>
            <option value="">Tất cả đánh giá</option>{[4, 3, 2, 1].map(rating =>
              <option key={rating} value={rating}>{rating} sao trở lên</option>)}</select></label>
        </aside>
        <div className="mkt-catalogue">
          <section className="mkt-hero">
            <div><h1>Nông sản Việt<br />Tươi ngon từ nông trại đến bàn ăn</h1>
              <p>Sản phẩm chất lượng – Người nông dân được hỗ trợ –<br />Cộng đồng phát triển bền vững</p>
              <a className="mkt-primary" href="#mkt-products" onClick={event => {
                event.preventDefault(); document.getElementById('mkt-products')?.scrollIntoView({ behavior: 'smooth' });
              }}>Khám phá ngay <Icon name="arrow" /></a></div>
            <div className="mkt-hero-art" aria-hidden="true"><Icon /><Icon /><Icon /></div>
          </section>
          {filters?.categories.length > 0 && <nav className="mkt-category-strip" aria-label="Danh mục nhanh">
            {filters.categories.map(category => <button key={category.id}
              onClick={() => updateFilters({ categoryId: category.id })}>
              <Icon /><span>{category.name}</span></button>)}
          </nav>}
          <section id="mkt-products" aria-busy={!catalogue && !error}>
            <div className="mkt-results-heading"><h2><Icon />{query ? 'Kết quả tìm kiếm' : 'Sản phẩm nổi bật'}</h2>
              <label>Sắp xếp<select value={params.get('sort') || 'newest'}
                onChange={event => updateFilters({ sort: event.target.value })}>
                <option value="newest">Mới nhất</option><option value="price-asc">Giá tăng dần</option>
                <option value="price-desc">Giá giảm dần</option><option value="rating">Đánh giá cao nhất</option>
              </select></label></div>
            {error ? <div className="mkt-state" role="alert"><p>{error}</p>
              <button onClick={() => setReload(value => value + 1)}>Thử lại</button>
              <button onClick={() => navigate('/marketplace')}>Xóa bộ lọc</button></div>
              : !catalogue ? <div className="mkt-state" role="status">Đang tải sản phẩm…</div>
                : !catalogue.items.length ? <div className="mkt-state" role="status">
                  <Icon name="search" /><h3>Không tìm thấy sản phẩm phù hợp</h3>
                  <p>Thử thay đổi từ khóa hoặc xóa bộ lọc để xem thêm sản phẩm.</p>
                  <button onClick={() => navigate('/marketplace')}>Xóa bộ lọc</button></div>
                  : <><p className="mkt-result-count" role="status">{catalogue.total} sản phẩm</p>
                    <div className="mkt-product-grid">{catalogue.items.map(product =>
                      <ProductCard key={product.id} product={product} query={query} />)}</div></>}
            {catalogue && catalogue.total > 0 && <div className="mkt-pagination">
              <nav aria-label="Phân trang"><button aria-label="Trang trước" disabled={currentPage <= 1}
                onClick={() => updateFilters({ page: currentPage - 1 })}>‹</button>
                {pageNumbers.map((page, index) => <React.Fragment key={page}>
                  {index > 0 && page - pageNumbers[index - 1] > 1 && <span>…</span>}
                  <button aria-label={`Trang ${page}`} aria-current={page === currentPage ? 'page' : undefined}
                    onClick={() => updateFilters({ page })}>{page}</button></React.Fragment>)}
                <button aria-label="Trang sau" disabled={currentPage >= pages}
                  onClick={() => updateFilters({ page: currentPage + 1 })}>›</button></nav>
              <label>Hiển thị<select value={params.get('pageSize') || '12'}
                onChange={event => updateFilters({ pageSize: event.target.value })}>
                {[12, 24, 48].map(size => <option key={size} value={size}>{size} sản phẩm</option>)}
              </select></label></div>}
          </section>
        </div>
      </main>}
  </div>;
}
