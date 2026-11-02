using System;
using System.Collections.Generic;

namespace AgriSustain.Domain.Entities;

public partial class RefundTransaction
{
    public long RefundTransactionId { get; set; }

    public long PaymentTransactionId { get; set; }

    public long? DisputeTicketId { get; set; }

    public long? ContractTerminationRequestId { get; set; }

    public long ProcessedByUserId { get; set; }

    public string RefundCode { get; set; } = null!;

    public decimal Amount { get; set; }

    public string? Reason { get; set; }

    public string RefundStatus { get; set; } = null!;

    public DateTime? ProcessedAt { get; set; }

    public string? GatewayReference { get; set; }

    public long? PaymentInvoiceAllocationId { get; set; }

    public long? ContractDepositAllocationId { get; set; }

    public string RequestKey { get; set; } = null!;

    public string? ReceiverBankCode { get; set; }

    public string? ReceiverAccountNumber { get; set; }

    public string? ReceiverAccountHolder { get; set; }

    public DateTime? ReceiverAccountProvidedAt { get; set; }

    public virtual ContractDepositAllocation? ContractDepositAllocation { get; set; }

    public virtual ContractTerminationRequest? ContractTerminationRequest { get; set; }

    public virtual DisputeTicket? DisputeTicket { get; set; }

    public virtual PaymentInvoiceAllocation? PaymentInvoiceAllocation { get; set; }

    public virtual PaymentTransaction PaymentTransaction { get; set; } = null!;

    public virtual User ProcessedByUser { get; set; } = null!;

    public virtual SupportedBank? ReceiverBankCodeNavigation { get; set; }
}
