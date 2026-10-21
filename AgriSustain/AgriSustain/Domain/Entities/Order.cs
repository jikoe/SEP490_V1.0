using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class Order
{
    public long OrderId { get; set; }

    public long BuyerUserId { get; set; }

    public string OrderCode { get; set; } = null!;

    public string OrderType { get; set; } = null!;

    public bool IsPreOrder { get; set; }

    public DateTime OrderDate { get; set; }

    public DateTime? ExpectedDeliveryDate { get; set; }

    public string ShippingAddress { get; set; } = null!;

    public decimal TotalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal DeliveryFee { get; set; }

    public decimal FinalAmount { get; set; }

    public string OrderStatus { get; set; } = null!;

    public DateTime? ReservationExpiresAt { get; set; }

    public string RecipientNameSnapshot { get; set; } = null!;

    public string RecipientPhoneSnapshot { get; set; } = null!;

    public DateTime? CompletedAt { get; set; }

    public Guid? RetailQuoteId { get; set; }

    public string? DeliveryMethod { get; set; }

    public string? PaymentMethod { get; set; }

    public string? OrderNotes { get; set; }

    public string? VoucherCodeSnapshot { get; set; }

    public decimal? VoucherDiscount { get; set; }

    public decimal? PointDiscount { get; set; }

    public int? RedeemedPoints { get; set; }

    public string? ProvinceCodeSnapshot { get; set; }

    public string? ProvinceNameSnapshot { get; set; }

    public string? WardCodeSnapshot { get; set; }

    public string? WardNameSnapshot { get; set; }

    public string? AddressDetailSnapshot { get; set; }

    public string? AddressCatalogVersionSnapshot { get; set; }

    public virtual User BuyerUser { get; set; } = null!;

    public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();

    public virtual RetailCheckoutQuote? RetailQuote { get; set; }

    public virtual ICollection<SellerOrder> SellerOrders { get; set; } = new List<SellerOrder>();

    public virtual ICollection<VoucherRedemption> VoucherRedemptions { get; set; } = new List<VoucherRedemption>();
}
