import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath = "C:/Users/huyng_jgxkbi0/Downloads/SEP490/outputs/effort-split-report2/Project_Tracking_Effort_By_Phase_Report2.xlsx";
const outputDir = "C:/Users/huyng_jgxkbi0/Downloads/SEP490/outputs/effort-split-report2-nozeros";
const outputPath = `${outputDir}/Project_Tracking_Effort_By_Phase_Report2_NoZero.xlsx`;

function allocateInteger(total, weights, minimum = 0) {
  const values = Array(weights.length).fill(minimum);
  let remaining = total - minimum * weights.length;
  if (remaining < 0) throw new Error("Allocation total is below the required minimum.");
  const weightSum = weights.reduce((sum, value) => sum + value, 0);
  const raw = weights.map((weight) => remaining * weight / weightSum);
  const floorValues = raw.map(Math.floor);
  for (let index = 0; index < values.length; index += 1) values[index] += floorValues[index];
  remaining -= floorValues.reduce((sum, value) => sum + value, 0);
  const order = raw.map((value, index) => ({ index, fraction: value - Math.floor(value) }))
    .sort((a, b) => b.fraction - a.fraction || a.index - b.index);
  for (let index = 0; index < remaining; index += 1) values[order[index].index] += 1;
  return values;
}

function allocateTenths(total, weights, minimumTenths = 1) {
  return allocateInteger(total * 10, weights, minimumTenths).map((value) => value / 10);
}

const input = await FileBlob.load(inputPath);
const workbook = await SpreadsheetFile.importXlsx(input);
const sheet = workbook.worksheets.getItem("WBS");

const levels = sheet.getRange("F2:F43").values.flat();
const baseline = sheet.getRange("G2:K43").values.map((row) => row.reduce((sum, value) => sum + Number(value || 0), 0));
const complexityWeight = levels.map((level) => ({ Simple: 1, Medium: 1.35, Complex: 1.8 }[level] ?? 1.35));
const codingWeight = baseline.map((value, index) => value * complexityWeight[index]);
const testWeight = baseline.map((value, index) => value * complexityWeight[index] ** 1.25);
const deployWeight = baseline.map((value, index) => value * complexityWeight[index] ** 1.5);

// Exact Report 2 reconciliation with every UC receiving non-zero effort in all five phases.
const specification = Array(42).fill(0);
const design = Array(42).fill(0);
const coding = Array(42).fill(0);
const test = Array(42).fill(0);
const deploy = Array(42).fill(0);
const iterationGroups = [
  { start: 0, end: 15, specification: 15, design: 16, coding: 46, test: 17, deploy: 7 },
  { start: 15, end: 28, specification: 13, design: 13, coding: 48, test: 16, deploy: 10 },
  { start: 28, end: 42, specification: 14, design: 14, coding: 30, test: 20, deploy: 12 },
];
for (const group of iterationGroups) {
  const range = { start: group.start, length: group.end - group.start };
  specification.splice(range.start, range.length, ...allocateTenths(group.specification, baseline.slice(group.start, group.end).map(() => 1)));
  design.splice(range.start, range.length, ...allocateTenths(group.design, complexityWeight.slice(group.start, group.end)));
  coding.splice(range.start, range.length, ...allocateTenths(group.coding, codingWeight.slice(group.start, group.end)));
  test.splice(range.start, range.length, ...allocateTenths(group.test, testWeight.slice(group.start, group.end)));
  deploy.splice(range.start, range.length, ...allocateTenths(group.deploy, deployWeight.slice(group.start, group.end)));
}
const phaseValues = specification.map((value, index) => [value, design[index], coding[index], test[index], deploy[index]]);

sheet.getRange("G2:K43").values = phaseValues;
sheet.getRange("G2:K43").format.numberFormat = "0.0";

// Update the phase reconciliation summary while preserving the existing iteration grouping.
sheet.getRange("G47:N51").clear({ applyTo: "all" });
sheet.getRange("G47:N47").values = [["Planned", "Functions", "Đặc tả", "Thiết kế", "Coding", "Test", "Deploy", "Tổng PDS"]];
sheet.getRange("G48:G51").values = [["Iteration 1"], ["Iteration 2"], ["Iteration 3"], ["Grand Total"]];
sheet.getRange("H48").formulas = [["=COUNTIF($L$2:$L$43,G48)"]];
sheet.getRange("I48").formulas = [["=SUMIF($L$2:$L$43,$G48,$G$2:$G$43)"]];
sheet.getRange("J48").formulas = [["=SUMIF($L$2:$L$43,$G48,$H$2:$H$43)"]];
sheet.getRange("K48").formulas = [["=SUMIF($L$2:$L$43,$G48,$I$2:$I$43)"]];
sheet.getRange("L48").formulas = [["=SUMIF($L$2:$L$43,$G48,$J$2:$J$43)"]];
sheet.getRange("M48").formulas = [["=SUMIF($L$2:$L$43,$G48,$K$2:$K$43)"]];
sheet.getRange("N48").formulas = [["=SUM(I48:M48)"]];
sheet.getRange("H48:N50").fillDown();
sheet.getRange("H51").formulas = [["=SUM(H48:H50)"]];
sheet.getRange("I51").formulas = [["=SUM(I48:I50)"]];
sheet.getRange("J51").formulas = [["=SUM(J48:J50)"]];
sheet.getRange("K51").formulas = [["=SUM(K48:K50)"]];
sheet.getRange("L51").formulas = [["=SUM(L48:L50)"]];
sheet.getRange("M51").formulas = [["=SUM(M48:M50)"]];
sheet.getRange("N51").formulas = [["=SUM(N48:N50)"]];
sheet.getRange("G47:N47").format = {
  fill: "#D9E8FF",
  font: { bold: true, color: "#1F1F1F" },
  horizontalAlignment: "center",
  verticalAlignment: "center",
  borders: { preset: "all", style: "thin", color: "#A6A6A6" },
};
sheet.getRange("G48:N51").format.borders = { preset: "all", style: "thin", color: "#A6A6A6" };
sheet.getRange("G51:N51").format = {
  fill: "#D9E8FF",
  font: { bold: true },
  borders: { preset: "all", style: "thin", color: "#A6A6A6" },
};
sheet.getRange("H48:N51").format.numberFormat = "0";

sheet.getRange("A53").values = [["Each UC has non-zero effort in all five phases. Phase totals reconcile to Report 2: Specification 42, Design 43, Coding 124, Test 53, Deploy 29 = 291 PDS."]];
sheet.getRange("A54").values = [["Individual UC allocations use 0.1 PDS increments; iteration and grand totals are whole PDS. Test includes Phase 4 (38 PDS) and UAT support (15 PDS). Deploy includes Report 6 (5 PDS), release/deployment (8 PDS), and closing/handover (16 PDS)."]];
sheet.getRange("A53:A54").format.font = { italic: true, color: "#595959" };

workbook.recalculate();

const check = await workbook.inspect({
  kind: "table",
  range: "WBS!F1:N54",
  include: "values,formulas",
  tableMaxRows: 60,
  tableMaxCols: 16,
  maxChars: 18000,
});
console.log(check.ndjson);

const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
});
console.log(errors.ndjson);

const preview = await workbook.render({ sheetName: "WBS", range: "A1:R54", scale: 1.25, format: "png" });
await fs.mkdir(outputDir, { recursive: true });
await fs.writeFile(`${outputDir}/wbs-preview.png`, new Uint8Array(await preview.arrayBuffer()));
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(`OUTPUT:${outputPath}`);
