using System.Text.Json;
using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class OfftakeRepository(AgriSustainContext db) : IOfftakeRepository
{
    private static readonly string[] s_wholesaleOrderTypes = ["B2B", "Wholesale", "PreOrder"];

    public Task<long?> ApprovedBuyerAsync(long userId, CancellationToken ct) =>
        db.WholesaleBuyerProfiles.Where(x => x.UserId == userId
            && x.User.Status == "Active" && x.VerificationStatus == "Approved")
            .Select(x => (long?)x.WholesaleBuyerProfileId).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<OfftakeProduct>> CatalogAsync(string? keyword,
        CancellationToken ct)
    {
        var products = db.Products.AsNoTracking().Where(MarketplaceRules.PublicProduct);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            products = products.Where(x => x.ProductName.Contains(keyword));
        }
        return await ProductOptions(products.OrderBy(x => x.ProductName).ThenBy(x => x.ProductId)
            .Take(50)).ToListAsync(ct);
    }

    public Task<OfftakeProduct?> FindProductAsync(long farmerId, long productId,
        CancellationToken ct) =>
        ProductOptions(db.Products.AsNoTracking().Where(MarketplaceRules.PublicProduct)
            .Where(x => x.FarmerProfileId == farmerId && x.ProductId == productId))
            .SingleOrDefaultAsync(ct);

    public Task<B2bcontract?> FindSubmissionAsync(long buyerId, string code, CancellationToken ct) =>
        db.B2bcontracts.AsNoTracking().Include(x => x.ContractVersions)
            .SingleOrDefaultAsync(x => x.WholesaleBuyerProfileId == buyerId
                && x.ContractNumber == code, ct);

    public Task<B2bcontract?> FindManagedAsync(long buyerId, long contractId, CancellationToken ct) =>
        db.B2bcontracts.Include(x => x.FarmerProfile)
            .Include(x => x.ContractVersions).ThenInclude(x => x.ContractSignatures)
            .Include(x => x.ContractVersions).ThenInclude(x => x.ContractLines)
            .Include(x => x.ContractVersions).ThenInclude(x => x.ContractMilestones).ThenInclude(m => m.Invoices)
            .Include(x => x.PaymentTransactions).AsSplitQuery()
            .SingleOrDefaultAsync(x => x.WholesaleBuyerProfileId == buyerId
                && x.B2bcontractId == contractId, ct);

    public Task<bool> HasCommitmentsAsync(long contractId, CancellationToken ct, bool editingRevision = false) =>
        ContractsWithCommitments(editingRevision).AnyAsync(x => x.B2bcontractId == contractId, ct);

    public Task<Shipment?> FindShipmentForInspectionAsync(long buyerId, long contractId, long shipmentId,
        CancellationToken ct) =>
        db.Shipments.Include(s => s.SellerOrder).ThenInclude(o => o.Order)
            .Include(s => s.SellerOrder).ThenInclude(o => o.DisputeTickets).ThenInclude(t => t.DisputeEvidences)
            .Include(s => s.ShipmentItems).ThenInclude(i => i.OrderItemBatchAllocation).ThenInclude(a => a.OrderItem)
            .Include(s => s.ContractMilestone).ThenInclude(m => m!.ContractVersion).ThenInclude(v => v.B2bcontract)
            .Include(s => s.ContractMilestone).ThenInclude(m => m!.ContractVersion).ThenInclude(v => v.ContractLines)
            .Include(s => s.ContractMilestone).ThenInclude(m => m!.Invoices)
            .Include(s => s.ContractMilestone).ThenInclude(m => m!.ContractDepositAllocations).ThenInclude(a => a.PaymentTransaction)
            .Include(s => s.ContractMilestone).ThenInclude(m => m!.Shipments).ThenInclude(s => s.ShipmentItems)
                .ThenInclude(i => i.OrderItemBatchAllocation).ThenInclude(a => a.OrderItem)
            .AsSplitQuery().SingleOrDefaultAsync(s => s.ShipmentId == shipmentId
                && s.SellerOrder.B2bcontractId == contractId && s.ContractMilestone != null
                && s.ContractMilestone.B2bcontractId == contractId
                && s.ContractMilestone.ContractVersion.B2bcontract.WholesaleBuyerProfileId == buyerId
                && s.SellerOrder.Order.BuyerUserId == s.ContractMilestone.ContractVersion.B2bcontract.WholesaleBuyerProfile.UserId, ct);

    public Task<DisputeEvidence?> FindContractEvidenceAsync(long buyerUserId, long contractId, long evidenceId,
        CancellationToken ct) =>
        db.DisputeEvidences.AsNoTracking().Where(e => e.DisputeEvidenceId == evidenceId
            && (e.DisputeTicket.B2bcontractId == contractId || e.DisputeTicket.SellerOrder!.B2bcontractId == contractId)
            && Contracts(buyerUserId).Any(c => c.B2bcontractId == contractId)).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<OfftakeDiscrepancyReply>> DiscrepancyRepliesAsync(long contractId,
        long ticketId, CancellationToken ct) => Replies(await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == "FarmerOfftakeContract" && a.EntityId == contractId && a.Action == "Discrepancy")
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.AuditLogId).ToListAsync(ct), ticketId);

    public async Task<IReadOnlyList<long>> OfftakeReviewersAsync(CancellationToken ct) =>
        await db.Users.Where(u => u.Status == "Active" && u.Role == "CustomerSupport")
            .Select(u => u.UserId).ToListAsync(ct);

    private IQueryable<B2bcontract> ContractsWithCommitments(bool editingRevision) =>
        db.B2bcontracts.Where(x => x.SignedDate != null || x.DigitalSignatureStatus == "FullySigned"
                || x.PaymentTransactions.Any() || x.SellerOrders.Any()
                || x.ContractMilestones.Any(m => m.DeliveredQuantity > 0 || m.PaidAmount > 0
                    || m.Invoices.Any() || m.Shipments.Any())
                || x.ContractVersions.Any(v => v.VersionStatus == "Signed"
                    || ((!editingRevision || v.ProposedByUserId != x.FarmerProfile.UserId)
                        && v.ContractSignatures.Any(s => s.PartyRole == "Farmer"
                            && s.ConfirmationStatus == "Confirmed"))));

    public void Add(B2bcontract contract) => db.B2bcontracts.Add(contract);

    public Task<long> FarmerUserIdAsync(long farmerId, CancellationToken ct) =>
        db.FarmerProfiles.Where(x => x.FarmerProfileId == farmerId)
            .Select(x => x.UserId).SingleAsync(ct);

    public void AddNotification(Notification notification) => db.Notifications.Add(notification);

    public async Task<OfftakeTrackingPage> TrackAsync(long userId, bool orders,
        OfftakeTrackingQuery query, CancellationToken ct)
    {
        var statuses = orders ? Orders(userId).Select(x => x.OrderStatus)
            : Contracts(userId).Select(x => x.Status);
        var counts = await statuses.GroupBy(x => x)
            .Select(x => new OfftakeStatusCount(x.Key, x.Count())).ToListAsync(ct);
        var keyword = query.Keyword?.Trim();
        var product = query.Product?.Trim();
        var rows = orders ? OrderRows(userId, keyword, product)
            : ContractRows(userId, keyword, product);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            rows = rows.Where(x => x.Status == query.Status);
        }
        if (query.From is DateOnly from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue).AddHours(-7);
            rows = rows.Where(x => x.CreatedAt >= start);
        }
        if (query.To is DateOnly to)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7);
            rows = rows.Where(x => x.CreatedAt < end);
        }

        var total = await rows.CountAsync(ct);
        var page = Math.Min(query.Page, Math.Max(1, (total - 1) / query.PageSize + 1));
        var items = await rows.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new(items, total, page, query.PageSize, counts);
    }

    public Task<OfftakeTrackingDetail?> DetailAsync(long userId, bool orders, long id,
        CancellationToken ct) => orders ? OrderDetailAsync(userId, id, ct)
            : ContractDetailAsync(Contracts(userId), userId, id, false, ct);

    public Task<OfftakeTrackingDetail?> FarmerDetailAsync(long userId, long id,
        CancellationToken ct) =>
        ContractDetailAsync(db.B2bcontracts.AsNoTracking().Where(x =>
            x.FarmerProfile.UserId == userId && x.Status != "Draft"
            && x.ContractVersions.Any(v => v.ContractSignatures.Any(s =>
                s.SignedByUserId == x.WholesaleBuyerProfile.UserId
                && s.ConfirmationStatus == "Confirmed"))), userId, id, true, ct);

    private async Task<OfftakeTrackingDetail?> ContractDetailAsync(IQueryable<B2bcontract> contracts,
        long userId, long id, bool forFarmer, CancellationToken ct)
    {
        var details = contracts.Where(x => x.B2bcontractId == id)
            .Include(x => x.FarmerProfile)
            .Include(x => x.WholesaleBuyerProfile)
            .Include(x => x.ContractVersions).ThenInclude(x => x.ContractSignatures)
                .ThenInclude(x => x.SignedByUser)
            .Include(x => x.ContractLines).ThenInclude(x => x.Product)
            .Include(x => x.ContractMilestones).ThenInclude(x => x.Invoices).AsSplitQuery();
        if (!forFarmer)
        {
            details = details.Include(x => x.SellerOrders.Where(o => o.Order.BuyerUserId == userId))
                .ThenInclude(x => x.Invoices)
                .Include(x => x.SellerOrders.Where(o => o.Order.BuyerUserId == userId))
                .ThenInclude(x => x.Shipments).ThenInclude(s => s.ShipmentItems)
                .ThenInclude(i => i.OrderItemBatchAllocation).ThenInclude(a => a.HarvestBatch)
                .Include(x => x.SellerOrders.Where(o => o.Order.BuyerUserId == userId))
                .ThenInclude(x => x.Shipments).ThenInclude(s => s.ContractMilestone)
                .Include(x => x.SellerOrders.Where(o => o.Order.BuyerUserId == userId))
                .ThenInclude(x => x.Shipments).ThenInclude(s => s.ShipmentItems)
                .ThenInclude(i => i.OrderItemBatchAllocation).ThenInclude(a => a.OrderItem);
        }
        var contract = await details.SingleOrDefaultAsync(ct);
        if (contract is null)
        {
            return null;
        }

        var version = contract.ContractVersions
            .SingleOrDefault(x => x.VersionNo == contract.CurrentVersionNo);
        var versionLines = contract.ContractLines.Where(x => version is null
            || x.ContractVersionId == version.ContractVersionId).ToList();
        var displayUnit = versionLines.Count == 1 ? RequestedUnit(version, versionLines[0]) : null;
        var lines = versionLines
            .Select(x => new OfftakeLine(x.Product.ProductName, x.CommittedQuantity,
                x.UnitSnapshot, x.AgreedPrice, x.PriceFormula, x.QualitySpecification,
                displayUnit is null ? null : x.CommittedQuantity / displayUnit.Factor,
                displayUnit?.Unit)).ToList();
        var orderedMilestones = contract.ContractMilestones.OrderBy(x => x.DueDate).ToList();
        var milestones = orderedMilestones.Where(x => x.DeliveryQuantity != null && (version is null
                || x.ContractVersionId == version.ContractVersionId
                || x.DeliveredQuantity > 0 || x.Invoices.Count > 0))
            .Select(Delivery).ToList();
        var invoices = contract.ContractMilestones.SelectMany(x => x.Invoices)
            .Concat(contract.SellerOrders.SelectMany(x => x.Invoices));
        var confirmations = version?.ContractSignatures.Select(x => new OfftakeConfirmation(
            x.PartyRole, x.ConfirmedAt, x.ConfirmationStatus,
            x.ConfirmationStatus == "Confirmed" && x.ConfirmedAt.HasValue
                ? x.SignedByUser?.FullName : null)).ToList() ?? [];
        var partner = forFarmer ? contract.WholesaleBuyerProfile.CompanyName
            : contract.FarmerProfile.FarmName;
        var farmer = contract.FarmerProfile;
        var buyer = contract.WholesaleBuyerProfile;
        var (terms, proposedChanges) = version is null ? (null, null)
            : OfftakeContractRules.SplitAmendment(version.TermsAndConditions);
        var history = forFarmer ? [] : await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == "FarmerOfftakeContract" && a.EntityId == id
                && (a.Action == "Delay" || a.Action == "Discrepancy" || a.Action == "Review"))
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.AuditLogId).ToListAsync(ct);
        var relatedTickets = forFarmer ? [] : await db.DisputeTickets.AsNoTracking().Where(t =>
                t.DisputeReason == "ContractDeliveryDiscrepancy"
                && t.B2bcontractId == id && t.SellerOrder!.Order.BuyerUserId == userId)
            .Include(t => t.DisputeEvidences).OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
        return new(contract.ContractNumber, contract.Status, partner,
            version?.VersionNo, terms, contract.PaymentTerms,
            contract.DepositAmount, contract.ValidFrom, contract.ValidUntil, lines, milestones,
            Shipments(contract.SellerOrders, version, contract.Status == "Active"), Invoices(invoices, contract), confirmations)
        {
            Production = forFarmer ? [] : await ProductionAsync(versionLines, ct),
            DeliveryEvents = history.Where(a => a.Action == "Delay").Select(a =>
            {
                var input = ReadAction<ContractDelayRequest>(a);
                return new OfftakeContractEvent(a.Action, a.OccurredAt, a.Reason, input?.ProposedDate, input?.Quantity);
            }).ToList(),
            Discrepancies = relatedTickets.Select(t => new OfftakeDiscrepancy(t.DisputeTicketId, t.SellerOrderId,
                t.TicketStatus, t.Description, t.ResolutionNotes,
                Replies(history, t.DisputeTicketId),
                t.DisputeEvidences.OrderBy(e => e.SubmittedAt).Select(e =>
                    new OfftakeEvidence(e.DisputeEvidenceId, e.SubmittedByUserId == userId, e.Description)).ToList())).ToList(),
            FeedbackReason = version?.FeedbackReason,
            ProposedChanges = contract.Status == "Rejected" ? null : proposedChanges,
            CanEditRevision = !forFarmer && contract.Status is ("Negotiating" or "UnderNegotiation")
                && version is { VersionStatus: "Pending" }
                && version.ProposedByUserId == farmer.UserId
                && !confirmations.Any(x => (x.Role is "WholesaleBuyer" or "Buyer") && x.Status == "Confirmed")
                && !await HasCommitmentsAsync(id, ct, editingRevision: true),
            Document = forFarmer ? null : new(
                new(farmer.FarmName, farmer.RepresentativeName, farmer.Address, null),
                new(buyer.CompanyName, buyer.RepresentativeName, buyer.BusinessAddress, buyer.TaxCode),
                orderedMilestones.Where(x => x.ContractVersionId == version?.ContractVersionId
                    && x.DeliveryQuantity > 0).Select(Delivery).ToList(),
                version?.CreatedAt ?? contract.CreatedAt)
        };
    }

    private static List<OfftakeDiscrepancyReply> Replies(IEnumerable<AuditLog> history, long ticketId) =>
        history.Where(a => a.Action == "Discrepancy").Select(a => (Log: a, Input: ReadAction<ContractDiscrepancyResponseRequest>(a)))
            .Where(x => x.Input?.TicketId == ticketId)
            .Select(x => new OfftakeDiscrepancyReply(x.Input!.ShipmentItemId, x.Input.ProposedQuantity,
                x.Input.ProposedResolution, x.Log.Reason, DateTime.SpecifyKind(x.Log.OccurredAt, DateTimeKind.Utc))).ToList();

    private async Task<IReadOnlyList<OfftakeSeasonProgress>> ProductionAsync(List<ContractLine> lines, CancellationToken ct)
    {
        var seasonIds = lines.Where(l => l.CropSeasonId.HasValue).Select(l => l.CropSeasonId!.Value).Distinct().ToArray();
        if (seasonIds.Length == 0) return [];
        var seasons = await db.CropSeasons.AsNoTracking().Where(s => seasonIds.Contains(s.CropSeasonId))
            .Include(s => s.Product).Include(s => s.HarvestBatches)
            .Include(s => s.CultivationLogs.OrderByDescending(l => l.ActionDate)
                .ThenByDescending(l => l.CultivationLogId).Take(5)).AsSplitQuery().ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        return seasons.Select(s => new OfftakeSeasonProgress(s.Product.ProductName, s.SeasonName, s.Status,
            s.EstimatedHarvestDate, s.ActualHarvestDate,
            s.HarvestBatches.Where(b => b.ActualHarvestDate.HasValue).Sum(b => b.HarvestYield), s.YieldUnit,
            s.ActualHarvestDate is null && s.EstimatedHarvestDate < today
                && s.Status is not ("Cancelled" or "Crop Failure" or "Completed"),
            s.CultivationLogs.OrderByDescending(l => l.ActionDate).Select(l =>
                new OfftakeProductionLog(l.ActionDate, l.ActionType, l.DetailDescription)).ToList())).ToList();
    }

    private static T? ReadAction<T>(AuditLog log)
    {
        if (log.NewValue is null) return default;
        try
        {
            using var json = JsonDocument.Parse(log.NewValue);
            return json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("Input", out var input)
                ? input.Deserialize<T>() : default;
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static OfftakeDelivery Delivery(ContractMilestone milestone) =>
        new(milestone.MilestoneTitle, milestone.DueDate, milestone.DeliveryQuantity,
            milestone.DeliveredQuantity, milestone.AcceptedQuantity,
            milestone.FulfillmentStatus, milestone.PaymentStatus);

    private static OfftakeQuantityUnit? RequestedUnit(ContractVersion? version, ContractLine line)
    {
        var snapshot = version?.LineSnapshotJson;
        if (string.IsNullOrWhiteSpace(snapshot))
            return null;
        try
        {
            using var document = JsonDocument.Parse(snapshot);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(nameof(OfftakeProposalRequest.QuantityUnit), out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                return OfftakeQuantityUnit.For(line.UnitSnapshot)
                    .FirstOrDefault(unit => unit.Unit == value.GetString());
            }
        }
        catch (JsonException)
        {
            return null;
        }
        return null;
    }

    private async Task<OfftakeTrackingDetail?> OrderDetailAsync(long userId, long id,
        CancellationToken ct)
    {
        var order = await Orders(userId).Where(x => x.OrderId == id)
            .Include(x => x.SellerOrders).ThenInclude(x => x.FarmerProfile)
            .Include(x => x.SellerOrders).ThenInclude(x => x.OrderItems).ThenInclude(x => x.Product)
            .Include(x => x.SellerOrders).ThenInclude(x => x.Shipments)
            .Include(x => x.SellerOrders).ThenInclude(x => x.Invoices)
            .AsSplitQuery().SingleOrDefaultAsync(ct);
        if (order is null)
        {
            return null;
        }

        var lines = order.SellerOrders.SelectMany(x => x.OrderItems).Select(x =>
            new OfftakeLine(x.ProductNameSnapshot ?? x.Product.ProductName, x.Quantity,
                x.UnitSnapshot, x.UnitPriceSnapshot, null)).ToList();
        var partner = string.Join(", ", order.SellerOrders.Select(x =>
            x.SellerNameSnapshot ?? x.FarmerProfile.FarmName).Distinct());
        return new(order.OrderCode, order.OrderStatus, partner, null, null, null, null, null,
            null, lines, [], Shipments(order.SellerOrders),
            Invoices(order.SellerOrders.SelectMany(x => x.Invoices)), []);
    }

    private IQueryable<B2bcontract> Contracts(long userId) =>
        db.B2bcontracts.AsNoTracking().Where(x => x.WholesaleBuyerProfile.UserId == userId);

    private IQueryable<Order> Orders(long userId) =>
        db.Orders.AsNoTracking().Where(x => x.BuyerUserId == userId
            && s_wholesaleOrderTypes.Contains(x.OrderType));

    private IQueryable<OfftakeTrackingRow> ContractRows(long userId, string? keyword,
        string? product)
    {
        var committed = ContractsWithCommitments(editingRevision: true);
        return from contract in Contracts(userId)
        where string.IsNullOrEmpty(keyword) || contract.ContractNumber.Contains(keyword)
            || contract.FarmerProfile.FarmName.Contains(keyword)
        let lines = contract.ContractLines.Where(x =>
            x.ContractVersion.VersionNo == contract.CurrentVersionNo || !contract.ContractVersions.Any())
        let matchingLines = lines.Where(x =>
            string.IsNullOrEmpty(product) || x.Product.ProductName.Contains(product))
        where string.IsNullOrEmpty(product) || matchingLines.Any()
        let first = matchingLines.OrderBy(x => x.ContractLineId).FirstOrDefault()
        let milestones = contract.ContractMilestones.Where(x => x.DeliveryQuantity != null
            && (x.ContractVersion.VersionNo == contract.CurrentVersionNo || !contract.ContractVersions.Any()))
        select new OfftakeTrackingRow
        {
            Id = contract.B2bcontractId, Code = contract.ContractNumber,
            Partner = contract.FarmerProfile.FarmName,
            Product = first == null ? null : first.Product.ProductName,
            ImageUrl = first == null ? null : first.Product.ProductImages.OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.DisplayOrder).ThenBy(x => x.ProductImageId)
                .Select(x => x.ImageUrl).FirstOrDefault(),
            Quantity = first == null ? null : first.CommittedQuantity,
            Unit = first == null ? null : first.UnitSnapshot,
            Value = lines.Any() && !lines.Any(x => x.AgreedPrice == null)
                ? lines.Sum(x => x.CommittedQuantity * x.AgreedPrice) : null,
            CreatedAt = contract.CreatedAt, DeliveryFrom = milestones.Min(x => (DateOnly?)x.DueDate),
            DeliveryTo = milestones.Max(x => (DateOnly?)x.DueDate), Status = contract.Status,
            CanEditRevision = (contract.Status == "Negotiating" || contract.Status == "UnderNegotiation")
                && contract.ContractVersions.Any(v => v.VersionNo == contract.CurrentVersionNo
                    && v.ProposedByUserId == contract.FarmerProfile.UserId
                    && v.VersionStatus == "Pending" && !v.ContractSignatures.Any(s =>
                        (s.PartyRole == "WholesaleBuyer" || s.PartyRole == "Buyer")
                        && s.ConfirmationStatus == "Confirmed"))
                && !committed.Any(x => x.B2bcontractId == contract.B2bcontractId)
        };
    }

    private IQueryable<OfftakeTrackingRow> OrderRows(long userId, string? keyword, string? product) =>
        from order in Orders(userId)
        where string.IsNullOrEmpty(keyword) || order.OrderCode.Contains(keyword)
            || order.SellerOrders.Any(x =>
                (x.SellerNameSnapshot ?? x.FarmerProfile.FarmName).Contains(keyword))
        let seller = order.SellerOrders.OrderBy(x => x.SellerOrderId).FirstOrDefault()
        let matchingItems = order.SellerOrders.SelectMany(x => x.OrderItems)
            .Where(x => string.IsNullOrEmpty(product)
                || (x.ProductNameSnapshot ?? x.Product.ProductName).Contains(product))
        where string.IsNullOrEmpty(product) || matchingItems.Any()
        let first = matchingItems.OrderBy(x => x.OrderItemId).FirstOrDefault()
        select new OfftakeTrackingRow
        {
            Id = order.OrderId, Code = order.OrderCode,
            Partner = seller == null ? null : seller.SellerNameSnapshot ?? seller.FarmerProfile.FarmName,
            Product = first == null ? null : first.ProductNameSnapshot ?? first.Product.ProductName,
            ImageUrl = first == null ? null : first.Product.ProductImages.OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.DisplayOrder).ThenBy(x => x.ProductImageId)
                .Select(x => x.ImageUrl).FirstOrDefault(),
            Quantity = first == null ? null : first.Quantity,
            Unit = first == null ? null : first.UnitSnapshot,
            Value = order.FinalAmount, CreatedAt = order.OrderDate,
            DeliveryFrom = order.ExpectedDeliveryDate.HasValue
                ? DateOnly.FromDateTime(order.ExpectedDeliveryDate.Value) : null,
            Status = order.OrderStatus
        };

    private static IQueryable<OfftakeProduct> ProductOptions(IQueryable<Product> products) =>
        products.Select(x => new OfftakeProduct(x.ProductId, x.FarmerProfileId,
            x.FarmerProfile.FarmName, x.ProductName, x.Category.CategoryName, x.Unit,
            x.ProductImages.OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder)
                .ThenBy(i => i.ProductImageId).Select(i => i.ImageUrl).FirstOrDefault()));

    private static List<OfftakeInvoice> Invoices(IEnumerable<Invoice> invoices, B2bcontract? contract = null) =>
        invoices.DistinctBy(x => x.InvoiceId).OrderBy(x => x.IssuedAt)
            .Select(x => new OfftakeInvoice(x.InvoiceNumber, x.IssuedAt,
                x.DueAt, x.TotalAmount, x.InvoiceStatus)
            {
                MilestoneId = x.ContractMilestoneId,
                CanPay = contract?.Status == "Active" && x.InvoiceNumber.StartsWith("OFT-INV-", StringComparison.Ordinal)
                    && x.ContractMilestone is { } m && m.ContractVersionId == contract.ContractVersions
                        .SingleOrDefault(v => v.VersionNo == contract.CurrentVersionNo)?.ContractVersionId
                    && m.Amount > m.PaidAmount && x.InvoiceStatus is ("Issued" or "PartiallyPaid")
            }).ToList();

    private static List<OfftakeShipment> Shipments(IEnumerable<SellerOrder> orders, ContractVersion? version = null,
        bool active = false) =>
        orders.SelectMany(x => x.Shipments).OrderBy(x => x.ShipmentId)
            .Select(x => new OfftakeShipment(x.TrackingNumber, x.PlannedDeliveryAt,
                x.ShippedAt, x.DeliveredAt, x.ShipmentStatus)
            {
                Id = x.ShipmentId, SellerOrderId = x.SellerOrderId,
                CanInspect = active && version != null && x.ContractMilestone?.ContractVersionId == version.ContractVersionId
                    && OfftakeContractRules.CanInspect(x),
                Items = x.ShipmentItems.Select(i =>
                {
                    var allocation = i.OrderItemBatchAllocation;
                    return new OfftakeShipmentLine(i.ShipmentItemId, allocation.OrderItem.ProductNameSnapshot ?? "Sản phẩm",
                        allocation.HarvestBatch.BatchCode, allocation.OrderItem.UnitSnapshot, i.ShippedQuantity,
                        i.ReceivedQuantity, i.AcceptedQuantity, i.QualityResult, i.ReconciliationStatus);
                }).ToList()
            }).ToList();
}
