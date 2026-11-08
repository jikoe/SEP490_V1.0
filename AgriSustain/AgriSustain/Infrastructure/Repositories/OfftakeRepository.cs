using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Application.Interfaces;
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
            products = products.Where(x => x.ProductName.Contains(keyword)
                || x.FarmerProfile.FarmName.Contains(keyword));
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
        db.B2bcontracts.Include(x => x.ContractVersions).ThenInclude(x => x.ContractSignatures)
            .Include(x => x.ContractLines).Include(x => x.ContractMilestones).AsSplitQuery()
            .SingleOrDefaultAsync(x => x.WholesaleBuyerProfileId == buyerId
                && x.B2bcontractId == contractId, ct);

    public Task<bool> HasCommitmentsAsync(long contractId, CancellationToken ct) =>
        db.B2bcontracts.AnyAsync(x => x.B2bcontractId == contractId
            && (x.SignedDate != null || x.DigitalSignatureStatus == "FullySigned"
                || x.PaymentTransactions.Any() || x.SellerOrders.Any()
                || x.ContractMilestones.Any(m => m.DeliveredQuantity > 0 || m.PaidAmount > 0
                    || m.Invoices.Any() || m.Shipments.Any())
                || x.ContractVersions.Any(v => v.VersionStatus == "Signed"
                    || v.ContractSignatures.Any(s => s.PartyRole == "Farmer"
                        && s.ConfirmationStatus == "Confirmed"))), ct);

    public Task<B2bcontract?> FindRevisionAsync(long buyerId, long contractId,
        CancellationToken ct) =>
        db.B2bcontracts.Include(x => x.FarmerProfile).Include(x => x.ContractVersions
                .Where(v => v.VersionNo > 1)).ThenInclude(x => x.ContractSignatures)
            .Include(x => x.PaymentTransactions.Where(p => p.PaymentPurpose == "ContractDeposit"
                && p.PaymentStatus == "Succeeded" && p.PaidAt != null))
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.WholesaleBuyerProfileId == buyerId
                && x.B2bcontractId == contractId, ct);

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
            .Include(x => x.ContractLines).ThenInclude(x => x.Product)
            .Include(x => x.ContractMilestones).ThenInclude(x => x.Invoices).AsSplitQuery();
        if (!forFarmer)
        {
            details = details.Include(x => x.SellerOrders.Where(o => o.Order.BuyerUserId == userId))
                .ThenInclude(x => x.Invoices)
                .Include(x => x.SellerOrders.Where(o => o.Order.BuyerUserId == userId))
                .ThenInclude(x => x.Shipments);
        }
        var contract = await details.SingleOrDefaultAsync(ct);
        if (contract is null)
        {
            return null;
        }

        var version = contract.ContractVersions
            .SingleOrDefault(x => x.VersionNo == contract.CurrentVersionNo);
        var lines = contract.ContractLines.Where(x => version is null
                || x.ContractVersionId == version.ContractVersionId)
            .Select(x => new OfftakeLine(x.Product.ProductName, x.CommittedQuantity,
                x.UnitSnapshot, x.AgreedPrice, x.PriceFormula)).ToList();
        var milestones = contract.ContractMilestones.Where(x => version is null
                || x.ContractVersionId == version.ContractVersionId
                || x.DeliveredQuantity > 0 || x.Invoices.Count > 0)
            .OrderBy(x => x.DueDate).Select(x => new OfftakeDelivery(x.MilestoneTitle, x.DueDate,
                x.DeliveryQuantity, x.DeliveredQuantity, x.AcceptedQuantity,
                x.FulfillmentStatus, x.PaymentStatus)).ToList();
        var invoices = contract.ContractMilestones.SelectMany(x => x.Invoices)
            .Concat(contract.SellerOrders.SelectMany(x => x.Invoices));
        var confirmations = version?.ContractSignatures.Select(x => new OfftakeConfirmation(
            x.PartyRole, x.ConfirmedAt, x.ConfirmationStatus)).ToList() ?? [];
        var partner = forFarmer ? contract.WholesaleBuyerProfile.CompanyName
            : contract.FarmerProfile.FarmName;
        return new(contract.ContractNumber, contract.Status, partner,
            version?.VersionNo, version?.TermsAndConditions, contract.PaymentTerms,
            contract.DepositAmount, contract.ValidFrom, contract.ValidUntil, lines, milestones,
            Shipments(contract.SellerOrders), Invoices(invoices), confirmations,
            !forFarmer && version is { VersionNo: > 1, VersionStatus: "Pending" }
                && version.ProposedByUserId == contract.FarmerProfile.UserId
                && contract.Status is not ("Rejected" or "Cancelled" or "Terminated" or "Completed")
                && !confirmations.Any(x => x.Role is "WholesaleBuyer" or "Buyer"
                    && x.Status == "Confirmed"));
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
        string? product) =>
        from contract in Contracts(userId)
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
            DeliveryTo = milestones.Max(x => (DateOnly?)x.DueDate), Status = contract.Status
        };

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

    private static List<OfftakeInvoice> Invoices(IEnumerable<Invoice> invoices) =>
        invoices.DistinctBy(x => x.InvoiceId).OrderBy(x => x.IssuedAt)
            .Select(x => new OfftakeInvoice(x.InvoiceNumber, x.IssuedAt,
                x.DueAt, x.TotalAmount, x.InvoiceStatus)).ToList();

    private static List<OfftakeShipment> Shipments(IEnumerable<SellerOrder> orders) =>
        orders.SelectMany(x => x.Shipments).OrderBy(x => x.ShipmentId)
            .Select(x => new OfftakeShipment(x.TrackingNumber, x.PlannedDeliveryAt,
                x.ShippedAt, x.DeliveredAt, x.ShipmentStatus)).ToList();
}
