using AgriSustain.Application.Interfaces;

namespace AgriSustain.Application.Services.Refunds;

public sealed class RejectedOrderRefundRequestService(IFarmerOrderRepository repository,
    FulfillmentRules rules, TimeProvider clock) : OrderRefundService(repository, rules, clock);
