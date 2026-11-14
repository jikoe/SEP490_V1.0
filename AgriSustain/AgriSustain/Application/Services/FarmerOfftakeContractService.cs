using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class FarmerOfftakeContractService(
    IFarmerOfftakeContractRepository repository,
    OfftakeContractRules rules,
    IFarmerComplaintFiles files)
{
    private static FeatureResult<T> Fail<T>(string code) => FeatureResult<T>.Fail(
        code == "MSG121" ? "FORBIDDEN" : code is "MSG122" or "MSG163" ? "CONFLICT" : code,
        FarmerBusinessMessages.Text(code));

    public async Task<FeatureResult<object>> GetContractsAsync(long actor, ContractQuery query, CancellationToken ct)
    {
        if (!await Authorized(actor, ct)) return Fail<object>("MSG121");
        if (query.Page < 1 || query.Page > 100000 || query.Keyword?.Length > 150 || query.Status?.Length > 50)
            return Fail<object>("MSG120");
        if (query.From > query.To) return Fail<object>("MSG111");
        if (query.To == DateOnly.MaxValue) return Fail<object>("MSG120");
        return FeatureResult<object>.Ok(await repository.SearchOwnedAsync(actor, query, ct));
    }

    public async Task<FeatureResult<object>> GetContextAsync(long actor, long id, CancellationToken ct)
    {
        if (!await Authorized(actor, ct)) return Fail<object>("MSG121");
        var context = await repository.LoadContextAsync(actor, id, false, ct);
        if (context is null) return Fail<object>("MSG121");
        return FeatureResult<object>.Ok(Details(context));
    }

    public Task<FeatureResult<ContractActionResult>> ConfirmTermsAsync(long actor, long id,
        ContractTermsResponseRequest input, CancellationToken ct) => Execute(actor, id, "Confirm", input, context =>
    {
        var error = rules.ValidateConfirmation(context, input.CropSeasonId, input.VersionNo);
        if (error != null) return error;
        var version = OfftakeContractRules.Current(context);
        if (version.ContractSignatures.Any(x => x.PartyRole == "Farmer" && OfftakeContractRules.Confirmed(x))) return "MSG122";
        foreach (var line in version.ContractLines) line.CropSeasonId = input.CropSeasonId;
        var signature = version.ContractSignatures.SingleOrDefault(x => x.PartyRole == "Farmer");
        if (signature == null)
        {
            signature = new ContractSignature { SignedByUserId = actor, PartyRole = "Farmer", ConfirmationMethod = "InSystem" };
            version.ContractSignatures.Add(signature);
        }
        signature.SignedByUserId = actor;
        signature.RecordConfirmation(DateTime.UtcNow);
        bool both = version.ContractSignatures.Any(x => x.PartyRole == "WholesaleBuyer" && OfftakeContractRules.Confirmed(x));
        context.Contract.DigitalSignatureStatus = both ? "FullySigned" : "PartiallySigned";
        if (!both) return "MSG151";
        context.Contract.SignedDate = DateTime.UtcNow;
        version.VersionStatus = "Signed";
        context.Contract.Status = rules.CanExecute(context) ? "Active" : "AwaitingDeposit";
        if (context.Contract.Status == "AwaitingDeposit")
            OfftakeContractRules.PrepareDeposit(context.Contract, version, context.Contract.SignedDate.Value);
        return context.Contract.Status == "Active" ? "MSG153" : "MSG152";
    }, ct);

    public Task<FeatureResult<ContractActionResult>> RejectProposalAsync(long actor, long id,
        ContractActionRequest input, CancellationToken ct) => Execute(actor, id, "Reject", input, context =>
    {
        if (!OfftakeContractRules.Feedback(input.Reason)) return "MSG109";
        if (context.Contract.Status is not ("PendingApproval" or "Proposed" or "Pending" or "PendingConfirmation" or "Negotiating")) return "MSG131";
        context.Contract.RejectProposal(input.Reason!.Trim());
        return "MSG105";
    }, ct);

    public Task<FeatureResult<ContractActionResult>> RequestAmendmentAsync(long actor, long id,
        AmendContractRequest input, CancellationToken ct) => Execute(actor, id, "Amend", input, context =>
    {
        if (!OfftakeContractRules.Feedback(input.Reason)) return "MSG109";
        if (!OfftakeContractRules.Text(input.ProposedChanges, 10000)) return "MSG02";
        if (context.Contract.Status is "AwaitingDeposit" or "Rejected" or "Terminated" or "Completed") return "MSG131";
        if (!rules.PreserveAcceptedCommitments(context)) return "MSG130";
        var old = OfftakeContractRules.Current(context);
        var revision = new ContractVersion
        {
            VersionNo = context.Contract.CurrentVersionNo + 1,
            ProposedByUserId = actor,
            CreatedAt = DateTime.UtcNow,
            VersionStatus = "Pending",
            FeedbackReason = input.Reason!.Trim(),
            TermsAndConditions = OfftakeContractRules.SplitAmendment(old.TermsAndConditions).Terms
                + OfftakeContractRules.AmendmentHeading + input.ProposedChanges.Trim(),
            LineSnapshotJson = JsonSerializer.Serialize(old.ContractLines.Select(x => new
            { x.ProductId, x.CommittedQuantity, x.UnitSnapshot, x.AgreedPrice, x.PriceFormula, x.CropSeasonId }))
        };
        foreach (var line in old.ContractLines)
        {
            var copy = new ContractLine
            {
                B2bcontractId = id,
                ProductId = line.ProductId,
                CropSeasonId = line.CropSeasonId,
                CommittedQuantity = line.CommittedQuantity,
                UnitSnapshot = line.UnitSnapshot,
                QualitySpecification = line.QualitySpecification,
                PriceType = line.PriceType,
                AgreedPrice = line.AgreedPrice,
                MinPrice = line.MinPrice,
                MaxPrice = line.MaxPrice,
                PriceFormula = line.PriceFormula
            };
            revision.ContractLines.Add(copy);
            foreach (var milestone in old.ContractMilestones.Where(x => x.ContractLineId == line.ContractLineId))
                revision.ContractMilestones.Add(CopyMilestone(milestone, id, copy));
        }
        foreach (var milestone in old.ContractMilestones.Where(x => x.ContractLineId == null))
            revision.ContractMilestones.Add(CopyMilestone(milestone, id, null));
        context.Contract.ContractVersions.Add(revision);
        context.Contract.CurrentVersionNo = revision.VersionNo;
        context.Contract.Status = "Negotiating";
        context.Contract.DigitalSignatureStatus = "Pending";
        context.Contract.SignedDate = null;
        return "MSG154";
    }, ct);

    private static ContractMilestone CopyMilestone(ContractMilestone source, long id, ContractLine? line) => new()
    {
        B2bcontractId = id,
        ContractLine = line,
        MilestoneTitle = source.MilestoneTitle,
        MilestoneType = source.MilestoneType,
        DueDate = source.DueDate,
        Amount = source.Amount,
        DeliveryQuantity = source.DeliveryQuantity,
        PaymentStatus = "Unpaid",
        FulfillmentStatus = "Pending"
    };

    public Task<FeatureResult<ContractActionResult>> DispatchDeliveryAsync(long actor, long id,
        DispatchContractDeliveryRequest input, CancellationToken ct) => Execute(actor, id, "Dispatch", input, context =>
    {
        if (rules.ValidateDispatch(context, input) is { } error) return error;
        var milestone = OfftakeContractRules.Milestones(context).Single(x => x.ContractMilestoneId == input.MilestoneId);
        var line = OfftakeContractRules.Lines(context).Single(x => x.ContractLineId == milestone.ContractLineId);
        decimal quantity = input.BatchQuantities.Sum(x => x.Quantity);
        var buyer = context.Contract.WholesaleBuyerProfile;
        string key = Guid.NewGuid().ToString("N");
        var order = new Order
        {
            BuyerUserId = buyer.UserId,
            OrderCode = "CT-" + key,
            OrderType = "Wholesale",
            OrderDate = DateTime.UtcNow,
            ShippingAddress = buyer.BusinessAddress,
            RecipientNameSnapshot = buyer.RepresentativeName,
            RecipientPhoneSnapshot = buyer.User.PhoneNumber ?? "",
            OrderStatus = "Shipping",
            CheckoutRequestKey = "contract:" + key,
            PaymentMethod = "Contract",
            DeliveryMethod = "Contract"
        };
        var seller = new SellerOrder
        {
            Order = order,
            FarmerProfileId = context.Contract.FarmerProfileId,
            B2bcontractId = id,
            SellerOrderCode = "CTF-" + key,
            SellerNameSnapshot = context.Contract.FarmerProfile.FarmName,
            Status = "Shipping"
        };
        // Formula/quality prices are finalized by recorded acceptance, never invented from retail prices.
        decimal price = line.AgreedPrice ?? 0;
        var item = new OrderItem
        {
            ProductId = line.ProductId,
            ContractLineId = line.ContractLineId,
            ProductNameSnapshot = line.Product.ProductName,
            Quantity = quantity,
            UnitSnapshot = line.UnitSnapshot,
            UnitPriceSnapshot = price,
            SubTotal = quantity * price,
            ItemStatus = "Shipping"
        };
        seller.OrderItems.Add(item);
        var shipment = new Shipment
        {
            ContractMilestoneId = milestone.ContractMilestoneId,
            ShipmentStatus = "Pending"
        };
        shipment.MarkDispatchedOnce(DateTime.UtcNow);
        int sequence = 0;
        foreach (var selected in input.BatchQuantities)
        {
            var batch = context.Batches.Single(x => x.HarvestBatchId == selected.HarvestBatchId);
            var allocation = new OrderItemBatchAllocation
            {
                OrderItem = item,
                HarvestBatchId = batch.HarvestBatchId,
                AllocatedQuantity = selected.Quantity,
                AllocationSequence = ++sequence,
                AllocationStatus = "Shipped",
                ReservedAt = DateTime.UtcNow
            };
            shipment.ShipmentItems.Add(new ShipmentItem
            {
                OrderItemBatchAllocation = allocation,
                ShippedQuantity = selected.Quantity,
                ReconciliationStatus = "Pending"
            });
            batch.AvailableQuantity -= selected.Quantity;
            batch.InventoryMovements.Add(new InventoryMovement
            {
                OrderItemBatchAllocation = allocation,
                RecordedByUserId = actor,
                MovementType = "StockOut",
                QuantityChange = -selected.Quantity,
                Reason = "Giao theo hợp đồng " + context.Contract.ContractNumber,
                OccurredAt = DateTime.UtcNow,
                RequestKey = "oc:" + input.RequestKey + ":" + batch.HarvestBatchId
            });
        }
        seller.Subtotal = seller.FinalAmount = order.TotalAmount = order.FinalAmount = item.SubTotal;
        seller.Shipments.Add(shipment);
        context.Contract.SellerOrders.Add(seller);
        milestone.DeliveredQuantity += quantity;
        milestone.FulfillmentStatus = milestone.DeliveredQuantity >= milestone.DeliveryQuantity ? "Shipped" : "PartiallyShipped";
        return "MSG155";
    }, ct);

    public Task<FeatureResult<ContractActionResult>> ReportDelayAsync(long actor, long id,
        ContractDelayRequest input, CancellationToken ct) => Execute(actor, id, "Delay", input, context =>
    {
        var milestone = OfftakeContractRules.Milestones(context).SingleOrDefault(x => x.ContractMilestoneId == input.MilestoneId);
        if (context.Contract.Status != "Active") return "MSG131";
        if (milestone == null || input.Quantity <= 0 || input.Quantity > (milestone.DeliveryQuantity ?? 0) - milestone.DeliveredQuantity
            || input.ProposedDate < DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)) || !OfftakeContractRules.Text(input.Reason)) return "MSG120";
        return "MSG145"; // Recorded in immutable history; agreed milestone stays unchanged.
    }, ct);

    public async Task<FeatureResult<ContractActionResult>> SubmitDiscrepancyResponseAsync(long actor, long id,
        ContractDiscrepancyResponseRequest input, CancellationToken ct)
    {
        if (input.Evidence == null || input.Evidence.Count == 0) return Fail<ContractActionResult>("MSG02");
        if (!await ValidFiles(actor, input.Evidence, ct)) return Fail<ContractActionResult>("MSG120");
        return await Execute(actor, id, "Discrepancy", input, context =>
        {
            var ticket = context.Disputes.SingleOrDefault(x => x.DisputeTicketId == input.TicketId);
            var item = context.Contract.SellerOrders.SelectMany(x => x.Shipments).SelectMany(x => x.ShipmentItems)
                .SingleOrDefault(x => x.ShipmentItemId == input.ShipmentItemId);
            if (ticket == null || !OfftakeContractRules.OpenDispute(ticket) || item?.ReceivedQuantity == null
                || (ticket.SellerOrderId.HasValue && item.Shipment.SellerOrderId != ticket.SellerOrderId)
                || (ticket.DisputeReason == "ContractDeliveryDiscrepancy" && item.ReconciliationStatus != "Disputed")) return "MSG131";
            if (!OfftakeContractRules.Text(input.Reason) || !OfftakeContractRules.Text(input.ProposedResolution, 500)
                || input.ProposedQuantity < 0 || input.ProposedQuantity > item.ReceivedQuantity
                || (input.ProposedQuantity is decimal proposed && decimal.Round(proposed, 3) != proposed)
                || item.ReceivedQuantity > item.ShippedQuantity) return "MSG120";
            foreach (string url in input.Evidence)
                ticket.DisputeEvidences.Add(new DisputeEvidence
                {
                    SubmittedByUserId = actor,
                    FileUrl = url,
                    Description = input.ProposedResolution.Trim(),
                    SubmittedAt = DateTime.UtcNow
                });
            return "MSG-OC-01";
        }, ct);
    }

    public Task<FeatureResult<ContractActionResult>> RequestReviewAsync(long actor, long id,
        ContractDiscrepancyResponseRequest input, CancellationToken ct) => Execute(actor, id, "Review", input, context =>
    {
        var ticket = context.Disputes.SingleOrDefault(x => x.DisputeTicketId == input.TicketId);
        if (ticket == null || !OfftakeContractRules.OpenDispute(ticket) || context.Reviewers.Count == 0) return "MSG131";
        if (!context.History.Any(x => x.Action == "Discrepancy" && x.NewValue != null
            && x.NewValue.Contains("\"TicketId\":" + input.TicketId + ",", StringComparison.Ordinal))) return "MSG131";
        ticket.TicketStatus = "UnderReview";
        return "MSG158";
    }, ct);

    public async Task<FeatureResult<ContractActionResult>> RequestTerminationAsync(long actor, long id,
        TerminateContractRequest input, CancellationToken ct)
    {
        if (input.Evidence == null || input.Evidence.Count == 0) return Fail<ContractActionResult>("MSG02");
        if (!await ValidFiles(actor, input.Evidence, ct)) return Fail<ContractActionResult>("MSG120");
        return await Execute(actor, id, "Termination", input, context =>
        {
            if (!OfftakeContractRules.Text(input.Reason) || input.SettlementProposal?.Length > 2000) return "MSG02";
            if (context.Contract.Status is "Rejected" or "Completed" or "Terminated") return "MSG131";
            if (context.Contract.ContractTerminationRequest != null) return "MSG122";
            if (context.Reviewers.Count == 0) return "MSG131";
            context.Contract.ContractTerminationRequest = new ContractTerminationRequest
            {
                RequestedByUserId = actor,
                Reason = input.Reason!.Trim(),
                EvidenceUrl = input.Evidence[0],
                RequestedAt = DateTime.UtcNow,
                RequestStatus = "Pending"
            };
            return "MSG107";
        }, ct);
    }

    public Task<FeatureResult<ContractActionResult>> GetFulfillmentProgressAsync(long actor, long id, CancellationToken ct) =>
        CompleteIfEligibleAsync(actor, id, ct);

    public async Task<FeatureResult<ContractActionResult>> CompleteIfEligibleAsync(long actor, long id, CancellationToken ct)
    {
        if (!await Authorized(actor, ct)) return Fail<ContractActionResult>("MSG121");
        return await repository.SaveAtomicAsync(actor, id, context =>
        {
            string code = context.Contract.Status == "Completed" ? "MSG159" : "MSG162";
            if (rules.CanComplete(context))
            {
                context.Contract.MarkCompleted(DateTime.UtcNow);
                // Recompute from recorded outcomes; repeated reads cannot increment the score.
                decimal shipped = OfftakeContractRules.Milestones(context).Sum(x => x.DeliveredQuantity);
                decimal accepted = OfftakeContractRules.Milestones(context).Sum(x => x.AcceptedQuantity);
                decimal outcomeScore = shipped > 0 ? decimal.Round(5 * Math.Min(accepted / shipped, 1), 2) : 0;
                decimal delta = outcomeScore - context.Contract.FarmerProfile.ReputationScore;
                context.Contract.FarmerProfile.ReputationScore = Math.Clamp((context.Contract.FarmerProfile.ReputationScore * context.CompletedContractCount + outcomeScore) / (context.CompletedContractCount + 1), 0, 5);
                repository.Record(new AuditLog
                {
                    PerformedByUserId = actor,
                    EntityType = "FarmerOfftakeContract",
                    EntityId = id,
                    Action = "Completed",
                    NewValue = JsonSerializer.Serialize(new { shipped, accepted, delta }),
                    OccurredAt = DateTime.UtcNow
                });
                Queue(context, "Completed", "MSG159", "complete:" + id, true);
                code = "MSG159";
            }
            return Task.FromResult(FeatureResult<ContractActionResult>.Ok(new(id, context.Contract.Status,
                FarmerBusinessMessages.Text(code), OfftakeContractRules.Version(context))));
        }, ct);
    }

    private async Task<FeatureResult<ContractActionResult>> Execute<T>(long actor, long id, string action,
        T input, Func<OfftakeContext, string> apply, CancellationToken ct) where T : ContractActionRequest
    {
        if (!Guid.TryParse(input.RequestKey, out _) || input.ExpectedVersion?.Length != 64) return Fail<ContractActionResult>("MSG120");
        if (!await Authorized(actor, ct)) return Fail<ContractActionResult>("MSG121");
        string payload = JsonSerializer.Serialize(input);
        string fingerprint = Hash(payload);
        string trace = Hash(actor + ":" + Guid.Parse(input.RequestKey).ToString("N"));
        return await repository.SaveAtomicAsync(actor, id, context =>
        {
            var previous = context.History.SingleOrDefault(x => x.TraceId == trace);
            if (previous != null)
            {
                var saved = JsonSerializer.Deserialize<SavedAction>(previous.NewValue!);
                return Task.FromResult(saved?.Fingerprint == fingerprint && previous.Action == action
                    ? FeatureResult<ContractActionResult>.Ok(new(id, context.Contract.Status, FarmerBusinessMessages.Text(saved.Code), OfftakeContractRules.Version(context)))
                    : Fail<ContractActionResult>("MSG122"));
            }
            if (OfftakeContractRules.Version(context) != input.ExpectedVersion) return Task.FromResult(Fail<ContractActionResult>("MSG163"));
            string code = apply(context);
            if (code is not ("MSG105" or "MSG107" or "MSG145" or "MSG151" or "MSG152" or "MSG153" or "MSG154" or "MSG155" or "MSG158" or "MSG-OC-01"))
                return Task.FromResult(Fail<ContractActionResult>(code));
            var audit = new AuditLog
            {
                EntityType = "FarmerOfftakeContract",
                EntityId = id,
                Action = action,
                PerformedByUserId = actor,
                NewValue = JsonSerializer.Serialize(new SavedAction(fingerprint, code, JsonSerializer.SerializeToElement(input))),
                Reason = input.Reason?.Trim() is { } reason ? reason[..Math.Min(reason.Length, 500)] : null,
                TraceId = trace,
                OccurredAt = DateTime.UtcNow
            };
            repository.Record(audit);
            Queue(context, action, code, input.RequestKey, false, input.Reason);
            return Task.FromResult(FeatureResult<ContractActionResult>.Ok(new(id, context.Contract.Status,
                (action == "Confirm" && code is "MSG152" or "MSG153" ? FarmerBusinessMessages.Text("MSG104") + " " : "")
                    + FarmerBusinessMessages.Text(code), OfftakeContractRules.Version(context))));
        }, ct);
    }

    private sealed record SavedAction(string Fingerprint, string Code, JsonElement Input);
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public Task<bool> CanUploadAsync(long actor, CancellationToken ct) => Authorized(actor, ct);

    public async Task<string?> GetEvidencePathAsync(long actor, long id, long evidenceId, CancellationToken ct)
    {
        if (!await Authorized(actor, ct)) return null;
        var context = await repository.LoadContextAsync(actor, id, false, ct);
        var evidence = context?.Disputes.SelectMany(x => x.DisputeEvidences)
            .SingleOrDefault(x => x.DisputeEvidenceId == evidenceId);
        return evidence == null ? null : files.Resolve(evidence.SubmittedByUserId, evidence.FileUrl);
    }

    private async Task<bool> Authorized(long actor, CancellationToken ct)
    {
        var farmer = await repository.FarmerAsync(actor, ct);
        return farmer?.User is { Role: "Farmer", Status: "Active" } && farmer.VerificationStatus == "Approved";
    }

    private async Task<bool> ValidFiles(long actor, List<string> evidence, CancellationToken ct)
    {
        if (evidence == null || evidence.Count is < 1 or > 5 || evidence.Distinct().Count() != evidence.Count) return false;
        foreach (string url in evidence)
            if (!await files.IsValidOwnedAsync(actor, url, ct)) return false;
        return true;
    }

    private void Queue(OfftakeContext context, string action, string code, string eventKey, bool includeFarmer, string? reason = null)
    {
        var contract = context.Contract;
        var recipients = new List<long> { contract.WholesaleBuyerProfile.UserId };
        if (includeFarmer) recipients.Add(contract.FarmerProfile.UserId);
        if (action is "Termination" or "Review") recipients.AddRange(context.Reviewers.Select(x => x.UserId));
        string content = $"Hợp đồng {contract.ContractNumber}, phiên bản {contract.CurrentVersionNo}. {FarmerBusinessMessages.Text(action == "Termination" ? "MSG106" : code)}";
        if (!string.IsNullOrWhiteSpace(reason)) content += "\n" + reason.Trim();
        repository.Queue(recipients.Distinct().SelectMany(user => new[] { "InApp", "Email" }.Select(channel => new Notification
        {
            UserId = user,
            Title = "Cập nhật hợp đồng bao tiêu " + contract.ContractNumber,
            Content = content,
            NotificationType = "FarmerOfftakeContract",
            DeliveryChannel = channel,
            DeliveryStatus = channel == "InApp" ? "Sent" : "Pending",
            SentAt = DateTime.UtcNow,
            RelatedRecordType = "B2BContract",
            RelatedRecordId = contract.B2bcontractId,
            PayloadJson = JsonSerializer.Serialize(new { EventKey = eventKey, Action = action })
        })));
    }

    private static object Details(OfftakeContext context)
    {
        var contract = context.Contract;
        var version = OfftakeContractRules.Current(context);
        var (terms, proposedChanges) = OfftakeContractRules.SplitAmendment(version.TermsAndConditions);
        return new
        {
            notificationFailed = context.NotificationFailed,
            id = contract.B2bcontractId,
            code = contract.ContractNumber,
            contract.Status,
            contract.CurrentVersionNo,
            contract.ValidFrom,
            contract.ValidUntil,
            contract.DepositAmount,
            depositPaid = OfftakeContractRules.DepositPaid(context),
            buyer = contract.WholesaleBuyerProfile.CompanyName,
            contract.PaymentTerms,
            termsAndConditions = terms,
            version.FeedbackReason,
            proposedChanges = contract.Status == "Rejected" ? null : proposedChanges,
            canConfirmTerms = OfftakeContractRules.CanConfirmProposal(version, contract.FarmerProfile.UserId),
            version = OfftakeContractRules.Version(context),
            signatures = version.ContractSignatures.Select(x => new { x.PartyRole, x.ConfirmationStatus, x.ConfirmedAt }),
            lines = version.ContractLines.Select(x => new
            {
                id = x.ContractLineId,
                x.ProductId,
                x.Product.ProductName,
                x.CropSeasonId,
                x.CommittedQuantity,
                x.UnitSnapshot,
                x.PriceType,
                x.AgreedPrice,
                x.MinPrice,
                x.MaxPrice,
                x.PriceFormula,
                x.QualitySpecification
            }),
            seasons = context.Seasons.Select(x => new { id = x.CropSeasonId, x.SeasonName, x.ProductId, x.EstimatedYield, x.EstimatedHarvestDate, x.Status }),
            batches = context.Batches.Select(x => new
            {
                id = x.HarvestBatchId,
                x.BatchCode,
                x.ProductId,
                x.CropSeasonId,
                x.ActualHarvestDate,
                x.AvailableQuantity,
                x.ReservedQuantity,
                x.Status,
                x.ExpirationDate,
                hasQr = x.TraceabilityQrcode is { IsRevoked: false }
            }),
            milestones = version.ContractMilestones.Select(x => new
            {
                id = x.ContractMilestoneId,
                x.MilestoneTitle,
                x.ContractLineId,
                x.DueDate,
                x.DeliveryQuantity,
                x.DeliveredQuantity,
                x.AcceptedQuantity,
                x.Amount,
                x.PaidAmount,
                x.PaymentStatus,
                x.FulfillmentStatus
            }),
            shipments = contract.SellerOrders.SelectMany(x => x.Shipments).Select(x => new
            {
                id = x.ShipmentId,
                x.SellerOrderId,
                x.ContractMilestoneId,
                x.ShipmentStatus,
                x.ShippedAt,
                x.ReceivedAt,
                items = x.ShipmentItems.Select(i => new
                {
                    id = i.ShipmentItemId,
                    i.ShippedQuantity,
                    i.ReceivedQuantity,
                    i.AcceptedQuantity,
                    i.QualityResult,
                    i.ReconciliationStatus
                })
            }),
            disputes = context.Disputes.Select(x => new
            {
                id = x.DisputeTicketId,
                x.SellerOrderId,
                x.DisputeReason,
                x.Description,
                x.TicketStatus,
                x.ResolutionNotes,
                x.RefundAmount,
                x.ResolvedAt,
                evidence = x.DisputeEvidences.Select(e => new { e.DisputeEvidenceId, e.SubmittedByUserId, e.Description, e.FileUrl })
            }),
            termination = contract.ContractTerminationRequest is not { } t ? null : new { t.RequestStatus, t.Reason, t.SettlementAmount, t.ResolvedAt },
            payments = contract.PaymentTransactions.Select(x => new { x.TransactionCode, x.PaymentPurpose, x.PaymentStatus, x.Amount, x.PaidAt, x.ProviderTransactionReference }),
            invoices = version.ContractMilestones.SelectMany(x => x.Invoices).Select(x => new { x.InvoiceNumber, x.InvoiceStatus, x.TotalAmount, x.IssuedAt }),
            payouts = context.Payouts.Select(x => new { x.PayoutCode, x.PayoutStatus, x.NetAmount, x.ProcessedAt, x.BankTransferRef }),
            history = context.History.Select(x => new { x.Action, x.Reason, x.OccurredAt, x.NewValue })
        };
    }
}
