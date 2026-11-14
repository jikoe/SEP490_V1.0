document.addEventListener("DOMContentLoaded", () => {
  const ui = new FarmUI();
  const $ = ui.$;
  const f = $("#batchForm");
  const issue = $("#issueForm");
  const el = (form, name) => form.elements.namedItem(name);
  let current = null;
  let labelData = null;
  let saving = false;
  let loadVersion = 0;

  function productOptions() {
    ui.options(el(f, "product"), ui.optionsData.products, "productId",
      (x) => x.productName, "Chọn sản phẩm / cây trồng");
    ui.options(el($("#filters"), "filterProduct"), ui.optionsData.products,
      "productId", (x) => x.productName, "Tất cả sản phẩm");
    ui.options(el($("#filters"), "filterSeason"), ui.optionsData.seasons,
      "cropSeasonId", (x) => x.seasonName, "Tất cả mùa vụ");
  }

  function seasonOptions(selected) {
    const productId = Number(ui.value(f, "product"));
    const seasons = ui.optionsData.seasons.filter((x) =>
      x.productId === productId && (current || ["Planting", "Growing", "Harvesting"].includes(x.status)));
    ui.options(el(f, "season"), seasons, "cropSeasonId", (x) => x.seasonName, "Chọn mùa vụ");
    if (selected) el(f, "season").value = selected;
    el(f, "unit").value = ui.optionsData.products.find((x) => x.productId === productId)?.unit || "";
    dates();
  }

  function dates() {
    const season = ui.optionsData.seasons.find((x) => x.cropSeasonId === Number(ui.value(f, "season")));
    el(f, "harvest").min = season?.plantingDate || "";
    el(f, "harvest").max = ui.optionsData.today;
    el(f, "expiry").min = ui.value(f, "harvest");
  }

  function sync() {
    for (const name of ["code", "harvest", "yield"]) el(f, name).readOnly = !!current;
    for (const name of ["product", "season"]) el(f, name).disabled = !!current;
    el(f, "changeReason").required = !!current;
    el(f, "changeReason").closest(".field").hidden = !current;
    $("#batchFormTitle").textContent = current ? ui.m.harvestDetailsTitle : ui.m.createHarvestTitle;
    f.querySelector("[type=submit]").textContent = current ? ui.m.updateHarvest : ui.m.recordHarvest;
    f.querySelector("[type=submit]").disabled = saving || ui.busy;
    $("#generateQr").disabled = saving || ui.busy || !current?.qrEligible;
    const quality = ui.value(issue, "issueType") === "quality";
    el(issue, "affected").disabled = !quality;
    el(issue, "affected").required = quality;
    el(issue, "adjustment").disabled = quality;
    el(issue, "adjustment").required = !quality;
    issue.querySelector("[type=submit]").disabled = saving || ui.busy || !current;
  }

  function open(record) {
    current = record;
    labelData = null;
    f.reset();
    issue.reset();
    ui.clearErrors(f);
    ui.clearErrors(issue);
    ui.fill(f, record ? {
      code: record.batchCode,
      product: record.productId,
      harvest: record.actualHarvestDate,
      expiry: record.expirationDate,
      yield: record.harvestYield,
      notes: record.qualityNotes,
    } : { code: "", product: "", harvest: "", expiry: "", yield: "", notes: "" });
    seasonOptions(record?.cropSeasonId);
    el(f, "available").value = record
      ? `${ui.number(record.availableQuantity)} ${record.unit}` : ui.m.noActualStock;
    $("#qrConditions").innerHTML = record
      ? ui.escape(ui.m.qrContext(ui.date(record.actualHarvestDate), record.logCount,
          ui.m.batch[record.status] || record.status, record.qrIdentity ? ui.m.qrPresent : ui.m.qrAbsent))
          + "<br>" + ui.commitments(record.commitments)
      : ui.escape(ui.m.qrPrerequisites);
    el(issue, "issueBatch").value = record?.batchCode || "";
    $("#issueHistory").innerHTML = ui.history(record?.history || []);
    $('[data-preview="issueEvidence"]').replaceChildren();
    sync();
    ui.go("form");
  }

  async function detail(id, view = "form") {
    if (saving || ui.busy) return;
    saving = true;
    try {
      open(await ui.request("/" + id));
      ui.go(view);
    } catch (error) {
      ui.failure(error);
    } finally {
      saving = false;
      sync();
    }
  }

  async function load(silent = false) {
    const revision = ++loadVersion;
    try {
      const data = await ui.request("?" + API.toQueryString(ui.filter()));
      if (revision !== loadVersion) return;
      ui.pager(data);
      ui.table(data.items.map((b) => `
        <tr>
          <td><strong>${ui.escape(b.batchCode)}</strong><br>${ui.escape(b.productName)}</td>
          <td>${ui.escape(b.seasonName)}<br>Thu hoạch ${ui.date(b.actualHarvestDate)}<br>HSD ${ui.date(b.expirationDate)}</td>
          <td>Thực tế ${ui.number(b.harvestYield)} ${ui.escape(b.unit)}<br>
            Điều chỉnh ${ui.number(b.adjustmentQuantity)} · Giữ ${ui.number(b.reservedQuantity)} · Đã xuất ${ui.number(b.shippedQuantity)}<br>
            <strong>Còn ${ui.number(b.availableQuantity)} ${ui.escape(b.unit)}</strong></td>
          <td><span class="pill">${ui.escape(ui.m.batch[b.status] || b.status)}</span></td>
          <td><button class="btn" type="button" data-open="${b.harvestBatchId}">Xem / ghi chú</button>
            <button class="btn" type="button" data-issue="${b.harvestBatchId}">Ghi vấn đề</button></td>
        </tr>`).join(""), 5);
    } catch (error) {
      if (!silent && revision === loadVersion) ui.failure(error);
    }
  }

  async function label(force = false) {
    if (saving || ui.busy) return;
    labelData = null;
    if (!current) return ui.notice(ui.m.selectRecord, true);
    saving = true;
    sync();
    try {
      const id = current.harvestBatchId;
      const result = !current.qrIdentity && !force
        ? await ui.command("/" + id + "/qr", { version: current.version }, f)
        : await ui.request("/" + id + "/label");
      if (!result) return;
      const target = new URL(result.traceabilityUrl);
      if (!["http:", "https:"].includes(target.protocol)) throw new Error(ui.m.MSG126);
      labelData = result;
      $("#qrProduct").textContent = result.productName;
      $("#qrBatch").textContent = result.batchCode;
      $("#qrInfo").textContent = `${result.farmName} · ${result.seasonName} · Thu hoạch ${ui.date(result.harvestDate)} · HSD ${ui.date(result.expirationDate)}`;
      let image = $("#qrImage");
      if (!image) {
        image = document.createElement("img");
        image.id = "qrImage";
        $(".qr").replaceWith(image);
      }
      image.src = result.qrPng;
      image.alt = result.qrIdentity;
      $("#qrStatus").textContent = result.qrIdentity;
      $("#qrLookup").href = target.href;
      $("#qrLookup").target = "_blank";
      $("#qrLookup").rel = "noopener noreferrer";
      $("#traceInfo").textContent = ui.m.labelContext(result.farmName, result.seasonName);
      ui.go("label");
      ui.notice(result.message);
      current = await ui.request("/" + id);
    } catch (error) {
      labelData = null;
      ui.failure(error);
    } finally {
      saving = false;
      sync();
    }
  }

  ui.handlers = {
    load,
    sync,
    navigate: (view) => {
      if (saving || ui.busy) return;
      if (view === "list") {
        ui.go("list");
        load();
      } else if (view === "label") label();
      else if (view === "form" && !current) open(null);
      else if (current) ui.go(view);
      else ui.notice(ui.m.selectRecord, true);
    },
  };
  $("#newActual").onclick = () => {
    if (!saving && !ui.busy) open(null);
  };
  el(f, "product").onchange = () => seasonOptions();
  el(f, "season").onchange = dates;
  el(f, "harvest").onchange = dates;
  el(issue, "issueType").onchange = sync;
  $("#rows").onclick = (e) => {
    const button = e.target.closest("[data-open],[data-issue]");
    if (button) detail(button.dataset.open || button.dataset.issue, button.dataset.issue ? "issue" : "form");
  };

  f.onsubmit = async (e) => {
    e.preventDefault();
    if (saving || ui.busy || !ui.validate(f)) return;
    const harvest = ui.value(f, "harvest");
    const expiry = ui.value(f, "expiry");
    if (!current && (harvest < el(f, "harvest").min || harvest > ui.optionsData.today)) {
      ui.mark(f, "actualHarvestDate", ui.m.DateRange);
      return ui.notice(ui.m.DateRange, true);
    }
    if (expiry <= harvest) {
      ui.mark(f, "expirationDate", ui.m.MSG102);
      return ui.notice(ui.m.MSG102, true);
    }
    saving = true;
    sync();
    try {
      if (!(await ui.confirm(current ? ui.m.confirmUpdate : ui.m.confirmHarvest))) return;
      const body = {
        productId: Number(ui.value(f, "product")),
        cropSeasonId: Number(ui.value(f, "season")),
        batchCode: ui.value(f, "code"),
        unit: ui.value(f, "unit"),
        actualHarvestDate: harvest,
        actualYield: Number(ui.value(f, "yield")),
        expirationDate: expiry,
        qualityNotes: ui.value(f, "notes"),
        reason: ui.value(f, "changeReason"),
        version: current?.version,
        _method: current ? "PUT" : "POST",
      };
      const saved = await ui.command(current ? "/" + current.harvestBatchId : "/harvest", body, f, "batch-save");
      if (saved) {
        open(saved);
        await load(true);
        ui.notice(saved.message);
      }
    } finally {
      saving = false;
      sync();
    }
  };

  issue.onsubmit = async (e) => {
    e.preventDefault();
    if (!current || saving || ui.busy || !ui.validate(issue)) return;
    const quality = ui.value(issue, "issueType") === "quality";
    const quantity = Number(ui.value(issue, quality ? "affected" : "adjustment"));
    if (quantity === 0 || Math.abs(quantity * 1000 - Math.round(quantity * 1000)) > 0.0001) {
      ui.mark(issue, "quantity", ui.m.MSG103);
      return ui.notice(ui.m.MSG103, true);
    }
    saving = true;
    sync();
    try {
      if (!(await ui.confirm(ui.m.confirmIssue))) return;
      const evidence = await ui.upload(el(issue, "issueEvidence"), "batch");
      const saved = await ui.command("/" + current.harvestBatchId + "/issues", {
        issueType: ui.value(issue, "issueType"),
        quantity,
        reason: ui.value(issue, "issueReason"),
        evidenceUrls: evidence,
        version: current.version,
      }, issue);
      if (saved) {
        open(saved);
        await load(true);
        ui.go("issue");
        ui.notice(saved.message);
      }
    } catch (error) {
      ui.failure(error, issue);
    } finally {
      saving = false;
      sync();
    }
  };

  $("#generateQr").onclick = () => label();
  $("#printQr").onclick = async () => {
    await label(true);
    if (labelData && ui.$('.view[data-view="label"]').classList.contains("active")) window.print();
  };
  $("#downloadQr").onclick = async () => {
    try {
      labelData = null;
      await label(true);
      if (!labelData) return;
      const data = labelData;
      const image = new Image();
      image.src = data.qrPng;
      await image.decode();
      const canvas = document.createElement("canvas");
      canvas.width = 1300;
      canvas.height = 510;
      const ctx = canvas.getContext("2d");
      ctx.fillStyle = "white";
      ctx.fillRect(0, 0, 1300, 510);
      ctx.imageSmoothingEnabled = false;
      ctx.drawImage(image, 25, 25, 400, 400);
      ctx.fillStyle = "#16452b";
      ctx.font = "bold 30px Arial";
      ctx.fillText(data.productName, 460, 80, 800);
      ctx.font = "24px Arial";
      ctx.fillText(data.batchCode, 460, 130, 800);
      ctx.fillText(data.farmName, 460, 180, 800);
      ctx.fillText(data.seasonName, 460, 225, 800);
      ctx.fillText(
        "Thu hoạch " + ui.date(data.harvestDate) + " · HSD " + ui.date(data.expirationDate),
        460,
        270,
        800,
      );
      ctx.font = "16px Arial";
      ctx.fillText(data.qrIdentity, 460, 320, 800);
      ctx.fillText(data.traceabilityUrl, 30, 475, 1240);
      const blob = await new Promise((resolve) => canvas.toBlob(resolve, "image/png"));
      if (!blob) throw new Error(ui.m.downloadFailed);
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.download = data.batchCode + ".png";
      a.href = url;
      document.body.append(a);
      a.click();
      a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 60000);
      ui.notice(ui.m.MSG99);
    } catch (error) {
      ui.notice(ui.m.downloadFailed, true);
    }
  };
  ui.start(async () => {
    productOptions();
    open(null);
    ui.go("list");
    await load();
    const id = new URLSearchParams(location.search).get("id");
    if (id && /^\d+$/.test(id)) await detail(id);
  });
});
