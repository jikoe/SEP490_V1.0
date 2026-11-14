using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Finance;
using AgriSustain.Application.Validators;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class OfftakeService(IOfftakeRepository repository, IUseCaseUnitOfWork unitOfWork,
    IActorContext actor, IPersistenceErrorClassifier errors, TimeProvider clock,
    AccountNotificationSettings support) : IOfftakeService
{
    public const string ProposalNotificationType = "OfftakeProposalSubmitted";
    private const string InvalidProposal = "MSG48 – Vui lòng kiểm tra thông tin đề xuất.";
    private const string IncompleteArticles = "Vui lòng nhập tiêu đề và nội dung cho từng điều khoản.";
    private const string Ineligible = "Hồ sơ doanh nghiệp phải được duyệt để gửi đề xuất.";
    private const string Unavailable = "MSG54 – Hồ sơ hợp đồng hoặc đơn hàng không còn khả dụng.";
    private const string Confirmed = "Confirmed";
    private const string Buyer = "WholesaleBuyer";
    private const string DraftConflict = "Bản nháp đã thay đổi hoặc không còn chỉnh sửa được.";
    private const string RevisionConflict = "Yêu cầu sửa đổi đã thay đổi hoặc không còn chỉnh sửa được.";

    public async Task<Result<OfftakeCatalog>> CatalogAsync(string? keyword, CancellationToken ct)
    {
        if (keyword?.Length > 100)
        {
            return Result<OfftakeCatalog>.Fail("Từ khóa tối đa 100 ký tự.");
        }
        if (await repository.ApprovedBuyerAsync(actor.UserId, ct) is null)
        {
            return Result<OfftakeCatalog>.Fail(Ineligible, 403);
        }

        var products = await repository.CatalogAsync(keyword?.Trim(), ct);
        return Result<OfftakeCatalog>.Ok(new(products, support.SupportEmail));
    }

    public async Task<Result<OfftakeProposalReceipt>> ProposeAsync(
        OfftakeProposalRequest request, CancellationToken ct, bool draft = false)
    {
        if (!OfftakeProposalValidator.IsValid(request) || decimal.Truncate(request.DepositAmount) != request.DepositAmount)
        {
            return Result<OfftakeProposalReceipt>.Fail(InvalidProposal);
        }

        var snapshot = Snapshot(request);
        var code = "OFTC-" + Hash($"{actor.UserId}:{request.RequestId:N}")[..24];
        var now = clock.GetUtcNow().UtcDateTime;
        try
        {
            await using var transaction = await unitOfWork.BeginAsync(ct);
            if (await repository.ApprovedBuyerAsync(actor.UserId, ct) is not long buyerId)
            {
                return Result<OfftakeProposalReceipt>.Fail(Ineligible, 403);
            }
            var existing = await repository.FindSubmissionAsync(buyerId, code, ct);
            if (existing is not null)
            {
                return (draft ? existing.Status == "Draft" : existing.Status != "Draft")
                    && existing.ContractVersions.Any(x => x.VersionNo == 1
                        && x.DocumentHash == Hash(snapshot))
                    ? Result<OfftakeProposalReceipt>.Ok(new(existing.B2bcontractId, code))
                    : Result<OfftakeProposalReceipt>.Fail("Đề xuất đã được gửi với nội dung khác.", 409);
            }

            if (!OfftakeProposalValidator.HasCompleteArticles(request))
            {
                return Result<OfftakeProposalReceipt>.Fail(IncompleteArticles);
            }
            var product = await repository.FindProductAsync(request.FarmerId, request.ProductId, ct);
            if (product is null || !SupportsQuantityUnit(product, request))
            {
                return Result<OfftakeProposalReceipt>.Fail(InvalidProposal);
            }

            var contract = new B2bcontract
            {
                WholesaleBuyerProfileId = buyerId, FarmerProfileId = product.FarmerId,
                ContractNumber = code, CurrentVersionNo = 1, Status = "Draft",
                DigitalSignatureStatus = "Unsigned", DepositAmount = request.DepositAmount,
                PaymentTerms = request.PaymentTerms?.Trim(), ValidFrom = request.ValidFrom,
                ValidUntil = request.ValidUntil, CreatedAt = now
            };
            var version = AddProposalVersion(contract, request, product, snapshot, Hash(snapshot), now);
            repository.Add(contract);
            await unitOfWork.SaveChangesAsync(ct);
            if (!draft)
            {
                await SubmitAsync(contract, version, now, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);

            return Result<OfftakeProposalReceipt>.Ok(new(contract.B2bcontractId, code),
                draft ? "Đã lưu bản nháp." : $"MSG34 – Đã gửi đề xuất tới {product.Farmer}.", 201);
        }
        catch (Exception ex) when (errors.IsConflict(ex))
        {
            return Result<OfftakeProposalReceipt>.Fail(
                "MSG49 – Chưa thể gửi đề xuất. Vui lòng thử lại.", 409);
        }
    }

    public async Task<Result<OfftakeDraft>> DraftAsync(long id, CancellationToken ct)
    {
        if (await repository.ApprovedBuyerAsync(actor.UserId, ct) is not long buyerId)
        {
            return Result<OfftakeDraft>.Fail(Ineligible, 403);
        }
        var contract = await repository.FindManagedAsync(buyerId, id, ct);
        if (contract is null)
        {
            return Result<OfftakeDraft>.Fail(Unavailable, 404);
        }
        var revision = IsNegotiating(contract);
        var current = EditableVersion(contract, revision);
        var proposal = EditableProposal(contract, current, revision);
        if (current is null || proposal is null
            || revision && await repository.HasCommitmentsAsync(id, ct, editingRevision: true))
        {
            return Result<OfftakeDraft>.Fail(revision ? RevisionConflict : DraftConflict, 409);
        }
        var product = await repository.FindProductAsync(proposal.FarmerId, proposal.ProductId, ct);
        if (revision && product is null)
        {
            return Result<OfftakeDraft>.Fail("Sản phẩm trong hợp đồng không còn khả dụng để gửi lại.", 409);
        }
        return Result<OfftakeDraft>.Ok(new OfftakeDraft(contract.ContractNumber, current.VersionNo,
            contract.CreatedAt, proposal, product)
        {
            RevisionReason = revision ? current.FeedbackReason : null,
            RevisionChanges = revision ? OfftakeContractRules.SplitAmendment(current.TermsAndConditions).Changes
                ?? current.TermsAndConditions : null
        });
    }

    public async Task<Result<OfftakeProposalReceipt>> UpdateDraftAsync(long id, int version,
        OfftakeProposalRequest request, bool submit, CancellationToken ct)
    {
        if (!OfftakeProposalValidator.IsValid(request) || decimal.Truncate(request.DepositAmount) != request.DepositAmount)
        {
            return Result<OfftakeProposalReceipt>.Fail(InvalidProposal);
        }
        var conflict = DraftConflict;
        try
        {
            await using var transaction = await unitOfWork.BeginAsync(ct);
            if (await repository.ApprovedBuyerAsync(actor.UserId, ct) is not long buyerId)
            {
                return Result<OfftakeProposalReceipt>.Fail(Ineligible, 403);
            }
            var contract = await repository.FindManagedAsync(buyerId, id, ct);
            if (contract is null)
            {
                return Result<OfftakeProposalReceipt>.Fail(Unavailable, 404);
            }
            var revision = IsNegotiating(contract);
            if (revision) conflict = RevisionConflict;
            var current = EditableVersion(contract, revision);
            var original = EditableProposal(contract, current, revision);
            var snapshot = Snapshot(request);
            var hash = Hash(snapshot);
            var receipt = new OfftakeProposalReceipt(id, contract.ContractNumber);
            if (submit && contract.Status == "PendingApproval"
                && contract.CurrentVersionNo - (long)version is 0 or 1
                && contract.ContractVersions.Any(v => v.VersionNo == contract.CurrentVersionNo
                    && v.DocumentHash == hash && v.ContractSignatures.Any(s =>
                        s.SignedByUserId == actor.UserId && s.ConfirmationStatus == Confirmed)))
            {
                return Result<OfftakeProposalReceipt>.Ok(receipt);
            }
            if (current is null || current.VersionNo != version || version == int.MaxValue
                || original?.RequestId != request.RequestId
                || revision && (!submit || original.FarmerId != request.FarmerId
                    || original.ProductId != request.ProductId)
                || await repository.HasCommitmentsAsync(id, ct, editingRevision: revision))
            {
                return Result<OfftakeProposalReceipt>.Fail(conflict, 409);
            }
            if (!OfftakeProposalValidator.HasCompleteArticles(request))
            {
                return Result<OfftakeProposalReceipt>.Fail(IncompleteArticles);
            }
            if (current.ContractLines.Count != 1 || current.ContractMilestones.Count != 1)
            {
                return Result<OfftakeProposalReceipt>.Fail(conflict, 409);
            }
            var product = await repository.FindProductAsync(request.FarmerId, request.ProductId, ct);
            if (product is null || !SupportsQuantityUnit(product, request))
            {
                return Result<OfftakeProposalReceipt>.Fail(InvalidProposal);
            }

            var now = clock.GetUtcNow().UtcDateTime;
            if (revision || current.DocumentHash != hash)
            {
                contract.FarmerProfileId = product.FarmerId;
                contract.DepositAmount = request.DepositAmount;
                contract.PaymentTerms = request.PaymentTerms?.Trim();
                contract.ValidFrom = request.ValidFrom;
                contract.ValidUntil = request.ValidUntil;
                contract.CurrentVersionNo++;
                if (revision)
                {
                    current = AddProposalVersion(contract, request, product, snapshot, hash, now);
                }
                else
                {
                    current.VersionNo = contract.CurrentVersionNo;
                    current.TermsAndConditions = request.DeliveryTerms!.Trim();
                    current.LineSnapshotJson = snapshot;
                    current.DocumentHash = hash;
                    var line = current.ContractLines.Single();
                    line.ProductId = product.Id;
                    line.UnitSnapshot = product.Unit;
                    line.CommittedQuantity = request.Quantity;
                    line.AgreedPrice = request.UnitPrice;
                    line.CropSeasonId = null;
                    var milestone = current.ContractMilestones.Single();
                    milestone.DueDate = request.DeliveryDate;
                    milestone.DeliveryQuantity = request.Quantity;
                    milestone.Amount = OfftakeProposalValidator.Total(request);
                }
                await unitOfWork.SaveChangesAsync(ct);
            }
            if (submit)
            {
                await SubmitAsync(contract, current, now, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return Result<OfftakeProposalReceipt>.Ok(receipt,
                submit ? $"MSG34 – Đã gửi đề xuất tới {product.Farmer}." : "Đã lưu bản nháp.");
        }
        catch (Exception ex) when (errors.IsConflict(ex))
        {
            return Result<OfftakeProposalReceipt>.Fail(conflict, 409);
        }
    }

    public async Task<Result<OfftakeProposalReceipt>> CancelAsync(long id, CancellationToken ct)
    {
        const string conflict = "Chỉ có thể hủy đề xuất chưa có cam kết, giao hàng hoặc thanh toán.";
        try
        {
            await using var transaction = await unitOfWork.BeginAsync(ct);
            if (await repository.ApprovedBuyerAsync(actor.UserId, ct) is not long buyerId)
            {
                return Result<OfftakeProposalReceipt>.Fail(Ineligible, 403);
            }
            var contract = await repository.FindManagedAsync(buyerId, id, ct);
            if (contract is null)
            {
                return Result<OfftakeProposalReceipt>.Fail(Unavailable, 404);
            }
            var receipt = new OfftakeProposalReceipt(id, contract.ContractNumber);
            if (contract.Status == "Cancelled")
            {
                return Result<OfftakeProposalReceipt>.Ok(receipt, "Đã hủy hợp đồng.");
            }
            if (contract.Status is not ("Draft" or "PendingApproval" or "UnderNegotiation"
                or "Negotiating") || await repository.HasCommitmentsAsync(id, ct))
            {
                return Result<OfftakeProposalReceipt>.Fail(conflict, 409);
            }

            contract.Status = "Cancelled";
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result<OfftakeProposalReceipt>.Ok(receipt, "Đã hủy hợp đồng.");
        }
        catch (Exception ex) when (errors.IsConflict(ex))
        {
            return Result<OfftakeProposalReceipt>.Fail(conflict, 409);
        }
    }

    public async Task<Result<PaymentQr>> RequestDepositAsync(long id, IPaymentQrProvider qrProvider, CancellationToken ct)
    {
        const string conflict = "Khoản đặt cọc chưa khả dụng. Vui lòng tải lại hợp đồng.";
        try
        {
            await using var transaction = await unitOfWork.BeginAsync(ct);
            if (await repository.ApprovedBuyerAsync(actor.UserId, ct) is not long buyerId)
                return Result<PaymentQr>.Fail(Ineligible, 403);
            var contract = await repository.FindManagedAsync(buyerId, id, ct);
            if (contract is null) return Result<PaymentQr>.Fail(Unavailable, 404);
            var version = contract.ContractVersions.SingleOrDefault(v => v.VersionNo == contract.CurrentVersionNo);
            if (contract.Status != "AwaitingDeposit" || version is not { VersionStatus: "Signed" }
                || contract.SignedDate is not DateTime signedAt
                || !version.ContractSignatures.Any(s => s.PartyRole == Buyer && OfftakeContractRules.Confirmed(s))
                || !version.ContractSignatures.Any(s => s.PartyRole == "Farmer" && OfftakeContractRules.Confirmed(s)))
                return Result<PaymentQr>.Fail(conflict, 409);

            var deposit = OfftakeContractRules.PrepareDeposit(contract, version, signedAt);
            var remaining = deposit is null ? 0 : deposit.Amount - deposit.PaidAmount;
            if (deposit is null || remaining <= 0) return Result<PaymentQr>.Fail(conflict, 409);
            if (decimal.Truncate(remaining) != remaining)
                return Result<PaymentQr>.Fail("Số tiền đặt cọc phải là số nguyên VND. Vui lòng liên hệ tài chính.", 409);
            if (deposit.ContractMilestoneId == 0) await unitOfWork.SaveChangesAsync(ct);
            var qr = qrProvider.Create(ContractPaymentRules.Reference(deposit.ContractMilestoneId),
                remaining, clock.GetUtcNow().UtcDateTime.AddMinutes(15));
            if (qr is null) return Result<PaymentQr>.Fail("Chưa cấu hình tài khoản nhận tiền đặt cọc.", 503);

            await transaction.CommitAsync(ct);
            return Result<PaymentQr>.Ok(qr);
        }
        catch (Exception ex) when (errors.IsConflict(ex))
        {
            return Result<PaymentQr>.Fail(conflict, 409);
        }
    }

    public async Task<Result<PaymentQr>> DeliveryPaymentAsync(long id, long milestoneId, IPaymentQrProvider qrProvider,
        CancellationToken ct)
    {
        if (await repository.ApprovedBuyerAsync(actor.UserId, ct) is not long buyerId)
            return Result<PaymentQr>.Fail(Ineligible, 403);
        var contract = await repository.FindManagedAsync(buyerId, id, ct);
        if (contract is null) return Result<PaymentQr>.Fail(Unavailable, 404);
        var milestone = contract.ContractVersions.SingleOrDefault(v => v.VersionNo == contract.CurrentVersionNo)
            ?.ContractMilestones.SingleOrDefault(m => m.ContractMilestoneId == milestoneId);
        if (contract.Status != "Active" || milestone is null || !milestone.Invoices.Any(i =>
                i.InvoiceNumber.StartsWith("OFT-INV-", StringComparison.Ordinal) && i.InvoiceStatus is ("Issued" or "PartiallyPaid")))
            return Result<PaymentQr>.Fail("Hóa đơn giao hàng chưa sẵn sàng thanh toán.", 409);
        var due = milestone.Amount - milestone.PaidAmount;
        if (due <= 0 || due != decimal.Truncate(due))
            return Result<PaymentQr>.Fail("Số tiền còn lại không thể thanh toán qua QR. Vui lòng liên hệ tài chính.", 409);
        var qr = qrProvider.Create(ContractPaymentRules.Reference(milestoneId), due, clock.GetUtcNow().UtcDateTime.AddMinutes(15));
        return qr is null ? Result<PaymentQr>.Fail("Chưa cấu hình tài khoản nhận tiền.", 503) : Result<PaymentQr>.Ok(qr);
    }

    public async Task<Result<OfftakeInspectionResult>> InspectAsync(long id, long shipmentId,
        OfftakeInspectionRequest input, IFarmerComplaintFiles files, CancellationToken ct)
    {
        if (input.Items is not { Count: >= 1 and <= 50 }
            || input.Items.Any(x => x is null || x.ItemId <= 0 || x.Accepted < 0 || x.Received < x.Accepted
                || decimal.Round(x.Received, 3) != x.Received || decimal.Round(x.Accepted, 3) != x.Accepted
                || !OfftakeContractRules.Text(x.Grade, 255))
            || input.Items.Select(x => x.ItemId).Distinct().Count() != input.Items.Count
            || input.Reason?.Length > 500 || input.Evidence?.Count > 5)
            return Result<OfftakeInspectionResult>.Fail("Thông tin kiểm nhận không hợp lệ.");

        const string conflict = "Lô giao đã được kiểm nhận hoặc hợp đồng chưa cho phép kiểm nhận.";
        try
        {
            await using var transaction = await unitOfWork.BeginAsync(ct);
            if (await repository.ApprovedBuyerAsync(actor.UserId, ct) is not long buyerId)
                return Result<OfftakeInspectionResult>.Fail(Ineligible, 403);
            var shipment = await repository.FindShipmentForInspectionAsync(buyerId, id, shipmentId, ct);
            if (shipment is null) return Result<OfftakeInspectionResult>.Fail(Unavailable, 404);
            var milestone = shipment.ContractMilestone!;
            var contract = milestone.ContractVersion.B2bcontract;
            var seller = shipment.SellerOrder;
            var items = shipment.ShipmentItems.ToDictionary(x => x.ShipmentItemId);
            if (contract.Status != "Active" || contract.CurrentVersionNo != milestone.ContractVersion.VersionNo
                || !OfftakeContractRules.CanInspect(shipment))
                return Result<OfftakeInspectionResult>.Fail(conflict, 409);
            if (input.Items.Count != items.Count || input.Items.Any(x =>
                !items.TryGetValue(x.ItemId, out var item) || x.Received > item.ShippedQuantity))
                return Result<OfftakeInspectionResult>.Fail("Hãy kiểm nhận đủ các lô và nhập số lượng trong giới hạn đã giao.");

            var mismatch = !string.IsNullOrWhiteSpace(input.Reason) || input.Items.Any(x => x.Accepted < items[x.ItemId].ShippedQuantity);
            if (mismatch)
            {
                if (!OfftakeContractRules.Text(input.Reason, 500) || input.Evidence is not { Count: > 0 }
                    || input.Evidence.Distinct().Count() != input.Evidence.Count)
                    return Result<OfftakeInspectionResult>.Fail("Vui lòng nhập lý do và đính kèm bằng chứng chênh lệch.");
                foreach (var url in input.Evidence)
                    if (string.IsNullOrWhiteSpace(url) || !await files.IsValidOwnedAsync(actor.UserId, url, ct))
                        return Result<OfftakeInspectionResult>.Fail("Tệp bằng chứng không hợp lệ.");
            }

            var now = clock.GetUtcNow().UtcDateTime;
            foreach (var line in input.Items)
            {
                var item = items[line.ItemId];
                item.ReceivedQuantity = line.Received;
                item.AcceptedQuantity = line.Accepted;
                item.QualityResult = line.Grade.Trim();
                item.ReconciliationStatus = mismatch ? "Disputed" : "Accepted";
                item.ReconciledAt = mismatch ? null : now;
            }
            shipment.ReceivedAt = now;
            shipment.DeliveredAt ??= now;
            shipment.ShipmentStatus = "Received";
            seller.ReceivedAt = now;
            seller.Status = "Received";
            seller.Order.OrderStatus = "Received";

            DisputeTicket? ticket = null;
            if (mismatch)
            {
                ticket = new DisputeTicket
                {
                    B2bcontractId = id, SubmittedByUserId = actor.UserId, CreatedAt = now,
                    DisputeReason = "ContractDeliveryDiscrepancy", Description = input.Reason!.Trim(), TicketStatus = "Open"
                };
                foreach (var url in input.Evidence!)
                    ticket.DisputeEvidences.Add(new DisputeEvidence { SubmittedByUserId = actor.UserId, FileUrl = url, SubmittedAt = now });
                seller.DisputeTickets.Add(ticket);
            }
            else
            {
                FinalizeInspection(milestone, input.Items.Sum(x => x.Accepted), now);
            }

            repository.AddNotification(new Notification
            {
                UserId = await repository.FarmerUserIdAsync(contract.FarmerProfileId, ct),
                Title = $"Kết quả kiểm nhận hợp đồng {contract.ContractNumber}",
                Content = mismatch ? "Bên mua đã ghi nhận chênh lệch. Mở hợp đồng để xem bằng chứng và phản hồi."
                    : "Bên mua đã xác nhận lô giao đạt yêu cầu.",
                NotificationType = "FarmerOfftakeContract", DeliveryChannel = "InApp", DeliveryStatus = "Sent",
                RelatedRecordType = "B2BContract", RelatedRecordId = id, SentAt = now
            });
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result<OfftakeInspectionResult>.Ok(new(ticket?.DisputeTicketId),
                mismatch ? "Đã gửi chênh lệch cho nông dân phản hồi." : "Đã lưu kết quả kiểm nhận.");
        }
        catch (Exception ex) when (errors.IsConflict(ex))
        {
            return Result<OfftakeInspectionResult>.Fail(conflict, 409);
        }
    }

    public async Task<Result> DecideDiscrepancyAsync(long id, long shipmentId, long ticketId,
        OfftakeDiscrepancyDecision input, CancellationToken ct)
    {
        if (input.Accept ? input.ReplyAt == null : !OfftakeContractRules.Text(input.Reason, 500))
            return Result.Fail("Chọn phản hồi của nông dân hoặc nhập lý do chuyển xem xét.");
        const string conflict = "Hồ sơ hoặc phản hồi đã thay đổi. Vui lòng tải lại.";
        try
        {
            await using var transaction = await unitOfWork.BeginAsync(ct);
            if (await repository.ApprovedBuyerAsync(actor.UserId, ct) is not long buyerId)
                return Result.Fail(Ineligible, 403);
            var shipment = await repository.FindShipmentForInspectionAsync(buyerId, id, shipmentId, ct);
            if (shipment is null) return Result.Fail(Unavailable, 404);
            var milestone = shipment.ContractMilestone!;
            var contract = milestone.ContractVersion.B2bcontract;
            var ticket = shipment.SellerOrder.DisputeTickets.SingleOrDefault(t => t.DisputeTicketId == ticketId
                && t.B2bcontractId == id && t.DisputeReason == "ContractDeliveryDiscrepancy");
            var disputed = shipment.ShipmentItems.Where(i => i.ReconciliationStatus == "Disputed").ToList();
            if (contract.Status != "Active" || milestone.ContractVersion.VersionNo != contract.CurrentVersionNo
                || ticket?.TicketStatus != "Open" || disputed.Count == 0) return Result.Fail(conflict, 409);
            var now = clock.GetUtcNow().UtcDateTime;
            var recipients = new List<long> { await repository.FarmerUserIdAsync(contract.FarmerProfileId, ct) };
            if (input.Accept)
            {
                var replies = await repository.DiscrepancyRepliesAsync(id, ticketId, ct);
                if (replies.FirstOrDefault()?.At != input.ReplyAt) return Result.Fail(conflict, 409);
                var byItem = replies.DistinctBy(r => r.ShipmentItemId).ToDictionary(r => r.ShipmentItemId);
                if (disputed.Any(i => !byItem.TryGetValue(i.ShipmentItemId, out var reply)
                    || i.ReceivedQuantity == null || (reply.Quantity ?? i.AcceptedQuantity) is not decimal quantity
                    || quantity < 0 || quantity > i.ReceivedQuantity))
                    return Result.Fail("Cần phản hồi hợp lệ cho từng lô hàng có chênh lệch trước khi chốt.", 409);
                foreach (var line in disputed)
                {
                    line.AcceptedQuantity = byItem[line.ShipmentItemId].Quantity ?? line.AcceptedQuantity;
                    line.ReconciliationStatus = "Resolved";
                    line.ReconciledAt = now;
                }
                ticket.TicketStatus = "Resolved";
                ticket.ResolvedAt = now;
                ticket.ResolutionNotes = "Hai bên thống nhất số lượng nghiệm thu theo phản hồi của từng lô.";
                FinalizeInspection(milestone, disputed.Sum(i => i.AcceptedQuantity!.Value), now);
            }
            else
            {
                var reviewers = await repository.OfftakeReviewersAsync(ct);
                if (reviewers.Count == 0) return Result.Fail("Chưa có nhân viên hỗ trợ để tiếp nhận hồ sơ.", 409);
                recipients.AddRange(reviewers);
                ticket.TicketStatus = "UnderReview";
                ticket.ResolutionNotes = input.Reason!.Trim();
            }
            foreach (var recipient in recipients.Distinct())
                repository.AddNotification(new Notification
                {
                    UserId = recipient, Title = $"Cập nhật chênh lệch hợp đồng {contract.ContractNumber}",
                    Content = ticket.ResolutionNotes, SentAt = now, NotificationType = "FarmerOfftakeContract",
                    DeliveryChannel = "InApp", DeliveryStatus = "Sent", RelatedRecordType = "B2BContract", RelatedRecordId = id
                });
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return Result.Ok(input.Accept ? "Đã chốt số lượng nghiệm thu." : "Đã chuyển hồ sơ cho nhân viên hỗ trợ.");
        }
        catch (Exception ex) when (errors.IsConflict(ex))
        {
            return Result.Fail(conflict, 409);
        }
    }

    private static void FinalizeInspection(ContractMilestone milestone, decimal accepted, DateTime now)
    {
        milestone.AcceptedQuantity += accepted;
        milestone.FulfillmentStatus = milestone.AcceptedQuantity >= milestone.DeliveryQuantity ? "Delivered" : "PartiallyShipped";
        if (milestone.MilestoneType != "DeliveryAndPayment" || milestone.DeliveredQuantity < milestone.DeliveryQuantity
            || milestone.Invoices.Count != 0 || milestone.PaidAmount != 0) return;
        var items = milestone.Shipments.SelectMany(s => s.ShipmentItems).ToList();
        if (items.Any(i => i.ReconciliationStatus is not ("Accepted" or "Resolved")
                || i.OrderItemBatchAllocation.OrderItem.ContractLineId != milestone.ContractLineId)
            || milestone.ContractVersion.ContractLines.SingleOrDefault(l => l.ContractLineId == milestone.ContractLineId)
                is not { PriceType: "Fixed", AgreedPrice: > 0 })
            return;

        var invoice = new Invoice { InvoiceNumber = $"OFT-INV-{milestone.ContractMilestoneId}",
            IssuedAt = now, DueAt = milestone.DueDate.ToDateTime(TimeOnly.MinValue), InvoiceStatus = "Issued" };
        foreach (var item in items.Where(i => i.AcceptedQuantity > 0))
        {
            var orderItem = item.OrderItemBatchAllocation.OrderItem;
            invoice.InvoiceLines.Add(new InvoiceLine
            {
                ShipmentItem = item, Quantity = item.AcceptedQuantity!.Value, UnitPriceSnapshot = orderItem.UnitPriceSnapshot,
                LineAmount = decimal.Round(item.AcceptedQuantity.Value * orderItem.UnitPriceSnapshot, 2, MidpointRounding.AwayFromZero)
            });
        }
        invoice.TotalAmount = invoice.InvoiceLines.Sum(l => l.LineAmount);
        if (invoice.TotalAmount <= 0) return;
        var remaining = invoice.TotalAmount;
        foreach (var allocation in milestone.ContractDepositAllocations.Where(a =>
                     OfftakeContractRules.Verified(a.PaymentTransaction.PaymentStatus) && a.PaymentTransaction.PaidAt != null))
        {
            var amount = Math.Min(remaining, allocation.Amount);
            if (amount <= 0) break;
            invoice.PaymentInvoiceAllocations.Add(new PaymentInvoiceAllocation
            {
                PaymentTransaction = allocation.PaymentTransaction, ContractDepositAllocation = allocation,
                Amount = amount, CreatedAt = now, RequestKey = Guid.NewGuid().ToString("N")
            });
            remaining -= amount;
        }
        milestone.Amount = remaining;
        milestone.PaymentStatus = remaining == 0 ? "Paid" : "Unpaid";
        invoice.InvoiceStatus = milestone.PaymentStatus == "Unpaid" ? "Issued" : milestone.PaymentStatus;
        milestone.Invoices.Add(invoice);
    }

    public async Task<Result<OfftakeTrackingPage>> TrackAsync(bool orders,
        OfftakeTrackingQuery query, CancellationToken ct)
    {
        if (query.Page is < 1 or > 1000000 || query.PageSize is < 1 or > 100
            || query.Keyword?.Length > 100 || query.Product?.Length > 100
            || query.Status?.Length > 50 || query.From > query.To || query.To == DateOnly.MaxValue
            || query.From == DateOnly.MinValue)
        {
            return Result<OfftakeTrackingPage>.Fail("Bộ lọc không hợp lệ.");
        }

        var page = await repository.TrackAsync(actor.UserId, orders, query, ct);
        return Result<OfftakeTrackingPage>.Ok(page);
    }

    public async Task<Result<OfftakeTrackingDetail>> DetailAsync(bool orders, long id,
        CancellationToken ct)
    {
        var detail = id <= 0 ? null : await repository.DetailAsync(actor.UserId, orders, id, ct);
        return detail is null ? Result<OfftakeTrackingDetail>.Fail(Unavailable, 404)
            : Result<OfftakeTrackingDetail>.Ok(detail);
    }

    public async Task<Result<OfftakeTrackingDetail>> FarmerDetailAsync(long id,
        CancellationToken ct)
    {
        var detail = id <= 0 ? null : await repository.FarmerDetailAsync(actor.UserId, id, ct);
        return detail is null ? Result<OfftakeTrackingDetail>.Fail(Unavailable, 404)
            : Result<OfftakeTrackingDetail>.Ok(detail);
    }

    public static string ProposalReviewPath(long id) => $"/Farmer/offtake-proposal.html?id={id}";

    private void RecordBuyerConfirmation(ContractVersion version, DateTime now)
    {
        version.ContractSignatures.Add(new ContractSignature
        {
            SignedByUserId = actor.UserId, PartyRole = Buyer, ConfirmationStatus = Confirmed,
            ConfirmationMethod = "InApp", ConfirmedAt = now
        });
    }

    private static bool IsNegotiating(B2bcontract contract) =>
        contract.Status is "Negotiating" or "UnderNegotiation";

    private ContractVersion? EditableVersion(B2bcontract contract, bool revision)
    {
        if (!revision && contract.Status != "Draft") return null;
        var proposer = revision ? contract.FarmerProfile.UserId : actor.UserId;
        return contract.ContractVersions.SingleOrDefault(v =>
            v.VersionNo == contract.CurrentVersionNo && v.ProposedByUserId == proposer
            && v.VersionStatus == "Pending" && (revision
                ? !v.ContractSignatures.Any(s => (s.PartyRole is Buyer or "Buyer") && s.ConfirmationStatus == Confirmed)
                : v.ContractSignatures.Count == 0));
    }

    private OfftakeProposalRequest? EditableProposal(B2bcontract contract, ContractVersion? current, bool revision)
    {
        if (!revision || current is null) return ReadSnapshot(current);
        // Farmer amendments store the requested changes separately from the buyer's proposal fields.
        return contract.ContractVersions
            .Where(v => v.VersionNo < current.VersionNo && v.ProposedByUserId == actor.UserId)
            .OrderByDescending(v => v.VersionNo).Select(ReadSnapshot).FirstOrDefault(p => p is not null);
    }

    private ContractVersion AddProposalVersion(B2bcontract contract, OfftakeProposalRequest request,
        OfftakeProduct product, string snapshot, string hash, DateTime now)
    {
        var version = new ContractVersion
        {
            VersionNo = contract.CurrentVersionNo, ProposedByUserId = actor.UserId, VersionStatus = "Pending",
            TermsAndConditions = request.DeliveryTerms!.Trim(),
            LineSnapshotJson = snapshot, DocumentHash = hash, CreatedAt = now
        };
        var line = new ContractLine
        {
            ProductId = product.Id, UnitSnapshot = product.Unit,
            CommittedQuantity = request.Quantity, PriceType = "Fixed",
            AgreedPrice = request.UnitPrice, ContractVersion = version
        };
        contract.ContractVersions.Add(version);
        contract.ContractLines.Add(line);
        contract.ContractMilestones.Add(new ContractMilestone
        {
            ContractLine = line, ContractVersion = version, MilestoneType = "DeliveryAndPayment",
            MilestoneTitle = "Giao hàng theo đề xuất", DueDate = request.DeliveryDate,
            DeliveryQuantity = request.Quantity, Amount = OfftakeProposalValidator.Total(request),
            FulfillmentStatus = "Pending", PaymentStatus = "Unpaid"
        });
        return version;
    }

    private async Task SubmitAsync(B2bcontract contract, ContractVersion version, DateTime now,
        CancellationToken ct)
    {
        RecordBuyerConfirmation(version, now);
        contract.Status = "PendingApproval";
        var contractId = contract.B2bcontractId;
        var farmerUserId = await repository.FarmerUserIdAsync(contract.FarmerProfileId, ct);
        var content = $"Bạn nhận được đề xuất hợp đồng Offtake {contract.ContractNumber} "
            + $"(phiên bản {version.VersionNo}). Vui lòng đăng nhập AgriSustain để xem điều khoản.";
        var payload = JsonSerializer.Serialize(new
        {
            EventKey = $"offtake-proposal:{contractId}:{version.VersionNo}",
            ReviewPath = ProposalReviewPath(contractId),
            VersionNo = version.VersionNo
        });
        foreach (var channel in new[] { "InApp", "Email" })
        {
            repository.AddNotification(new Notification
            {
                UserId = farmerUserId,
                Title = $"[AgriSustain] Đề xuất Offtake {contract.ContractNumber}",
                Content = content,
                NotificationType = ProposalNotificationType,
                DeliveryChannel = channel,
                DeliveryStatus = channel == "Email" ? "Pending" : "Sent",
                SentAt = now,
                RelatedRecordType = "B2BContract",
                RelatedRecordId = contractId,
                PayloadJson = payload
            });
        }
    }

    private static OfftakeProposalRequest? ReadSnapshot(ContractVersion? version)
    {
        if (string.IsNullOrWhiteSpace(version?.LineSnapshotJson))
        {
            return null;
        }
        try
        {
            var proposal = JsonSerializer.Deserialize<OfftakeProposalRequest>(
                version.LineSnapshotJson);
            return proposal is not null && OfftakeProposalValidator.IsValid(proposal)
                ? proposal : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool SupportsQuantityUnit(OfftakeProduct product, OfftakeProposalRequest request) =>
        request.QuantityUnit is null || product.QuantityUnits.Any(unit => unit.Unit == request.QuantityUnit);

    private static string Snapshot(OfftakeProposalRequest request) =>
        JsonSerializer.Serialize(request with
        {
            DeliveryTerms = request.DeliveryTerms?.Trim(), PaymentTerms = request.PaymentTerms?.Trim()
        });

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
