/* Shared UI plumbing. Domain feedback is kept in messages/farmer-business.js. */
window.FarmerBusinessUI = class {
  constructor() {
    this.root = document
      .getElementById("business-root")
      .attachShadow({ mode: "open" });
    this.root.append(
      document.getElementById("business-template").content.cloneNode(true),
    );
    this.$ = (selector) => this.root.querySelector(selector);
    this.$$ = (selector) => [...this.root.querySelectorAll(selector)];
    this.m = Messages.business;
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
      : Number(value).toLocaleString("vi-VN", { maximumFractionDigits: 2 }) +
          " ₫";
  }
  number(value) {
    return value == null
      ? "—"
      : Number(value).toLocaleString("vi-VN", { maximumFractionDigits: 3 });
  }
  date(value) {
    if (!value) return "—";
    const normalized = /^\d{4}-\d{2}-\d{2}$/.test(value)
      ? value + "T00:00:00+07:00"
      : value;
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
    this.$("#feedback").classList.toggle("error", error);
  }
  go(name) {
    this.$$("[data-screen]").forEach((node) => {
      node.hidden = node.dataset.screen !== name;
    });
    this.$$("[data-screen-go][role=tab]").forEach((node) => {
      node.setAttribute(
        "aria-selected",
        String(node.dataset.screenGo === name),
      );
      node.classList.toggle("active", node.dataset.screenGo === name);
    });
  }
  async request(path, options = {}) {
    const result = await API.request(path, {
      suppressErrorToast: true,
      ...options,
    });
    if (!result.success)
      throw new Error(result.status ? result.error : this.m.loadFailed);
    return result.data;
  }
  options(selector, rows, label, prompt = "Chọn") {
    const node = this.$(selector);
    node.replaceChildren(new Option(prompt, ""));
    rows.forEach((row) => node.add(new Option(label(row), row.id)));
  }
  kv(selector, pairs) {
    this.$(selector).innerHTML = pairs
      .map(
        ([key, value]) =>
          `<dt>${this.escape(key)}</dt><dd>${this.escape(value ?? "—")}</dd>`,
      )
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
          !/\.(pdf|jpe?g|png)$/i.test(file.name) ||
          file.size <= 0 ||
          file.size > 5 * 1024 * 1024,
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
