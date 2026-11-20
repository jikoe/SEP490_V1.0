class FieldFeedback {
  constructor(root) {
    this.root = root;
    this.active = new Map();
    const clearEdited = ({ target }) => {
      this.clear(target);
      if (target.type === "date" && /(?:From|To)$/.test(target.id)) {
        const prefix = target.id.replace(/(?:From|To)$/, "");
        this.clear(this.root.querySelector(`#${prefix}From`));
        this.clear(this.root.querySelector(`#${prefix}To`));
      }
      const sources = target.closest("#requestSourceRows");
      if (sources) this.clear(sources.closest("table"));
    };
    root.addEventListener("input", clearEdited);
    root.addEventListener("change", clearEdited, true);
    root.addEventListener("reset", (event) => this.clearWithin(event.target));
  }

  clear(control) {
    const entry = this.active.get(control);
    if (!entry) return;
    entry.notice.remove();
    for (const [attr, previous] of Object.entries(entry.attributes)) {
      if (previous === null) control.removeAttribute(attr);
      else control.setAttribute(attr, previous);
    }
    this.active.delete(control);
  }

  clearWithin(container = this.root) {
    for (const control of this.active.keys()) {
      if (!control.isConnected || container.contains(control)) this.clear(control);
    }
  }

  show(entries) {
    const errors = entries.filter(([control, message]) => control && message);
    for (const [control, message, anchor = control] of errors) {
      this.clear(control);
      const notice = control.ownerDocument.createElement("div");
      notice.className = "field-error";
      notice.id = `${control.id || control.querySelector("tbody")?.id}-field-error`;
      notice.setAttribute("role", "alert");
      notice.textContent = message;
      const attributes = {
        "aria-invalid": control.getAttribute("aria-invalid"),
        "aria-describedby": control.getAttribute("aria-describedby"),
      };
      control.setAttribute("aria-invalid", "true");
      control.setAttribute(
        "aria-describedby",
        [attributes["aria-describedby"], notice.id].filter(Boolean).join(" "),
      );
      anchor.before(notice);
      this.active.set(control, { notice, attributes });
    }
    if (!errors.length) return false;
    const [first] = errors
      .map(([control]) => control)
      .sort((a, b) => (a.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_PRECEDING ? 1 : -1));
    const target = first.matches("table") ? first.querySelector("input:not(:disabled)") : first;
    if (target && !target.disabled) target.focus();
    else first.scrollIntoView({ block: "nearest" });
    return true;
  }

  validateForm(form, messageFor) {
    this.clearWithin(form);
    const entries = Array.from(form.elements)
      .filter((control) => control.willValidate && control.id)
      .map((control) => [control, messageFor(control)]);
    return !this.show(entries);
  }

  validateDates(from, to, { required = false, rangeMessage, grouped = false }) {
    this.clear(from);
    this.clear(to);
    const entries = [];
    if (from.validity.badInput || (required && !from.value))
      entries.push([from, "Vui lòng nhập Từ ngày hợp lệ.", grouped ? from.parentElement : from]);
    if (to.validity.badInput || (required && !to.value))
      entries.push([to, "Vui lòng nhập Đến ngày hợp lệ.", grouped ? to.parentElement : to]);
    if (from.value && to.value && from.value > to.value)
      entries.push([to, rangeMessage, grouped ? from.parentElement : to]);
    return !this.show(entries);
  }

  applyServerErrors(error, selectors) {
    const entries = Object.entries(error.fields || {}).map(([key, messages]) => {
      const normalized = key.toLowerCase();
      const name = Object.keys(selectors).find(
        (candidate) => normalized === candidate || normalized.endsWith(`.${candidate}`),
      );
      return [
        name ? this.root.querySelector(selectors[name]) : null,
        messages?.[0] || error.message,
      ];
    });
    return this.show(
      entries.map(([control, message]) => [
        control?.closest("table") && control.matches("tbody") ? control.closest("table") : control,
        message,
      ]),
    );
  }
}

