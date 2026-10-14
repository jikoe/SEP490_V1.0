import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const roles = {
  "UC-AUTH-01": "Guest (Consumer or Farmer)",
  "UC-AUTH-02": "Guest (B2B Buyer)",
  "UC-AUTH-03": "Registered User",
  "UC-AUTH-04": "Registered User",
  "UC-AUTH-05": "Registered User",
  "UC-AUTH-06": "Registered User",
  "UC-AUTH-07": "Guest / Registered User",
  "UC-MKT-01": "Marketplace Visitor",
  "UC-MKT-02": "Marketplace Visitor",
  "UC-FAR-01": "Farmer",
  "UC-FAR-02": "Farmer",
  "UC-FAR-03": "Farmer",
  "UC-FAR-04": "Farmer",
  "UC-FAR-05": "Farmer",
  "UC-FAR-06": "Farmer",
  "UC-FAR-07": "Farmer",
  "UC-FAR-08": "Farmer",
  "UC-FAR-09": "Farmer",
  "UC-CON-01": "Consumer",
  "UC-CON-02": "Consumer",
  "UC-CON-03": "Consumer",
  "UC-CON-04": "Consumer",
  "UC-CON-05": "Consumer",
  "UC-CON-06": "Consumer",
  "UC-TRC-01": "Marketplace Visitor",
  "UC-B2B-01": "B2B Buyer",
  "UC-B2B-02": "B2B Buyer",
  "UC-B2B-03": "B2B Buyer",
  "UC-B2B-04": "B2B Buyer",
  "UC-B2B-05": "B2B Buyer",
  "UC-B2B-06": "B2B Buyer / Farmer",
  "UC-B2B-07": "B2B Buyer / Farmer",
  "UC-B2B-08": "B2B Buyer",
  "UC-ADM-01": "System Administrator",
  "UC-ADM-02": "Verification Officer",
  "UC-ADM-03": "System Administrator",
  "UC-ADM-04": "Content Moderator",
  "UC-ADM-05": "System Administrator",
  "UC-ADM-06": "Dispute Officer",
  "UC-ADM-07": "Marketing / Promotion Staff",
  "UC-ADM-08": "Content Moderator",
  "UC-ADM-09": "System Administrator",
};

const source = await FileBlob.load("tmp_tracking_source.xlsx");
const workbook = await SpreadsheetFile.importXlsx(source);
const sheet = workbook.worksheets.getItem("WBS");
const values = sheet.getRange("C2:C51").values;
const roleValues = values.map(([id]) => [roles[id] ?? null]);

sheet.getRange("S1").values = [["Primary Actor Role"]];
sheet.getRange("S2:S51").values = roleValues;
sheet.getRange("S1").format = {
  fill: "#1F4E78",
  font: { name: "Arial", size: 10, bold: true, color: "#FFFFFF" },
  horizontalAlignment: "center",
  verticalAlignment: "center",
  wrapText: true,
};
sheet.getRange("S2:S43").format = {
  font: { name: "Arial", size: 10, color: "#1F1F1F" },
  verticalAlignment: "center",
  wrapText: true,
  borders: { preset: "outside", style: "thin", color: "#D9E2F3" },
};
sheet.getRange("S1:S43").format.columnWidth = 28;
sheet.getRange("S1:S43").format.autofitRows();
sheet.freezePanes.freezeRows(1);

workbook.recalculate();
const check = await workbook.inspect({
  kind: "table",
  range: "WBS!A1:S43",
  include: "values,formulas",
  tableMaxRows: 45,
  tableMaxCols: 20,
});
console.log(check.ndjson);
const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!",
  options: { useRegex: true, maxResults: 300 },
  summary: "final formula error scan",
});
console.log(errors.ndjson);
const preview = await workbook.render({ sheetName: "WBS", range: "A1:S43", scale: 1 });
await fs.mkdir("output", { recursive: true });
await fs.writeFile("output/primary_actor_roles_preview.png", new Uint8Array(await preview.arrayBuffer()));
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save("output/Project_Tracking_with_Primary_Actor_Roles.xlsx");
