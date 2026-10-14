import fs from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { FileBlob, PresentationFile } from "@oai/artifact-tool";

const workspaceDir = "C:/Users/huyng_jgxkbi0/Downloads/SEP490";
const skillDir = "C:/Users/huyng_jgxkbi0/.codex/plugins/cache/openai-primary-runtime/presentations/26.909.12148/skills/presentations";
const templatePath = "C:/Users/huyng_jgxkbi0/.codex/plugins/cache/openai-curated-remote/openai-templates/0.1.1/skills/artifact-template-operating-review/assets/reference.pptx";
const buildDir = path.join(workspaceDir, ".ppt-build");
const stagingDir = path.join(workspaceDir, ".codex-finalizer");
const outputDir = path.join(workspaceDir, "output");
const finalPath = path.join(outputDir, "AgriSustain_Graduation_Project_Presentation_v4.pptx");
const nodePython = "C:/Users/huyng_jgxkbi0/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe";

await fs.mkdir(buildDir, { recursive: true });
await fs.mkdir(stagingDir, { recursive: true });
await fs.mkdir(outputDir, { recursive: true });

const deck = await PresentationFile.importPptx(await FileBlob.load(templatePath));

// Keep the first 12 imported template slides and remove unused source slides.
for (let i = deck.slides.items.length - 1; i >= 12; i -= 1) deck.slides.getItem(i).delete();

const navy = "#082B57";
const blue = "#0E5DAA";
const ink = "#12263A";
const muted = "#52657A";
const pale = "#F3F8FC";
const line = "#D9E4EE";
const white = "#FFFFFF";
const font = "Aptos";

function box(slide, text, left, top, width, height, style = {}) {
  const shape = slide.shapes.add({
    geometry: "textbox",
    position: { left, top, width, height },
    fill: "none",
    line: { fill: "none", width: 0 },
  });
  shape.text = text;
  shape.text.style = {
    typeface: font,
    fontSize: 18,
    color: ink,
    autoFit: "shrinkText",
    ...style,
  };
  return shape;
}

function rect(slide, left, top, width, height, fill = pale) {
  return slide.shapes.add({
    geometry: "roundRect",
    position: { left, top, width, height },
    fill,
    line: { fill: "none", width: 0 },
  });
}

function clear(slide, number, dark = false) {
  slide.shapes.deleteAll();
  for (const table of [...slide.tables.items]) {
    table.setValues(Array.from({ length: table.rowCount }, () => Array(table.columnCount).fill("")));
    table.frame = { left: 41, top: 280, width: 823, height: 345 };
    table.fill = "none";
    for (let row = 0; row < table.rowCount; row += 1) {
      for (let col = 0; col < table.columnCount; col += 1) {
        const cell = table.getCell(row, col);
        cell.fill = "none";
        cell.borders = { top: { fill: "none", width: 0 }, bottom: { fill: "none", width: 0 }, left: { fill: "none", width: 0 }, right: { fill: "none", width: 0 } };
      }
    }
  }
  for (const image of [...slide.images.items]) image.delete();
  slide.background.fill = dark ? navy : white;
  if (!dark) {
    const rule = slide.shapes.add({
      geometry: "rect",
      position: { left: 40, top: 38, width: 1198, height: 3 },
      fill: blue,
      line: { fill: "none", width: 0 },
    });
    void rule;
    box(slide, `AGRISUSTAIN  |  SEP490 GRADUATION PROJECT`, 42, 670, 420, 18, {
      fontSize: 9, color: muted, bold: true,
    });
    box(slide, String(number), 1188, 668, 42, 18, { fontSize: 10, color: muted, align: "right" });
  }
}

function title(slide, text, subtitle = "") {
  box(slide, text, 42, 65, 1120, 54, { fontSize: 30, bold: true, color: ink });
  if (subtitle) box(slide, subtitle, 42, 126, 1120, 42, { fontSize: 15, color: muted });
}

function card(slide, x, y, w, h, heading, body, accent = blue) {
  rect(slide, x, y, w, h, pale);
  box(slide, heading, x + 22, y + 20, w - 44, 38, { fontSize: 18, bold: true, color: accent });
  box(slide, body, x + 22, y + 68, w - 44, h - 86, { fontSize: 15, color: ink });
}

function bullets(items) { return items.map((x) => `• ${x}`).join("\n"); }
function notes(slide, source, speaker) { slide.speakerNotes.text = `Sources: ${source}\n\nSpeaker notes: ${speaker}`; }

