import { FileBlob, PresentationFile } from "@oai/artifact-tool";
const deck=await PresentationFile.importPptx(await FileBlob.load("C:/Users/huyng_jgxkbi0/.codex/plugins/cache/openai-curated-remote/openai-templates/0.1.1/skills/artifact-template-operating-review/assets/reference.pptx"));
const t=deck.slides.getItem(1).tables.items[0]; console.log(Object.getOwnPropertyNames(Object.getPrototypeOf(t))); console.log(Object.keys(t)); console.log(t.rows,t.cols,t.rowCount,t.columnCount,t.frame);
