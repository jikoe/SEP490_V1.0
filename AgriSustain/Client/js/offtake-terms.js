export const contractArticles = [
  ['Điều 1: Nội dung hợp đồng', 'article1', 'Cam kết mua và giao sản phẩm, phạm vi bao tiêu'],
  ['Điều 2: Yêu cầu về sản xuất sản phẩm', 'article2', 'Quy trình sản xuất, nguồn gốc và thu hoạch'],
  ['Điều 3: Kiểm soát chất lượng sản phẩm sau thu hoạch', 'article3', 'Tiêu chuẩn, phẩm cấp và cách nghiệm thu'],
  ['Điều 4: Yêu cầu về vận chuyển và giao nhận hàng', 'deliveryTerms', 'Địa điểm giao, đóng gói, vận chuyển và bên chịu chi phí'],
  ['Điều 5: Thanh toán', 'paymentTerms', 'Hạn thanh toán, phương thức và cách hoàn hoặc cấn trừ tiền cọc'],
  ['Điều 6: Quyền lợi và nghĩa vụ của các bên', 'article6', 'Trách nhiệm của bên bán và bên mua'],
  ['Điều 7: Hiệu lực của hợp đồng', 'article7', 'Điều kiện có hiệu lực hoặc chấm dứt nếu có thỏa thuận thêm'],
  ['Điều 8: Điều khoản chung', 'article8', 'Cách sửa đổi, lập phụ lục và giải quyết tranh chấp'],
];

const storedArticles = contractArticles.filter(([, field]) => field !== 'paymentTerms');

export function writeContractTerms(values) {
  return storedArticles.map(([title, field]) => {
    // Indenting user text keeps pasted headings separate from the stored article markers.
    const lines = (values[field] ?? '').trim().split(/\r?\n/).map(line => '  ' + line);
    return [title, ...lines].join('\n').trimEnd();
  }).join('\n\n');
}

export function readContractTerms(terms, paymentTerms) {
  const text = (terms ?? '').replace(/\r\n/g, '\n');
  const revisionStart = text.indexOf('\n\nĐề xuất sửa đổi:\n');
  const original = revisionStart === -1 ? text : text.slice(0, revisionStart);
  const sections = original.split(/\n\n(?=Điều [1-8]: )/);
  const values = { paymentTerms: paymentTerms ?? '' };
  if (sections.length !== storedArticles.length || !storedArticles.every(([title], index) =>
    sections[index].startsWith(title.split(':')[0] + ': '))) {
    return { ...values, deliveryTerms: text };
  }

  storedArticles.forEach(([, field], index) => {
    const lineBreak = sections[index].indexOf('\n');
    values[field] = lineBreak < 0 ? '' : sections[index].slice(lineBreak + 1).replace(/^  /gm, '').trim();
  });
  if (revisionStart !== -1) values.revisionTerms = text.slice(revisionStart + 2);
  return values;
}
