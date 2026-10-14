import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const input = await FileBlob.load("C:/Users/huyng_jgxkbi0/Downloads/Project_Tracking_Template_Updated.xlsx");
const workbook = await SpreadsheetFile.importXlsx(input);
console.log(workbook.help("table", { search: "resize|range|delete", include: "index,examples,notes", maxChars: 4000 }).ndjson);