// 1. Cover
{
  const s = deck.slides.getItem(0); clear(s, 1, true);
  box(s, "AGRISUSTAIN", 54, 70, 460, 36, { fontSize: 16, bold: true, color: "#A9D4FF" });
  box(s, "Digital Platform for\nSustainable Agriculture", 54, 175, 760, 165, { fontSize: 42, bold: true, color: white });
  box(s, "A graduation capstone project that connects farmers, consumers, and B2B buyers through trusted digital trade.", 58, 380, 720, 58, { fontSize: 20, color: "#D9EBFB" });
  box(s, "SEP490_AGRISUSTAIN\nWeb application for B2B and B2C agricultural commerce", 58, 590, 510, 48, { fontSize: 13, color: white });
  notes(s, "Report 1: Project Information and Project Purpose", "Open with the project purpose and the three audiences served.");
}

// 2. Agenda
{
  const s = deck.slides.getItem(1); clear(s, 2); title(s, "Presentation roadmap", "A 15-minute overview of the project idea, delivery plan, and expected outcomes.");
  const agenda = [
    ["01", "Problem and opportunity"], ["02", "AgriSustain solution"], ["03", "Scope and system design"],
    ["04", "Delivery, quality, and risk management"], ["05", "Expected contribution"],
  ];
  agenda.forEach(([n, t], i) => { box(s, n, 80, 220 + i * 70, 55, 30, { fontSize: 16, bold: true, color: blue }); box(s, t, 155, 220 + i * 70, 560, 30, { fontSize: 22, color: ink }); });
  box(s, "The presentation prioritizes Reports 1 and 2. Report 3 provides the functional evidence behind the solution.", 800, 230, 340, 150, { fontSize: 20, bold: true, color: navy });
  notes(s, "Reports 1–3", "Set expectations: the presentation explains both the product idea and the plan to deliver it.");
}

// 3. Problem
{
  const s = deck.slides.getItem(2); clear(s, 3); title(s, "Agricultural trade faces three connected gaps", "Fragmented market access, weak product evidence, and limited price visibility create risk for every participant.");
  card(s, 42, 230, 360, 290, "Supply and demand mismatch", "Farmers may face harvest-time oversupply while buyers elsewhere cannot identify reliable suppliers with the quantity and schedule they need.");
  card(s, 460, 230, 360, 290, "Trust and food safety", "Buyers often depend on seller descriptions because cultivation history, harvest dates, and certificates are difficult to verify.");
  card(s, 878, 230, 360, 290, "Price information gaps", "Fragmented and outdated regional price information makes it hard to judge whether an offer is fair.");
  notes(s, "Report 1: Product Background", "Explain that the platform addresses a connected market problem, not only an online storefront.");
}

// 4. Solution
{
  const s = deck.slides.getItem(3); clear(s, 4); title(s, "AgriSustain connects trade with evidence", "One responsive web platform combines direct commerce, farming data, and transparent product information.");
  box(s, "Farmer", 82, 235, 190, 34, { fontSize: 24, bold: true, color: navy, align: "center" });
  box(s, "Consumer", 515, 235, 190, 34, { fontSize: 24, bold: true, color: navy, align: "center" });
  box(s, "B2B Buyer", 947, 235, 190, 34, { fontSize: 24, bold: true, color: navy, align: "center" });
  rect(s, 70, 300, 230, 150); rect(s, 502, 300, 230, 150); rect(s, 934, 300, 230, 150);
  box(s, "List products\nRecord farming logs\nManage harvest batches", 92, 332, 185, 95, { fontSize: 17, color: ink, align: "center" });
  box(s, "Browse verified products\nScan batch QR codes\nOrder and review", 524, 332, 185, 95, { fontSize: 17, color: ink, align: "center" });
  box(s, "Discover suppliers\nPlace bulk orders\nManage offtake contracts", 956, 332, 185, 95, { fontSize: 17, color: ink, align: "center" });
  box(s, "The platform links product listings to batches, cultivation logs, certification evidence, and transactions.", 240, 550, 800, 44, { fontSize: 20, bold: true, color: blue, align: "center" });
  notes(s, "Report 1: Proposed Solution", "Frame AgriSustain as an integrated value-chain platform, with B2C and B2B in the same ecosystem.");
}

// 5. Scope
{
  const s = deck.slides.getItem(4); clear(s, 5); title(s, "Six capabilities define the first release", "The scope focuses on functions that make transactions trustworthy and operationally feasible.");
  const items = [
    ["Direct trade", "B2C marketplace and B2B procurement"], ["Harvest-linked orders", "Order status, pre-orders, and fulfilment"],
    ["Seller verification", "KYC and certificate review before selling"], ["QR traceability", "Batch-level history from field to buyer"],
    ["Price reference", "Regional benchmark information"], ["Quality resolution", "Reviews, complaints, and administration"],
  ];
  items.forEach(([h,b],i)=>{ const x=42+(i%3)*400, y=210+Math.floor(i/3)*205; card(s,x,y,360,160,h,b); });
  notes(s, "Report 1: Major Features FE-01 to FE-06", "Use this slide as the functional summary before showing requirements detail.");
}

