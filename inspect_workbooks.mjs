import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const files = [
  "C:/Users/huyng_jgxkbi0/Downloads/UseCase_List_Agricultural_Platform.xlsx",
  "C:/Users/huyng_jgxkbi0/Downloads/Project_Tracking_Template.xlsx",
];

for (const filePath of files) {
  const input = await FileBlob.load(filePath);
  const workbook = await SpreadsheetFile.importXlsx(input);
  console.log(`\n=== ${filePath.split("/").at(-1)} ===`);
  console.log((await workbook.inspect({
    kind: "workbook,sheet,table",
    maxChars: 10000,
    tableMaxRows: 20,
    tableMaxCols: 20,
    tableMaxCellChars: 100,
  })).ndjson);
  for (const sheet of workbook.worksheets.items) {
    const used = sheet.getUsedRange();
    console.log(`\n-- ${sheet.name}: ${used.address} --`);
    console.log((await workbook.inspect({
      kind: "table",
      range: used.address,
      include: "values,formulas",
      maxChars: 12000,
      tableMaxRows: 80,
      tableMaxCols: 25,
      tableMaxCellChars: 100,
    })).ndjson);
    if ((filePath.includes("Project_Tracking") && sheet.name === "WBS") || (filePath.includes("UseCase_List") && sheet.name === "Use Case List")) {
      const preview = await workbook.render({ sheetName: sheet.name, range: "A1:J20", scale: 1.5, format: "png" });
      const safe = filePath.includes("Project_Tracking") ? "template-wbs" : "source-usecases";
      await fs.writeFile(`${safe}.png`, new Uint8Array(await preview.arrayBuffer()));
    }
  }
}
