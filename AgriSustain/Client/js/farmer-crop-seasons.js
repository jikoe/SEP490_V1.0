document.addEventListener("DOMContentLoaded", () => {
  const ui = new FarmUI(),
    $ = ui.$,
    f = $("#seasonForm"),
    complete = $("#completeForm"),
    cancel = $("#cancelForm");
  let current = null,
    rows = [],
    saving = false;
  const named = (form, name) => form.elements.namedItem(name);
  function products() {
    ui.options(
      named(f, "product"),
      ui.optionsData.products,
      "productId",
      (x) => x.productName,
      "Chọn sản phẩm / cây trồng",
    );
  }
  function units(value) {
    const p = ui.optionsData.products.find((x) => x.productId === Number(ui.value(f, "product")));
    const list = [
      ...new Set(
        [
          p?.unit,
          ...(p && ["kg", "g", "tấn", "tạ", "yến"].includes(p.unit) ? ["kg", "g", "tấn", "tạ", "yến"] : []),
        ].filter(Boolean),
      ),
    ];
    const s = named(f, "unit");
    s.replaceChildren(new Option("Chọn đơn vị", ""));
    list.forEach((x) => s.add(new Option(x, x)));
    s.value = value || p?.unit || "";
  }
  function sync() {
    const planned = !current || current.status === "Planned";
    for (const e of f.elements)
      if (["INPUT", "SELECT"].includes(e.tagName) && e.name !== "status") e.disabled = !planned;
    f.querySelector("[type=submit]").disabled = !planned;
    $("#planting").disabled = !current || current.status !== "Planned";
    $("#growing").disabled = !current || current.status !== "Planting";
    $("#harvesting").disabled = !current || current.status !== "Growing";
    complete.querySelector("[type=submit]").disabled = !current || current.status !== "Harvesting";
    cancel.querySelector("[type=submit]").disabled =
      !current || ["Completed", "Cancelled", "Crop Failure"].includes(current.status);
  }
  function context() {
    $("#commitments").innerHTML = ui.commitments(current?.commitments || []);
    const activities = (current?.activities || [])
      .map(
        (x) =>
          `<p>${ui.date(x.actionDate)} · ${ui.escape(x.actionType)} · ${ui.escape(x.description)}${x.correctsLogId ? " · " + ui.escape(ui.m.correction(x.correctsLogId)) : ""}${x.correctionReason ? " · " + ui.escape(x.correctionReason) : ""}</p>`,
      )
      .join("");
    const batches = (current?.harvestBatches || [])
      .map(
        (x) =>
          `<p><a href="harvest-batches.html?id=${x.harvestBatchId}">${ui.escape(x.batchCode)}</a> · ${ui.date(x.actualHarvestDate)} · ${ui.number(x.quantity)} ${ui.escape(x.unit)}</p>`,
      )
      .join("");
    $("#seasonHistory").innerHTML =
      `<p>Nhật ký: ${current?.logCount || 0} hoạt động · Tổng lô đã thu hoạch: ${ui.number(current?.recordedHarvestYield)} ${ui.escape(current?.yieldUnit || "")}</p>${activities}${batches}${ui.history(current?.history || [])}`;
    $("#closeSeasonTitle").textContent = current
      ? `${current.seasonName} · ${ui.m.stage[current.status]}`
      : ui.m.selectRecord;
    $("#harvestSummary").textContent = current
      ? `Tổng các lô thực tế: ${ui.number(current.recordedHarvestYield)} ${current.yieldUnit}. Hoàn thành mùa vụ không làm tăng tồn kho.`
      : ui.m.selectRecord;
    ui.fill(complete, {
      completion: "",
      actual: "",
      batchTotal: current ? `${ui.number(current.recordedHarvestYield)} ${current.yieldUnit}` : "—",
    });
    ui.fill(cancel, { realized: "", outcome: "", reason: "" });
    cancel.querySelector("input[type=file]").value = "";
    sync();
  }
  function open(record) {
    current = record;
    f.reset();
    ui.clearErrors(f);
    ui.clearErrors(complete);
    ui.clearErrors(cancel);
    products();
    ui.fill(
      f,
      record
        ? {
            name: record.seasonName,
            product: record.productId,
            variety: record.cropVariety,
            area: record.cultivatedArea,
            plant: record.plantingDate,
            estimate: record.estimatedHarvestDate,
            yield: record.estimatedYield,
            status: ui.m.stage[record.status],
          }
        : {
            name: "",
            product: "",
            variety: "",
            area: "",
            plant: "",
            estimate: "",
            yield: "",
            status: ui.m.stage.Planned,
          },
    );
    units(record?.yieldUnit);
    context();
    ui.go("form");
  }
  async function detail(id, view = "form") {
    try {
      const data = await ui.request("/" + id);
      open(data);
      ui.go(view);
    } catch (error) {
      ui.failure(error);
    }
  }
  async function load(silent = false) {
    try {
      const q = ui.filter();
      const data = await ui.request("?" + API.toQueryString(q));
      rows = data.items;
      ui.pager(data);
      ui.table(
        rows
          .map(
            (s) =>
              `<tr><td><strong>${ui.escape(s.seasonName)}</strong><br>${ui.escape(s.productName)} · ${ui.escape(s.cropVariety)}</td><td>${ui.number(s.cultivatedArea)} ha</td><td>Trồng: ${ui.date(s.plantingDate)}<br>Dự kiến: ${ui.date(s.estimatedHarvestDate)}</td><td>Ước tính ${ui.number(s.estimatedYield)} ${ui.escape(s.yieldUnit)}<br>Thực tế: ${s.actualYield === null ? "chưa chốt" : ui.number(s.actualYield) + " " + ui.escape(s.yieldUnit)}</td><td><span class="pill">${ui.escape(ui.m.stage[s.status] || s.status)}</span></td><td><button class="btn" type="button" data-open="${s.cropSeasonId}">Xem / cập nhật</button> <button class="btn" type="button" data-close="${s.cropSeasonId}">Kết thúc / mất mùa</button></td></tr>`,
          )
          .join(""),
        6,
      );
    } catch (error) {
      if (!silent) ui.failure(error);
    }
  }
  ui.handlers = {
    load,
    sync,
    navigate: (view) => {
      if (view === "list") {
        ui.go("list");
        load();
      } else if (view === "form") {
        if (current) ui.go(view);
        else open(null);
      } else if (current) {
        context();
        ui.go(view);
      } else ui.notice(ui.m.selectRecord, true);
    },
  };
  $("#newSeason").onclick = () => open(null);
  named(f, "product").onchange = () => units();
  $("#rows").onclick = (e) => {
    const b = e.target.closest("[data-open],[data-close]");
    if (b) detail(b.dataset.open || b.dataset.close, b.dataset.close ? "close" : "form");
  };
  f.onsubmit = async (e) => {
    e.preventDefault();
    if (saving || !ui.validate(f)) return;
    if (current && current.status !== "Planned") return ui.notice(ui.m.MSG131, true);
    const body = {
      seasonName: ui.value(f, "name"),
      productId: Number(ui.value(f, "product")),
      cropVariety: ui.value(f, "variety"),
      cultivatedArea: Number(ui.value(f, "area")),
      plantingDate: ui.value(f, "plant"),
      estimatedHarvestDate: ui.value(f, "estimate"),
      estimatedYield: Number(ui.value(f, "yield")),
      yieldUnit: ui.value(f, "unit"),
      version: current?.version,
      _method: current ? "PUT" : "POST",
    };
    saving = true;
    const isNew = !current;
    try {
      const saved = await ui.command(current ? "/" + current.cropSeasonId : "", body, f, "plan");
      if (saved) {
        open(saved);
        await load(true);
        if (isNew) ui.go("list");
        ui.notice(saved.message);
      }
    } finally {
      saving = false;
      sync();
    }
  };
  for (const [id, next] of [
    ["planting", "Planting"],
    ["growing", "Growing"],
    ["harvesting", "Harvesting"],
  ])
    $("#" + id).onclick = async () => {
      if (!current || saving) return;
      if (!(await ui.confirm(ui.m.confirmStage(ui.m.stage[next])))) return;
      const saved = await ui.command(
        "/" + current.cropSeasonId + "/stage",
        { nextStage: next, version: current.version },
        f,
      );
      if (saved) {
        open(saved);
        await load(true);
        ui.notice(saved.message);
      }
    };
  complete.onsubmit = async (e) => {
    e.preventDefault();
    if (!current || saving || !ui.validate(complete)) return;
    if (!(await ui.confirm(ui.m.confirmComplete))) return;
    const saved = await ui.command(
      "/" + current.cropSeasonId + "/complete",
      {
        completionDate: ui.value(complete, "completion"),
        actualYield: Number(ui.value(complete, "actual")),
        version: current.version,
      },
      complete,
    );
    if (saved) {
      open(saved);
      await load(true);
      ui.go("close");
      ui.notice(saved.message);
    }
  };
  cancel.onsubmit = async (e) => {
    e.preventDefault();
    if (!current || saving || !ui.validate(cancel)) return;
    if (!(await ui.confirm(ui.m.confirmCancel))) return;
    saving = true;
    try {
      const proof = await ui.upload(cancel.querySelector("input[type=file]"), "season");
      const saved = await ui.command(
        "/" + current.cropSeasonId + "/cancel",
        {
          outcome: ui.value(cancel, "outcome"),
          reason: ui.value(cancel, "reason"),
          actualYield: Number(ui.value(cancel, "realized")),
          evidenceUrls: proof,
          version: current.version,
        },
        cancel,
      );
      if (saved) {
        open(saved);
        await load(true);
        ui.go("close");
        ui.notice(saved.message);
      }
    } catch (error) {
      ui.failure(error, cancel);
    } finally {
      saving = false;
      sync();
    }
  };
  ui.start(async () => {
    products();
    open(null);
    ui.go("list");
    await load();
    if (!ui.optionsData.products.length) ui.notice(ui.m.emptyProducts);
    const id = new URLSearchParams(location.search).get("id");
    if (id && /^\d+$/.test(id)) await detail(id);
  });
});
