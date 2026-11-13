document.addEventListener("DOMContentLoaded", async () => {
  if (!Auth.requireAuth("Farmer")) return;
  const ui = new FarmerBusinessUI();
  const $ = ui.$;
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
    const maximum = Math.max(
      1,
      ...rows.flatMap((row) => [row.sales, row.receipts, row.payout]),
    );
    $("#revenueChart").innerHTML = rows
      .map((row, index) => {
        const x = 80 + index * 225;
        return (
          [row.sales, row.receipts, row.payout]
            .map((amount, bar) => {
              const height = (Math.max(0, amount) / maximum) * 140;
              return `<rect x="${x + bar * 38}" y="${165 - height}" width="30" height="${height}" fill="${["#26724a", "#70ad84", "#b5d6bd"][bar]}"><title>${ui.escape(ui.label(row.channel))}: ${ui.money(amount)}</title></rect>`;
            })
            .join("") +
          `<text x="${x}" y="192" font-size="13">${ui.escape(ui.label(row.channel))}</text>`
        );
      })
      .join("");
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
    if (screen === "detail" && !selected) {
      ui.notice(ui.m.selectTransaction, true);
      return;
    }
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
    page = 1;
    load();
  };
  $("#resetRevenue").onclick = () => {
    defaults();
    filters = applied();
    page = 1;
    load();
  };
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
  ui.go("dashboard");
  await load();
});
