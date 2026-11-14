document.addEventListener("DOMContentLoaded", async () => {
  if (!Auth.requireAuth("Farmer")) return;
  const ui = new FarmerBusinessUI();
  const $ = ui.$;
  const payments = new FarmerPaymentRequestsUI(ui, load);
  let report = null;
  let selected = null;
  let filters = {};
  let page = 1;
  let sequence = 0;
  function defaults() {
    const now = new Date();
    const today = new Date(now.getTime() + 7 * 3600000)
      .toISOString()
      .slice(0, 10);
    const start = new Date(today + "T00:00:00Z");
    start.setUTCDate(start.getUTCDate() - 29);
    $("#revenueFrom").value = start.toISOString().slice(0, 10);
    $("#revenueTo").value = today;
    [
      "#revenueSeason",
      "#revenueChannel",
      "#revenueStage",
      "#revenueStatus",
    ].forEach((id) => {
      $(id).value = "";
    });
  }
  function applied() {
    return {
      from: $("#revenueFrom").value,
      to: $("#revenueTo").value,
      season: $("#revenueSeason").value,
      channel: $("#revenueChannel").value,
      paymentStage: $("#revenueStage").value,
      settlementState: $("#revenueStatus").value,
      dateBasis: "EventDate",
    };
  }
  function syncFilters() {
    for (const suffix of ["Season", "Channel", "From", "To", "Stage", "Status"]) {
      $("#history" + suffix).innerHTML = $("#revenue" + suffix).innerHTML;
      $("#history" + suffix).value = $("#revenue" + suffix).value;
    }
  }
  function query() {
    return new URLSearchParams(
      Object.entries({ ...filters, page }).filter(([, value]) => value !== ""),
    );
  }
  function metrics(totals) {
    [
      "sales",
      "receipts",
      "deposits",
      "commission",
      "refund",
      "eligible",
      "held",
      "payout",
    ].forEach((key) => {
      $("#" + key + "Metric").textContent = totals
        ? ui.money(totals[key === "refund" ? "refunds" : key])
        : "—";
    });
  }
  function chart(rows) {
    const channels = ["B2C", "B2B"];
    const series = [
      { key: "sales", color: "#176a49", label: "Giá trị bán" },
      { key: "receipts", color: "#6ea894", label: "Thu đã xác minh" },
      { key: "payout", color: "#d6aa56", label: "Thực nhận" },
    ];
    const groups = channels.map((channel) => ({
      channel,
      values: series.map(({ key }) => rows
        .filter((row) => row.channel === channel)
        .reduce((total, row) => total + Math.max(0, Number(row[key]) || 0), 0)),
    }));
    const maximum = Math.max(1, ...groups.flatMap((group) => group.values));
    const parts = [
      '<text x="12" y="18" fill="#687c70" font-size="12">Triệu VND</text>',
      '<line x1="75" y1="175" x2="950" y2="175" stroke="#dce6df"/>',
    ];
    groups.forEach((group, index) => {
      group.values.forEach((value, bar) => {
        const height = value / maximum * 130;
        const x = 190 + index * 400 + bar * 70;
        const label = (value / 1000000).toLocaleString("vi-VN", {
          maximumFractionDigits: 6,
        });
        parts.push(
          `<rect x="${x}" y="${175 - height}" width="48" height="${height}" rx="3" fill="${series[bar].color}">`
          + `<title>${group.channel} · ${series[bar].label}: ${ui.money(value)}</title></rect>`,
          `<text x="${x + 24}" y="${165 - height}" text-anchor="middle" font-size="11" fill="#405c4c">${label}</text>`,
        );
      });
      parts.push(`<text x="${285 + index * 400}" y="202" text-anchor="middle" fill="#687c70">${group.channel}</text>`);
    });
    $("#revenueChart").innerHTML = parts.join("");
  }
  async function load() {
    const request = ++sequence;
    try {
      const data = await ui.request("/farmer/revenue?" + query());
      if (request !== sequence) return;
      report = data;
      const selectedSeason = $("#revenueSeason").value;
      ui.options(
        "#revenueSeason",
        data.seasons,
        (row) => row.name,
        "Tất cả mùa vụ",
      );
      $("#revenueSeason").value = selectedSeason;
      syncFilters();
      metrics(data.totals);
      chart(data.chart);
      $("#appliedFilterLabel").textContent =
        `${data.filters.from} – ${data.filters.to} · ${data.dateBasis} · ${data.currency} · cập nhật ${ui.date(data.generatedAt)}`;
      $("#revenueRows").innerHTML = data.items
        .map(
          (row) => `<tr>
        <td>${ui.escape(row.reference)}<small>${ui.date(row.at)}</small></td>
        <td>${ui.escape(ui.label(row.kind))}<small>${ui.escape(row.season)}</small></td>
        <td>${ui.escape(ui.label(row.channel))}<small>${ui.escape(ui.label(row.stage))}</small></td>
        <td class="amount">${ui.money(row.eventAmount || row.sales || row.receipts || row.payout || row.eligible || row.held || row.deposits || row.refunds)}</td>
        <td>${ui.money(row.commission)} / ${ui.money(row.refunds)}</td>
        <td>${ui.escape(ui.label(row.state))}</td><td><button type="button" class="btn" data-transaction="${ui.escape(row.id)}">Xem chi tiết</button></td></tr>`,
        )
        .join("");
      $("#revenueEmpty").hidden = data.total > 0;
      $("#revenueCount").textContent =
        `${data.total} bản ghi · 10 bản ghi/trang`;
      $("#revenuePage").textContent = page;
      $("#revenuePrev").disabled = page <= 1;
      $("#revenueNext").disabled = page * 10 >= data.total;
      $("#exportRevenue").disabled = false;
      ui.notice(data.message);
    } catch (error) {
      if (request !== sequence) return;
      metrics(null);
      $("#exportRevenue").disabled = true;
      ui.notice(error.message || ui.m.loadFailed, true);
      // Preserve the previous register; unavailable figures are not displayed as zero.
    }
  }
  async function detail(id) {
    selected = await ui.request(
      "/farmer/revenue/transaction/" + encodeURIComponent(id),
    );
    ui.kv("#revenueDetail", [
      ["Tham chiếu", selected.reference],
      ["Ngày ghi nhận", ui.date(selected.at)],
      ["Loại", ui.label(selected.kind)],
      ["Kênh", ui.label(selected.channel)],
      ["Mùa vụ", selected.season],
      ["Giai đoạn", ui.label(selected.stage)],
      ["Trạng thái", ui.label(selected.state)],
      ["Giá trị sự kiện", ui.money(selected.eventAmount)],
      ["Số lượng", ui.number(selected.quantity)],
      ["Đơn giá thỏa thuận", ui.money(selected.unitPrice)],
      ["Doanh số", ui.money(selected.sales)],
      ["Khoản thu", ui.money(selected.receipts)],
      ["Cọc", ui.money(selected.deposits)],
      ["Hoàn", ui.money(selected.refunds)],
      ["Hoa hồng", ui.money(selected.commission)],
      ["Khoản khấu trừ", ui.money(selected.deductions)],
      ["Đủ điều kiện", ui.money(selected.eligible)],
      ["Đang giữ", ui.money(selected.held)],
      ["Thực nhận", ui.money(selected.payout)],
      ["Mã chuyển khoản", selected.transferReference],
      ["Lý do giữ", selected.holdReason],
    ]);
    ui.go("detail");
    if (selected.state === "Held") ui.notice(ui.m.pending);
  }
  ui.navigate = (screen) => {
    if (["requests", "request-create", "request-detail"].includes(screen)) {
      payments.navigate(screen);
      return;
    }
    if (payments.busy) return;
    if (screen === "detail" && !selected) {
      ui.notice(ui.m.selectTransaction, true);
      return;
    }
    syncFilters();
    ui.go(screen);
  };
  $("#revenueRows").onclick = (event) => {
    const button = event.target.closest("[data-transaction]");
    if (button)
      detail(button.dataset.transaction).catch((error) =>
        ui.notice(error.message, true),
      );
  };
  $("#applyRevenue").onclick = () => {
    const next = applied();
    if (!next.from || !next.to) {
      ui.notice(ui.m.required, true);
      return;
    }
    if (next.from > next.to) {
      ui.notice(ui.m.invalidDates, true);
      return;
    }
    filters = next;
    syncFilters();
    page = 1;
    load();
  };
  $("#resetRevenue").onclick = () => {
    defaults();
    filters = applied();
    syncFilters();
    page = 1;
    load();
  };
  $("#applyHistory").onclick = () => {
    for (const suffix of ["Season", "Channel", "From", "To", "Stage", "Status"]) {
      $("#revenue" + suffix).value = $("#history" + suffix).value;
    }
    $("#applyRevenue").click();
  };
  $("#resetHistory").onclick = () => $("#resetRevenue").click();
  $("#revenuePrev").onclick = () => {
    if (page > 1) {
      page--;
      load();
    }
  };
  $("#revenueNext").onclick = () => {
    if (report && page * 10 < report.total) {
      page++;
      load();
    }
  };
  $("#exportRevenue").onclick = async () => {
    $("#exportRevenue").disabled = true;
    try {
      const format = $("#exportFormat").value;
      const params = query();
      params.set("format", format);
      const response = await fetch(
        `${CONFIG.API_BASE_URL}/farmer/revenue/export?${params}`,
        { headers: { Authorization: "Bearer " + Auth.getAccessToken() } },
      );
      if (!response.ok) throw new Error(ui.m.exportFailed);
      const url = URL.createObjectURL(await response.blob());
      const link = document.createElement("a");
      link.href = url;
      link.download = `doanh-thu-${filters.from}-${filters.to}.${format}`;
      link.click();
      setTimeout(() => URL.revokeObjectURL(url), 10000);
      ui.notice(ui.m.exported);
    } catch (error) {
      ui.notice(ui.m.exportFailed, true);
    } finally {
      $("#exportRevenue").disabled = false;
    }
  };
  defaults();
  filters = applied();
  syncFilters();
  ui.go("dashboard");
  await load();
});
