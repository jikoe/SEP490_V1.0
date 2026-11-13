document.addEventListener("DOMContentLoaded", async () => {
  if (!Auth.requireAuth("Finance")) return;
  const ui = new FarmerBusinessUI();
  ui.m = Messages.payout;
  const $ = ui.$;
  const base = "/finance/farmer-payouts";
  let page = 1,
    total = 0,
    current = null,
    preview = null,
    busy = false,
    generation = 0;
  let selected = new Set();
  const requests = new Map();
  const field = (id) => $(id).value.trim();
  const display = (id, text) => {
    $(id).textContent = text ?? "";
  };

  async function list() {
    const request = ++generation;
    const params = new URLSearchParams({ page });
    [
      ["keyword", "#payoutSearch"],
      ["farmerId", "#payoutFarmerFilter"],
      ["status", "#payoutStatusFilter"],
      ["from", "#payoutFrom"],
      ["to", "#payoutTo"],
    ].forEach(([key, selector]) => {
      if (field(selector)) params.set(key, field(selector));
    });
    try {
      const data = await ui.request(base + "?" + params);
      if (request !== generation) return;
      total = data.total;
      $("#payoutRows").innerHTML = data.items
        .map(
          (row) => `<tr>
        <td><strong>${ui.escape(row.code)}</strong><small>${ui.date(row.at)}</small></td>
        <td>${ui.escape(row.farmer)}</td><td class="amount">${ui.money(row.amount)}</td>
        <td class="amount">${ui.money(row.deductedCommission)} / ${ui.money(row.deduction)}</td><td class="amount">${ui.money(row.netAmount)}</td>
        <td>${ui.escape(ui.label(row.payoutStatus))}</td>
        <td><button class="btn" type="button" data-payout="${row.id}">Xem / xử lý</button></td></tr>`,
        )
        .join("");
      $("#payoutEmpty").hidden = total > 0;
      display("#payoutCount", `${total} khoản chi trả · 10 bản ghi/trang`);
      display("#payoutPage", page);
      $("#payoutPrev").disabled = page <= 1;
      $("#payoutNext").disabled = page * 10 >= total;
      if (!total) ui.notice(ui.m.empty);
    } catch (error) {
      ui.notice(error.message || ui.m.loadFailed, true);
    }
  }

  function totals(data) {
    display("#sourceCount", selected.size);
    for (const [selector, key] of [
      ["#sourceGross", "gross"],
      ["#sourceFee", "commission"],
      ["#sourceDeduction", "deduction"],
      ["#sourceNet", "net"],
    ])
      display(selector, data ? ui.money(data[key]) : "—");
    $("#createPayoutButton").disabled = busy || !data?.canCreate;
  }

  function renderSources(data) {
    ui.kv("#recipientDetails", [
      ["Ngân hàng", data.recipient?.bankName],
      ["Số tài khoản", data.recipient?.accountNumber],
      ["Chủ tài khoản", data.recipient?.accountHolder],
      ["Đã xác minh", ui.date(data.recipient?.verifiedAt)],
    ]);
    $("#sourceRows").innerHTML = data.sources
      .map(
        (row) => `<tr>
      <td><input type="checkbox" aria-label="Chọn ${ui.escape(row.reference)}" data-source="${row.id}"
        ${row.eligible ? "" : "disabled"} ${selected.has(row.id) ? "checked" : ""}></td>
      <td>${ui.escape(row.reference)}<small>${ui.escape(row.product)} · ${ui.date(row.at)}</small></td>
      <td>${ui.escape(row.holdReason || "Đủ điều kiện")}<small>Đã chi ${ui.money(row.settled)} · đang giữ ${ui.money(row.held)}</small></td>
      <td class="amount">${ui.money(row.gross)}</td><td class="amount">${ui.money(row.commission)}</td>
      <td class="amount">${ui.money(row.deduction)}</td></tr>`,
      )
      .join("");
    if (data.message) ui.notice(data.message, true);
  }

  async function creationContext() {
    selected = new Set();
    preview = null;
    totals(null);
    $("#sourceRows").replaceChildren();
    const farmer = Number(field("#recipientFarmer"));
    if (!farmer) {
      ui.kv("#recipientDetails", []);
      return;
    }
    const requested = ++generation;
    try {
      const data = await ui.request(`${base}/creation-context/${farmer}`);
      if (requested !== generation) return;
      renderSources(data);
      if (!data.sources.length) ui.notice(ui.m.noSources);
    } catch (error) {
      ui.notice(error.message, true);
    }
  }

  async function previewSelection() {
    preview = null;
    totals(null);
    if (!selected.size) return;
    const requested = ++generation;
    try {
      const data = await ui.request(base + "/preview", {
        method: "POST",
        body: {
          farmerId: Number(field("#recipientFarmer")),
          selectedSourceIds: [...selected].sort((a, b) => a - b),
        },
      });
      if (requested !== generation) return;
      preview = data;
      totals(data);
    } catch (error) {
      ui.notice(error.message, true);
    }
  }

  function renderDetail(data) {
    current = data;
    display("#payoutCode", data.code);
    display("#payoutBadge", ui.label(data.status));
    ui.kv("#savedPayoutDetails", [
      ["Nông dân", data.farmer],
      ["Ngân hàng nhận", data.recipient.bankCode],
      ["Số tài khoản nhận", data.recipient.accountNumber],
      ["Chủ tài khoản", data.recipient.accountHolder],
      ["Ngày lập", ui.date(data.approvedAt)],
      [
        "Nguồn chi trả",
        data.sources
          .map((row) => `${row.reference || row.id}: ${ui.money(row.gross)}`)
          .join(" · "),
      ],
    ]);
    [
      ["#savedGross", "gross"],
      ["#savedFee", "commission"],
      ["#savedDeduction", "deduction"],
      ["#savedNet", "net"],
    ].forEach(([selector, key]) => display(selector, ui.money(data[key])));
    ui.kv("#transferDetails", [
      ["Trạng thái", ui.label(data.status)],
      ["Mã giao dịch", data.bankTransferRef],
      ["Thời điểm tiền ra đã xác minh", ui.date(data.processedAt)],
      [
        "Cơ sở ghi nhận",
        data.status === "Processed" ? ui.m.sourceDebit : ui.m.pending,
      ],
    ]);
    display(
      "#reconciliationText",
      data.status === "Processed"
        ? ui.m.sourceDebit
        : data.integrationMessage || data.holdReason || ui.m.pending,
    );
    display(
      "#failureText",
      data.status === "Failed" ? ui.m.failed : ui.m.noFailure,
    );
    display(
      "#payoutNotification",
      data.notificationFailed ? ui.m.notificationFailed : ui.m.notifications,
    );
    $("#processPayout").disabled =
      busy ||
      !data.canProcess ||
      !!data.holdReason ||
      !!data.integrationMessage;
    $("#cancelPayout").disabled = busy || !data.canCancel;
    $("#checkGateway").disabled = busy || !data.canReconcile;
    $("#qrPaymentBox").hidden = !data.plan || data.status !== "Processing";
    if (data.plan) {
      $("#payoutQrImage").src = data.plan.qrUrl;
      ui.kv("#payoutQrDetails", [
        [
          "Tài khoản nguồn",
          `${data.plan.sourceBankName} · ${data.plan.sourceAccountNumber}`,
        ],
        [
          "Tài khoản nhận",
          `${data.recipient.bankCode} · ${data.recipient.accountNumber}`,
        ],
        ["Chủ tài khoản nhận", data.recipient.accountHolder],
        ["Số tiền", ui.money(data.plan.amount)],
        ["Nội dung bắt buộc", data.plan.operationKey],
      ]);
    }
    $("#payoutHistory").innerHTML = data.history
      .map(
        (row) =>
          `<li>${ui.date(row.occurredAt)} · ${ui.escape(ui.label(row.action))}${row.reason ? " · " + ui.escape(row.reason) : ""}</li>`,
      )
      .join("");
  }

  async function detail(id, quiet = false) {
    const data = await ui.request(`${base}/${id}`);
    if (quiet && current?.id !== id) return;
    renderDetail(data);
    if (!quiet) ui.go("detail");
  }

  async function create() {
    if (busy || !preview?.canCreate) return;
    if (!(await ui.confirm(ui.m.confirmCreate))) return;
    const body = {
      farmerId: preview.farmerId,
      selectedSourceIds: [...selected].sort((a, b) => a - b),
      expectedBalance: preview.expectedBalance,
    };
    const key = JSON.stringify(body);
    if (!requests.has(key)) requests.set(key, crypto.randomUUID());
    busy = true;
    totals(preview);
    try {
      const result = await ui.request(base, {
        method: "POST",
        body: { ...body, requestKey: requests.get(key) },
      });
      requests.delete(key);
      selected.clear();
      preview = null;
      await detail(result.id);
      ui.notice(result.message);
    } catch (error) {
      ui.notice(error.message || ui.m.saveFailed, true);
    } finally {
      busy = false;
      totals(preview);
      if (current) renderDetail(current);
    }
  }

  async function command(action) {
    if (!current || busy) return;
    const id = current.id;
    if (action !== "reconcile") {
      const message =
        action === "process"
          ? ui.m.confirmTransfer(
              ui.money(current.net),
              current.recipient.accountHolder,
              current.recipient.bankCode,
              current.recipient.accountNumber,
            )
          : ui.m.confirm;
      if (!(await ui.confirm(message))) return;
    }
    busy = true;
    renderDetail(current);
    try {
      const body =
        action === "reconcile"
          ? { existingOperationKey: current.plan.operationKey }
          : {
              expectedVersion: current.version,
              confirmedNetAmount: current.net,
              requestKey: crypto.randomUUID(),
            };
      const result = await ui.request(`${base}/${id}/${action}`, {
        method: "POST",
        body,
      });
      await detail(id);
      ui.notice(result.message);
      if (action === "process" && current.plan)
        $("#qrPaymentBox").scrollIntoView({
          behavior: "smooth",
          block: "start",
        });
    } catch (error) {
      ui.notice(error.message || ui.m.saveFailed, true);
    } finally {
      busy = false;
      if (current) renderDetail(current);
    }
  }

  ui.navigate = async (screen) => {
    if (busy) return;
    if (screen === "detail" && !current) {
      ui.notice(ui.m.selectPayout, true);
      return;
    }
    ui.go(screen);
    if (screen === "list") await list();
  };
  $("#newPayout").onclick = () => {
    if (!busy) {
      ui.go("create");
      creationContext();
    }
  };
  $("#recipientFarmer").onchange = creationContext;
  $("#sourceRows").onchange = (event) => {
    const checkbox = event.target.closest("[data-source]");
    if (!checkbox || busy) return;
    checkbox.checked
      ? selected.add(Number(checkbox.dataset.source))
      : selected.delete(Number(checkbox.dataset.source));
    previewSelection();
  };
  $("#createPayoutButton").onclick = create;
  $("#discardPayout").onclick = () => {
    if (!busy) {
      selected.clear();
      preview = null;
      ui.go("list");
      list();
    }
  };
  $("#processPayout").onclick = () => command("process");
  $("#cancelPayout").onclick = () => command("cancel");
  $("#checkGateway").onclick = () => command("reconcile");
  $("#copyPayoutReference").onclick = async () => {
    try {
      await navigator.clipboard.writeText(current.plan.operationKey);
      ui.notice(ui.m.copied);
    } catch {
      ui.notice(ui.m.copyFailed, true);
    }
  };
  $("#payoutRows").onclick = (event) => {
    const button = event.target.closest("[data-payout]");
    if (button && !busy)
      detail(Number(button.dataset.payout)).catch((error) =>
        ui.notice(error.message, true),
      );
  };
  $("#applyPayoutFilter").onclick = () => {
    if (
      field("#payoutFrom") &&
      field("#payoutTo") &&
      field("#payoutFrom") > field("#payoutTo")
    ) {
      ui.notice(ui.m.dates, true);
      return;
    }
    page = 1;
    list();
  };
  $("#resetPayoutFilter").onclick = () => {
    [
      "#payoutSearch",
      "#payoutFarmerFilter",
      "#payoutStatusFilter",
      "#payoutFrom",
      "#payoutTo",
    ].forEach((id) => {
      $(id).value = "";
    });
    page = 1;
    list();
  };
  $("#payoutPrev").onclick = () => {
    if (page > 1) {
      page--;
      list();
    }
  };
  $("#payoutNext").onclick = () => {
    if (page * 10 < total) {
      page++;
      list();
    }
  };
  const timer = setInterval(async () => {
    if (
      busy ||
      document.hidden ||
      !current ||
      $("#screen-detail").hidden ||
      !current.canReconcile
    )
      return;
    const id = current.id;
    busy = true;
    try {
      await ui.request(`${base}/${id}/reconcile`, {
        method: "POST",
        body: { existingOperationKey: current.plan.operationKey },
      });
      await detail(id, true);
      if (current.status === "Processed") ui.notice(ui.m.sourceDebit);
    } catch {
      /* Retain previous values; unknown results never trigger a second transfer. */
    } finally {
      busy = false;
      if (current) renderDetail(current);
    }
  }, 15000);
  window.addEventListener("pagehide", () => clearInterval(timer), {
    once: true,
  });
  ui.go("list");
  totals(null);
  try {
    const farmers = await ui.request(base + "/farmers");
    ui.options(
      "#payoutFarmerFilter",
      farmers,
      (row) => row.name,
      "Tất cả nông dân",
    );
    ui.options("#recipientFarmer", farmers, (row) => row.name, "Chọn nông dân");
    await list();
  } catch (error) {
    ui.notice(error.message, true);
  }
});
