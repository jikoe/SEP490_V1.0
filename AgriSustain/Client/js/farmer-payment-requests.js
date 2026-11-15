/* Farmer-owned payment requests. Feedback is shared in messages/finance-payouts.js. */
window.FarmerPaymentRequestsUI = class {
  constructor(ui, refreshRevenue) {
    this.ui = ui;
    this.$ = ui.$;
    this.m = Messages.payout;
    this.base = "/farmer/revenue/payment-requests";
    this.refreshRevenue = refreshRevenue;
    this.page = 1;
    this.total = 0;
    this.selected = new Set();
    this.keys = new Map();
    this.busy = false;
    this.generation = 0;
    this.current = null;
    const $ = this.$;
    $("#requestRows").onclick = (event) => {
      const button = event.target.closest("[data-request]");
      if (button) this.run(() => this.detail(Number(button.dataset.request)));
    };
    $("#requestSourceRows").onchange = (event) => {
      const checkbox = event.target.closest("[data-source]");
      if (!checkbox || this.busy) return;
      if (checkbox.checked && this.selected.size >= 100) {
        checkbox.checked = false;
        ui.notice(this.m.selectionLimit, true);
        return;
      }
      checkbox.checked ? this.selected.add(Number(checkbox.dataset.source))
        : this.selected.delete(Number(checkbox.dataset.source));
      this.run(() => this.previewSelection());
    };
    $("#submitRequest").onclick = () => this.run(() => this.submit());
    $("#discardRequest").onclick = () => this.navigate("requests");
    $("#requestPrev").onclick = () => {
      if (this.page > 1) { this.page--; this.run(() => this.list()); }
    };
    $("#requestNext").onclick = () => {
      if (this.page * 10 < this.total) { this.page++; this.run(() => this.list()); }
    };
  }

  async run(action) {
    try { await action(); }
    catch (error) { this.ui.notice(error.message || this.m.loadFailed, true); }
  }

  async navigate(screen) {
    if (this.busy || this.confirming) return;
    await this.run(async () => {
      if (screen === "request-detail" && !this.current) {
        this.ui.notice(this.m.selectRequest, true);
        return;
      }
      this.ui.go(screen);
      if (screen === "requests") await this.list();
      if (screen === "request-create") await this.context();
    });
  }

  label(value) { return this.m.status[value] || value || "—"; }

  async list() {
    const ticket = ++this.generation;
    const data = await this.ui.request(`${this.base}?page=${this.page}`);
    if (ticket !== this.generation) return;
    this.total = data.total;
    const ui = this.ui, $ = this.$;
    $("#requestRows").innerHTML = data.items.map(row => `<tr>
      <td><strong>${ui.escape(row.code)}</strong><small>${ui.date(row.at)}</small></td>
      <td class="amount">${ui.money(row.netAmount)}</td>
      <td>${ui.escape(this.label(row.payoutStatus))}</td>
      <td><button class="btn" data-request="${row.id}">Xem chi tiết</button></td>
    </tr>`).join("");
    $("#requestCount").textContent = this.m.requestCount(data.total);
    $("#requestPage").textContent = this.page;
    $("#requestPrev").disabled = this.page <= 1;
    $("#requestNext").disabled = this.page * 10 >= data.total;
    if (!data.total) ui.notice(this.m.requestEmpty);
  }

  totals(data) {
    const $ = this.$;
    $("#requestSourceCount").textContent = this.selected.size;
    for (const [selector, key] of [["#requestGross", "gross"], ["#requestFee", "commission"],
      ["#requestDeduction", "deduction"], ["#requestNet", "net"]]) {
      $(selector).textContent = data ? this.ui.money(data[key]) : "—";
    }
    $("#submitRequest").disabled = this.busy || !data?.canCreate;
  }

  async context() {
    this.selected.clear();
    this.preview = null;
    this.totals(null);
    const generation = ++this.generation;
    const data = await this.ui.request(this.base + "/context");
    if (generation !== this.generation) return;
    const ui = this.ui, $ = this.$;
    this.ui.kv("#requestRecipient", [["Nông trại", data.farmerName],
      ["Ngân hàng", data.recipient?.bankName], ["Số tài khoản", data.recipient?.accountNumber],
      ["Chủ tài khoản", data.recipient?.accountHolder]]);
    $("#requestBankState").textContent = data.message || this.m.recipientReady;
    $("#requestSourceRows").innerHTML = data.sources.map(row => `<tr>
      <td><input type="checkbox" data-source="${row.id}" aria-label="Chọn ${ui.escape(row.reference)}" ${row.eligible ? "" : "disabled"}></td>
      <td><strong>${ui.escape(row.reference)}</strong><small>${ui.escape(row.channel)} · ${ui.escape(row.product)}</small></td>
      <td>${ui.escape(row.holdReason || this.m.eligible)}</td>
      <td class="amount">${ui.money(row.gross)}</td><td class="amount">${ui.money(row.commission)}</td>
      <td class="amount">${ui.money(row.deduction)}</td><td class="amount">${ui.money(row.eligible ? row.net : 0)}</td>
    </tr>`).join("");
    this.totals(data);
    if (data.message) ui.notice(data.message, true);
    else if (!data.sources.length) ui.notice(this.m.allSources);
  }

  async previewSelection() {
    const generation = ++this.generation;
    this.preview = null;
    this.totals(null);
    if (!this.selected.size) return;
    const data = await this.ui.request(this.base + "/preview", {
      method: "POST", body: { selectedSourceIds: [...this.selected] },
    });
    if (generation !== this.generation) return;
    this.preview = data;
    this.totals(data);
  }

  async submit() {
    if (this.busy || this.confirming || !this.preview?.canCreate) return;
    const candidate = this.preview;
    const ids = [...this.selected].sort((a, b) => a - b);
    this.confirming = true;
    let confirmed;
    try { confirmed = await this.ui.confirm(this.m.confirmSubmit); }
    finally { this.confirming = false; }
    if (!confirmed) return;
    if (this.preview !== candidate) return;
    const body = { selectedSourceIds: ids, expectedBalance: candidate.expectedBalance };
    const signature = JSON.stringify(body);
    if (!this.keys.has(signature)) this.keys.set(signature, crypto.randomUUID());
    this.busy = true;
    this.totals(candidate);
    this.ui.$$("[data-source]").forEach(input => { input.disabled = true; });
    try {
      const result = await this.ui.request(this.base, {
        method: "POST", body: { ...body, requestKey: this.keys.get(signature) },
      });
      // Keep the same key after an uncertain response, so retry cannot reserve money twice.
      this.keys.delete(signature);
      this.preview = null;
      this.selected.clear();
      this.page = 1;
      await this.detail(result.id);
      await this.refreshRevenue();
      this.ui.notice(result.message);
    } finally {
      this.busy = false;
      this.totals(this.preview);
      this.ui.$$("[data-source]").forEach(input => {
        input.disabled = !this.preview?.sources.some(row => row.id === Number(input.dataset.source) && row.eligible);
      });
    }
  }

  async detail(id) {
    const data = await this.ui.request(`${this.base}/${id}`);
    this.current = data;
    const ui = this.ui, $ = this.$;
    $("#requestDetailCode").textContent = data.code;
    $("#requestDetailState").textContent = this.label(data.status);
    ui.kv("#requestDetails", [["Ngày gửi", ui.date(data.submittedAt)],
      ["Ngân hàng / Tài khoản", `${data.recipient.bankCode} · ${data.recipient.accountNumber}`],
      ["Chủ tài khoản", data.recipient.accountHolder],
      ["Nguồn thanh toán", data.sources.map(row => `${row.reference}: ${ui.money(row.gross)}`).join(" · ")],
      ["Mã giao dịch", data.bankTransferRef], ["Thời điểm xác minh", ui.date(data.processedAt)],
      ["Lý do từ chối", data.rejectionReason]]);
    $("#requestDetailSummary").innerHTML = [["Tổng gộp", data.gross], ["Hoa hồng", data.commission],
      ["Khấu trừ", data.deduction], ["Thực nhận dự kiến", data.net]].map(([title, value]) =>
      `<div class="line"><span>${ui.escape(title)}</span><strong>${ui.money(value)}</strong></div>`).join("");
    $("#requestHistory").innerHTML = data.history.map(row => `<li>${ui.date(row.occurredAt)} · ${ui.escape(this.label(row.action))}${row.reason ? " · " + ui.escape(row.reason) : ""}</li>`).join("");
    ui.go("request-detail");
    if (data.notificationFailed) ui.notice(this.m.notificationFailed);
  }
};