// 6. Requirements and architecture
{
  const s = deck.slides.getItem(5); clear(s, 6); title(s, "The requirements cover the full trading lifecycle", "The SRS defines 43 use cases across authentication, marketplace, farm management, transactions, B2B contracts, and administration.");
  card(s, 42, 210, 278, 280, "Onboarding and trust", bullets(["Role-based accounts", "Email OTP", "Farmer and buyer KYC", "Certificate dossiers"]));
  card(s, 346, 210, 278, 280, "Commerce", bullets(["Marketplace browsing", "Cart and retail checkout", "PayOS/VietQR payment", "Order fulfilment"]));
  card(s, 650, 210, 278, 280, "Farm operations", bullets(["Crop seasons", "E-farming logs", "Product listings", "Traceability batches"]));
  card(s, 954, 210, 278, 280, "Business control", bullets(["Supplier search", "Offtake contracts", "Revenue dashboard", "Moderation and complaints"]));
  notes(s, "Report 3: Use Cases, External API Inventory, Functional Requirements", "Make clear that Report 3 turns the business idea into testable system behavior.");
}

// 7. Technology
{
  const s = deck.slides.getItem(6); clear(s, 7); title(s, "Technical foundation for a responsive web platform", "The delivery stack supports browser-based access and integrates payment and email notification services.");
  box(s, "User browsers", 65, 245, 230, 35, { fontSize: 24, bold: true, color: navy, align: "center" });
  box(s, "Responsive web interface\nReact 19.1.0", 65, 295, 230, 80, { fontSize: 18, color: ink, align: "center" });
  box(s, "Application services", 520, 245, 230, 35, { fontSize: 24, bold: true, color: navy, align: "center" });
  box(s, "ASP.NET Web API 8.0\nBusiness rules and APIs", 520, 295, 230, 80, { fontSize: 18, color: ink, align: "center" });
  box(s, "Data and integrations", 945, 245, 230, 35, { fontSize: 24, bold: true, color: navy, align: "center" });
  box(s, "MS SQL Server\nPayOS / VietQR\nGmail Notification API", 945, 295, 230, 95, { fontSize: 18, color: ink, align: "center" });
  box(s, "Azure staging supports integration, system testing, and stakeholder demonstrations.", 115, 535, 1000, 42, { fontSize: 20, bold: true, color: blue, align: "center" });
  notes(s, "Report 2: Tools & Infrastructures; Report 3: External API Inventory", "Describe the technical choices as documented project technologies, not as completed production claims.");
}

// 8. Roadmap
{
  const s = deck.slides.getItem(7); clear(s, 8); title(s, "A 15-week delivery plan turns the concept into a release", "The plan sequences discovery, design, three implementation sprints, verification, deployment, and handover.");
  const phases = [
    ["Weeks 1–3", "Initiation and backlog", "42 pds"], ["Weeks 4–5", "Architecture and design", "43 pds"],
    ["Weeks 6–11", "Three delivery sprints", "124 pds"], ["Week 12", "Integration and verification", "38 pds"],
    ["Weeks 13–14", "UAT and deployment", "28 pds"], ["Week 15", "Handover and defense", "16 pds"],
  ];
  phases.forEach(([wk, name, effort], i) => { const x=40+i*200; rect(s,x,260,175,220,i===2?"#DCEEFF":pale); box(s,wk,x+16,285,143,24,{fontSize:14,bold:true,color:blue,align:"center"}); box(s,name,x+16,335,143,62,{fontSize:17,bold:true,color:ink,align:"center"}); box(s,effort,x+16,435,143,22,{fontSize:15,color:muted,align:"center"}); });
  box(s, "Total planned effort: 291 person-days", 400, 570, 480, 30, { fontSize: 21, bold: true, color: navy, align: "center" });
  notes(s, "Report 2: Cost & Time Estimations", "Point out that the product scope is staged by dependency and business value.");
}

