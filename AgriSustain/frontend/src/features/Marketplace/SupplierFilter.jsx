import React, { useId, useRef, useState } from 'react';

const searchable = value => value.toLocaleLowerCase('vi-VN').normalize('NFD')
  .replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd');

export default function SupplierFilter({ options, value, onChange }) {
  const id = useId();
  const dropdown = useRef(null);
  const [search, setSearch] = useState('');
  const selected = options.find(option => String(option.id) === value);
  const visible = options.filter(option => searchable(option.name).includes(searchable(search.trim())));
  const optionElements = items => <>
    <option value="">Tất cả nhà cung cấp</option>
    {items.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
  </>;

  if (options.length <= 10) return <label className="mkt-field">Nhà cung cấp
    <select value={value} onChange={event => onChange(event.target.value)}>
      {optionElements(options)}
    </select>
  </label>;

  function close() {
    dropdown.current.open = false;
    dropdown.current.querySelector('summary').focus();
  }

  return <div className="mkt-field"><span>Nhà cung cấp</span>
    <details className="mkt-supplier-picker" ref={dropdown}
      onToggle={event => { if (!event.currentTarget.open) setSearch(''); }}
      onBlur={event => {
        if (!event.currentTarget.contains(event.relatedTarget)) dropdown.current.open = false;
      }}
      onKeyDown={event => { if (event.key === 'Escape') { event.preventDefault(); close(); } }}>
      <summary role="button" aria-label="Chọn nhà cung cấp">
        <span>{selected?.name || 'Tất cả nhà cung cấp'}</span><span aria-hidden="true">▾</span>
      </summary>
      <div className="mkt-supplier-options">
        <label htmlFor={id}>Tìm nhà cung cấp</label>
        <input id={id} type="search" maxLength={150} value={search}
          placeholder="Nhập tên nhà cung cấp…" onChange={event => setSearch(event.target.value)} />
        <select aria-label="Nhà cung cấp" size={6} value={value}
          onChange={event => { onChange(event.target.value); close(); }}>
          {selected && !visible.includes(selected) && <option value={value} hidden>{selected.name}</option>}
          {optionElements(visible)}
        </select>
        {!visible.length && <p role="status">Không tìm thấy nhà cung cấp.</p>}
      </div>
    </details>
  </div>;
}
