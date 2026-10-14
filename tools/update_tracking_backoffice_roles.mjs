import fs from 'node:fs/promises';
import { FileBlob, SpreadsheetFile } from '@oai/artifact-tool';

const inputPath = 'output/Project_Tracking_with_Primary_Actor_Roles.xlsx';
const outputPath = 'output/Project_Tracking_Revised_Backoffice_Features.xlsx';

const input = await FileBlob.load(inputPath);
const workbook = await SpreadsheetFile.importXlsx(input);
const sheet = workbook.worksheets.getItem('WBS');

const changes = [
  { row: 10, feature: 'System Administration', actor: 'System Administrator' },
  { row: 11, feature: 'System Administration', actor: 'System Administrator' },
  { row: 16, feature: 'Content Governance', actor: 'Content Moderator' },
  { row: 22, feature: 'Verification & Certification', actor: 'Verification Officer' },
  { row: 26, feature: 'Customer Support & Dispute', actor: 'Customer Support / Dispute Officer' },
  { row: 27, feature: 'Content Governance', actor: 'Content Moderator' },
  { row: 29, feature: 'Marketing & Promotion', actor: 'Marketing / Promotion Staff' },
  { row: 31, feature: 'Back-office Dashboard', actor: 'System Administrator', screen: 'Back-office Dashboard Screen' },
  { row: 43, feature: 'System Administration', actor: 'System Administrator' },
];

for (const change of changes) {
  sheet.getRange(`B${change.row}`).values = [[change.feature]];
  sheet.getRange(`S${change.row}`).values = [[change.actor]];
  if (change.screen) sheet.getRange(`A${change.row}`).values = [[change.screen]];
}

workbook.recalculate();
const check = await workbook.inspect({
  kind: 'table',
  range: 'WBS!A1:S43',
  include: 'values,formulas',
  tableMaxRows: 43,
  tableMaxCols: 19,
});
await fs.mkdir('output', { recursive: true });
const preview = await workbook.render({ sheetName: 'WBS', range: 'A1:S43', scale: 1, format: 'png' });
await fs.writeFile('output/Project_Tracking_Revised_Backoffice_Features_preview.png', new Uint8Array(await preview.arrayBuffer()));
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(check.ndjson);
console.log(outputPath);
