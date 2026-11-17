document.addEventListener('DOMContentLoaded', () => {
  if (!Auth.requireAuth('RetailCustomer')) return;

  const messages = CustomerComplaintMessages;
  const byId = (id) => document.getElementById('complaint-' + id);
  const params = new URLSearchParams(location.search);
  const ticketId = params.get('id');
  const orderId = params.get('orderId');
  const listUrl = 'complaints.html' + (orderId ? '?orderId=' + encodeURIComponent(orderId) : '');
  const filter = byId('filter');
  const form = byId('form');
  const input = byId('upload');
  const explanation = byId('explanation');
  const formatter = new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh'
  });
  let page = 1;
  let version;
  let activeRequest;
  let requestKey;
  let sending = false;
  let files = [];
  let uploads = new Map();
  let previews = [];

  function element(tag, value, className) {
    const node = document.createElement(tag);
    if (value != null) node.textContent = value;
    if (className) node.className = className;
    return node;
  }

  function notice(message = '', error = '') {
    byId('status').textContent = message;
    byId('status').hidden = !message;
    byId('error').textContent = error;
    byId('error').hidden = !error;
  }

  function formError(message = '') {
    byId('form-error').textContent = message;
    byId('form-error').hidden = !message;
  }

  function status(value) {
    return Object.hasOwn(messages.statuses, value) ? messages.statuses[value] : value;
  }

  function reason(value) {
    return Object.hasOwn(messages.reasons, value) ? messages.reasons[value] : value;
  }

  function statusBadge(value, needsResponse = false) {
    return element('span', status(value),
      'feedback-badge' + (needsResponse ? ' complaint-needed' : ''));
  }

  function loadFailed(result) {
    notice('', result.error || messages.loadFailed);
    byId('retry').hidden = false;
  }

  async function loadList() {
    byId('retry').hidden = true;
    byId('items').replaceChildren();
    byId('pagination').hidden = true;
    byId('page').textContent = '';
    filter.disabled = byId('prev').disabled = byId('next').disabled = true;
    notice(messages.loading);
    const result = await API.getMyComplaints({ page, status: filter.value, orderId });
    filter.disabled = false;
    if (!result.success) return loadFailed(result);

    const { items, pageSize, total } = result.data;
    const rows = items.map((item) => {
      const row = element('li', null, 'complaint-row');
      const summary = element('div');
      summary.append(element('h3', 'Khiếu nại #' + item.id),
        element('p', item.orderCode + ' · ' + reason(item.reason), 'feedback-muted'));
      const actions = element('div', null, 'complaint-row-actions');
      const link = element('a', 'Xem chi tiết', 'feedback-support');
      link.setAttribute('aria-label', 'Xem chi tiết khiếu nại #' + item.id);
      const query = new URLSearchParams({ id: item.id });
      if (orderId) query.set('orderId', orderId);
      link.href = 'complaints.html?' + query;
      const badge = statusBadge(item.status, item.needsResponse);
      if (item.needsResponse) badge.textContent = messages.needsResponse;
      actions.append(badge, link);
      row.append(summary, actions);
      return row;
    });
    byId('items').replaceChildren(...rows);
    if (!rows.length) byId('items').append(element('li', messages.empty, 'feedback-empty'));
    notice();
    byId('pagination').hidden = total <= pageSize;
    byId('page').textContent = 'Trang ' + page + '/' + Math.max(1, Math.ceil(total / pageSize));
    byId('prev').disabled = page <= 1;
    byId('next').disabled = page * pageSize >= total;
  }

  function attachment(file) {
    const button = element('button', file.name);
    button.type = 'button';
    button.onclick = async () => {
      button.disabled = true;
      try {
        const blob = await API.openMyComplaintEvidence(ticketId, file.id);
        const url = URL.createObjectURL(blob);
        const link = element('a');
        link.href = url;
        link.download = file.name;
        link.click();
        setTimeout(() => URL.revokeObjectURL(url), 30000);
      } catch {
        notice('', messages.fileFailed);
      } finally {
        button.disabled = false;
      }
    };
    return button;
  }

  function showCountdown() {
    if (!activeRequest) return;
    const seconds = Math.max(0, Math.ceil((Date.parse(activeRequest.dueAt) - Date.now()) / 1000));
    byId('submit').disabled = sending || seconds === 0;
    byId('countdown').textContent = seconds
      ? 'Còn ' + Math.floor(seconds / 3600) + ' giờ ' + Math.floor(seconds % 3600 / 60)
        + ' phút ' + seconds % 60 + ' giây'
      : messages.expired;
  }

  function selectRequest(request) {
    activeRequest = request;
    requestKey = null;
    form.reset();
    files = [];
    uploads = new Map();
    formError();
    showPreviews();
    updateCharacters();
    form.hidden = !request;
    if (request) byId('form-title').textContent = 'Phản hồi yêu cầu #' + request.id;
    showCountdown();
  }

  async function loadDetail(message = '') {
    byId('retry').hidden = true;
    notice(message || messages.loading);
    const result = await API.getMyComplaint(ticketId);
    if (!result.success) return loadFailed(result);

    const detail = result.data;
    version = detail.version;
    byId('title').textContent = 'Khiếu nại #' + detail.id;
    byId('case-status').textContent = status(detail.status);
    byId('case-status').classList.toggle('complaint-needed', detail.requests.some(request => request.canRespond));
    byId('order-code').textContent = detail.orderCode;
    byId('reason').textContent = reason(detail.reason);
    byId('description').textContent = detail.description;
    byId('description').hidden = !detail.description;
    byId('original').hidden = detail.originalEvidence.length === 0;
    byId('original-files').replaceChildren(...detail.originalEvidence.map(attachment));
    byId('resolution').textContent = [
      detail.resolution,
      detail.refundAmount != null ? 'Số tiền hoàn ' + Utils.formatVND(detail.refundAmount) : '',
      ({ Pending: 'Chờ hoàn tiền', Processing: 'Đang hoàn tiền', Completed: 'Đã hoàn tiền',
        Failed: 'Hoàn tiền chưa thành công', NeedsReconciliation: 'Đang đối soát khoản hoàn' })[detail.refundStatus]
    ].filter(Boolean).join('\n');
    byId('outcome').hidden = !byId('resolution').textContent;
    const pending = detail.requests.filter((request) => request.canRespond);
    const requests = detail.requests.map((request) => {
      const panel = element('article', null, 'complaint-request');
      const heading = element('div', null, 'complaint-card-heading');
      heading.append(element('h3', 'Yêu cầu #' + request.id), statusBadge(request.status, request.canRespond));
      panel.append(heading);
      if (request.instructions) panel.append(element('p', request.instructions, 'complaint-text'));
      const deadline = element('time', 'Hạn phản hồi ' + formatter.format(new Date(request.dueAt)));
      deadline.dateTime = request.dueAt;
      panel.append(deadline);
      if (request.respondedAt) {
        const files = element('div', null, 'complaint-files');
        files.append(...request.files.map(attachment));
        panel.append(
          element('p', 'Đã phản hồi lúc ' + formatter.format(new Date(request.respondedAt)), 'complaint-request-deadline'),
          element('p', request.explanation, 'complaint-text'),
          files
        );
      }
      if (request.canRespond) {
        const button = element('button', 'Phản hồi yêu cầu #' + request.id, 'feedback-upload-button');
        button.type = 'button';
        button.onclick = () => {
          if (sending) return;
          if (request !== activeRequest) selectRequest(request);
          explanation.focus();
        };
        panel.append(button);
      }
      return panel;
    });
    byId('requests').replaceChildren(...(requests.length ? requests : [element('p', messages.noRequests, 'feedback-muted')]));
    selectRequest(pending[0]);
    byId('detail').hidden = false;
    notice(message);
    if (message) byId('title').focus();
  }

  function fileType(file) {
    const extension = file.name.split('.').pop().toLowerCase();
    const types = { jpg: 'image', jpeg: 'image', png: 'image', pdf: 'pdf', mp4: 'video' };
    return Object.hasOwn(types, extension) ? types[extension] : null;
  }

  function validFiles(files) {
    return files.length >= 1 && files.length <= 5
      && files.filter((file) => fileType(file) === 'video').length <= 1
      && files.every((file) => {
        const kind = fileType(file);
        return kind && file.size > 0 && file.size <= (kind === 'video' ? 20 : 5) * 1024 * 1024;
      });
  }

  function showPreviews() {
    previews.forEach((url) => URL.revokeObjectURL(url));
    previews = [];
    const list = byId('previews');
    list.replaceChildren();
    for (const file of files) {
      const row = element('li', null, 'complaint-preview');
      const label = element('div');
      const remove = element('button', 'Xóa');
      remove.type = 'button';
      remove.setAttribute('aria-label', 'Xóa tệp ' + file.name);
      remove.onclick = () => {
        files = files.filter(item => item !== file);
        uploads.delete(file);
        showPreviews();
        byId('add-file').focus();
      };
      label.append(element('span', file.name), remove);
      row.append(label);
      const url = URL.createObjectURL(file);
      previews.push(url);
      const kind = fileType(file);
      if (kind === 'pdf') {
        const link = element('a', 'Xem PDF');
        link.href = url;
        link.target = '_blank';
        link.rel = 'noopener';
        row.append(' ', link);
      } else {
        const media = element(kind === 'image' ? 'img' : 'video');
        media.src = url;
        if (kind === 'image') media.alt = file.name;
        else {
          media.controls = true;
          media.playsInline = true;
          media.preload = 'metadata';
          media.setAttribute('aria-label', file.name);
        }
        row.append(media);
      }
      list.append(row);
    }
  }

  function updateCharacters() {
    byId('characters').textContent = explanation.value.length.toLocaleString('vi-VN') + ' / 2.000';
  }

  explanation.addEventListener('input', updateCharacters);
  byId('add-file').onclick = () => input.click();
  input.addEventListener('change', () => {
    const selected = [...files, ...input.files];
    input.value = '';
    if (!validFiles(selected)) return formError(messages.fileRules);
    files = selected;
    formError();
    showPreviews();
  });
  form.addEventListener('submit', async (event) => {
    event.preventDefault();
    if (sending || !activeRequest) return;
    if (Date.parse(activeRequest.dueAt) < Date.now()) return formError(messages.expired);
    const text = explanation.value.trim();
    if (text.length < 15 || text.length > 2000 || !validFiles(files)) {
      formError(messages.invalid);
      (text.length < 15 || text.length > 2000 ? explanation : byId('add-file')).focus();
      return;
    }
    requestKey ||= crypto.randomUUID();
    sending = true;
    byId('fields').disabled = true;
    byId('submit').textContent = messages.sending;
    formError();
    try {
      for (const file of files) {
        if (!uploads.has(file)) {
          const upload = await API.uploadComplaintEvidence(file);
          if (!upload.success) return formError(upload.error || messages.saveFailed);
          uploads.set(file, upload.data.fileUrl);
        }
      }
      const response = await API.respondComplaint(ticketId, {
        evidenceRequestId: activeRequest.id,
        explanation: text,
        evidenceUrls: files.map((file) => uploads.get(file)),
        requestKey,
        version
      });
      if (!response.success) {
        formError(response.error || messages.saveFailed);
        byId('retry').hidden = response.status !== 409;
        return;
      }
      selectRequest(null);
      await loadDetail(response.data.message || messages.saved);
    } finally {
      sending = false;
      byId('fields').disabled = false;
      byId('submit').textContent = 'Gửi phản hồi';
      showCountdown();
    }
  });

  byId('rules').textContent = messages.fileRules;
  if (ticketId) {
    byId('back').href = listUrl;
    byId('heading').textContent = 'Chi tiết khiếu nại';
    byId('retry').onclick = () => loadDetail();
    setInterval(showCountdown, 1000);
    loadDetail();
  } else {
    for (const value of ['Open', 'WaitingCustomerInfo', 'UnderReview', 'RefundPending', 'Rejected', 'Closed']) {
      filter.add(new Option(status(value), value));
    }
    byId('list').hidden = false;
    byId('retry').onclick = loadList;
    filter.onchange = () => { page = 1; loadList(); };
    byId('prev').onclick = () => { page--; loadList(); };
    byId('next').onclick = () => { page++; loadList(); };
    loadList();
  }
});
