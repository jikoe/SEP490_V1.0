import pdfMake from 'pdfmake/build/pdfmake.js';
import fonts from 'pdfmake/build/vfs_fonts.js';

pdfMake.addVirtualFileSystem(fonts);

export async function downloadOfftakePdf(detail, code) {
  const title = 'Chi tiết hợp đồng Offtake';
  const content = [{ text: title, style: 'title' }, { text: code, style: 'heading' }];
  for (const fact of detail.querySelectorAll('.offtake-facts > div')) {
    content.push({ text: [
      { text: fact.querySelector('dt').textContent + '  ', bold: true },
      fact.querySelector('dd').textContent,
    ], margin: [0, 0, 0, 4] });
  }
  for (const section of detail.querySelectorAll('section')) {
    content.push({ text: section.querySelector('h3').textContent, style: 'heading' });
    const table = section.querySelector('table');
    if (!table) {
      content.push(section.querySelector('p').textContent);
      continue;
    }
    const headers = Array.from(table.tHead.rows[0].cells, cell => ({
      text: cell.textContent, bold: true, fillColor: '#e9f2ed',
    }));
    const rows = Array.from(table.tBodies[0].rows, row => Array.from(row.cells,
      cell => cell.lastElementChild.textContent));
    content.push({
      layout: 'lightHorizontalLines',
      table: { headerRows: 1, widths: headers.map(() => '*'), body: [headers, ...rows] },
    });
  }
  const filename = code.replace(/[^a-z0-9_-]/gi, '_').slice(0, 100) || 'offtake';
  await pdfMake.createPdf({
    pageSize: 'A4',
    content,
    defaultStyle: { fontSize: 10, color: '#203e39' },
    styles: {
      title: { fontSize: 18, bold: true, color: '#07583e', margin: [0, 0, 0, 8] },
      heading: { fontSize: 12, bold: true, margin: [0, 12, 0, 5] },
    },
  }).download(filename + '.pdf');
}
