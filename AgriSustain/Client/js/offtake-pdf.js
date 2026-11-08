import pdfMake from 'pdfmake/build/pdfmake.js';
import fonts from 'pdfmake/build/vfs_fonts.js';
import { localTime } from './page-ui.js';
import { date, dateRange, statusName } from './offtake-ui.js';
import { contractArticles, readContractTerms } from './offtake-terms.js';

pdfMake.addVirtualFileSystem(fonts);

const missing = 'Chưa có dữ liệu';
const amountFormat = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 });
const quantityFormat = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 9 });
const textOrMissing = value => value?.trim() || missing;
const money = value => value == null ? missing : amountFormat.format(value) + ' VND';
const quantity = (value, unit) => [quantityFormat.format(value), unit].filter(Boolean).join(' ');
const heading = text => ({ text, style: 'heading' });
const field = (label, value) => ({ text: [{ text: label + '  ', bold: true }, value] });

function versionDate(value) {
  const [day, month, year] = date(value).split('/');
  return year ? 'Ngày ' + day + ' tháng ' + month + ' năm ' + year : 'Ngày lập phiên bản ' + missing;
}

function party(title, profile) {
  return { stack: [
    { text: title, bold: true, margin: [0, 0, 0, 3] },
    field('Tên bên', textOrMissing(profile?.name)),
    field('Người đại diện', textOrMissing(profile?.representative)),
    field('Địa chỉ', textOrMissing(profile?.address)),
    field('Mã số thuế / Số định danh', textOrMissing(profile?.taxCode)),
  ] };
}

function confirmation(title, name, records, roles) {
  const entries = records.filter(item => roles.includes(item.role));
  return { stack: [
    { text: title, bold: true, margin: [0, 0, 0, 3] },
    textOrMissing(name),
    ...(entries.length ? entries.flatMap(item => [
      ...(item.confirmedBy?.trim() ? [field('Người xác nhận', item.confirmedBy)] : []),
      item.confirmedAt ? statusName(item.status) + ' · ' + localTime(item.confirmedAt) : statusName(item.status),
    ]) : ['Chưa có xác nhận']),
  ] };
}

function productTable(lines) {
  if (!lines.length) return missing;
  return {
    layout: 'lightHorizontalLines',
    table: {
      headerRows: 1,
      dontBreakRows: true,
      widths: ['*', 92, 104, 102],
      body: [
        ['Sản phẩm', 'Khối lượng', 'Đơn giá / Cách tính', 'Thành tiền']
          .map(text => ({ text, bold: true, fillColor: '#f0f0f0' })),
        ...lines.map(line => [
          line.product,
          line.displayQuantity == null
            ? quantity(line.quantity, line.unit)
            : quantity(line.displayQuantity, line.displayUnit) + '\n(' + quantity(line.quantity, line.unit) + ')',
          line.priceFormula || (line.unitPrice == null ? missing : money(line.unitPrice) + '/' + line.unit),
          line.unitPrice == null || line.priceFormula ? 'Theo điều khoản' : money(line.quantity * line.unitPrice),
        ]),
      ],
    },
  };
}

export function buildOfftakePdf(detail) {
  const { document: contract, lines, confirmations } = detail;
  const terms = readContractTerms(detail.terms, detail.paymentTerms);
  const articleHeading = number => heading(contractArticles[number - 1][0]);
  const supplier = contract?.supplier ?? { name: detail.partner };
  const buyer = contract?.buyer;
  const schedule = contract?.deliverySchedule ?? detail.milestones.filter(item => item.quantity > 0);
  const units = [...new Set(lines.map(line => line.unit))];
  const scheduleUnit = units.length === 1 ? units[0] : '';
  const quality = lines.filter(line => line.quality?.trim());
  return {
    pageSize: 'A4',
    pageMargins: [38, 31, 38, 37],
    defaultStyle: { fontSize: 10, lineHeight: 1.18, color: '#000000', margin: [0, 0, 0, 3] },
    styles: {
      title: { fontSize: 18, bold: true, alignment: 'center', margin: [0, 12, 0, 3] },
      heading: { fontSize: 11, bold: true, margin: [0, 9, 0, 3] },
    },
    footer: (page, pages) => ({
      text: detail.code + ' · ' + page + '/' + pages, alignment: 'right',
      margin: [38, 8, 38, 0], fontSize: 8,
    }),
    content: [
      { text: 'CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM', alignment: 'center', bold: true },
      { text: 'Độc lập – Tự do – Hạnh phúc', alignment: 'center', bold: true },
      { text: versionDate(contract?.versionCreatedAt), alignment: 'right', italics: true, margin: [0, 6, 0, 0] },
      { text: 'HỢP ĐỒNG BAO TIÊU SẢN PHẨM', style: 'title' },
      { text: [
        'Số hợp đồng ' + detail.code,
        detail.version == null ? '' : ' · Phiên bản ' + detail.version,
        ' · ' + statusName(detail.status),
      ], alignment: 'center' },
      { text: 'Bản trích xuất từ AgriSustain. Thông tin nhận diện lấy từ hồ sơ hiện tại.', alignment: 'center',
        italics: true, fontSize: 9, margin: [0, 4, 0, 10] },
      { columns: [party('BÊN A · NHÀ CUNG CẤP', supplier), party('BÊN B · BÊN MUA', buyer)], columnGap: 16 },
      articleHeading(1),
      ...(terms.article1 ? [terms.article1] : []),
      productTable(lines),
      articleHeading(2),
      textOrMissing(terms.article2),
      articleHeading(3),
      ...quality.map(line => field(line.product, line.quality)),
      ...(terms.article3 || !quality.length ? [textOrMissing(terms.article3)] : []),
      articleHeading(4),
      ...(schedule.length ? schedule.map(item => field(date(item.dueDate),
        quantity(item.quantity, scheduleUnit))) : [missing]),
      textOrMissing(terms.deliveryTerms),
      articleHeading(5),
      field('Tiền cọc', money(detail.deposit)),
      textOrMissing(terms.paymentTerms),
      articleHeading(6),
      textOrMissing(terms.article6),
      articleHeading(7),
      dateRange(detail.validFrom, detail.validUntil),
      ...(terms.article7 ? [terms.article7] : []),
      articleHeading(8),
      textOrMissing(terms.article8),
      ...(terms.revisionTerms ? [terms.revisionTerms] : []),
      heading('Xác nhận của hai bên trên AgriSustain'),
      { columns: [
        confirmation('BÊN A · NHÀ CUNG CẤP', supplier?.name, confirmations, ['Farmer']),
        confirmation('BÊN B · BÊN MUA', buyer?.name, confirmations, ['WholesaleBuyer', 'Buyer']),
      ], columnGap: 16, unbreakable: true },
    ],
  };
}

export async function downloadOfftakePdf(detail) {
  const filename = detail.code.replace(/[^a-z0-9_-]/gi, '_').slice(0, 100) || 'offtake';
  await pdfMake.createPdf(buildOfftakePdf(detail)).download(filename + '.pdf');
}
