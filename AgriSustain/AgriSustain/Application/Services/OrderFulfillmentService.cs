using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgriSustain.Application.Services;

public sealed class OrderFulfillmentService(
    IOrderRepository repository,
    IUnitOfWork unitOfWork,
    FulfillmentRules rules,
    TimeProvider clock,
    IFileStorageService storage,
    INotificationGateway? notificationGateway = null,
    OrderRefundService? refundService = null,
    OrderNotificationService? notificationService = null,
    IOrderFeeService? fees = null)
{
    public async Task<FeatureResult<PageResult<SellerOrderResponse>>> GetListAsync(long userId,
        SellerOrderQuery query, CancellationToken cancellationToken)
    {
        if (query.Page is < 1 or > 100000 || query.PageSize is < 1 or > 100
            || query.Keyword?.Length > 255 || query.From > query.To
            || query.Status is not (null or "" or "Pending" or "Confirmed" or "Preparing"
                or "ReadyToShip" or "Shipping" or "Delivered" or "Cancelled" or "DeliveryFailed"
                or "Returned" or "Received" or "Completed")
            || query.OrderType is not (null or "" or "Retail" or "Wholesale" or "PreOrder")
            || query.PaymentStatus is not (null or "" or "Paid" or "AwaitingPayment"))
        {
            return FeatureResult<PageResult<SellerOrderResponse>>.Fail("VALIDATION",
                FarmerOrderMessages.InvalidFilters);
        }

        return FeatureResult<PageResult<SellerOrderResponse>>.Ok(
            await repository.SearchOwnedAsync(userId, query, cancellationToken));
    }

    public async Task<FeatureResult<SellerOrderResponse>> GetAsync(long userId, long id,
        CancellationToken cancellationToken)
    {
        var order = await repository.LoadOwnedContextAsync(userId, id, cancellationToken);
        return order is null ? Fail("NOT_FOUND", FarmerOrderMessages.ForbiddenOrder)
            : FeatureResult<SellerOrderResponse>.Ok(repository.Map(order));
    }

    public async Task<FeatureResult<SellerOrderResponse>> ActAsync(long userId, long id,
        string action, OrderActionRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !Guid.TryParseExact(request.RequestKey, "D", out _)
            || request.Version?.Length != 64 || request.Reason?.Length > (action == "reject" ? 500 : 100)
            || request.Explanation?.Length > 2000
            || request.EvidenceUrls?.Length > 5)
        {
            return Fail("VALIDATION", FarmerOrderMessages.InvalidAction);
        }

        if (action is "reject")
        {
            var trimmedReason = (request.Explanation ?? request.Reason)?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedReason))
            {
                return Fail("VALIDATION", FarmerOrderMessages.ReasonRequired);
            }
            if (trimmedReason.Length > 500)
            {
                return Fail("VALIDATION", FarmerOrderMessages.ReasonTooLong);
            }

        }

        if (action == "delivery-report" && request.Reason is not ("DeliveryFailure" or "Return"))
        {
            return Fail("VALIDATION", FarmerOrderMessages.ReportTypeRequired);
        }

        if (action is "delivery-report" or "evidence" &&
            (string.IsNullOrWhiteSpace(request.Explanation)
                || request.Explanation.Trim().Length < 15
                || request.EvidenceUrls is not { Length: > 0 }))
        {
            return Fail("VALIDATION", FarmerOrderMessages.EvidenceRequired);
        }

        if (request.EvidenceUrls?.Any(url => !IsOwnedEvidence(userId, url) || !storage.OwnsEvidence(userId, url)) == true)
        {
            return Fail("VALIDATION", FarmerOrderMessages.EvidenceNotOwned);
        }

        await unitOfWork.BeginAsync(cancellationToken);
        if (!await repository.IsApprovedFarmerAsync(userId, cancellationToken))
        {
            return Fail("FORBIDDEN", FarmerOrderMessages.FarmerNotApproved);
        }

        var order = await repository.LoadOwnedContextAsync(userId, id, cancellationToken);
        if (order is null)
        {
            return Fail("NOT_FOUND", FarmerOrderMessages.ForbiddenOrder);
        }

        if (await repository.HasRequestAsync(request.RequestKey, cancellationToken)
            || FulfillmentRules.Version(order) != request.Version)
        {
            return Fail("CONFLICT", FarmerOrderMessages.Stale);
        }

        if (order.B2bcontractId.HasValue)
        {
            return Fail("CONFLICT", FarmerOrderMessages.ContractOrder);
        }

        decimal refundedAmount = 0;
        string previous = order.Status;
        var now = clock.GetUtcNow().UtcDateTime;

        string? validationError = ValidateAction(order, action, request.RequestKey);
        if (validationError is not null)
        {
            return Fail("CONFLICT", validationError);
        }

        if (action is "confirm" or "ready" or "dispatch" or "start-shipping")
        {
            var invalidQuantity = order.OrderItems.FirstOrDefault(item => item.Quantity <= 0
                || item.OrderItemBatchAllocations.Any(allocation => allocation.AllocatedQuantity <= 0));
            if (invalidQuantity is not null)
            {
                return Fail("VALIDATION", $"Vui lòng kiểm tra số lượng sản phẩm {invalidQuantity.ProductNameSnapshot ?? invalidQuantity.Product.ProductName}: số lượng đặt và lượng phân bổ phải lớn hơn 0.");
            }

            var today = DateOnly.FromDateTime(clock.GetUtcNow().AddHours(7).DateTime);
            var blocked = order.OrderItems.SelectMany(item => item.OrderItemBatchAllocations)
                .FirstOrDefault(allocation =>
                    allocation.HarvestBatch.ExpirationDate is DateOnly expiry && expiry <= today
                    || allocation.HarvestBatch.Status is "Recalled" or "Blocked" or "Expired");
            if (blocked is not null)
            {
                return Fail("CONFLICT", $"Lô {blocked.HarvestBatch.BatchCode} đã hết hạn, bị thu hồi hoặc bị khóa. Không thể phân bổ hoặc xuất lô này.");
            }

            if (!rules.PaymentSatisfied(order) || !rules.AllocationsValid(order))
            {
                var items = string.Join(", ", order.OrderItems.Select(item =>
                    item.ProductNameSnapshot ?? item.Product.ProductName));
                return Fail("CONFLICT", action is "dispatch" or "start-shipping"
                    ? $"Không thể bắt đầu giao hàng. Vui lòng kiểm tra trạng thái đơn, thanh toán hoặc tiền cọc đã xác minh và phân bổ lô hàng hợp lệ cho: {items}."
                    : $"Đơn hàng chưa thể tiếp tục. Chưa đủ thanh toán hoặc tiền cọc đã xác minh, hoặc thiếu phân bổ lô hàng thực tế hợp lệ cho: {items}.");
            }
        }

        switch (action)
        {
            case "confirm":
                ApplyAction(order, action, request.RequestKey);
                break;
            case "ready":
                ApplyAction(order, action, request.RequestKey);
                break;
            case "dispatch" or "start-shipping":
                Dispatch(order, userId, request.RequestKey, now);
                ApplyAction(order, action, request.RequestKey);
                break;
            case "reject":
                var trimmedReason = (request.Explanation ?? request.Reason)?.Trim() ?? "Farmer rejected order before dispatch";
                ReleaseUndispatchedGoods(order, now);
                order.Cancel(trimmedReason);
                // BR-FEE-08: the rejected seller order carries no platform fee.
                if (fees is not null)
                {
                    await fees.ReverseCancelledAsync(order.SellerOrderId, cancellationToken);
                }
                if (refundService is not null)
                {
                    // A pending Finance refund request; rejection never initiates a bank transfer.
                    await refundService.RefundSellerShareOnceAsync(order, userId, trimmedReason,
                        request.RequestKey, now, cancellationToken);
                    refundedAmount = refundService.GetPendingAmount(order);
                }
                else if (rules.VerifiedPayment(order) > 0)
                    return Fail("UNAVAILABLE", FarmerOrderMessages.RefundUnavailable);
                await repository.ReleasePurchaseHoldsAsync(order, cancellationToken);
                break;
            case "delivery-report":
                ReportDelivery(order, userId, request, now);
                await repository.NotifySupportAsync(id,
                    $"Đơn {order.SellerOrderCode} có báo cáo giao hàng thất bại hoặc hàng trả. Vui lòng xem hồ sơ và bằng chứng.", cancellationToken);
                break;
            case "evidence":
                string? evidenceError = SubmitEvidence(order, userId, request, now);
                if (evidenceError is not null)
                {
                    return Fail("CONFLICT", evidenceError);
                }
                break;
            default:
                return Fail("VALIDATION", FarmerOrderMessages.UnsupportedAction);
        }

        if (action == "evidence")
        {
            await repository.NotifySupportAsync(id,
                $"Nông trại đã gửi giải trình và bằng chứng cho đơn {order.SellerOrderCode}. Vui lòng xem xét yêu cầu bổ sung bằng chứng.", cancellationToken);
        }

        string historyReason = request.Reason is null ? action
            : request.Reason + (string.IsNullOrWhiteSpace(request.Explanation) ? "" : ": " + request.Explanation);
        if (order.Status != previous)
        {
            order.OrderStatusHistories.Add(new OrderStatusHistory
            {
                ChangedByUserId = userId,
                OldStatus = previous,
                NewStatus = order.Status,
                Reason = historyReason[..Math.Min(historyReason.Length, 500)],
                OccurredAt = now
            });
        }

        FulfillmentRules.Aggregate(order.Order);
        repository.AddAudit(new AuditLog
        {
            PerformedByUserId = userId,
            EntityType = "SellerOrder",
            EntityId = id,
            Action = action,
            TraceId = request.RequestKey,
            OldValue = previous,
            NewValue = JsonSerializer.Serialize(new { order.Status, request.Explanation }),
            OccurredAt = now
        });

        // Notifications & Transactional e-mails
        if (notificationService is not null)
        {
            string eventCode = action switch
            {
                "confirm" => "MSG185",
                "dispatch" or "start-shipping" => "MSG65",
                "reject" => "MSG186",
                _ => "UPDATE"
            };
            await notificationService.NotifyRetailCustomerAsync(
                order, eventCode, request.RequestKey, request.Explanation ?? request.Reason,
                action == "reject" ? refundedAmount : 0, cancellationToken);
        }
        else
        {
            string buyerMessage = action switch
            {
                "dispatch" or "start-shipping" => $"Đơn hàng {order.SellerOrderCode} đã bắt đầu giao mô phỏng. Bạn có thể theo dõi tiến độ tại Chi tiết đơn hàng.",
                "reject" => refundedAmount > 0
                    ? $"Đơn hàng {order.SellerOrderCode} đã bị từ chối bởi nông trại. Lý do: {request.Explanation ?? request.Reason}. Khoản hoàn {refundedAmount:N0} ₫ đang chờ bộ phận Tài chính chuyển khoản; bộ phận Tài chính sẽ xử lý về tài khoản đã lưu."
                    : $"Đơn hàng {order.SellerOrderCode} đã bị từ chối. Lý do: {request.Explanation ?? request.Reason}",
                "confirm" => $"Đơn hàng {order.SellerOrderCode} đã được xác nhận và đang chuẩn bị.",
                "ready" => $"Đơn hàng {order.SellerOrderCode} đã sẵn sàng giao mô phỏng.",
                "delivery-report" => $"Đã ghi nhận vấn đề giao hàng hoặc hàng trả cho đơn {order.SellerOrderCode}. Vụ việc đang chờ xem xét.",
                _ => $"Đã gửi giải trình và bằng chứng cho đơn hàng {order.SellerOrderCode} để xem xét."
            };
            repository.Notify(order.Order.BuyerUserId, "Cập nhật đơn hàng", buyerMessage, order.SellerOrderId);

        }

        await repository.SaveFulfillmentAsync(order, unitOfWork, cancellationToken);
        return FeatureResult<SellerOrderResponse>.Ok(repository.Map(order));
    }

    public string? ValidateAction(SellerOrder order, string action, string requestKey)
    {
        return action switch
        {
            "confirm" when order.Status != "Pending" => FarmerOrderMessages.InvalidStatus,
            "ready" when !FulfillmentRules.IsConfirmed(order.Status) => FarmerOrderMessages.NotConfirmed,
            "dispatch" or "start-shipping" when !FulfillmentRules.IsReady(order.Status) =>
                FarmerOrderMessages.NotReady,
            "reject" when !FulfillmentRules.IsBeforeShipping(order.Status) =>
                FarmerOrderMessages.AlreadyDispatched,
            "delivery-report" when !FulfillmentRules.IsShipping(order.Status)
                && order.Status is not ("Delivered" or "DeliveryFailed") =>
                FarmerOrderMessages.ReportClosed,
            _ => null
        };
    }

    public void ApplyAction(SellerOrder order, string action, string requestKey)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        switch (action)
        {
            case "confirm":
                order.Confirm();
                break;
            case "ready":
                order.MarkReadyToShip();
                break;
            case "dispatch" or "start-shipping":
                order.MarkShipping();
                break;
            case "reject":
                throw new InvalidOperationException("Use the validated rejection flow with a reason.");
        }
    }

    public void ReleaseUndispatchedGoods(SellerOrder order, DateTime now)
    {
        foreach (var allocation in order.OrderItems.SelectMany(x => x.OrderItemBatchAllocations)
            .Where(x => x.AllocationStatus == "Reserved"))
        {
            allocation.HarvestBatch.ReleaseReservation(allocation.AllocatedQuantity);
            allocation.AllocationStatus = "Released";
            allocation.ReleasedAt = now;
        }
    }

    public PackingSlipResponse BuildPackingSlip(SellerOrder order)
    {
        var items = order.OrderItems.Select(item => new PackingSlipItemResponse(
            item.ProductNameSnapshot ?? item.Product.ProductName,
            item.UnitSnapshot,
            item.Quantity,
            item.UnitPriceSnapshot,
            item.SubTotal,
            item.OrderItemBatchAllocations.FirstOrDefault()?.HarvestBatch?.BatchCode,
            item.OrderItemBatchAllocations.FirstOrDefault()?.HarvestBatch?.ExpirationDate
        )).ToList();

        return new PackingSlipResponse(
            order.SellerOrderId,
            order.SellerOrderCode,
            order.Order.OrderCode,
            order.Order.RecipientNameSnapshot,
            order.Order.RecipientPhoneSnapshot,
            order.Order.ShippingAddress,
            order.Order.OrderDate,
            order.Order.OrderNotes,
            items,
            order.Subtotal,
            order.DeliveryFee,
            order.Order.VoucherCodeSnapshot,
            order.DiscountShare,
            order.FinalAmount
        );
    }

    private void Dispatch(SellerOrder order, long userId, string requestKey, DateTime now)
    {
        var shipment = new Shipment { ShipmentStatus = "InTransit", ShippedAt = now };
        foreach (var allocation in order.OrderItems.SelectMany(x => x.OrderItemBatchAllocations))
        {
            allocation.HarvestBatch.ConsumeReservation(allocation.AllocatedQuantity);
            allocation.AllocationStatus = "Consumed";
            repository.AddMovement(new InventoryMovement
            {
                HarvestBatchId = allocation.HarvestBatchId,
                OrderItemBatchAllocationId = allocation.OrderItemBatchAllocationId,
                RecordedByUserId = userId,
                MovementType = "StockOut",
                QuantityChange = -allocation.AllocatedQuantity,
                Reason = "Simulated dispatch",
                OccurredAt = now,
                RequestKey = requestKey + ":" + allocation.OrderItemBatchAllocationId
            });
            shipment.ShipmentItems.Add(new ShipmentItem
            {
                OrderItemBatchAllocationId = allocation.OrderItemBatchAllocationId,
                ShippedQuantity = allocation.AllocatedQuantity,
                ReconciliationStatus = "Pending"
            });
        }

        order.Shipments.Add(shipment);
    }

    private void ReportDelivery(SellerOrder order, long userId,
        OrderActionRequest request, DateTime now)
    {
        foreach (var shipment in order.Shipments.Where(x =>
            x.ShipmentStatus is "InTransit" or "Shipping" or "Delivered"))
        {
            shipment.ShipmentStatus = request.Reason == "Return" ? "Returned" : "Failed";
        }

        order.Status = request.Reason == "Return" ? "Returned" : "DeliveryFailed";
        var ticket = new DisputeTicket
        {
            SubmittedByUserId = userId,
            DisputeReason = "DeliveryIssue",
            Description = request.Explanation,
            TicketStatus = "Open",
            CreatedAt = now
        };
        foreach (var url in request.EvidenceUrls ?? [])
        {
            ticket.DisputeEvidences.Add(new DisputeEvidence
            {
                SubmittedByUserId = userId,
                FileUrl = url,
                Description = request.Explanation,
                SubmittedAt = now
            });
        }

        order.DisputeTickets.Add(ticket);
    }

    private string? SubmitEvidence(SellerOrder order, long userId,
        OrderActionRequest request, DateTime now)
    {
        var evidenceRequest = order.DisputeTickets.SelectMany(x => x.DisputeEvidenceRequests)
            .SingleOrDefault(x => x.EvidenceRequestId == request.EvidenceRequestId
                && x.RequestedPartyUserId == userId);
        if (evidenceRequest is null || evidenceRequest.RespondedAt.HasValue
            || evidenceRequest.RequestStatus is "Closed" or "Responded")
        {
            return "Yêu cầu bằng chứng không hợp lệ hoặc đã trả lời.";
        }

        if (now > (evidenceRequest.DueAt ?? evidenceRequest.RequestedAt.AddHours(48)))
        {
            return "Đã hết hạn 48 giờ phản hồi yêu cầu bổ sung bằng chứng. Vui lòng liên hệ nhân viên hỗ trợ được phân công; phản hồi quá hạn chưa được tiếp nhận.";
        }

        foreach (string url in request.EvidenceUrls ?? [])
        {
            repository.AddEvidence(new DisputeEvidence
            {
                DisputeTicketId = evidenceRequest.DisputeTicketId,
                EvidenceRequestId = evidenceRequest.EvidenceRequestId,
                SubmittedByUserId = userId,
                FileUrl = url,
                Description = request.Explanation,
                SubmittedAt = now
            });
        }

        evidenceRequest.RespondedAt = now;
        evidenceRequest.RequestStatus = "Responded";
        return null;
    }

    private static bool IsOwnedEvidence(long userId, string? url) => url is not null
        && url.StartsWith($"/api/files/evidence/{userId}/", StringComparison.Ordinal)
        && !url.Contains("..") && url.Length < 500;

    private static FeatureResult<SellerOrderResponse> Fail(string code, string message) =>
        FeatureResult<SellerOrderResponse>.Fail(code, message);
}
