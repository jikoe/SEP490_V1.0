"use strict";

document.addEventListener("DOMContentLoaded", () => {
  if (!Auth.requireAuth("Farmer")) return;
  const root = document.getElementById("complaint-root").attachShadow({ mode: "open" });
  root.append(document.getElementById("complaint-template").content.cloneNode(true));
  const $ = (selector) => root.querySelector(selector);
  const messages = Messages.farmerComplaint;
  const base = "/farmer/complaints";
  const uploaded = new WeakMap();
  let current = null;
  let selectedRequest = null;
  let attachments = [];
  let saving = false;
  let page = 1;
  let total = 0;
  let requestKey = null;
  let previewUrl = null;

  const text = (value) => String(value ?? "");
  const escape = (value) =>
    text(value).replace(
      /[&<>"']/g,
      (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c],
    );
  const formatDate = (value) => {
    if (!value) return "—";
    const parts = Object.fromEntries(
      new Intl.DateTimeFormat("vi-VN", {
        day: "2-digit",
        month: "2-digit",
        year: "numeric",
        hour: "2-digit",
        minute: "2-digit",
        hourCycle: "h23",
        timeZone: "Asia/Ho_Chi_Minh",
      })
        .formatToParts(new Date(value))
        .map((part) => [part.type, part.value]),
    );
    return `${parts.day}/${parts.month}/${parts.year} ${parts.hour}:${parts.minute}`;
  };
  const statusText = (value) =>
    value === "Resolved" ? messages.legacyResolved : messages.statuses[value] || value;
  const requestText = (value) => messages.requests[value] || value;
  const dirty = () => Boolean($("#explanation").value.trim() || attachments.length);

  function notice(message, bad = false) {
    $("#feedbackText").textContent = displayMessage(message);
    $("#feedback").classList.toggle("bad", bad);
    $("#feedback").hidden = false;
    $("#feedback").scrollIntoView({ behavior: "smooth", block: "nearest" });
  }

  async function request(path, options = {}) {
    const response = await API.request(base + path, { ...options, suppressErrorToast: true });
    if (!response.success) {
      const error = new Error(response.status ? displayMessage(response.error) : messages.loadFailed);
      error.status = response.status;
      throw error;
    }
    return response.data;
  }

  function options(select, values) {
    const all = select.options[0].textContent;
    select.replaceChildren(new Option(all, ""));
    Object.entries(values).forEach(([key, label]) => select.add(new Option(label, key)));
  }

  function details(element, pairs) {
    element.replaceChildren();
    pairs.forEach(([label, value]) => {
      const dt = document.createElement("dt");
      const dd = document.createElement("dd");
      dt.textContent = label;
      dd.textContent = value ?? "—";
      element.append(dt, dd);
    });
  }

  async function load() {
    const params = {
      keyword: $("#keyword").value.trim(),
      status: $("#caseStatus").value,
      requestStatus: $("#requestStatus").value,
      page,
    };
    try {
      const data = await request("?" + API.toQueryString(params));
      if (data.total && page > Math.ceil(data.total / 10)) {
        page = Math.max(1, Math.ceil(data.total / 10));
        return load();
      }
      total = data.total;
      $("#rows").innerHTML = data.items.length
        ? data.items
            .map(
              (item) => `
        <tr>
          <td><strong>#${item.id}</strong><small>${escape(item.orderCode)}</small></td>
          <td>${escape(item.reason)}</td>
          <td><span class="badge">${escape(statusText(item.status))}</span><small>${escape(requestText(item.requestStatus))}</small></td>
          <td>${formatDate(item.dueAt)}</td>
          <td><button type="button" data-open="${item.id}">${messages.open}</button></td>
        </tr>`,
            )
            .join("")
        : `<tr><td colspan="5">${messages.empty}</td></tr>`;
      $("#count").textContent = messages.page(total, page, Math.max(1, Math.ceil(total / 10)));
      $("#pageNumber").textContent = String(page);
      $("#previousPage").disabled = page <= 1;
      $("#nextPage").disabled = page * 10 >= total;
    } catch (error) {
      notice(error.message || messages.loadFailed, true);
    }
  }

  function closePreview() {
    if (previewUrl) URL.revokeObjectURL(previewUrl);
    previewUrl = null;
    $("#previewContent").replaceChildren();
  }

  function showPreview(blob, name) {
    closePreview();
    previewUrl = URL.createObjectURL(blob);
    const pdf = blob.type === "application/pdf" || /\.pdf$/i.test(name);
    const node = document.createElement(pdf ? "iframe" : "img");
    node.src = previewUrl;
    node.title = name;
    if (!pdf) node.alt = name;
    $("#previewContent").append(node);
    $("#preview").showModal();
  }

  async function previewSaved(file) {
    try {
      if (!/^\/api\/farmer\/complaints\/\d+\/evidence\/\d+$/.test(file.url))
        throw new Error(messages.loadFailed);
      const apiOrigin = CONFIG.API_BASE_URL.replace(/\/api\/?$/, "");
      let response = await fetch(apiOrigin + file.url, {
        headers: { Authorization: `Bearer ${Auth.getAccessToken()}` },
      });
      if (!response.ok) throw new Error(messages.loadFailed);
      showPreview(await response.blob(), file.name);
    } catch (error) {
      notice(messages.loadFailed, true);
    }
  }

  function savedFiles(target, files) {
    target.replaceChildren();
    if (!files.length) {
      target.textContent = messages.noEvidence;
      return;
    }
    files.forEach((file) => {
      const button = document.createElement("button");
      button.type = "button";
      button.textContent = messages.fileLabel(file.id) + (file.name.endsWith(".pdf") ? " · PDF" : "");
      button.onclick = () => previewSaved(file);
      target.append(button);
    });
  }

  function renderFiles() {
    $("#files").replaceChildren();
    attachments.forEach((file, index) => {
      const li = document.createElement("li");
      li.append(file.name + " · " + (file.size / 1024).toFixed(0) + " KB");
      const view = document.createElement("button");
      view.type = "button";
      view.textContent = messages.view;
      view.onclick = () => showPreview(file, file.name);
      const remove = document.createElement("button");
      remove.type = "button";
      remove.textContent = messages.remove;
      remove.disabled = saving;
      remove.onclick = () => {
        attachments.splice(index, 1);
        renderFiles();
      };
      li.append(view, remove);
      $("#files").append(li);
    });
  }

  function canRespond() {
    return selectedRequest?.canRespond && new Date(selectedRequest.dueAt).getTime() >= Date.now();
  }

  function lock() {
    ["explanation", "evidence", "submit"].forEach((id) => {
      $("#" + id).disabled = saving || !canRespond();
    });
    $("#requestSelect").disabled = saving;
    $("#back").disabled = saving;
    $("#cancel").disabled = saving;
    renderFiles();
  }

  function renderRequest() {
    const labels = messages.labels;
    details(
      $("#requestDetails"),
      selectedRequest
        ? [
            [labels.request, "#" + selectedRequest.id],
            [labels.instructions, selectedRequest.instructions],
            [labels.requestedAt, formatDate(selectedRequest.requestedAt)],
            [labels.due, formatDate(selectedRequest.dueAt)],
            [labels.respondedAt, formatDate(selectedRequest.respondedAt)],
            [labels.status, requestText(selectedRequest.status)],
          ]
        : [[labels.request, messages.noRequest]],
    );
    lock();
  }

  function resetForm() {
    $("#responseForm").reset();
    attachments = [];
    requestKey = null;
    $("#chars").textContent = "0 / 2.000 ký tự";
    $("#explanationError").textContent = "";
    $("#evidenceError").textContent = "";
    $("#explanation").removeAttribute("aria-invalid");
    $("#evidence").removeAttribute("aria-invalid");
  }

  function renderDetail(data) {
    current = data;
    resetForm();
    $("#listView").hidden = true;
    $("#detailView").hidden = false;
    $("#back").hidden = false;
    const labels = messages.labels;
    details($("#caseDetails"), [
      [labels.complaint, "#" + data.id],
      [labels.order, data.orderCode],
      [labels.buyer, data.buyerName + " · RetailCustomer"],
      [labels.reason, data.reason],
      [labels.description, data.description],
      [labels.status, statusText(data.status)],
      [labels.delivered, formatDate(data.deliveredAt)],
      [labels.complaintDeadline, formatDate(data.complaintDeadline)],
    ]);
    savedFiles($("#buyerEvidence"), data.buyerEvidence);
    selectedRequest = data.requests.find((item) => item.canRespond) || data.requests[0] || null;
    $("#requestSelect").replaceChildren();
    data.requests.forEach((item) =>
      $("#requestSelect").add(
        new Option(messages.requestLabel(item.id) + " · " + requestText(item.status), String(item.id)),
      ),
    );
    $("#requestSelect").value = String(selectedRequest?.id ?? "");
    renderRequest();
    $("#history").replaceChildren();
    if (!data.history.length) $("#history").textContent = messages.noHistory;
    data.history.forEach((item) => {
      const node = document.createElement("article");
      node.className = "event";
      const title = document.createElement("strong");
      title.textContent =
        messages.submittedAt + formatDate(item.submittedAt) + " · " + messages.requestLabel(item.requestId);
      const content = document.createElement("p");
      content.textContent = item.explanation;
      const files = document.createElement("div");
      files.className = "history-files";
      savedFiles(files, item.files);
      node.append(title, content, files);
      $("#history").append(node);
    });
    if (data.resolution || data.status === "Rejected") {
      const outcome = document.createElement("dl");
      const money =
        data.refundAmount == null
          ? "—"
          : new Intl.NumberFormat("vi-VN", { style: "currency", currency: "VND" }).format(data.refundAmount);
      details(outcome, [
        [labels.status, statusText(data.status)],
        [labels.resolution, data.resolution],
        [labels.amount, money],
        [labels.finance, messages.refund[data.refundStatus] || data.refundStatus || "—"],
      ]);
      $("#outcome").replaceChildren(outcome);
      if (
        data.status === "RefundPending" &&
        !["Succeeded", "Success", "Completed"].includes(data.refundStatus)
      ) {
        const pending = document.createElement("p");
        pending.textContent = messages.pendingFunds;
        $("#outcome").append(pending);
      }
    } else $("#outcome").textContent = messages.noOutcome;
    window.scrollTo(0, 0);
  }

  async function openCase(id) {
    if (saving) return;
    try {
      const data = await request("/" + id);
      $("#feedback").hidden = true;
      renderDetail(data);
      const url = new URL(location.href);
      url.searchParams.set("id", id);
      history.replaceState(null, "", url);
      if (data.notificationPending) notice(messages.pendingNotification, true);
      else if (selectedRequest?.status === "Expired") notice(messages.expired, true);
      else if (!canRespond()) notice(selectedRequest ? messages.closed : messages.noRequest);
    } catch (error) {
      notice(error.message, true);
    }
  }

  async function back() {
    if (saving || (dirty() && !confirm(messages.discard))) return;
    resetForm();
    current = null;
    selectedRequest = null;
    $("#listView").hidden = false;
    $("#detailView").hidden = true;
    $("#back").hidden = true;
    $("#feedback").hidden = true;
    const url = new URL(location.href);
    url.searchParams.delete("id");
    history.replaceState(null, "", url);
    await load();
  }

  options($("#caseStatus"), messages.statuses);
  options($("#requestStatus"), messages.requests);
  const requestLabel = document.createElement("label");
  requestLabel.textContent = messages.selectRequest;
  const requestSelect = document.createElement("select");
  requestSelect.id = "requestSelect";
  requestLabel.append(requestSelect);
  $("#requestDetails").before(requestLabel);
  requestSelect.onchange = () => {
    if (dirty() && !confirm(messages.discard)) {
      requestSelect.value = String(selectedRequest.id);
      return;
    }
    selectedRequest = current.requests.find((item) => item.id === Number(requestSelect.value));
    resetForm();
    renderRequest();
  };
  $("#filters").onsubmit = (event) => {
    event.preventDefault();
    page = 1;
    $("#feedback").hidden = true;
    load();
  };
  $("#filters").onreset = () => {
    page = 1;
    setTimeout(load, 0);
  };
  $("#previousPage").onclick = () => {
    if (page > 1) {
      page--;
      load();
    }
  };
  $("#nextPage").onclick = () => {
    if (page * 10 < total) {
      page++;
      load();
    }
  };
  $("#rows").onclick = (event) => {
    const button = event.target.closest("[data-open]");
    if (button) openCase(button.dataset.open);
  };
  $("#back").onclick = back;
  $("#cancel").onclick = back;
  $("#explanation").oninput = () => {
    $("#chars").textContent = $("#explanation").value.trim().length + " / 2.000 ký tự";
  };
  $("#evidence").onchange = (event) => {
    attachments.push(...event.target.files);
    event.target.value = "";
    renderFiles();
  };
  $("#closePreview").onclick = () => $("#preview").close();
  $("#preview").onclose = closePreview;
  window.addEventListener("beforeunload", (event) => {
    if (dirty()) {
      event.preventDefault();
      event.returnValue = messages.leave;
    }
  });
  $("#responseForm").onsubmit = async (event) => {
    event.preventDefault();
    if (saving) return;
    if (!canRespond())
      return notice(selectedRequest?.status === "Expired" ? messages.expired : messages.closed, true);
    const explanation = $("#explanation").value.trim();
    const validText = explanation.length >= 15 && explanation.length <= 2000;
    const validFiles =
      attachments.length >= 1 &&
      attachments.length <= 5 &&
      attachments.every(
        (file) =>
          /\.(jpe?g|png|pdf)$/i.test(file.name) &&
          file.size > 0 &&
          file.size <= 5 * 1024 * 1024 &&
          (!file.type || ["image/jpeg", "image/png", "application/pdf"].includes(file.type)),
      );
    $("#explanationError").textContent = validText ? "" : messages.explanationInvalid;
    $("#evidenceError").textContent = validFiles ? "" : messages.evidenceInvalid;
    $("#explanation").setAttribute("aria-invalid", String(!validText));
    $("#evidence").setAttribute("aria-invalid", String(!validFiles));
    if (!validText || !validFiles) return notice(messages.invalid, true);
    saving = true;
    lock();
    try {
      requestKey ??= crypto.randomUUID();
      const evidenceUrls = [];
      for (const file of attachments) {
        if (!uploaded.has(file)) {
          const form = new FormData();
          form.append("file", file);
          const result = await request("/evidence", { method: "POST", body: form });
          uploaded.set(file, result.fileUrl);
        }
        evidenceUrls.push(uploaded.get(file));
      }
      const data = await request("/" + current.id + "/responses", {
        method: "POST",
        body: {
          evidenceRequestId: selectedRequest.id,
          explanation,
          evidenceUrls,
          version: current.version,
          requestKey,
        },
      });
      renderDetail(data);
      notice(data.message || messages.saved, data.notificationPending);
    } catch (error) {
      notice(error.message || messages.saveFailed, true);
      if (error.status === 409) {
        const reload = document.createElement("button");
        reload.type = "button";
        reload.textContent = messages.reload;
        reload.onclick = () => {
          if (!dirty() || confirm(messages.discard)) openCase(current.id);
        };
        $("#feedback").append(reload);
      }
    } finally {
      saving = false;
      lock();
    }
  };
  load();
  const id = new URLSearchParams(location.search).get("id");
  if (id && /^\d+$/.test(id)) openCase(id);
});
