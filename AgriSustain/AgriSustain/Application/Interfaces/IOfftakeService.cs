using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Offtake;

namespace AgriSustain.Application.Interfaces;

public interface IOfftakeService
{
    Task<Result<OfftakeCatalog>> CatalogAsync(string? keyword, CancellationToken ct);
    Task<Result<OfftakeProposalReceipt>> ProposeAsync(
        OfftakeProposalRequest request, CancellationToken ct, bool draft = false);
    Task<Result<OfftakeDraft>> DraftAsync(long id, CancellationToken ct);
    Task<Result<OfftakeProposalReceipt>> UpdateDraftAsync(long id, int version,
        OfftakeProposalRequest request, bool submit, CancellationToken ct);
    Task<Result<OfftakeProposalReceipt>> CancelAsync(long id, CancellationToken ct);
    Task<Result<PaymentQr>> RequestDepositAsync(long id, IPaymentQrProvider qrProvider, CancellationToken ct);
    Task<Result<PaymentQr>> DeliveryPaymentAsync(long id, long milestoneId, IPaymentQrProvider qrProvider, CancellationToken ct);
    Task<Result<OfftakeInspectionResult>> InspectAsync(long id, long shipmentId, OfftakeInspectionRequest input,
        IFarmerComplaintFiles files, CancellationToken ct);
    Task<Result> DecideDiscrepancyAsync(long id, long shipmentId, long ticketId, OfftakeDiscrepancyDecision input,
        CancellationToken ct);
    Task<Result<OfftakeTrackingPage>> TrackAsync(bool orders, OfftakeTrackingQuery query,
        CancellationToken ct);
    Task<Result<OfftakeTrackingDetail>> DetailAsync(bool orders, long id, CancellationToken ct);
    Task<Result<OfftakeTrackingDetail>> FarmerDetailAsync(long id, CancellationToken ct);
}
