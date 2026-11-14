document.addEventListener("DOMContentLoaded", async () => {
  if (!Auth.requireAuth("Finance")) return;
  const ui = new FarmerBusinessUI();
  ui.m = Messages.payout;
  const $ = ui.$;
  const base = "/finance/farmer-payouts";
  let page = 1,
    total = 0,
    current = null,
    busy = false,
    generation = 0;
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
      display("#payoutCount", ui.m.requestCount(total));
      display("#payoutPage", page);
      $("#payoutPrev").disabled = page <= 1;
      $("#payoutNext").disabled = page * 10 >= total;
      if (!total) ui.notice(ui.m.empty);
    } catch (error) {
      ui.notice(error.message || ui.m.loadFailed, true);
    }
  }

  function renderDetail(data) {
    current = data;
    ui.kv("#savedRecipient", [
      ["Ngân hàng", data.recipient.bankCode], ["Số tài khoản", data.recipient.accountNumber],
      ["Chủ tài khoản", data.recipient.accountHolder],
      ["Xác minh", ui.label(data.recipient.verificationStatus)],
      ["Giữ chi trả đến", ui.date(data.recipient.payoutHoldUntil)],
    ]);
    $("#sourceRows").innerHTML = data.sources.map(row => `<tr>
      <td>${ui.escape(row.reference || row.id)}<small>${ui.escape(row.channel)}</small></td>
      <td>${ui.escape(data.status === "Processed" ? ui.m.sourceSettled : data.status === "Rejected"
        ? ui.m.sourceReleased : row.holdReason || ui.m.sourceReserved)}</td>
      <td class="amount">${ui.money(row.gross)}</td>
      <td class="amount">${ui.money(row.commission)}</td>
      <td class="amount">${ui.money(row.deduction)}</td>
    </tr>`).join("");

    display("#payoutCode", data.code);
    display("#payoutBadge", ui.label(data.status));
    ui.kv("#savedPayoutDetails", [
      ["Nông dân", data.farmer],
      ["Ngày gửi", ui.date(data.submittedAt)],
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
      data.rejectionReason || (data.status === "Failed" ? ui.m.failed : ui.m.noFailure),
    );
    display(
      "#payoutNotification",
      data.notificationFailed ? ui.m.notificationFailed : data.status === "Pending" ? ui.m.submittedPending
        : data.status === "Rejected" ? ui.m.notificationRejected : ui.m.notifications,
    );
    $("#processPayout").disabled =
      busy ||
      !data.canProcess ||
      !!data.holdReason ||
      !!data.integrationMessage;
    $("#rejectRequest").disabled = busy || !data.canReject;
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


  async function command(action) {
    if (!current || busy) return;
    const id = current.id;
    let snapshot = current;
    busy = true;
    renderDetail(current);
    try {
      if (action === "process") {
        snapshot = await ui.request(`${base}/${id}/qr-preview`);
        if (current?.id !== id) return;
        const confirmed = await ui.confirm(ui.m.confirmTransfer(ui.money(snapshot.net),
          snapshot.recipient.accountHolder, snapshot.recipient.bankCode, snapshot.recipient.accountNumber));
        if (!confirmed || current?.id !== id) return;
      }
      busy = true;
      renderDetail(current);
      const body = action === "reconcile"
        ? { existingOperationKey: snapshot.plan.operationKey }
        : { expectedVersion: snapshot.version, confirmedNetAmount: snapshot.net, requestKey: crypto.randomUUID() };
      const result = await ui.request(`${base}/${id}/${action}`, { method: "POST", body });
      await detail(id);
      ui.notice(result.message);
      if (action === "process" && current.plan) $("#qrPaymentBox").scrollIntoView({ behavior: "smooth", block: "start" });
    } catch (error) {
      ui.notice(error.message || ui.m.saveFailed, true);
    } finally {
      busy = false;
      if (current) renderDetail(current);
    }
  }

  let rejectSnapshot = null;
  let rejection = null;
  $("#rejectRequest").onclick = () => {
    if (busy || !current?.canReject) return;
    rejectSnapshot = { ...current };
    rejection = null;
    $("#rejectReason").value = "";
    $("#rejectError").textContent = "";
    $("#rejectDialog").showModal();
  };
  $("#closeReject").onclick = () => { if (!busy) $("#rejectDialog").close(); };
  $("#rejectDialog").addEventListener("cancel", event => { if (busy) event.preventDefault(); });
  $("#confirmReject").onclick = async () => {
    if (busy || !rejectSnapshot) return;
    const reason = $("#rejectReason").value.trim();
    if (!reason || reason.length > 500) {
      $("#rejectError").textContent = !reason ? ui.m.rejectionRequired : ui.m.rejectionTooLong;
      return;
    }
    const signature = JSON.stringify({ id: rejectSnapshot.id, version: rejectSnapshot.version, reason });
    if (rejection?.signature !== signature) rejection = { signature, key: crypto.randomUUID() };
    busy = true;
    $("#confirmReject").disabled = true;
    $("#closeReject").disabled = true;
    try {
      const result = await ui.request(`${base}/${rejectSnapshot.id}/reject`, {
        method: "POST", body: { expectedVersion: rejectSnapshot.version, requestKey: rejection.key, reason },
      });
      $("#rejectDialog").close();
      await detail(result.id);
      ui.notice(result.message);
    } catch (error) {
      $("#rejectError").textContent = error.message || ui.m.saveFailed;
    } finally {
      busy = false;
      $("#confirmReject").disabled = false;
      $("#closeReject").disabled = false;
      if (current) renderDetail(current);
    }
  };
  ui.navigate = async (screen) => {
    if (busy) return;
    if (screen === "detail" && !current) { ui.notice(ui.m.selectPayout, true); return; }
    ui.go(screen);
    if (screen === "list") await list();
  };
  $("#processPayout").onclick = () => command("process");
  $("#checkGateway").onclick = () => command("reconcile");
  $("#copyPayoutReference").onclick = async () => {
    try {
      await navigator.clipboard.writeText(current.plan.operationKey);
      ui.notice(ui.m.copied);
    } catch { ui.notice(ui.m.copyFailed, true); }
  };
  $("#payoutRows").onclick = (event) => {
    const button = event.target.closest("[data-payout]");
    if (button && !busy) detail(Number(button.dataset.payout)).catch(error => ui.notice(error.message, true));
  };
  $("#applyPayoutFilter").onclick = () => {
    if (field("#payoutFrom") && field("#payoutTo") && field("#payoutFrom") > field("#payoutTo")) {
      ui.notice(ui.m.dates, true);
      return;
    }
    page = 1;
    list();
  };
  $("#resetPayoutFilter").onclick = () => {
    ["#payoutSearch", "#payoutFarmerFilter", "#payoutStatusFilter", "#payoutFrom", "#payoutTo"].forEach(id => { $(id).value = ""; });
    page = 1;
    list();
  };
  $("#payoutPrev").onclick = () => { if (page > 1) { page--; list(); } };
  $("#payoutNext").onclick = () => { if (page * 10 < total) { page++; list(); } };
  const timer = setInterval(async () => {
    if (busy || document.hidden || !current?.canReconcile || $("#screen-detail").hidden
      || $("#rejectDialog").open || $("#confirmDialog").open) return;
    const id = current.id;
    busy = true;
    try {
      await ui.request(`${base}/${id}/reconcile`, { method: "POST", body: { existingOperationKey: current.plan.operationKey } });
      await detail(id, true);
      if (current?.id === id && current.status === "Processed") ui.notice(ui.m.sourceDebit);
    } catch {
      // Unknown bank results retain the original request and its reservations.
    } finally {
      busy = false;
      if (current) renderDetail(current);
    }
  }, 15000);
  window.addEventListener("pagehide", () => clearInterval(timer), { once: true });
  ui.go("list");
  try {
    const farmers = await ui.request(base + "/farmers");
    ui.options("#payoutFarmerFilter", farmers, row => row.name, "Tất cả nông dân");
    await list();
    const requestId = new URLSearchParams(location.search).get("requestId");
    if (/^[1-9]\d*$/.test(requestId || "")) await detail(Number(requestId));
  } catch (error) { ui.notice(error.message || ui.m.loadFailed, true); }
});
