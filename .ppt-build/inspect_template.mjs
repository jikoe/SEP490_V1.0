import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, PresentationFile } from "@oai/artifact-tool";

const source = "C:/Users/huyng_jgxkbi0/.codex/plugins/cache/openai-curated-remote/openai-templates/0.1.1/skills/artifact-template-operating-review/assets/reference.pptx";
const deck = await PresentationFile.importPptx(await FileBlob.load(source));
console.log((await deck.help("remove delete slide", { maxChars: 4000 })).ndjson ?? "");
const snapshot = await deck.inspect({
  kind: "deck,slide,textbox,shape,image,table,chart,notes,layout",
  maxChars: 20000,
});
await fs.writeFile("C:/Users/huyng_jgxkbi0/Downloads/SEP490/template-inspect.ndjson", snapshot.ndjson);
const preview = await deck.export({ format: "webp", montage: true, scale: 1 });
await fs.writeFile("C:/Users/huyng_jgxkbi0/Downloads/SEP490/template-montage.webp", new Uint8Array(await preview.arrayBuffer()));
console.log(snapshot.ndjson);
