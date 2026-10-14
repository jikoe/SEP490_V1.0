import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const input = await FileBlob.load("tmp_tracking_source.xlsx");
const workbook = await SpreadsheetFile.importXlsx(input);
const result = await workbook.inspect({
  kind: "workbook,sheet,table",
  maxChars: 20000,
  tableMaxRows: 25,
  tableMaxCols: 30,
  tableMaxCellChars: 200,
});
console.log(result.ndjson);
