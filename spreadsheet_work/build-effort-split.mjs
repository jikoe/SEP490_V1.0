import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const inputPath = "C:/Users/huyng_jgxkbi0/Downloads/Project_Tracking_Template_Updated.xlsx";
const outputDir = "C:/Users/huyng_jgxkbi0/Downloads/SEP490/outputs/effort-split";
const outputPath = `${outputDir}/Project_Tracking_Effort_By_Phase.xlsx`;

const input = await FileBlob.load(inputPath);
const workbook = await SpreadsheetFile.importXlsx(input);
const sheet = workbook.worksheets.getItem("WBS");

// Move existing tracking fields rightward before the phase effort columns replace G:K.
for (const [from, to] of [["N", "R"], ["M", "Q"], ["L", "P"], ["K", "O"], ["J", "N"], ["I", "M"], ["H", "L"]]) {
  sheet.getRange(`${to}1:${to}44`).copyFrom(sheet.getRange(`${from}1:${from}44"`.slice(0, -1)), "all");
}

// Give all five effort columns the original effort-column appearance.
for (const col of ["H", "I", "J", "K"]) {
  sheet.getRange(`${col}1:${col}44`).copyFrom(sheet.getRange("G1:G44"), "all");
}

const phaseHeaders = [["Đặc tả", "Thiết kế", "Coding", "Test", "Deploy"]];
sheet.getRange("G1:K1").values = phaseHeaders;

const ratios = {
  Simple: [0.10, 0.15, 0.45, 0.25, 0.05],
  Medium: [0.15, 0.20, 0.40, 0.20, 0.05],
  Complex: [0.15, 0.20, 0.35, 0.25, 0.05],
};

const levels = sheet.getRange("F2:F43").values.flat();
const totals = sheet.getRange("G2:G43").values.flat();
const phaseValues = totals.map((total, index) => {
  const r = ratios[levels[index]] ?? ratios.Medium;
  const spec = Math.round(total * r[0] * 10) / 10;
  const design = Math.round(total * r[1] * 10) / 10;
  const test = Math.round(total * r[3] * 10) / 10;
  const deploy = Math.max(0.1, Math.round(total * r[4] * 10) / 10);
  const coding = Math.round((total - spec - design - test - deploy) * 10) / 10;
  return [spec, design, coding, test, deploy];
});
sheet.getRange("G2:K43").values = phaseValues;
sheet.getRange("G2:K43").format.numberFormat = "0.0";
sheet.getRange("G1:K43").format.horizontalAlignment = "center";
sheet.getRange("G1:K43").format.verticalAlignment = "center";

// Keep tracking fields legible in their new positions.
sheet.getRange("G:G").format.columnWidth = 10;
sheet.getRange("H:K").format.columnWidth = 11;
sheet.getRange("L:L").format.columnWidth = 13;
sheet.getRange("M:M").format.columnWidth = 11;
sheet.getRange("N:N").format.columnWidth = 8;
sheet.getRange("O:O").format.columnWidth = 13;
sheet.getRange("P:P").format.columnWidth = 11;
sheet.getRange("Q:Q").format.columnWidth = 10;
sheet.getRange("R:R").format.columnWidth = 12;

// Replace the iteration summary with a phase-level reconciliation.
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
sheet.getRange("H48:N51").format.numberFormat = "0.0";

workbook.recalculate();

const check = await workbook.inspect({
  kind: "table",
  range: "WBS!F1:R51",
  include: "values,formulas",
  tableMaxRows: 55,
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

const preview = await workbook.render({ sheetName: "WBS", range: "A1:R51", scale: 1.25, format: "png" });
await fs.mkdir(outputDir, { recursive: true });
await fs.writeFile(`${outputDir}/wbs-preview.png`, new Uint8Array(await preview.arrayBuffer()));

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(`OUTPUT:${outputPath}`);