// 9. Team
{
  const s = deck.slides.getItem(8); clear(s, 9); title(s, "A five-member cross-functional team governs delivery", "Scrum roles and RACI ownership make priorities, quality, and technical responsibilities explicit.");
  const people = [
    ["Nguyễn Văn Huy", "Project Manager, Lead BA, Fullstack"], ["Lò Mạnh Xum", "Backend Lead, Database Architect, Fullstack"],
    ["Ngô Đức Dự", "Frontend Lead, UI/UX, Fullstack"], ["Trần Bá Tú", "QA/QC Lead, Fullstack"], ["Nguyễn Quang Thương", "DevOps and System Administration, Fullstack"],
  ];
  people.forEach(([name,role],i)=>{const y=195+i*75; box(s,`${String(i+1).padStart(2,"0")}`,75,y,45,28,{fontSize:15,bold:true,color:blue}); box(s,name,150,y,280,28,{fontSize:20,bold:true,color:ink}); box(s,role,475,y,620,34,{fontSize:17,color:muted});});
  notes(s, "Report 2: Role and Responsibilities; Scrum Roles & Responsibilities", "Explain that every member contributes to delivery while holding a clear primary responsibility.");
}

// 10. Quality
{
  const s = deck.slides.getItem(9); clear(s, 10); title(s, "Quality management is integrated into every sprint", "The team uses readiness criteria, code review, incremental testing, and UAT feedback before release.");
  card(s, 42, 215, 280, 260, "Definition of Ready", bullets(["Clear business rules", "Confirmed data dependencies", "Agreed estimate and acceptance criteria"]));
  card(s, 350, 215, 280, 260, "Definition of Done", bullets(["Implemented API and interface", "Peer review and test evidence", "Demo-ready sprint increment"]));
  card(s, 658, 215, 280, 260, "Testing strategy", bullets(["Unit and integration tests", "System testing on Azure staging", "UAT scenarios for farmer and buyer"]));
  card(s, 966, 215, 272, 260, "Project objectives", bullets(["90% timeliness target", "90% requirement completeness", "Zero acceptance-test defects recorded"]));
  notes(s, "Report 2: Quality Management, Testing Strategy, Project Objectives", "Discuss quality as a continuous control loop, not a final project phase.");
}

// 11. Risks and constraints
{
  const s = deck.slides.getItem(10); clear(s, 11); title(s, "Risk control preserves feasibility within the capstone scope", "The plan protects core value while recognizing technology, timing, and business constraints.");
  card(s, 42, 210, 360, 330, "Priority project risks", bullets(["Unclear business requirements", "Limited team availability", "Scope expansion requests", "Uneven workload", "Compatibility issues"]));
  card(s, 460, 210, 360, 330, "Management response", bullets(["Early clarification and benchmark research", "Daily commitments and weekly reviews", "Scope review with supervisors", "Explicit task assignment", "Early target-environment testing"]));
  card(s, 878, 210, 360, 330, "Deliberate exclusions", bullets(["No transport fleet or GPS tracking", "No physical IoT sensors", "No blockchain deployment", "No native mobile application"]));
  notes(s, "Report 1: Limitations & Exclusions; Report 2: Risks and Mitigations; Assumptions & Constraints", "Show that exclusions are delivery decisions, not missing project objectives.");
}

// 12. Closing
{
  const s = deck.slides.getItem(11); clear(s, 12); title(s, "Expected graduation project contribution", "AgriSustain provides a feasible blueprint for transparent agricultural trade across retail and B2B channels.");
  card(s, 42, 230, 374, 270, "For farmers", "Direct market access, digital farming records, verified profiles, and better visibility for quality products.");
  card(s, 453, 230, 374, 270, "For buyers", "A clearer way to evaluate supplier capacity, product origin, certificates, price, and order progress.");
  card(s, 864, 230, 374, 270, "For the project", "A complete capstone lifecycle from problem analysis and requirements to Scrum delivery, verification, and handover.");
  box(s, "Thank you", 42, 575, 1100, 48, { fontSize: 32, bold: true, color: navy, align: "center" });
  notes(s, "Reports 1–3", "Close by returning to the purpose: trusted digital trade that serves farmers, households, and business buyers.");
}

const candidatePath = path.join(stagingDir, "candidate-agrisustain.pptx");
await (await PresentationFile.exportPptx(deck)).save(candidatePath);

const { finalizePresentation } = await import(pathToFileURL(path.join(skillDir, "container_tools/artifact_tool_utils.mjs")).href);
const result = await finalizePresentation({
  workspaceDir,
  candidatePath,
  finalPath,
  pythonExecutable: nodePython,
  integrityValidatorPath: path.join(skillDir, "container_tools/inspect_presentation_package_integrity.py"),
  layoutValidatorPath: path.join(skillDir, "container_tools/inspect_presentation_layout_geometry.py"),
  layoutArgs: ["--expected-slide-size-emu", "12192000,6858000", "--validate-bullet-geometry", "--validate-heading-fit"],
  explicitTotalSlideCount: 12,
  sourceTemplatePath: templatePath,
  verifyArtifactToolImport: true,
  receiptPath: path.join(stagingDir, "AgriSustain_Graduation_Project_Presentation_v4.validation.json"),
});
console.log(JSON.stringify(result));
