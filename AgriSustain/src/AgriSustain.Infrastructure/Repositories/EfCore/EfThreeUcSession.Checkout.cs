using System.Text.Json;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Domain.Entities.Ordering;
using AgriSustain.Domain.Exceptions;
using AgriSustain.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Db = AgriSustain.Infrastructure.Data.Entities;

namespace AgriSustain.Infrastructure.Repositories.EfCore;

internal sealed partial class EfThreeUcSession
{
    /// <summary>
    /// Reads only the buyer's normal retail cart and active saved addresses using EF.
    /// Points remain an explicitly authorized dependency fixture pending the DB-09 contract.
    /// </summary>
    public async Task<CommerceContext> GetContextAsync(long buyerId, CancellationToken cancellationToken)
    {
        var items = await _db.CartItem.AsNoTracking()
            .Where(item => item.Cart.UserId == buyerId && item.PreOrderCampaignId == null)
            .OrderBy(item => item.CartItemId)
            .Select(item => new CartLine(item.CartItemId, item.ProductId, item.Quantity, item.IsSelected))
            .ToListAsync(cancellationToken);
        var addresses = await _db.DeliveryAddress.AsNoTracking()
            .Where(row => row.UserId == buyerId && row.Status == "Active")
            .OrderByDescending(row => row.IsDefault).ThenBy(row => row.DeliveryAddressId)
            .Select(row => new AddressSnapshot(row.RecipientName, row.RecipientPhone,
                row.ProvinceCity, row.Ward, row.AddressDetail, row.ProvinceCode ?? "",
                row.WardCode ?? "", row.AddressCatalogVersion ?? ""))
            .ToListAsync(cancellationToken);
        DateTime now = _clock.GetUtcNow().UtcDateTime;
        var vouchers = await _db.Voucher.AsNoTracking().Where(row => row.IsActive
            && row.StartDate <= now && row.EndDate > now).Select(row => row.VoucherCode)
            .ToListAsync(cancellationToken);
        if (!Points.ContainsKey(buyerId))
        {
            // DEMO-POINTS: replace this process-local balance with the loyalty team's adapter.
            bool primary = await _db.User.AnyAsync(row => row.UserId == buyerId
                && row.Username == "demo3uc_retail", cancellationToken);
            Points[buyerId] = primary ? 1000 : 300;
        }
        return new CommerceContext(items, addresses, Points[buyerId], vouchers);
    }

