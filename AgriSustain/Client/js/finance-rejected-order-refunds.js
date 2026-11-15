document.addEventListener("DOMContentLoaded", async () => {
  if (!Auth.requireAuth("Finance")) return;
  const ui = new FarmerBusinessUI();
  ui.m = Messages.rejectedRefund;
  const m = ui.m;
  const $ = ui.$;
  const base = "/finance/rejected-order-refunds";
  let current = null;
  let page = 1;
  let total = 0;
  let busy = false;
  let listGeneration = 0;
  let detailGeneration = 0;
  const text = (id, value) => { $(id).textContent = value ?? "—"; };

  function buttons() {
    $("#openQr").disabled = busy || !current?.canOpenQr;
    $("#check").disabled = busy || !["Processing", "NeedsReconciliation"].includes(current?.status);

  }

  async function loadList() {
    const generation = ++listGeneration;
    const from = $("#from").value;
    const to = $("#to").value;
    if (from && to && from > to) return ui.notice(m.invalidDates, true);
    const params = new URLSearchParams({ page });
    [["keyword", "#keyword"], ["status", "#status"], ["from", "#from"], ["to", "#to"]]
      .forEach(([key, id]) => {
        if ($(id).value.trim()) params.set(key, $(id).value.trim());
      });
    try {
      const data = await ui.request(`${base}?${params}`);
      if (generation !== listGeneration) return;
      total = data.total;
      $("#rows").innerHTML = data.items.map(row => `
        <tr>
          <td><strong>${ui.escape(row.code)}</strong><small>${ui.date(row.submittedAt)}</small></td>
          <td>${ui.escape(row.orderCode)}<small>${ui.escape(row.customer)}</small></td>
          <td>${ui.escape(row.farmer)}</td>
          <td class="amount">${ui.money(row.amount)}</td>
          <td><span class="badge">${ui.escape(ui.label(row.status))}</span></td>
          <td><button class="btn" type="button" data-request="${row.id}">Xem / xử lý</button></td>
        </tr>`).join("") || `<tr><td colspan="6">${ui.escape(m.empty)}</td></tr>`;
      text("#count", m.requestCount(total));
      text("#page", page);
      $("#prev").disabled = page <= 1;
      $("#next").disabled = page * 10 >= total;
    } catch (error) {
      ui.notice(error.message || m.loadFailed, true);
    }
  }

  function render(data) {
    current = data;
    text("#refundCode", data.code);
    text("#refundStatus", ui.label(data.status));
    ui.kv("#orderDetails", [
      ["Đơn nông trại", data.orderCode], ["RetailCustomer", data.customer], ["Nông trại", data.farmer],
      ["Ngày hủy / Gửi yêu cầu", ui.date(data.submittedAt)],
      ["Lý do từ chối", data.reason], ["Trạng thái đơn", "Đã hủy"],
    ]);
    $("#amountDetails").innerHTML = [
      ["Tiền hàng", data.subtotal], ["Phí giao hàng", data.deliveryFee],
      ["Giảm giá được phân bổ", data.discountShare], ["Giá trị đơn đã hủy", data.finalAmount],
      ["Thanh toán nguồn đã xác minh", data.verifiedPayment],
      ["Các khoản hoàn khác đã giữ / đã hoàn", data.otherReserved],
      ["Số tiền yêu cầu hoàn", data.amount],
    ].map(([label, value]) => `<div class="line"><span>${ui.escape(label)}</span><strong>${ui.money(value)}</strong></div>`).join("");
    ui.kv("#recipient", [
      ["Ngân hàng", data.recipient.bankCode], ["Số tài khoản", data.recipient.accountNumber],
      ["Chủ tài khoản", data.recipient.accountHolder],
    ]);
    const plan = data.plan;
    $("#qrBox").hidden = !plan || data.status === "Succeeded";
    if (plan) {
      $("#qr").src = plan.qrUrl;
      ui.kv("#transferData", [
        ["Tài khoản nguồn", `${plan.sourceBankCode} · ${plan.sourceAccountNumber}`],
        ["Ngân hàng nhận", plan.recipientBankCode], ["Tài khoản nhận", plan.recipientAccountNumber],
        ["Chủ tài khoản", data.recipient.accountHolder], ["Số tiền", ui.money(plan.amount)],
        ["Nội dung bắt buộc", plan.operationKey],
      ]);
    }
    text("#reconciliation", data.status === "Succeeded" ? m.succeeded : plan ? m.pending : data.eligibilityMessage || m.unopened);
    ui.kv("#result", [
      ["Trạng thái hoàn tiền", ui.label(data.status)], ["Số tiền", ui.money(data.amount)],
      ["Mã giao dịch ngân hàng", data.gatewayReference || m.notVerified],
      ["Thời điểm xác minh", ui.date(data.processedAt)], ["Trạng thái đơn hàng", "Đã hủy"],
    ]);
    $("#history").innerHTML = data.history.map(row =>
      `<li>${ui.date(row.occurredAt)} · ${ui.escape(ui.label(row.action))}<small>${ui.escape(row.reason || "")}</small></li>`).join("");
    buttons();
  }

  async function detail(id) {
    const generation = ++detailGeneration;
    try {
      const data = await ui.request(`${base}/${id}`);
      if (generation !== detailGeneration) return;
      render(data);
      ui.go("detail");
    } catch (error) {
      ui.notice(error.message || m.loadFailed, true);
    }
  }

  async function openQr() {
    if (busy || !current?.canOpenQr) return;
    busy = true;
    buttons();
    const id = current.id;
    try {
      const preview = await ui.request(`${base}/${id}/qr-preview`);
      const p = preview.plan;
      const accepted = await ui.confirm(m.confirm(p.operationKey, ui.money(p.amount),
        p.recipientBankCode, p.recipientAccountNumber, preview.recipientName));
      if (!accepted) return;
      const result = await ui.request(`${base}/${id}/open-qr`, {
        method: "POST",
        body: { expectedVersion: preview.expectedVersion, requestKey: crypto.randomUUID() },
      });
      await detail(id);
      ui.notice(result.message);
    } catch (error) {
      ui.notice(error.message || m.loadFailed, true);
      await detail(id);
    } finally {
      busy = false;
      buttons();
    }
  }

  async function reconcile() {
    if (busy || !current?.plan) return;
    busy = true;
    buttons();
    const id = current.id;
    try {
      const result = await ui.request(`${base}/${id}/reconcile`, { method: "POST" });
      await detail(id);
      ui.notice(result.message);
    } catch (error) {
      ui.notice(error.message || m.loadFailed, true);
    } finally {
      busy = false;
      buttons();
    }
  }

  ui.navigate = view => {
    if (busy) return;
    if (view === "detail" && !current) return ui.notice(m.choose, true);
    if (view === "list") {
      ++detailGeneration;
      ui.go("list");
      loadList();
    } else ui.go("detail");
  };
  $("#rows").addEventListener("click", event => {
    const button = event.target.closest("[data-request]");
    if (button && !busy) detail(button.dataset.request);
  });
  $("#apply").onclick = () => { page = 1; loadList(); };
  $("#clear").onclick = () => {
    ["#keyword", "#status", "#from", "#to"].forEach(id => { $(id).value = ""; });
    page = 1;
    loadList();
  };
  $("#prev").onclick = () => { if (page > 1) { --page; loadList(); } };
  $("#next").onclick = () => { if (page * 10 < total) { ++page; loadList(); } };
  $("#openQr").onclick = openQr;
  $("#check").onclick = reconcile;
  $("#qr").onerror = () => ui.notice(m.qrFailed, true);
  $("#copy").onclick = async () => {
    try {
      await navigator.clipboard.writeText(current.plan.operationKey);
      ui.notice(m.copied);
    } catch { ui.notice(m.copyFailed, true); }
  };
  ui.go("list");
  const keyword = new URLSearchParams(location.search).get("keyword");
  if (keyword) $("#keyword").value = keyword;
  await loadList();
  const requested = new URLSearchParams(location.search).get("id");
  if (requested && /^\d+$/.test(requested)) await detail(requested);
});
