document.addEventListener("DOMContentLoaded", () => {
  const ui = new FarmUI(),
    $ = ui.$,
    f = $("#batchForm"),
    issue = $("#issueForm");
  let current = null,
    labelData = null,
    saving = false;
  const el = (form, name) => form.elements.namedItem(name);
  function productOptions() {
    ui.options(
      el(f, "product"),
      ui.optionsData.products,
      "productId",
      (x) => x.productName,
      "Chọn sản phẩm / cây trồng",
    );
    ui.options(
      $("#filters").elements.filterProduct,
      ui.optionsData.products,
      "productId",
      (x) => x.productName,
      "Tất cả sản phẩm",
    );
    ui.options(
      $("#filters").elements.filterSeason,
      ui.optionsData.seasons,
      "cropSeasonId",
      (x) => x.seasonName,
      "Tất cả mùa vụ",
    );
  }
  function seasonOptions(selected) {
    const productId = Number(ui.value(f, "product"));
    ui.options(
      el(f, "season"),
      ui.optionsData.seasons.filter((x) => x.productId === productId),
      "cropSeasonId",
      (x) => x.seasonName,
      "Chọn mùa vụ",
    );
    if (selected) el(f, "season").value = selected;
    el(f, "unit").value = ui.optionsData.products.find((x) => x.productId === productId)?.unit || "";
    el(f, "unit").readOnly = true;
    dates();
  }
  function dates() {
    const s = ui.optionsData.seasons.find((x) => x.cropSeasonId === Number(ui.value(f, "season")));
    el(f, "harvest").min = s?.plantingDate || "";
    el(f, "harvest").max = ui.optionsData.today;
    el(f, "estimated").min = s?.plantingDate || "";
  }
  function sync() {
    const actual = ui.value(f, "mode") === "Actual",
      accepted = !!current && current.status !== "Planned";
    for (const name of ["estimated", "estimatedYield"]) {
      el(f, name).disabled = actual;
      el(f, name).required = !actual;
      el(f, name).closest(".field").hidden = actual;
    }
    for (const name of ["harvest", "expiry", "yield"]) {
      el(f, name).disabled = !actual;
      el(f, name).required = actual;
      el(f, name).closest(".field").hidden = !actual;
    }
    for (const name of ["code", "harvest", "yield"]) el(f, name).readOnly = accepted;
    for (const name of ["product", "season", "mode"]) el(f, name).disabled = accepted;
    const reasonLabel = f.querySelector("label[for=changeReason]");
    if (reasonLabel) reasonLabel.textContent = "Lý do thay đổi" + (accepted ? " *" : "");
    el(f, "changeReason").required = accepted;
    el(f, "changeReason").closest(".field").hidden = !accepted;
    $("#generateQr").disabled = !current || current.status === "Planned";
    el(f, "unit").readOnly = true;
    const quality = ui.value(issue, "issueType") === "quality";
    el(issue, "affected").disabled = !quality;
    el(issue, "affected").required = quality;
    el(issue, "adjustment").disabled = quality;
    el(issue, "adjustment").required = !quality;
    issue.querySelector("[type=submit]").disabled = !current || current.status === "Planned";
  }
  function open(record, mode) {
    current = record;
    labelData = null;
    f.reset();
    issue.reset();
    ui.clearErrors(f);
    ui.clearErrors(issue);
    productOptions();
    ui.fill(
      f,
      record
        ? {
            code: record.batchCode,
            product: record.productId,
            mode: record.status === "Planned" ? "Planned" : "Actual",
            estimated: record.estimatedHarvestDate,
            estimatedYield: record.estimatedYield || "",
            harvest: record.actualHarvestDate,
            expiry: record.expirationDate,
            yield: record.harvestYield || "",
            notes: record.qualityNotes,
            changeReason: "",
          }
        : {
            code: "",
            product: "",
            mode: mode || "Actual",
            estimated: "",
            estimatedYield: "",
            harvest: "",
            expiry: "",
            yield: "",
            notes: "",
            changeReason: "",
          },
    );
    seasonOptions(record?.cropSeasonId);
    if (record?.status === "Planned" && mode === "Actual")
      ui.fill(f, { mode: "Actual", harvest: "", expiry: "", yield: "" });
    el(f, "available").value =
      record && record.status !== "Planned"
        ? `${ui.number(record.availableQuantity)} ${record.unit}`
        : "Chưa ghi nhận hàng thực tế";
    $("#qrConditions").innerHTML = record
      ? `Ngày thu hoạch: ${ui.date(record.actualHarvestDate)} · ${record.logCount} nhật ký mùa vụ · ${ui.escape(ui.m.batch[record.status])} · ${record.qrIdentity ? "Đã cấp QR" : "Chưa cấp QR"}<br>${ui.commitments(record.commitments)}`
      : "Lưu thu hoạch hợp lệ trước khi cấp QR; yêu cầu có nhật ký canh tác.";
    el(issue, "issueBatch").value = record?.batchCode || "";
    $("#issueHistory").innerHTML = ui.history(record?.history || []);
    ui.$('[data-preview="issueEvidence"]').replaceChildren();
    sync();
    ui.go("form");
  }
  async function detail(id, mode, view = "form") {
    try {
      open(await ui.request("/" + id), mode);
      ui.go(view);
    } catch (error) {
      ui.failure(error);
    }
  }
  async function load(silent = false) {
    try {
      const data = await ui.request("?" + API.toQueryString(ui.filter()));
      ui.pager(data);
      ui.table(
        data.items
          .map(
            (b) =>
              `<tr><td><strong>${ui.escape(b.batchCode)}</strong><br>${ui.escape(b.productName)}</td><td>${ui.escape(b.seasonName)}<br>Dự kiến ${ui.date(b.estimatedHarvestDate)}<br>Thực tế ${ui.date(b.actualHarvestDate)} · HSD ${ui.date(b.expirationDate)}</td><td>${b.status === "Planned" ? "Ước tính " + ui.number(b.estimatedYield) + " " + ui.escape(b.unit) + "<br>Chưa nhập kho" : "Thực tế " + ui.number(b.harvestYield) + " " + ui.escape(b.unit) + "<br>Điều chỉnh " + ui.number(b.adjustmentQuantity) + " · Giữ " + ui.number(b.reservedQuantity) + " · Đã xuất " + ui.number(b.shippedQuantity) + "<br><strong>Còn " + ui.number(b.availableQuantity) + " " + ui.escape(b.unit) + "</strong>"}</td><td><span class="pill">${ui.escape(ui.m.batch[b.status] || b.status)}</span></td><td><button class="btn" type="button" data-open="${b.harvestBatchId}" data-mode="${b.status === "Planned" ? "Actual" : ""}">${b.status === "Planned" ? "Ghi thu hoạch" : "Xem / ghi chú"}</button>${b.status === "Planned" ? '<button class="btn" type="button" data-plan="' + b.harvestBatchId + '">Xem / sửa kế hoạch</button>' : ""}<button class="btn" type="button" data-issue="${b.harvestBatchId}" ${b.status === "Planned" ? "disabled" : ""}>Ghi vấn đề</button></td></tr>`,
          )
          .join(""),
        5,
      );
    } catch (error) {
      if (!silent) ui.failure(error);
    }
  }
  async function label(force = false) {
    labelData = null;
    if (!current) return ui.notice(ui.m.selectRecord, true);
    try {
      const result =
        !current.qrIdentity && !force
          ? await ui.command("/" + current.harvestBatchId + "/qr", { version: current.version }, f)
          : await ui.request("/" + current.harvestBatchId + "/label");
      if (!result) return;
      labelData = result;
      $("#qrProduct").textContent = result.productName;
      $("#qrBatch").textContent = result.batchCode;
      $("#qrInfo").textContent =
        `${result.farmName} · ${result.seasonName} · Thu hoạch ${ui.date(result.harvestDate)} · HSD ${ui.date(result.expirationDate)}`;
      let image = $("#qrImage");
      if (!image) {
        const node = ui.$(".qr");
        image = document.createElement("img");
        image.id = "qrImage";
        node.replaceWith(image);
      }
      image.src = result.qrPng;
      image.alt = result.qrIdentity;
      $("#qrStatus").textContent = result.qrIdentity;
      $("#traceInfo").textContent =
        `Nguồn gốc: ${result.farmName} · ${result.seasonName}. Nhãn giữ nguyên mã truy xuất của lô.`;
      ui.go("label");
      ui.notice(result.message);
      current = await ui.request("/" + current.harvestBatchId);
    } catch (error) {
      ui.failure(error);
    }
  }
  ui.handlers = {
    load,
    sync,
    navigate: (view) => {
      if (view === "list") {
        ui.go("list");
        load();
      } else if (view === "label") label();
      else if (!current && view === "form") open(null, "Actual");
      else if (current && (view !== "issue" || current.status !== "Planned")) {
        ui.go(view);
        sync();
      } else ui.notice(ui.m.selectRecord, true);
    },
  };
  $("#newPlan").onclick = () => open(null, "Planned");
  $("#newActual").onclick = () => open(null, "Actual");
  el(f, "product").onchange = () => seasonOptions();
  el(f, "season").onchange = dates;
  el(f, "mode").onchange = sync;
  el(issue, "issueType").onchange = sync;
  $("#rows").onclick = (e) => {
    const b = e.target.closest("[data-open],[data-plan],[data-issue]");
    if (b)
      detail(
        b.dataset.open || b.dataset.plan || b.dataset.issue,
        b.dataset.mode,
        b.dataset.issue ? "issue" : "form",
      );
  };
  f.onsubmit = async (e) => {
    e.preventDefault();
    if (saving || !ui.validate(f)) return;
    const actual = ui.value(f, "mode") === "Actual",
      existingActual = current && current.status !== "Planned";
    saving = true;
    try {
      if (
        (existingActual || actual) &&
        !(await ui.confirm(existingActual ? ui.m.confirmUpdate : ui.m.confirmHarvest))
      )
        return;
      const body = {
        productId: Number(ui.value(f, "product")),
        cropSeasonId: Number(ui.value(f, "season")),
        batchCode: ui.value(f, "code"),
        unit: ui.value(f, "unit"),
        estimatedHarvestDate: ui.value(f, "estimated") || null,
        estimatedYield: Number(ui.value(f, "estimatedYield")),
        actualHarvestDate: actual ? ui.value(f, "harvest") || null : null,
        actualYield: actual ? Number(ui.value(f, "yield")) : 0,
        expirationDate: actual ? ui.value(f, "expiry") || null : null,
        qualityNotes: ui.value(f, "notes"),
        reason: ui.value(f, "changeReason"),
        version: current?.version,
      };
      const path = existingActual
        ? "/" + current.harvestBatchId
        : actual
          ? current
            ? "/" + current.harvestBatchId + "/harvest"
            : "/harvest"
          : current
            ? "/" + current.harvestBatchId + "/plan"
            : "/plans";
      body._method = existingActual || (!actual && current) ? "PUT" : "POST";
      const saved = await ui.command(path, body, f, "batch-save");
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
    if (!current || current.status === "Planned" || saving || !ui.validate(issue)) return;
    saving = true;
    try {
      if (!(await ui.confirm(ui.m.confirmIssue))) return;
      const evidence = await ui.upload(el(issue, "issueEvidence"), "batch");
      const quality = ui.value(issue, "issueType") === "quality";
      const saved = await ui.command(
        "/" + current.harvestBatchId + "/issues",
        {
          issueType: ui.value(issue, "issueType"),
          quantity: Number(ui.value(issue, quality ? "affected" : "adjustment")),
          reason: ui.value(issue, "issueReason"),
          evidenceUrls: evidence,
          version: current.version,
        },
        issue,
      );
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
    open(null, "Actual");
    ui.go("list");
    await load();
    const id = new URLSearchParams(location.search).get("id");
    if (id && /^\d+$/.test(id)) await detail(id);
  });
});