    /// <summary>
    /// Reads the current SQL offer, seller eligibility and primary ProductImage through EF.
    /// FarmerProfileId and UserId remain distinct; no product-management UC is implemented.
    /// </summary>
    public Task<ProductOffer?> FindProductAsync(long id, CancellationToken cancellationToken)
    {
        DateTime now = _clock.GetUtcNow().UtcDateTime;
        return _db.Product.AsNoTracking().Where(row => row.ProductId == id)
            .Select(row => new ProductOffer(row.ProductId, row.CategoryId, row.FarmerProfileId,
                row.FarmerProfile.FarmName, row.ProductName, row.Unit, row.BaseRetailPrice,
                row.WholesalePrice, row.WholesaleMinQuantity, row.ApprovalStatus == "Approved",
                row.IsPublished, row.FarmerProfile.VerificationStatus == "Approved"
                    && row.FarmerProfile.User.Status == "Active"
                    && (row.FarmerProfile.User.LockedUntil == null
                        || row.FarmerProfile.User.LockedUntil <= now), row.FarmerProfile.UserId,
                row.ProductImage.OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.DisplayOrder).ThenBy(image => image.ProductImageId)
                    .Select(image => image.ImageUrl).FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>Restores SQL inventory counters without applying the reservation twice.</summary>
    private static HarvestBatch Batch(Db.HarvestBatch row) => HarvestBatch.RestorePersisted(
        new HarvestBatch(row.AvailableQuantity)
        {
            Id = row.HarvestBatchId, ProductId = row.ProductId, Code = row.BatchCode,
            Status = row.Status, ExpirationDate = row.ExpirationDate
        }, row.ReservedQuantity);

    /// <summary>Reads product-based batch candidates; Application keeps the existing FEFO rules.</summary>
    public async Task<IReadOnlyList<HarvestBatch>> FindBatchesAsync(
        long productId, CancellationToken cancellationToken) =>
        (await _db.HarvestBatch.AsNoTracking().Where(row => row.ProductId == productId)
            .ToListAsync(cancellationToken)).Select(Batch).ToArray();

    /// <summary>Captures exact prior counters for a subsequent conditional EF reservation update.</summary>
    public async Task<HarvestBatch?> FindBatchAsync(long id, CancellationToken cancellationToken)
    {
        var row = await _db.HarvestBatch.AsNoTracking()
            .SingleOrDefaultAsync(batch => batch.HarvestBatchId == id, cancellationToken);
        if (row == null)
        {
            return null;
        }
        _readStock[id] = (row.AvailableQuantity, row.ReservedQuantity);
        return Batch(row);
    }

    /// <summary>Reads persisted voucher quota including unexpired Reserved redemption rows.</summary>
    public async Task<VoucherOffer?> FindVoucherAsync(string code, CancellationToken cancellationToken)
    {
        var row = await _db.Voucher.AsNoTracking()
            .SingleOrDefaultAsync(voucher => voucher.VoucherCode == code, cancellationToken);
        if (row == null)
        {
            return null;
        }
        DateTime now = _clock.GetUtcNow().UtcDateTime;
        int reserved = await _db.VoucherRedemption.CountAsync(redemption =>
            redemption.VoucherId == row.VoucherId && redemption.Status == "Reserved"
            && (redemption.ExpiresAt == null || redemption.ExpiresAt > now), cancellationToken);
        return new VoucherOffer(row.VoucherId, row.VoucherCode, row.FarmerProfileId,
            row.DiscountType, row.DiscountValue, row.MaxDiscountAmount, row.MinOrderAmount,
            row.UsageLimit, row.UsedCount, reserved, Utc(row.StartDate), Utc(row.EndDate), row.IsActive);
    }

    /// <summary>Restores a SQL quote snapshot; service-level owner/expiry/fingerprint checks still apply.</summary>
    public async Task<CheckoutQuote?> FindQuoteAsync(Guid id, CancellationToken cancellationToken)
    {
        string? json = await _db.RetailCheckoutQuote.AsNoTracking().Where(row => row.QuoteId == id)
            .Select(row => row.AcceptedSnapshotJson).SingleOrDefaultAsync(cancellationToken);
        return json == null ? null : JsonSerializer.Deserialize<CheckoutQuote>(json)
            ?? throw new InvalidDataException("Invalid persisted quote snapshot.");
    }

    /// <summary>Stages an immutable quote entity without creating an order or reserving inventory.</summary>
    public Task SaveQuoteAsync(CheckoutQuote quote, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _db.RetailCheckoutQuote.Add(new Db.RetailCheckoutQuote
        {
            QuoteId = quote.Id, BuyerUserId = quote.BuyerId,
            AcceptedSnapshotJson = JsonSerializer.Serialize(quote), Fingerprint = quote.Fingerprint,
            CreatedAt = _clock.GetUtcNow().UtcDateTime, ExpiresAt = quote.ExpiresAt.UtcDateTime
        });
        return Task.CompletedTask;
    }

    /// <summary>
    /// DEMO-DB06: request keys stay process-local until the durable contract is approved.
    /// An EF existence query prevents returning a cached order removed from SQL.
    /// </summary>
    public async Task<RetailOrder?> FindOrderAsync(
        long buyerId, Guid requestKey, CancellationToken cancellationToken) =>
        RequestOrders.TryGetValue((buyerId, requestKey), out var order)
            && await _db.Order.AnyAsync(row => row.OrderId == order.Id, cancellationToken)
                ? order : null;

    /// <summary>
    /// Obtains SQL IDENTITY through an uncommitted EF placeholder for the existing service contract.
    /// The final snapshots, stock, cart, benefits and audit must all succeed before UoW commit.
    /// </summary>
    public async Task<long> NextOrderIdAsync(CancellationToken cancellationToken)
    {
        var row = new Db.Order
        {
            BuyerUserId = _actorId ?? throw new InvalidOperationException("Missing order actor."),
            OrderCode = "PENDING-" + Guid.NewGuid().ToString("N"), OrderType = "Retail",
            OrderDate = _clock.GetUtcNow().UtcDateTime, ShippingAddress = "",
            RecipientNameSnapshot = "", RecipientPhoneSnapshot = "", OrderStatus = "Initializing"
        };
        _db.Order.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return row.OrderId;
    }

    /// <summary>
    /// Uses EF ExecuteUpdate to move exactly the reserved delta with counter/status/expiry checks.
    /// A concurrent external writer cannot be silently overwritten; failure rolls back the UoW.
    /// </summary>
    public async Task ReserveBatchAsync(HarvestBatch batch, CancellationToken cancellationToken)
    {
        if (!_readStock.TryGetValue(batch.Id, out var before))
        {
            throw new InvalidOperationException("Stock must be read before reservation.");
        }
        decimal quantity = before.Available - batch.AvailableQuantity;
        if (quantity <= 0 || batch.ReservedQuantity - before.Reserved != quantity)
        {
            throw new InvalidOperationException("Invalid reservation delta.");
        }
        DateOnly today = DateOnly.FromDateTime(_clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).Date);
        int affected = await _db.HarvestBatch.Where(row => row.HarvestBatchId == batch.Id
            && row.Status == "Available" && row.AvailableQuantity == before.Available
            && row.ReservedQuantity == before.Reserved && row.AvailableQuantity >= quantity
            && (row.ExpirationDate == null || row.ExpirationDate >= today))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.AvailableQuantity, row => row.AvailableQuantity - quantity)
                .SetProperty(row => row.ReservedQuantity, row => row.ReservedQuantity + quantity),
                cancellationToken);
        if (affected != 1)
        {
            throw new BusinessRuleException("Tồn kho đã thay đổi. Vui lòng tính lại.");
        }
    }

    /// <summary>
    /// Maps the confirmed parent and seller/item/allocation graph to scaffolded EF entities.
    /// EF assigns child identities/FKs at SaveChanges; no snapshot is regenerated from mutable data.
    /// </summary>
    public async Task SaveOrderAsync(RetailOrder order, CancellationToken cancellationToken)
    {
        var parent = await _db.Order.FindAsync([order.Id], cancellationToken)
            ?? throw new InvalidOperationException("Order placeholder is missing.");
        parent.OrderCode = order.Code;
        parent.OrderStatus = order.Status;
        parent.OrderDate = order.CreatedAt.UtcDateTime;
        parent.ShippingAddress = $"{order.Address.Detail}, {order.Address.Ward}, {order.Address.Province}";
        parent.TotalAmount = order.Subtotal;
        parent.DiscountAmount = order.VoucherDiscount + order.PointsDiscount;
        parent.DeliveryFee = order.DeliveryFee;
        parent.FinalAmount = order.Total;
        parent.ReservationExpiresAt = order.ReservationExpiresAt.UtcDateTime;
        parent.RecipientNameSnapshot = order.Address.RecipientName;
        parent.RecipientPhoneSnapshot = order.Address.Phone;
        parent.RetailQuoteId = order.QuoteId;
        parent.DeliveryMethod = order.DeliveryMethod;
        parent.PaymentMethod = order.PaymentMethod;
        parent.OrderNotes = order.Notes;
        parent.VoucherCodeSnapshot = order.VoucherId == null ? null
            : await _db.Voucher.Where(row => row.VoucherId == order.VoucherId)
                .Select(row => row.VoucherCode).SingleAsync(cancellationToken);
        parent.VoucherDiscount = order.VoucherDiscount;
        parent.PointDiscount = order.PointsDiscount;
        parent.RedeemedPoints = order.RedeemedPoints;
        parent.ProvinceCodeSnapshot = order.Address.ProvinceCode;
        parent.ProvinceNameSnapshot = order.Address.Province;
        parent.WardCodeSnapshot = order.Address.WardCode;
        parent.WardNameSnapshot = order.Address.Ward;
        parent.AddressDetailSnapshot = order.Address.Detail;
        parent.AddressCatalogVersionSnapshot = order.Address.CatalogVersion;
        foreach (var seller in order.Sellers)
        {
            var sellerRow = new Db.SellerOrder
            {
                OrderId = order.Id, FarmerProfileId = seller.SellerId, SellerOrderCode = seller.Code,
                SellerNameSnapshot = order.Lines.First(line => line.SellerId == seller.SellerId).SellerName,
                Subtotal = seller.Subtotal, DiscountShare = seller.DiscountShare,
                DeliveryFee = seller.DeliveryFee, FinalAmount = seller.FinalAmount, Status = "PendingPayment"
            };
            _db.SellerOrder.Add(sellerRow);
            foreach (var line in order.Lines.Where(line => line.SellerId == seller.SellerId))
            {
                var item = new Db.OrderItem
                {
                    SellerOrder = sellerRow, ProductId = line.ProductId, Quantity = line.Quantity,
                    UnitPriceSnapshot = line.UnitPrice, UnitSnapshot = line.Unit,
                    SubTotal = line.Subtotal, ProductNameSnapshot = line.ProductName,
                    ItemStatus = "Pending"
                };
                _db.OrderItem.Add(item);
                int sequence = 0;
                foreach (var allocation in line.Allocations)
                {
                    _db.OrderItemBatchAllocation.Add(new Db.OrderItemBatchAllocation
                    {
                        OrderItem = item, HarvestBatchId = allocation.BatchId,
                        AllocationSequence = ++sequence, AllocatedQuantity = allocation.Quantity,
                        AllocationStatus = "Reserved", ReservedAt = order.CreatedAt.UtcDateTime,
                        ExpiresAt = order.ReservationExpiresAt.UtcDateTime
                    });
                }
            }
        }
        RequestOrders[(order.BuyerId, order.RequestKey)] = order;
    }

    /// <summary>
    /// Stages a durable voucher reservation and detached mock-point deduction, not a payment.
    /// Apply/release/expiry and durable point storage remain owned by the missing team modules.
    /// </summary>
    public Task ReserveBenefitsAsync(RetailOrder order, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int balance = Points.GetValueOrDefault(order.BuyerId);
        if (balance < order.RedeemedPoints)
        {
            throw new BusinessRuleException("Điểm khả dụng đã thay đổi.");
        }
        Points[order.BuyerId] = balance - order.RedeemedPoints;
        if (order.VoucherId is { } voucher)
        {
            _db.VoucherRedemption.Add(new Db.VoucherRedemption
            {
                VoucherId = voucher, OrderId = order.Id, DiscountSnapshot = order.VoucherDiscount,
                Status = "Reserved", ReservedAt = order.CreatedAt.UtcDateTime,
                ExpiresAt = order.ReservationExpiresAt.UtcDateTime
            });
        }
        return Task.CompletedTask;
    }

    /// <summary>Deletes only selected buyer-owned rows inside the transaction; rollback restores them.</summary>
    public async Task RemoveOrderedItemsAsync(
        long buyerId, IReadOnlyList<long> itemIds, CancellationToken cancellationToken) =>
        await _db.CartItem.Where(item => item.Cart.UserId == buyerId && item.IsSelected
            && itemIds.Contains(item.CartItemId)).ExecuteDeleteAsync(cancellationToken);
}
