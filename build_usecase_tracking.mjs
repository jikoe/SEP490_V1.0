import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const sourcePath = "C:/Users/huyng_jgxkbi0/Downloads/UseCase_List_Agricultural_Platform.xlsx";
const templatePath = "C:/Users/huyng_jgxkbi0/Downloads/Project_Tracking_Template.xlsx";
const outputDir = "C:/Users/huyng_jgxkbi0/Downloads/SEP490/outputs/usecase-tracking";
const outputPath = `${outputDir}/UseCase_List_Agricultural_Platform_Tracking.xlsx`;

function getScreenName(reference) {
  const match = String(reference ?? "").match(/^[^:]+:\s*(.*?)\s*\//);
  return match ? match[1] : String(reference ?? "");
}

function getType(reference) {
  return String(reference ?? "").startsWith("FN-") ? "Function" : "Screen";
}

function effortFromTransactions(value) {
  const text = String(value ?? "");
  const numbers = [...text.matchAll(/\d+/g)].map((match) => Number(match[0]));
  if (numbers.length === 0) return null;
  const estimate = Math.max(...numbers);
  return text.includes(">") ? estimate + 1 : estimate;
}

function iterationFor(module, sequence) {
  const first = new Set([
    "Authentication", "Marketplace", "Traceability", "Farm Management",
    "Product Management", "Product Traceability"
  ]);
  const second = new Set([
    "Consumer Ordering", "Consumer Payment", "Consumer Engagement",
    "B2B Procurement", "Farmer Sales"
  ]);
  if (first.has(module)) return "Iteration 1";
  if (second.has(module)) return "Iteration 2";
  if (module === "Administration") {
    if ([34, 35, 36, 38].includes(sequence)) return "Iteration 1";
    if ([37, 39, 41].includes(sequence)) return "Iteration 2";
  }
  return "Iteration 3";
}

const source = await SpreadsheetFile.importXlsx(await FileBlob.load(sourcePath));
const template = await SpreadsheetFile.importXlsx(await FileBlob.load(templatePath));
const sourceSheet = source.worksheets.getItem("Use Case List");
const wbs = template.worksheets.getItem("WBS");

// The template's old pivot is tied to its sample rows and would otherwise cover
// migrated records. Replace it with the refreshed static tracker summary below.
for (const pivot of wbs.pivotTables.items) {
  pivot.delete();
}

const sourceRows = sourceSheet.getRange("A2:I43").values;
const outputRows = sourceRows.map((row) => {
  const sequence = Number(row[0]);
  const module = row[1];
  const useCaseCode = row[2];
  const useCaseName = row[3];
  const reference = row[4];
  const complexity = row[7];
  return [
    getScreenName(reference),
    module,
    useCaseCode,
    getType(reference),
    useCaseName,
    complexity,
    effortFromTransactions(row[6]),
    iterationFor(module, sequence),
    "To Do",
    "To Do",
  ];
});

// Preserve the template's guidance and pivot formatting, then relocate them below all 42 WBS items.
wbs.getRange("A15:I20").copyTo(wbs.getRange("A46:I51"), "all");
wbs.getRange("A15:J45").clear({ contentsOnly: false });

// Extend the template's data-row format, validations, borders, and dropdown styling.
const styleRow = wbs.getRange("A2:Z2");
for (let row = 2; row <= 43; row += 1) {
  styleRow.copyTo(wbs.getRange(`A${row}:Z${row}`), "all");
}

wbs.getRange("A2:J43").values = outputRows;
wbs.getRange("A2:J43").format.wrapText = true;
wbs.getRange("A2:J43").format.rowHeight = 34;
wbs.getRange("A2:J43").format.verticalAlignment = "center";
wbs.getRange("A2:J43").format.borders = { preset: "all", style: "thin", color: "#5F5F5F" };
wbs.getRange("A:A").format.columnWidth = 32;
wbs.getRange("B:B").format.columnWidth = 24;
wbs.getRange("C:C").format.columnWidth = 18;
wbs.getRange("D:D").format.columnWidth = 13;
wbs.getRange("E:E").format.columnWidth = 45;
wbs.getRange("F:F").format.columnWidth = 12;
wbs.getRange("G:G").format.columnWidth = 13;
wbs.getRange("H:H").format.columnWidth = 15;
wbs.getRange("I:I").format.columnWidth = 13;
wbs.getRange("J:J").format.columnWidth = 12;

// Refresh the template summary to reflect the migrated work items and estimated effort.
const iterations = ["Iteration 1", "Iteration 2", "Iteration 3"];
const summaryRows = iterations.map((iteration) => {
  const items = outputRows.filter((row) => row[7] === iteration);
  return [iteration, items.length, items.reduce((sum, row) => sum + Number(row[6] ?? 0), 0)];
});
wbs.getRange("G47:I50").values = [
  ["Row Labels", "Functions", "Total Effort"],
  ...summaryRows,
];

template.recalculate({ force: true });
// Reapply the principal data after calculation so the saved workbook contains the
// migrated WBS values and the updated summary.
wbs.getRange("A2:J43").values = outputRows;
wbs.getRange("A2:J43").format.borders = { preset: "all", style: "thin", color: "#5F5F5F" };
wbs.getRange("G47:I50").values = [["Row Labels", "Functions", "Total Effort"], ...summaryRows];
wbs.getRange("G51:I51").clear({ contentsOnly: true });
await fs.mkdir(outputDir, { recursive: true });
await SpreadsheetFile.exportXlsx(template).then((file) => file.save(outputPath));

const check = await template.inspect({
  kind: "table",
  range: "WBS!A1:J51",
  include: "values,formulas",
  tableMaxRows: 55,
  tableMaxCols: 10,
  maxChars: 14000,
});
console.log(check.ndjson);

const preview = await template.render({
  sheetName: "WBS",
  range: "A1:J51",
  scale: 1.15,
  format: "png",
});
await fs.writeFile("usecase-tracking-preview.png", new Uint8Array(await preview.arrayBuffer()));
console.log(`Created: ${outputPath}`);
