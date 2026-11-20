/* Shared plumbing for the three farmer screens. Business messages live in messages/farm-management.js. */
window.FarmUI = class FarmUI {
  constructor() {
    this.host = document.getElementById("farm-root");
    this.feature = this.host.dataset.feature;
    this.root = this.host.attachShadow({ mode: "open" });
    this.root.append(document.getElementById("farm-template").content.cloneNode(true));
    this.m = Messages.farm;
    this.$ = (s) => this.root.querySelector(s);
    this.$$ = (s) => [...this.root.querySelectorAll(s)];
    this.page = 1;
    this.pageSize = 10;
    this.total = 0;
    this.busy = false;
    this.pending = new Map();
    this.uploads = new WeakMap();
    this.blobs = [];
    this.loadingButtons = this.$$("button").filter((x) => !x.disabled);
    this.loadingButtons.forEach((x) => (x.disabled = true));
    this.handlers = {};
    this.root.addEventListener("click", (e) => {
      const close = e.target.closest("[data-close-dialog]");
      if (close) {
        close.closest("dialog").close();
        return;
      }
      const go = e.target.closest("[data-go]");
      if (go) {
        e.preventDefault();
        this.handlers.navigate?.(go.dataset.go);
      }
    });
    for (const eventName of ["input", "change"]) {
      this.root.addEventListener(eventName, (event) => this.clearError(event.target));
    }
    this.root.addEventListener("reset", (event) => this.clearErrors(event.target));
    this.$("#filters").onsubmit = (e) => {
      e.preventDefault();
      const form = e.currentTarget;
      const from = this.value(form, "from");
      const to = this.value(form, "to");
      if (from && to && from > to) {
        this.fieldProblem(form, "to", this.m.DateRange);
        return;
      }
      this.page = 1;
      this.handlers.load?.();
    };
    this.$("#clearFilters").onclick = () => {
      this.$("#filters").reset();
      this.page = 1;
      this.handlers.load?.();
    };
    this.$("#previousPage").onclick = () => {
      if (this.page > 1) {
        this.page--;
        this.handlers.load?.();
      }
    };
    this.$("#nextPage").onclick = () => {
      if (this.page * this.pageSize < this.total) {
        this.page++;
        this.handlers.load?.();
      }
    };
    this.root.addEventListener("change", (event) => {
      if (event.target.matches("input[type=file]")) this.previewFiles(event.target);
    });
    window.addEventListener("beforeunload", () =>
      this.blobs.forEach((u) => URL.revokeObjectURL(u)),
    );
  }
  escape(value) {
    return String(value ?? "").replace(
      /[&<>"']/g,
      (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c],
    );
  }
  number(value) {
    return Number(value ?? 0).toLocaleString("vi-VN", { maximumFractionDigits: 3 });
  }
  date(value) {
    return value
      ? new Date(String(value).length === 10 ? value + "T00:00:00" : value).toLocaleDateString(
          "vi-VN",
        )
      : "—";
  }
  datetime(value) {
    return value ? new Date(value).toLocaleString("vi-VN") : "—";
  }
  value(form, name) {
    return String(form.elements.namedItem(name)?.value ?? "").trim();
  }
  fill(form, values) {
    for (const [name, value] of Object.entries(values)) {
      const e = form.elements.namedItem(name);
      if (e && e.type !== "file") e.value = value ?? "";
    }
  }
  options(select, values, valueKey, label, empty = "Chọn thông tin") {
    const old = select.value;
    select.replaceChildren();
    const first = new Option(empty, "");
    select.add(first);
    for (const row of values) select.add(new Option(label(row), String(row[valueKey])));
    select.value = old;
  }
  go(view) {
    this.$$(".view").forEach((x) => x.classList.toggle("active", x.dataset.view === view));
    this.$$("[data-view-indicator]").forEach((item) => {
      if (item.dataset.viewIndicator === view) item.setAttribute("aria-current", "step");
      else item.removeAttribute("aria-current");
    });
    this.$(".create-actions").style.display = view === "list" ? "" : "none";
    this.hideNotice();
    if (view !== "list") this.$('.view.active [data-go]')?.focus();
  }
  hideNotice() {
    const box = this.$("#feedback");
    box.hidden = true;
    box.style.display = "";
  }
  notice(message, error = false) {
    const box = this.$("#feedback");
    box.hidden = false;
    box.style.display = "block";
    box.className = error ? "notice error" : "notice";
    box.setAttribute("role", error ? "alert" : "status");
    box.textContent = displayMessage(message);
    box.scrollIntoView({ behavior: "smooth", block: "nearest" });
  }
  clearError(control) {
    const error = control.previousElementSibling;
    if (!error?.classList.contains("field-error")) return;
    error.remove();
    control.removeAttribute("aria-invalid");
    control.removeAttribute("aria-describedby");
    control.classList.remove("invalid");
  }
  clearErrors(form) {
    form?.querySelectorAll("[aria-invalid]").forEach((control) => this.clearError(control));
  }
  fieldProblem(form, field, message) {
    this.hideNotice();
    this.mark(form, field, message);
    form.querySelector("[aria-invalid]")?.focus();
  }
  mark(form, key, message) {
    if (!form) return;
    const map = {
      seasonName: ["name"],
      productId: ["product"],
      cropVariety: ["variety"],
      cultivatedArea: ["area"],
      plantingDate: ["plant"],
      estimatedHarvestDate: ["estimate", "estimated"],
      estimatedYield: form.id === "seasonForm" ? ["yield"] : ["estimatedYield"],
      yieldUnit: ["unit"],
      unit: ["unit"],
      completionDate: ["completion"],
      actualYield: ["actual", "realized", "yield"],
      reason: ["reason", "issueReason", "changeReason"],
      cropSeasonId: ["season", "logseason", "correctseason"],
      actionDate: ["logexecution", "correctexecution"],
      actionType: ["logactivity", "correctactivity"],
      detailDescription: ["logdescription", "correctdescription"],
      performedBy: ["logperformer", "correctperformer"],
      inputMaterial: ["logmaterial", "correctmaterial"],
      dosage: ["logdosage", "correctdosage"],
      correctionReason: ["correctionReason"],
      batchCode: ["code"],
      expirationDate: ["expiry"],
      actualHarvestDate: ["harvest"],
      quantity: ["affected", "adjustment"],
      qualityNotes: ["notes"],
      outcome: ["outcome"],
      issueType: ["issueType"],
    };
    const clean = key.split(".").pop();
    const lookup = clean ? clean.charAt(0).toLowerCase() + clean.slice(1) : "";
    let el =
      lookup === "evidenceUrls"
        ? form.querySelector("input[type=file]")
        : (map[lookup] || [lookup])
            .map((x) => form.elements.namedItem(x))
            .find((x) => x && !x.disabled);
    if (!el) return false;
    this.clearError(el);
    el.classList.add("invalid");
    el.setAttribute("aria-invalid", "true");
    const label = document.createElement("small");
    label.className = "field-error";
    label.id = el.id + "-error";
    label.setAttribute("role", "alert");
    label.textContent = displayMessage(message);
    el.before(label);
    el.setAttribute("aria-describedby", label.id);
    return true;
  }
  validate(form) {
    this.clearErrors(form);
    let valid = true;
    for (const el of form.elements) {
      if (el.disabled || !["INPUT", "SELECT", "TEXTAREA"].includes(el.tagName)) continue;
      const value = String(el.value ?? "").trim();
      let bad = el.required && (el.type === "file" ? !el.files.length : !value);
      if (el.type === "number" && value) {
        const n = Number(value);
        bad ||=
          !Number.isFinite(n) ||
          (el.min !== "" && n < Number(el.min)) ||
          (el.max !== "" && n > Number(el.max));
      }
      if (el.maxLength > 0 && value.length > el.maxLength) bad = true;
      const invalidImage = el.type === "file" && [...el.files].some((file) => !this.validImage(file));
      if (bad || invalidImage) {
        this.mark(form, el.name, invalidImage ? this.m.MSG96 : this.m.required);
        valid = false;
      }
    }
    if (!valid) {
      this.hideNotice();
      form.querySelector("[aria-invalid]")?.focus();
    }
    return valid;
  }
  filter() {
    const f = this.$("#filters");
    return {
      keyword: this.value(f, "keyword"),
      status: this.value(f, "filterStatus"),
      seasonId: this.value(f, "filterSeason"),
      productId: this.value(f, "filterProduct"),
      from: this.value(f, "from"),
      to: this.value(f, "to"),
      page: this.page,
      pageSize: this.pageSize,
    };
  }
  pager(result) {
    this.total = result.total;
    const pages = Math.max(1, Math.ceil(this.total / this.pageSize));
    this.$("#pageNumber").textContent = this.page;
    this.$("#pageSummary").textContent = this.m.page(this.total, this.page, pages);
    this.$("#previousPage").disabled = this.page <= 1;
    this.$("#nextPage").disabled = this.page >= pages;
  }
  async request(path = "", options = {}) {
    const endpoint = path.startsWith("/farmer/") ? path : `/farmer/${this.feature}${path}`;
    const result = await API.request(endpoint, { ...options, suppressErrorToast: true });
    if (!result.success) {
      const error = new Error(
        !result.status || result.status >= 500
          ? options.method && options.method !== "GET"
            ? this.m.MSG123
            : this.m.MSG126
          : result.error || this.m.MSG126,
      );
      error.fields = result.data?.errors || {};
      error.status = result.status;
      throw error;
    }
    return result.data;
  }
  failure(error, form) {
    this.clearErrors(form);
    const unmapped = [];
    for (const [key, values] of Object.entries(error.fields || {})) {
      const message = Array.isArray(values) ? values.join(" ") : String(values);
      if (!this.mark(form, key, message)) unmapped.push(message);
    }
    const invalid = form?.querySelector("[aria-invalid]");
    if (!invalid || unmapped.length) {
      this.notice(unmapped.join(" ") || error.message || this.m.MSG126, true);
    } else this.hideNotice();
    invalid?.focus();
  }
  async command(path, body, form, actionKey = path) {
    if (this.busy) return null;
    const signature = path + JSON.stringify(body);
    let pending = this.pending.get(actionKey);
    if (!pending || pending.signature !== signature) {
      pending = { signature, key: crypto.randomUUID() };
      this.pending.set(actionKey, pending);
    }
    this.busy = true;
    const buttons = this.$$("button").filter((x) => !x.disabled);
    buttons.forEach((x) => (x.disabled = true));
    try {
      this.clearErrors(form);
      const data = await this.request(path, {
        method: body._method || "POST",
        body: { ...body, _method: undefined, requestKey: pending.key },
      });
      this.pending.delete(actionKey);
      return data;
    } catch (error) {
      this.failure(error, form);
      return null;
    } finally {
      this.busy = false;
      buttons.forEach((x) => (x.disabled = false));
      this.handlers.sync?.();
      this.$("#previousPage").disabled = this.page <= 1;
      this.$("#nextPage").disabled = this.page * this.pageSize >= this.total;
    }
  }
  confirm(message) {
    return new Promise((resolve) => {
      const dialog = this.$("#confirm");
      this.$("#confirmText").textContent = message;
      const ok = this.$("#confirmOk");
      let accepted = false;
      ok.onclick = () => {
        accepted = true;
        dialog.close();
      };
      dialog.addEventListener("close", () => resolve(accepted), { once: true });
      dialog.showModal();
    });
  }
  table(rows, colspan) {
    this.$("#rows").innerHTML =
      rows ||
      `<tr><td colspan="${colspan}" class="empty-state">${this.escape(this.m.MSG124)}</td></tr>`;
  }
  commitments(rows) {
    return rows.length
      ? rows
          .map(
            (x) =>
              `${this.escape(x.source)}: ${this.number(x.quantity)} ${this.escape(x.unit)}${x.deliveryDate ? " · " + this.date(x.deliveryDate) : ""}`,
          )
          .join("<br>")
      : this.escape(this.m.noCommitments);
  }
  history(rows) {
    return rows.length
      ? rows
          .map(
            (x) =>
              /* HTML */ `<div class="history-entry">
                <strong>${this.escape(this.m.actions[x.action] || x.action)}</strong> ·
                ${this.datetime(x.occurredAt)} ·
                ${this.escape(x.recorder)}${x.reason
                  ? "<br>" + this.escape(x.reason)
                  : ""}${this.historyProof(x.after)}
              </div>`,
          )
          .join("")
      : this.escape(this.m.noHistory);
  }
  historyProof(after) {
    try {
      const data = JSON.parse(after || "{}");
      return this.proofs(data.EvidenceUrls || data.evidenceUrls || []);
    } catch {
      return "";
    }
  }
  proofs(urls) {
    return urls.length
      ? '<div class="proof-list">' +
          urls
            .map(
              (url, i) =>
                `<button class="btn" type="button" data-evidence="${this.escape(url)}">${this.escape(this.m.savedProof(i + 1))}</button>`,
            )
            .join("") +
          "</div>"
      : "";
  }
  async showEvidence(reference) {
    try {
      if (
        !/^\/api\/farmer\/evidence\/[a-f0-9]{32}\.(png|jpg)$/.test(reference) &&
        !/^\/media\/\d+\/[a-f0-9]{32}\.(png|jpg|jpeg)$/.test(reference)
      )
        throw new Error(this.m.MSG126);
      const base = CONFIG.API_BASE_URL.replace(/\/api\/?$/, "");
      const headers = reference.startsWith("/api/farmer/")
        ? { Authorization: `Bearer ${Auth.getAccessToken()}` }
        : {};
      const response = await fetch(base + reference, { headers });
      if (!response.ok) throw new Error(this.m.MSG126);
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      this.blobs.push(url);
      const image = document.createElement("img");
      image.alt = this.m.savedProof(1);
      image.className = "proof-image";
      image.src = url;
      this.$("#proofContent").replaceChildren(image);
      if (!this.$("#proofViewer").open) this.$("#proofViewer").showModal();
    } catch (error) {
      this.failure(error);
    }
  }
  validImage(file) {
    return /\.(jpe?g|png)$/i.test(file.name) && file.size > 0 && file.size <= 5 * 1024 * 1024;
  }
  previewFiles(input) {
    const gallery = this.$(`[data-preview="${input.name}"]`);
    if (!gallery) return;
    gallery.replaceChildren();
    if ([...input.files].some((file) => !this.validImage(file))) {
      this.fieldProblem(input.form, input.name, this.m.MSG96);
      return;
    }
    for (const file of input.files) {
      const url = URL.createObjectURL(file);
      this.blobs.push(url);
      const image = document.createElement("img");
      image.src = url;
      image.alt = file.name;
      gallery.append(image);
    }
  }
  async upload(input, feature) {
    if (!input.files.length) return [];
    const signature = [...input.files]
      .map((x) => [x.name, x.size, x.lastModified].join(":"))
      .join("|");
    const old = this.uploads.get(input);
    if (old?.signature === signature) return old.urls;
    for (const file of input.files)
      if (!this.validImage(file)) {
        const error = new Error(this.m.MSG96);
        error.fields = { [input.name]: [this.m.MSG96] };
        throw error;
      }
    const form = new FormData();
    for (const file of input.files) form.append("files", file);
    const data = await this.request("/farmer/evidence?feature=" + feature, {
      method: "POST",
      body: form,
    });
    this.uploads.set(input, { signature, urls: data.evidenceUrls });
    return data.evidenceUrls;
  }
  bindEvidence() {
    this.root.addEventListener("click", (e) => {
      const b = e.target.closest("[data-evidence]");
      if (b) this.showEvidence(b.dataset.evidence);
    });
  }
  async start(load) {
    if (!Auth.requireAuth("Farmer")) return;
    this.bindEvidence();
    this.go("list");
    try {
      this.optionsData = await this.request("/farmer/" + this.feature + "/options");
      this.loadingButtons.forEach((x) => (x.disabled = false));
      await load();
    } catch (error) {
      this.failure(error);
      if (!this.optionsData) {
        const retry = document.createElement("button");
        retry.type = "button";
        retry.className = "btn";
        retry.textContent = this.m.retry;
        retry.onclick = () => this.start(load);
        this.$("#feedback").append(" ", retry);
      }
    }
  }
};
