document.addEventListener("DOMContentLoaded", () => {
  const ui = new FarmUI(),
    $ = ui.$,
    f = $("#logForm"),
    cf = $("#correctionForm");
  let original = null,
    originals = [],
    saving = false,
    selection = 0;
  const el = (form, key) => form.elements.namedItem(key);
  function selections() {
    const seasons = ui.optionsData.seasons,
      activities = ui.optionsData.activities.map((x) => ({ value: x }));
    for (const [form, name] of [
      [f, "logseason"],
      [cf, "correctseason"],
    ])
      ui.options(
        el(form, name),
        seasons,
        "cropSeasonId",
        (x) => `${x.seasonName} · ${ui.m.stage[x.status] || x.status}`,
        "Chọn mùa vụ",
      );
    ui.options(
      ui.$("#filters").elements.filterSeason,
      seasons,
      "cropSeasonId",
      (x) => x.seasonName,
      "Tất cả mùa vụ",
    );
    for (const [form, name] of [
      [f, "logactivity"],
      [cf, "correctactivity"],
    ])
      ui.options(el(form, name), activities, "value", (x) => x.value, "Chọn hoạt động");
    ui.options(
      ui.$("#filters").elements.filterStatus,
      activities,
      "value",
      (x) => x.value,
      "Tất cả hoạt động",
    );
  }
  function requirements(form, prefix) {
    const activity = ui.value(form, prefix + "activity"),
      pest = activity === "Phòng trừ sâu bệnh";
    el(form, prefix + "material").required = pest || activity === "Bón phân";
    el(form, prefix + "dosage").required = pest || activity === "Bón phân";
    const reuse =
      form === cf && (original?.evidenceUrls || []).some((x) => x.startsWith("/api/farmer/evidence/"));
    el(form, prefix + "images").required = pest && !reuse;
    for (const [suffix, title, required] of [
      ["material", "Vật tư", pest || activity === "Bón phân"],
      ["dosage", "Liều lượng / cách dùng", pest || activity === "Bón phân"],
      ["images", "Ảnh bằng chứng", pest && !reuse],
    ]) {
      const input = el(form, prefix + suffix),
        label = form.querySelector('label[for="' + input.id + '"]');
      if (label) label.textContent = title + (required ? " *" : "");
    }
    const season = ui.optionsData.seasons.find(
      (x) => x.cropSeasonId === Number(ui.value(form, prefix + "season")),
    );
    el(form, prefix + "execution").min = season?.plantingDate || "";
    el(form, prefix + "execution").max = ui.optionsData.today;
  }
  function fresh() {
    f.reset();
    ui.clearErrors(f);
    ui.$('[data-preview="logimages"]').replaceChildren();
    requirements(f, "log");
    ui.go("form");
  }
  async function originalOptions(id) {
    if (!originals.length) {
      const data = await ui.request("?pageSize=100");
      originals = data.items.filter((x) => !x.correctsLogId);
    }
    if (id && !originals.some((x) => x.cultivationLogId === Number(id))) {
      const data = await ui.request("/" + id);
      if (!data.correctsLogId) originals.push(data);
    }
    ui.options(
      el(cf, "original"),
      originals,
      "cultivationLogId",
      (x) => `NK-${x.cultivationLogId} · ${ui.date(x.actionDate)} · ${x.actionType}`,
      "Chọn bản gốc",
    );
    if (id) el(cf, "original").value = id;
  }
  async function syncOriginal() {
    const id = ui.value(cf, "original"),
      seq = ++selection;
    if (!id) {
      original = null;
      $("#originalLog").textContent = ui.m.noOriginal;
      return;
    }
    try {
      const data = await ui.request("/" + id);
      if (seq !== selection) return;
      original = data;
      $("#originalLog").innerHTML =
        `<strong>NK-${data.cultivationLogId}</strong> · ${ui.escape(data.seasonName)} · ${ui.date(data.actionDate)} · ${ui.escape(data.detailDescription)}<br>Người thực hiện: ${ui.escape(data.performedBy)}${ui.proofs(data.evidenceUrls)}`;
      if (![...el(cf, "correctactivity").options].some((x) => x.value === data.actionType))
        el(cf, "correctactivity").add(new Option(data.actionType, data.actionType));
      ui.fill(cf, {
        correctseason: data.cropSeasonId,
        correctexecution: data.actionDate,
        correctactivity: data.actionType,
        correctmaterial: data.inputMaterial,
        correctdosage: data.dosage,
        correctperformer: data.performedBy,
        correctdescription: data.detailDescription,
      });
      el(cf, "correctseason").disabled = true;
      requirements(cf, "correct");
    } catch (error) {
      ui.failure(error, cf);
    }
  }
  async function correction(id) {
    try {
      cf.reset();
      ui.clearErrors(cf);
      ui.$('[data-preview="correctimages"]').replaceChildren();
      await originalOptions(id);
      ui.go("correction");
      if (id) await syncOriginal();
      else {
        original = null;
        $("#originalLog").textContent = ui.m.noOriginal;
      }
      if (!originals.length) ui.notice(ui.m.missingOriginal, true);
    } catch (error) {
      ui.failure(error);
    }
  }
  async function details(id) {
    try {
      const l = await ui.request("/" + id);
      const box = $("#proofContent");
      box.innerHTML = `<h3>NK-${l.cultivationLogId} · ${ui.escape(l.seasonName)}</h3><p>${ui.date(l.actionDate)} · ${ui.escape(l.actionType)}</p><p>${ui.escape(l.detailDescription)}</p><p>${ui.escape(l.inputMaterial)} · ${ui.escape(l.dosage)}</p><p>Người thực hiện: ${ui.escape(l.performedBy)}<br>Người ghi: ${ui.escape(l.recorder)} · ${ui.datetime(l.recordedAt)}</p>${l.correctsLogId ? "<p>" + ui.escape(ui.m.correction(l.correctsLogId)) + " · " + ui.escape(l.correctionReason) + "</p>" : ""}${ui.proofs(l.evidenceUrls)}`;
      $("#proofViewer").showModal();
    } catch (error) {
      ui.failure(error);
    }
  }
  async function load(silent = false) {
    try {
      const q = ui.filter();
      q.activity = q.status;
      delete q.status;
      const data = await ui.request("?" + API.toQueryString(q));
      ui.pager(data);
      const season = ui.optionsData.seasons.find((x) => x.cropSeasonId === Number(q.seasonId));
      $("#seasonContext").textContent = season
        ? `${season.seasonName} · ${ui.m.stage[season.status]} · Trồng ${ui.date(season.plantingDate)} · ${ui.number(season.cultivatedArea)} ha`
        : "Chỉ hiển thị mùa vụ và nhật ký thuộc nông trại. Bản gốc và các bản đính chính được giữ nguyên.";
      ui.table(
        data.items
          .map(
            (l) =>
              `<tr><td><strong>NK-${l.cultivationLogId}</strong><br>${ui.date(l.actionDate)} · ${ui.escape(l.actionType)}<br>${ui.escape(l.seasonName)}${l.correctsLogId ? '<br><span class="pill amber">' + ui.escape(ui.m.correction(l.correctsLogId)) + "</span><br><small>Lý do: " + ui.escape(l.correctionReason) + "</small>" : ""}</td><td>${ui.escape(l.detailDescription)}<br><small>${ui.escape(l.inputMaterial)} ${ui.escape(l.dosage)}</small></td><td>${ui.escape(l.performedBy)}<br><small>Người ghi: ${ui.escape(l.recorder)}<br>${ui.datetime(l.recordedAt)}<br>Không sửa / xóa</small></td><td>${l.evidenceUrls.length ? ui.proofs(l.evidenceUrls) : ui.escape(ui.m.noProof)}<br><button class="btn" data-detail="${l.cultivationLogId}" type="button">Xem chi tiết</button>${!l.correctsLogId ? '<button class="btn" data-correct="' + l.cultivationLogId + '" type="button">Thêm đính chính</button>' : ""}</td></tr>`,
          )
          .join(""),
        4,
      );
    } catch (error) {
      if (!silent) ui.failure(error);
    }
  }
  ui.handlers = {
    load,
    navigate: (view) => {
      if (view === "list") {
        ui.go("list");
        load();
      } else if (view === "form") fresh();
      else correction();
    },
  };
  $("#newLog").onclick = fresh;
  $("#newCorrection").onclick = () => correction();
  el(cf, "original").onchange = syncOriginal;
  for (const [form, prefix] of [
    [f, "log"],
    [cf, "correct"],
  ]) {
    el(form, prefix + "activity").onchange = () => requirements(form, prefix);
    el(form, prefix + "season").onchange = () => requirements(form, prefix);
  }
  $("#rows").onclick = (e) => {
    const b = e.target.closest("[data-correct],[data-detail]");
    if (b) {
      if (b.dataset.correct) correction(b.dataset.correct);
      else details(b.dataset.detail);
    }
  };
  async function save(e, form, prefix, correcting) {
    e.preventDefault();
    if (saving) return;
    if (correcting && !original) return ui.notice(ui.m.noOriginal, true);
    requirements(form, prefix);
    if (!ui.validate(form)) return;
    saving = true;
    try {
      const seasonId = correcting ? original.cropSeasonId : Number(ui.value(form, prefix + "season"));
      const season = ui.optionsData.seasons.find((x) => x.cropSeasonId === seasonId);
      if (!correcting && !["Planting", "Growing", "Harvesting"].includes(season?.status)) {
        ui.notice(ui.m.MSG133, true);
        return;
      }
      if (correcting && !(await ui.confirm(ui.m.confirmCorrection))) return;
      let evidence = await ui.upload(el(form, prefix + "images"), "log");
      if (correcting && !evidence.length)
        evidence = original.evidenceUrls.filter((x) => x.startsWith("/api/farmer/evidence/"));
      const body = {
        cropSeasonId: seasonId,
        actionDate: ui.value(form, prefix + "execution"),
        actionType: ui.value(form, prefix + "activity"),
        detailDescription: ui.value(form, prefix + "description"),
        performedBy: ui.value(form, prefix + "performer"),
        inputMaterial: ui.value(form, prefix + "material"),
        dosage: ui.value(form, prefix + "dosage"),
        evidenceUrls: evidence,
        correctionReason: correcting ? ui.value(form, "correctionReason") : "",
        version: correcting ? original.version : undefined,
      };
      const saved = await ui.command(
        correcting ? "/" + original.cultivationLogId + "/corrections" : "",
        body,
        form,
      );
      if (saved) {
        form.reset();
        originals = [];
        original = null;
        ui.clearErrors(form);
        await load(true);
        ui.go("list");
        ui.notice(saved.message);
      }
    } catch (error) {
      ui.failure(error, form);
    } finally {
      saving = false;
    }
  }
  f.onsubmit = (e) => save(e, f, "log", false);
  cf.onsubmit = (e) => save(e, cf, "correct", true);
  ui.start(async () => {
    selections();
    fresh();
    ui.go("list");
    await load();
    const id = new URLSearchParams(location.search).get("logId");
    if (id && /^\d+$/.test(id)) await details(id);
  });
});
