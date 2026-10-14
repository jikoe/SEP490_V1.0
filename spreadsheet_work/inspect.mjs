import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";
import fs from "node:fs/promises";

const inputPath = "C:/Users/huyng_jgxkbi0/Downloads/Project_Tracking_Template_Updated.xlsx";
const input = await FileBlob.load(inputPath);
const workbook = await SpreadsheetFile.importXlsx(input);

const summary = await workbook.inspect({
  kind: "workbook,sheet,table",
  maxChars: 12000,
  tableMaxRows: 12,
  tableMaxCols: 20,
  tableMaxCellChars: 100,
});
console.log(summary.ndjson);

for (const sheet of workbook.worksheets.items) {
  const used = sheet.getUsedRange();
  console.log(`SHEET:${sheet.name} USED:${used.address}`);
  const detail = await workbook.inspect({
    kind: "table",
    sheetId: sheet.name,
    range: used.address,
    include: "values,formulas",
    tableMaxRows: 60,
    tableMaxCols: 30,
    maxChars: 18000,
  });
  console.log(detail.ndjson);
}

const preview = await workbook.render({ sheetName: "WBS", range: "A1:N51", scale: 1.5, format: "png" });
await fs.writeFile("source-wbs.png", new Uint8Array(await preview.arrayBuffer()));
