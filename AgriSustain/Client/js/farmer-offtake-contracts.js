document.addEventListener("DOMContentLoaded", async () => {
  if (!Auth.requireAuth("Farmer")) return;
  const ui = new FarmerBusinessUI();
  const $ = ui.$;
  const base = "/farmer/offtake-contracts";
  let current = null;
  let page = 1;
  let total = 0;
  let busy = false;
  let loadSequence = 0;
  const requests = new Map();
  const value = (id) => $(id).value.trim();
  const text = (id, content) => {
    $(id).textContent = content ?? "";
  };
  const money = (value) => ui.money(value);

  async function load() {
    const sequence = ++loadSequence;
    const query = new URLSearchParams({ page });
    [
      ["keyword", "#contractSearch"],
      ["status", "#contractStatus"],
      ["from", "#contractFrom"],
      ["to", "#contractTo"],
    ].forEach(([key, selector]) => {
      if (value(selector)) query.set(key, value(selector));
    });
    try {
      const result = await ui.request(base + "?" + query);
      if (sequence !== loadSequence) return;
      total = result.total;
      $("#contractRows").innerHTML = result.items
        .map(
          (row) => `<tr>
        <td><strong>${ui.escape(row.code)}</strong><small>${ui.escape(row.buyer)}</small></td>
        <td>${ui.escape((row.products || []).map((item) => `${item.productName}: ${ui.number(item.committedQuantity)} ${item.unitSnapshot}`).join(" · ")) || ui.number(row.quantity)}</td><td>${ui.date(row.validFrom)} – ${ui.date(row.validUntil)}</td>
        <td><span class="badge">${ui.escape(ui.label(row.status))}</span></td>
        <td><button type="button" class="btn" data-contract="${row.id}">Xem và xử lý</button></td></tr>`,
        )
        .join("");
      $("#contractEmpty").hidden = total !== 0;
      text("#contractCount", `${total} hợp đồng · 10 bản ghi/trang`);
      text("#contractPage", page);
      $("#contractPrev").disabled = page <= 1;
      $("#contractNext").disabled = page * 10 >= total;
      if (result.message) ui.notice(result.message);
    } catch (error) {
      ui.notice(error.message, true);
    }
  }

  function inspection() {
    const ticket = current?.disputes.find(
      (row) => row.id === Number(value("#disputeTicket")),
    );
    const items =
      current?.shipments.flatMap((shipment) => shipment.items) || [];
    const item = items.find(
      (row) => row.id === Number(value("#disputeShipment")),
    );
    text(
      "#discrepancyStatus",
      ticket ? ui.label(ticket.ticketStatus) : ui.m.noRecords,
    );
    ui.kv("#inspectionDetail", [
      ["Nội dung", ticket?.description || ticket?.disputeReason],
      ["Đã giao", ui.number(item?.shippedQuantity)],
      ["Đã nhận", ui.number(item?.receivedQuantity)],
      ["Đã chấp nhận", ui.number(item?.acceptedQuantity)],
      ["Kiểm nhận", item?.qualityResult],
      ["Trạng thái", ui.label(item?.reconciliationStatus)],
    ]);
    $("#proposedQuantity").max = item?.receivedQuantity ?? "";
    text("#discrepancyOutcome", ticket?.resolutionNotes || ui.m.noAgreement);
    $("#receivedEvidence").innerHTML = (ticket?.evidence || [])
      .map(
        (file) =>
          `<button type="button" class="btn" data-proof="${file.disputeEvidenceId}">${ui.escape(file.description || "Bằng chứng")}</button>`,
      )
      .join("");
  }

  function render() {
    const row = current;
    text("#contractCode", row.code);
    text("#contractBadge", ui.label(row.status));
    ui.kv("#contractTerms", [
      ["Doanh nghiệp", row.buyer],
      ["Thời hạn", `${ui.date(row.validFrom)} – ${ui.date(row.validUntil)}`],
      ["Phiên bản", row.currentVersionNo],
      ["Điều khoản", row.termsAndConditions],
      [
        "Hàng hóa",
        row.lines
          .map(
            (line) =>
              `${line.productName}: ${ui.number(line.committedQuantity)} ${line.unitSnapshot}`,
          )
          .join("\n"),
      ],
    ]);
    ui.kv("#priceTerms", [
      [
        "Giá theo thỏa thuận",
        row.lines
          .map(
            (line) =>
              `${line.productName}: ${line.agreedPrice == null ? line.priceFormula || "Theo kiểm nhận và thỏa thuận" : money(line.agreedPrice)} / ${line.unitSnapshot}`,
          )
          .join("\n"),
      ],
      [
        "Chất lượng",
        row.lines.map((line) => line.qualitySpecification || "—").join("\n"),
      ],
      ["Thanh toán", row.paymentTerms],
      ["Tiền cọc yêu cầu", money(row.depositAmount)],
    ]);
    text(
      "#confirmationState",
      row.signatures
        .map(
          (item) =>
            `${ui.label(item.partyRole)}: ${ui.label(item.confirmationStatus)}`,
        )
        .join(" · ") || ui.m.noRecords,
    );
    text(
      "#depositState",
      `${money(row.depositPaid)} / ${money(row.depositAmount)}`,
    );
    ui.options(
      "#contractSeason",
      row.seasons.filter((season) =>
        row.lines.every((line) => line.productId === season.productId),
      ),
      (season) =>
        `${season.seasonName} · ${ui.number(season.estimatedYield)} · ${ui.date(season.estimatedHarvestDate)}`,
      "Chọn mùa vụ",
    );
    if (row.lines.length)
      $("#contractSeason").value = row.lines[0].cropSeasonId || "";
    ui.options(
      "#shipmentMilestone",
      row.milestones.filter(
        (item) => item.deliveryQuantity > item.deliveredQuantity,
      ),
      (item) =>
        `${item.milestoneTitle} · ${ui.date(item.dueDate)} · còn ${ui.number(item.deliveryQuantity - item.deliveredQuantity)}`,
      "Chọn đợt giao",
    );
    ui.options(
      "#delayMilestone",
      row.milestones.filter(
        (item) => item.deliveryQuantity > item.deliveredQuantity,
      ),
      (item) => item.milestoneTitle,
      "Chọn nghĩa vụ",
    );
    $("#milestoneRows").innerHTML = row.milestones
      .map(
        (item) => `<tr><td>${ui.escape(item.milestoneTitle)}</td>
      <td>${ui.date(item.dueDate)}</td><td>${ui.number(item.deliveryQuantity)}</td><td>${ui.number(item.deliveredQuantity)} / ${ui.number(item.acceptedQuantity)}</td>
      <td>${ui.escape(ui.label(item.fulfillmentStatus))} · ${ui.escape(ui.label(item.paymentStatus))}</td></tr>`,
      )
      .join("");
    ui.options(
      "#disputeTicket",
      row.disputes,
      (item) => `#${item.id} · ${item.disputeReason}`,
      "Chọn chênh lệch",
    );
    ui.options(
      "#disputeShipment",
      row.shipments.flatMap((shipment) => shipment.items),
      (item) =>
        `#${item.id} · đã giao ${ui.number(item.shippedQuantity)} · đã nhận ${ui.number(item.receivedQuantity)}`,
      "Chọn hàng đã kiểm nhận",
    );
    const firstTicket = row.disputes[0];
    if (firstTicket) $("#disputeTicket").value = firstTicket.id;
    const firstItem = row.shipments
      .flatMap((shipment) => shipment.items)
      .find((item) => item.receivedQuantity != null);
    if (firstItem) $("#disputeShipment").value = firstItem.id;
    inspection();
    const termination = row.termination;
    text(
      "#terminationResult",
      termination
        ? `${ui.label(termination.requestStatus)} · ${termination.reason}`
        : ui.m.noTermination,
    );
    text(
      "#completionState",
      row.status === "Completed" ? ui.label(row.status) : ui.m.pending,
    );
    ui.kv("#financialProgress", [
      [
        "Khoản thu được xác minh",
        row.payments
          .map(
            (item) =>
              `${item.transactionCode} · ${money(item.amount)} · ${ui.label(item.paymentStatus)} · ${item.providerTransactionReference || "—"}`,
          )
          .join("\n") || ui.m.noRecords,
      ],
      [
        "Hóa đơn",
        row.invoices
          .map(
            (item) =>
              `${item.invoiceNumber}: ${money(item.totalAmount)} · ${ui.label(item.invoiceStatus)}`,
          )
          .join("\n") || ui.m.noRecords,
      ],
      [
        "Chi trả",
        row.payouts
          .map(
            (item) =>
              `${item.payoutCode}: ${money(item.netAmount)} · ${ui.label(item.payoutStatus)} · ${item.bankTransferRef || "—"}`,
          )
          .join("\n") || ui.m.noRecords,
      ],
    ]);
    $("#contractHistory").innerHTML =
      row.history
        .map((item) => {
          let input = null;
          try {
            input = JSON.parse(item.newValue || "{}").Input;
          } catch {
            /* Historical notes may be plain text. */
          }
          const description = [
            item.reason,
            input?.ProposedChanges,
            input?.SettlementProposal,
            input?.ProposedResolution,
            input?.Quantity
              ? `${ui.number(input.Quantity)} · ${input.ProposedDate || ""}`
              : null,
          ]
            .filter(Boolean)
            .join(" · ");
          return `<li>${ui.date(item.occurredAt)} · ${ui.escape(ui.label(item.action))}<p>${ui.escape(description)}</p></li>`;
        })
        .join("") || `<li>${ui.escape(ui.m.noRecords)}</li>`;
    syncButtons();
    batches();
    if (row.notificationFailed) ui.notice(ui.m.noticeFailed, true);
  }

  function batches() {
    const milestone = current?.milestones.find(
      (row) => row.id === Number(value("#shipmentMilestone")),
    );
    const line = current?.lines.find(
      (row) => row.id === milestone?.contractLineId,
    );
    ui.options(
      "#shipmentBatch",
      (current?.batches || []).filter(
        (batch) =>
          batch.productId === line?.productId &&
          batch.cropSeasonId === line?.cropSeasonId &&
          batch.hasQr &&
          batch.actualHarvestDate &&
          batch.availableQuantity > batch.reservedQuantity,
      ),
      (batch) =>
        `${batch.batchCode} · còn ${ui.number(batch.availableQuantity - batch.reservedQuantity)}`,
      "Chọn lô hàng thực tế",
    );
  }

  function syncButtons() {
    const proposal =
      current &&
      [
        "PendingApproval",
        "Proposed",
        "Pending",
        "PendingConfirmation",
        "Negotiating",
        "AwaitingDeposit",
      ].includes(current.status);
    $("#confirmContract").disabled =
      busy ||
      !proposal ||
      current.signatures.some(
        (item) =>
          item.partyRole === "Farmer" &&
          item.confirmationStatus === "Confirmed",
      );
    $("#rejectContract").disabled =
      busy || !proposal || current.status === "AwaitingDeposit";
    $("#amendContract").disabled =
      busy ||
      !current ||
      ["Completed", "Terminated", "Rejected"].includes(current.status);
    $("#dispatchButton").disabled = busy || current?.status !== "Active";
    $("#delayButton").disabled = busy || current?.status !== "Active";
    $("#terminationButton").disabled =
      busy ||
      !current ||
      !!current.termination ||
      ["Completed", "Terminated", "Rejected"].includes(current.status);
    const ticket = current?.disputes.find(
      (item) => item.id === Number(value("#disputeTicket")),
    );
    const closed =
      !ticket ||
      ["Resolved", "Closed", "Rejected"].includes(ticket.ticketStatus);
    $("#submitDiscrepancy").disabled = busy || closed;
    $("#escalateDiscrepancy").disabled = busy || closed;
  }

  async function detail(id, screen = "terms") {
    const row = await ui.request(`${base}/${id}`);
    current = row;
    render();
    ui.go(screen);
  }

  async function command(action, payload, form = null) {
    if (!current || busy) return;
    if (form && !form.reportValidity()) return;
    if (
      !(await ui.confirm(
        action === "dispatch" ? ui.m.dispatchConfirm : ui.m.confirm,
      ))
    )
      return;
    busy = true;
    syncButtons();
    try {
      if (payload.files) {
        payload.evidence = await ui.upload(payload.files);
        delete payload.files;
      }
      const identity = action + JSON.stringify(payload) + current.version;
      if (!requests.has(identity)) requests.set(identity, crypto.randomUUID());
      const result = await ui.request(`${base}/${current.id}/${action}`, {
        method: "POST",
        body: {
          ...payload,
          expectedVersion: current.version,
          requestKey: requests.get(identity),
        },
      });
      requests.delete(identity);
      const screen = ui.$$("[data-screen]").find((node) => !node.hidden)
        .dataset.screen;
      if (form) form.reset();
      await detail(current.id, screen);
      ui.notice(result.message);
    } catch (error) {
      ui.notice(error.message, true);
    } finally {
      busy = false;
      syncButtons();
    }
  }

  ui.navigate = async (screen) => {
    if (screen === "list") {
      ui.go(screen);
      await load();
      return;
    }
    if (!current) {
      ui.notice(ui.m.selectContract, true);
      return;
    }
    if (screen === "history" && !busy) {
      try {
        const result = await ui.request(`${base}/${current.id}/progress`, {
          method: "POST",
        });
        await detail(current.id, screen);
        if (result.status === "Completed") ui.notice(result.message);
      } catch (error) {
        ui.notice(error.message, true);
      }
    } else ui.go(screen);
  };
  $("#contractRows").onclick = (event) => {
    const button = event.target.closest("[data-contract]");
    if (button)
      detail(Number(button.dataset.contract)).catch((error) =>
        ui.notice(error.message, true),
      );
  };
  $("#filterContracts").onclick = () => {
    page = 1;
    load();
  };
  $("#resetContracts").onclick = () => {
    [
      "#contractSearch",
      "#contractStatus",
      "#contractFrom",
      "#contractTo",
    ].forEach((id) => {
      $(id).value = "";
    });
    page = 1;
    load();
  };
  $("#contractPrev").onclick = () => {
    if (page > 1) {
      page--;
      load();
    }
  };
  $("#contractNext").onclick = () => {
    if (page * 10 < total) {
      page++;
      load();
    }
  };
  $("#confirmContract").onclick = () =>
    command("confirm", {
      versionNo: current.currentVersionNo,
      cropSeasonId: Number(value("#contractSeason")) || null,
    });
  $("#rejectContract").onclick = () => {
    if (value("#contractFeedback").length < 20) {
      ui.notice(ui.m.reason, true);
      return;
    }
    command("reject", { reason: value("#contractFeedback") });
  };
  $("#amendContract").onclick = () => {
    if (value("#contractFeedback").length < 20) {
      ui.notice(ui.m.reason, true);
      return;
    }
    if (!value("#amendmentChanges")) {
      ui.notice(ui.m.required, true);
      return;
    }
    command("amend", {
      reason: value("#contractFeedback"),
      proposedChanges: value("#amendmentChanges"),
    });
  };
  $("#clearTerms").onclick = () => {
    $("#contractFeedback").value = "";
    $("#amendmentChanges").value = "";
  };
  $("#shipmentMilestone").onchange = batches;
  $("#dispatchForm").onsubmit = (event) => {
    event.preventDefault();
    command(
      "dispatch",
      {
        milestoneId: Number(value("#shipmentMilestone")),
        batchQuantities: [
          {
            harvestBatchId: Number(value("#shipmentBatch")),
            quantity: Number(value("#shipmentQuantity")),
          },
        ],
      },
      event.currentTarget,
    );
  };
  $("#delayForm").onsubmit = (event) => {
    event.preventDefault();
    command(
      "delay",
      {
        milestoneId: Number(value("#delayMilestone")),
        quantity: Number(value("#delayQuantity")),
        proposedDate: value("#delayDate"),
        reason: value("#delayReason"),
      },
      event.currentTarget,
    );
  };
  $("#disputeTicket").onchange = () => {
    inspection();
    syncButtons();
  };
  $("#disputeShipment").onchange = inspection;
  $("#discrepancyForm").onsubmit = (event) => {
    event.preventDefault();
    command(
      "discrepancy",
      {
        ticketId: Number(value("#disputeTicket")),
        shipmentItemId: Number(value("#disputeShipment")),
        proposedQuantity:
          value("#proposedQuantity") === ""
            ? null
            : Number(value("#proposedQuantity")),
        proposedResolution: value("#proposedGrade"),
        reason: value("#discrepancyExplanation"),
        files: "#discrepancyEvidence",
      },
      event.currentTarget,
    );
  };
  $("#escalateDiscrepancy").onclick = () =>
    command("review", { ticketId: Number(value("#disputeTicket")) });
  $("#terminationForm").onsubmit = (event) => {
    event.preventDefault();
    command(
      "termination",
      {
        reason: value("#terminationReason"),
        settlementProposal: value("#terminationPlan"),
        files: "#terminationEvidence",
      },
      event.currentTarget,
    );
  };
  $("#receivedEvidence").onclick = async (event) => {
    const button = event.target.closest("[data-proof]");
    if (!button) return;
    try {
      const response = await fetch(
        `${CONFIG.API_BASE_URL}${base}/${current.id}/evidence/${button.dataset.proof}`,
        { headers: { Authorization: "Bearer " + Auth.getAccessToken() } },
      );
      if (!response.ok) throw new Error(ui.m.loadFailed);
      const url = URL.createObjectURL(await response.blob());
      window.open(url, "_blank", "noopener");
      setTimeout(() => URL.revokeObjectURL(url), 60000);
    } catch (error) {
      ui.notice(error.message, true);
    }
  };
  ui.go("list");
  syncButtons();
  await load();
  const requested = Number(new URLSearchParams(location.search).get("id"));
  if (requested > 0)
    detail(requested).catch((error) => ui.notice(error.message, true));
});
