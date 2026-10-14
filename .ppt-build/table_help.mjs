import { FileBlob, PresentationFile } from "@oai/artifact-tool";
const deck=await PresentationFile.importPptx(await FileBlob.load("C:/Users/huyng_jgxkbi0/.codex/plugins/cache/openai-curated-remote/openai-templates/0.1.1/skills/artifact-template-operating-review/assets/reference.pptx"));
console.log((await deck.help("slide tables delete table",{maxChars:6000})).ndjson);
