// Fixed frontend options: the selected value is sent to the API as a string.
export const productUnitGroups = [
  { label: 'Khối lượng', values: [['g','Gam (g)'], ['kg','Kilôgam (kg)'], ['tạ','Tạ (100 kg)'], ['tấn','Tấn (1.000 kg)']] },
  { label: 'Thể tích', values: [['ml','Mililít (ml)'], ['lít','Lít']] },
  { label: 'Số lượng', values: ['cái','quả','củ','bắp','bó','chùm','buồng','cây','cành','bông','con'].map(value => [value, value[0].toLocaleUpperCase('vi') + value.slice(1)]) },
  { label: 'Đóng gói', values: ['gói','túi','hộp','khay','vỉ','chai','lọ','hũ','lon','can','bao','thùng','sọt','rổ'].map(value => [value, value[0].toLocaleUpperCase('vi') + value.slice(1)]) },
];
export const productUnits = productUnitGroups.flatMap(group => group.values.map(([value]) => value));
