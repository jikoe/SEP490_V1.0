using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Validators;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class OfftakeService(IOfftakeRepository repository, IUseCaseUnitOfWork unitOfWork,
    IActorContext actor, IPersistenceErrorClassifier errors, TimeProvider clock,
    AccountNotificationSettings support) : IOfftakeService
{
    public const string ProposalNotificationType = "OfftakeProposalSubmitted";
    private const string InvalidProposal = "MSG48 – Vui lòng kiểm tra thông tin đề xuất.";
    private const string IncompleteArticles = "Vui lòng nhập đầy đủ nội dung của cả 8 điều khoản.";
    private const string Ineligible = "Hồ sơ doanh nghiệp phải được duyệt để gửi đề xuất.";
    private const string Unavailable = "MSG54 – Hồ sơ hợp đồng hoặc đơn hàng không còn khả dụng.";
    private const string Confirmed = "Confirmed";
    private const string Buyer = "WholesaleBuyer";
    private const string DraftConflict = "Bản nháp đã thay đổi hoặc không còn chỉnh sửa được.";

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
        if (!OfftakeProposalValidator.IsValid(request))
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
            var version = new ContractVersion
            {
                VersionNo = 1, ProposedByUserId = actor.UserId, VersionStatus = "Pending",
                TermsAndConditions = request.DeliveryTerms?.Trim() ?? "",
                LineSnapshotJson = snapshot, DocumentHash = Hash(snapshot), CreatedAt = now
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
        var current = EditableVersion(contract);
        var proposal = ReadSnapshot(current);
        if (current is null || proposal is null)
        {
            return Result<OfftakeDraft>.Fail(DraftConflict, 409);
        }
        var product = await repository.FindProductAsync(proposal.FarmerId, proposal.ProductId, ct);
        return Result<OfftakeDraft>.Ok(new(contract.ContractNumber, current.VersionNo,
            contract.CreatedAt, proposal, product));
    }

    public async Task<Result<OfftakeProposalReceipt>> UpdateDraftAsync(long id, int version,
        OfftakeProposalRequest request, bool submit, CancellationToken ct)
    {
        if (!OfftakeProposalValidator.IsValid(request))
        {
            return Result<OfftakeProposalReceipt>.Fail(InvalidProposal);
        }
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
            var current = EditableVersion(contract);
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
                || ReadSnapshot(current)?.RequestId != request.RequestId
                || await repository.HasCommitmentsAsync(id, ct))
            {
                return Result<OfftakeProposalReceipt>.Fail(DraftConflict, 409);
            }
            if (!OfftakeProposalValidator.HasCompleteArticles(request))
            {
                return Result<OfftakeProposalReceipt>.Fail(IncompleteArticles);
            }
            var lines = contract.ContractLines.Where(x => x.ContractVersionId
                == current.ContractVersionId).Take(2).ToArray();
            var milestones = contract.ContractMilestones.Where(x => x.ContractVersionId
                == current.ContractVersionId).Take(2).ToArray();
            if (lines.Length != 1 || milestones.Length != 1)
            {
                return Result<OfftakeProposalReceipt>.Fail(DraftConflict, 409);
            }
            var product = await repository.FindProductAsync(request.FarmerId, request.ProductId, ct);
            if (product is null || !SupportsQuantityUnit(product, request))
            {
                return Result<OfftakeProposalReceipt>.Fail(InvalidProposal);
            }

            if (current.DocumentHash != hash)
            {
                contract.FarmerProfileId = product.FarmerId;
                contract.DepositAmount = request.DepositAmount;
                contract.PaymentTerms = request.PaymentTerms?.Trim();
                contract.ValidFrom = request.ValidFrom;
                contract.ValidUntil = request.ValidUntil;
                contract.CurrentVersionNo = ++current.VersionNo;
                current.TermsAndConditions = request.DeliveryTerms?.Trim() ?? "";
                current.LineSnapshotJson = snapshot;
                current.DocumentHash = hash;
                var line = lines[0];
                line.ProductId = product.Id;
                line.UnitSnapshot = product.Unit;
                line.CommittedQuantity = request.Quantity;
                line.AgreedPrice = request.UnitPrice;
                line.CropSeasonId = null;
                var milestone = milestones[0];
                milestone.DueDate = request.DeliveryDate;
                milestone.DeliveryQuantity = request.Quantity;
                milestone.Amount = OfftakeProposalValidator.Total(request);
                await unitOfWork.SaveChangesAsync(ct);
            }
            if (submit)
            {
                await SubmitAsync(contract, current, clock.GetUtcNow().UtcDateTime, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return Result<OfftakeProposalReceipt>.Ok(receipt,
                submit ? $"MSG34 – Đã gửi đề xuất tới {product.Farmer}." : "Đã lưu bản nháp.");
        }
        catch (Exception ex) when (errors.IsConflict(ex))
        {
            return Result<OfftakeProposalReceipt>.Fail(DraftConflict, 409);
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

    public async Task<Result<OfftakeProposalReceipt>> ConfirmRevisionAsync(long id, int version,
        CancellationToken ct)
    {
        try
        {
            await using var transaction = await unitOfWork.BeginAsync(ct);
            if (await repository.ApprovedBuyerAsync(actor.UserId, ct) is not long buyerId)
            {
                return Result<OfftakeProposalReceipt>.Fail(Ineligible, 403);
            }

            var contract = await repository.FindRevisionAsync(buyerId, id, ct);
            if (contract is null)
            {
                return Result<OfftakeProposalReceipt>.Fail(Unavailable, 404);
            }
            var current = contract.ContractVersions.SingleOrDefault(x => x.VersionNo == version);
            if (version <= 1 || contract.CurrentVersionNo != version || current is null
                || current.ProposedByUserId != contract.FarmerProfile.UserId)
            {
                return Result<OfftakeProposalReceipt>.Fail(
                    "MSG163 – Điều khoản đã thay đổi. Vui lòng tải lại.", 409);
            }
            var receipt = new OfftakeProposalReceipt(contract.B2bcontractId, contract.ContractNumber);
            var buyer = current.ContractSignatures.FirstOrDefault(x => x.PartyRole == Buyer)
                ?? current.ContractSignatures.FirstOrDefault(x => x.PartyRole == "Buyer");
            if (buyer?.ConfirmationStatus == Confirmed)
            {
                return Result<OfftakeProposalReceipt>.Ok(receipt);
            }
            if (current.VersionStatus != "Pending" || contract.Status is "Rejected" or "Cancelled"
                or "Terminated" or "Completed")
            {
                return Result<OfftakeProposalReceipt>.Fail("MSG131 – Chưa thể xác nhận phiên bản.", 409);
            }

            var now = clock.GetUtcNow().UtcDateTime;
            if (buyer is null)
            {
                RecordBuyerConfirmation(current, now);
            }
            else
            {
                buyer.ConfirmationStatus = Confirmed;
                buyer.ConfirmedAt = now;
                buyer.ConfirmationMethod = "InApp";
            }

            await unitOfWork.SaveChangesAsync(ct);
            if (current.ContractSignatures.Any(x =>
                x.PartyRole == "Farmer" && x.ConfirmationStatus == Confirmed))
            {
                current.VersionStatus = "Signed";
                contract.DigitalSignatureStatus = "FullySigned";
                contract.SignedDate = now;
                if (contract.Status is "Draft" or "PendingApproval" or "UnderNegotiation"
                    or "Negotiating" or "AwaitingDeposit")
                {
                    var verifiedDeposit = contract.PaymentTransactions.Sum(x => x.Amount);
                    contract.Status = verifiedDeposit >= contract.DepositAmount
                        ? "Active" : "AwaitingDeposit";
                }
                await unitOfWork.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return Result<OfftakeProposalReceipt>.Ok(receipt, "Đã xác nhận điều khoản sửa đổi.");
        }
        catch (Exception ex) when (errors.IsConflict(ex))
        {
            return Result<OfftakeProposalReceipt>.Fail(
                "MSG163 – Không thể xác nhận phiên bản. Vui lòng tải lại.", 409);
        }
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

    private ContractVersion? EditableVersion(B2bcontract contract) =>
        contract.Status == "Draft" ? contract.ContractVersions.SingleOrDefault(v =>
            v.VersionNo == contract.CurrentVersionNo && v.ProposedByUserId == actor.UserId
            && v.VersionStatus == "Pending" && v.ContractSignatures.Count == 0) : null;

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