/* Shared UI plumbing. Domain feedback is kept in messages/farmer-business.js. */
window.FarmerBusinessUI = class {
  constructor() {
    this.root = document.getElementById("business-root").attachShadow({ mode: "open" });
    this.root.append(document.getElementById("business-template").content.cloneNode(true));
    this.$ = (selector) => this.root.querySelector(selector);
    this.$$ = (selector) => [...this.root.querySelectorAll(selector)];
    this.m = Messages.business;
    this.fields = new FieldFeedback(this.root);
    this.uploads = new WeakMap();
    this.root.addEventListener("click", (event) => {
      const button = event.target.closest("[data-screen-go]");
      if (button) {
        event.preventDefault();
        this.navigate?.(button.dataset.screenGo);
      }
    });
  }
  escape(value) {
    return String(value ?? "").replace(
      /[&<>"']/g,
      (char) =>
        ({
          "&": "&amp;",
          "<": "&lt;",
          ">": "&gt;",
          '"': "&quot;",
          "'": "&#39;",
        })[char],
    );
  }
  money(value) {
    return value == null
      ? "—"
      : Number(value).toLocaleString("vi-VN", { maximumFractionDigits: 2 }) + " ₫";
  }
  number(value) {
    return value == null
      ? "—"
      : Number(value).toLocaleString("vi-VN", { maximumFractionDigits: 3 });
  }
  date(value) {
    if (!value) return "—";
    const normalized = /^\d{4}-\d{2}-\d{2}$/.test(value) ? value + "T00:00:00+07:00" : value;
    const date = new Date(normalized);
    return /^\d{4}-\d{2}-\d{2}$/.test(value)
      ? date.toLocaleDateString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" })
      : date.toLocaleString("vi-VN", { timeZone: "Asia/Ho_Chi_Minh" });
  }
  label(value) {
    return this.m.status[value] || value || "—";
  }
  notice(text, error = false) {
    this.$("#feedback").hidden = !text;
    this.$("#feedbackText").textContent = text || "";
    this.$("#feedback").classList.toggle("bad", error === true);
    this.$("#feedback").classList.toggle("warn", error === "warning");
    this.$("#feedback").setAttribute("role", error === true ? "alert" : "status");
    this.$("#feedback").setAttribute("aria-live", error === true ? "assertive" : "polite");
  }
  go(name) {
    this.$$("[data-screen]").forEach((node) => {
      node.hidden = node.dataset.screen !== name;
    });
    this.$$("[data-screen-indicator]").forEach((node) => {
      const active = node.dataset.screenIndicator === name;
      if (active) node.setAttribute("aria-current", "step");
      else node.removeAttribute("aria-current");
    });
    this.$$("[data-screen-go][role=tab]").forEach((node) => {
      node.setAttribute("aria-selected", String(node.dataset.screenGo === name));
      node.classList.toggle("active", node.dataset.screenGo === name);
    });
  }
  async request(path, options = {}) {
    const result = await API.request(path, {
      suppressErrorToast: true,
      ...options,
    });
    if (!result.success) {
      const error = new Error(result.status ? result.error : this.m.loadFailed);
      error.fields = result.data?.errors;
      error.status = result.status;
      throw error;
    }
    return result.data;
  }
  options(selector, rows, label, prompt = "Chọn") {
    const node = this.$(selector);
    node.replaceChildren(new Option(prompt, ""));
    rows.forEach((row) => node.add(new Option(label(row), row.id)));
  }
  kv(selector, pairs) {
    this.$(selector).innerHTML = pairs
      .map(([key, value]) => `<dt>${this.escape(key)}</dt><dd>${this.escape(value ?? "—")}</dd>`)
      .join("");
  }
  confirm(text = this.m.confirm) {
    const dialog = this.$("#confirmDialog");
    this.$("#confirmText").textContent = text;
    return new Promise((resolve) => {
      const done = (value) => {
        dialog.close();
        resolve(value);
      };
      this.$("#confirmOk").onclick = () => done(true);
      this.$("#confirmCancel").onclick = () => done(false);
      dialog.oncancel = (event) => {
        event.preventDefault();
        done(false);
      };
      dialog.showModal();
    });
  }
  async upload(selector) {
    const node = this.$(selector);
    const list = [...node.files];
    if (
      list.length < 1 ||
      list.length > 5 ||
      list.some(
        (file) =>
          !/\.(pdf|jpe?g|png)$/i.test(file.name) || file.size <= 0 || file.size > 5 * 1024 * 1024,
      )
    )
      throw new Error(this.m.fileInvalid);
    const signature = list
      .map((file) => [file.name, file.size, file.lastModified].join(":"))
      .join("|");
    const previous = this.uploads.get(node);
    if (previous?.signature === signature) return previous.urls;
    const urls = [];
    for (const file of list) {
      const form = new FormData();
      form.append("file", file);
      const result = await this.request("/farmer/offtake-contracts/evidence", {
        method: "POST",
        body: form,
      });
      urls.push(result.url);
    }
    this.uploads.set(node, { signature, urls });
    return urls;
  }
};
