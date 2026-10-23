using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class OrderFulfillmentService(IOrderRepository repository, IUnitOfWork unitOfWork,
    FulfillmentRules rules, TimeProvider clock, IFileStorageService storage)
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
                "Bộ lọc hoặc khoảng ngày không hợp lệ (MSG120).");
        }

        return FeatureResult<PageResult<SellerOrderResponse>>.Ok(
            await repository.SearchOwnedAsync(userId, query, cancellationToken));
    }

    public async Task<FeatureResult<SellerOrderResponse>> GetAsync(long userId, long id,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetOwnedAsync(userId, id, cancellationToken);
        return order is null ? Fail("NOT_FOUND", "Không tìm thấy đơn hàng của bạn (MSG121).")
            : FeatureResult<SellerOrderResponse>.Ok(repository.Map(order));
    }

    public async Task<FeatureResult<SellerOrderResponse>> ActAsync(long userId, long id,
        string action, OrderActionRequest request, CancellationToken cancellationToken)
    {
        if (request is null || !Guid.TryParseExact(request.RequestKey, "D", out _)
            || request.Version?.Length != 64 || request.Reason?.Length > 100
            || request.Explanation?.Length > 2000
            || request.EvidenceUrls?.Length > 5)
        {
            return Fail("VALIDATION", "Thông tin thao tác không hợp lệ (MSG120).");
        }

        if (action is "reject" && (string.IsNullOrWhiteSpace(request.Reason)
            || request.Reason is not ("Quality" or "Shortfall" or "ForceMajeure")
            || request.Explanation?.Trim().Length < 15 || request.Explanation is null))
        {
            return Fail("VALIDATION", "Chọn lý do và giải thích ít nhất 15 ký tự (MSG70).");
        }

        if (action == "delivery-report" && request.Reason is not ("DeliveryFailure" or "Return"))
        {
            return Fail("VALIDATION", "Chọn loại báo cáo giao hàng.");
        }

        if (action is "delivery-report" or "evidence" &&
            (string.IsNullOrWhiteSpace(request.Explanation)
                || request.Explanation.Trim().Length < 15
                || request.EvidenceUrls is not { Length: > 0 }))
        {
            return Fail("VALIDATION", "Nhập giải trình (ít nhất 15 ký tự) và bằng chứng.");
        }

        if (request.EvidenceUrls?.Any(url => !IsOwnedEvidence(userId, url) || !storage.OwnsEvidence(userId, url)) == true)
        {
            return Fail("VALIDATION", "Tệp bằng chứng không thuộc tài khoản của bạn.");
        }

        await unitOfWork.BeginAsync(cancellationToken);
        if (!await repository.IsApprovedFarmerAsync(userId, cancellationToken))
        {
            return Fail("FORBIDDEN", "Hồ sơ nông trại chưa được duyệt (MSG75).");
        }

        var order = await repository.GetOwnedAsync(userId, id, cancellationToken);
        if (order is null)
        {
            return Fail("NOT_FOUND", "Không tìm thấy đơn hàng của bạn (MSG121).");
        }

        if (await repository.HasRequestAsync(request.RequestKey, cancellationToken)
            || FulfillmentRules.Version(order) != request.Version)
        {
            return Fail("CONFLICT", "Thao tác đã được xử lý hoặc dữ liệu đã đổi (MSG122).");
        }

        if (order.B2bcontractId.HasValue)
        {
            return Fail("CONFLICT", "Đơn hợp đồng được xử lý qua quản lý hợp đồng UC-FAR-08.");
        }

        string previous = order.Status;
        var now = clock.GetUtcNow().UtcDateTime;
        string? error = action switch
        {
            "confirm" when order.Status != "Pending" => "Trạng thái không hợp lệ (MSG131).",
            "ready" when !FulfillmentRules.IsConfirmed(order.Status) =>
                "Đơn chưa được xác nhận (MSG131).",
            "dispatch" when !FulfillmentRules.IsReady(order.Status) =>
                "Đơn chưa sẵn sàng giao (MSG131).",
            "reject" when !FulfillmentRules.IsBeforeShipping(order.Status) =>
                "Chỉ từ chối trước khi giao (MSG131).",
            "delivery-report" when !FulfillmentRules.IsShipping(order.Status)
                && order.Status is not ("Delivered" or "DeliveryFailed") =>
                "Đơn chưa giao hoặc không còn nhận báo cáo (MSG131).",
            _ => null
        };
        if (error is not null)
        {
            return Fail("CONFLICT", error);
        }

        if (action is "confirm" or "ready" or "dispatch"
            && (!rules.PaymentSatisfied(order) || !rules.AllocationsValid(order)))
        {
            return Fail("CONFLICT", "Thiếu thanh toán/cọc đã xác minh hoặc lô hợp lệ (MSG148).");
        }

        switch (action)
        {
            case "confirm":
                order.Status = "Confirmed";
                break;
            case "ready":
                order.Status = "ReadyToShip";
                break;
            case "dispatch":
                Dispatch(order, userId, request.RequestKey, now);
                break;
            case "reject":
                Reject(order, now);
                await repository.ReleasePurchaseHoldsAsync(order, cancellationToken);
                if (rules.VerifiedPayment(order) > 0)
                {
                    await repository.QueueFinanceReviewAsync(order, userId, cancellationToken);
                }
                break;
            case "delivery-report":
                ReportDelivery(order, userId, request, now);
                break;
            case "evidence":
                error = SubmitEvidence(order, userId, request, now);
                if (error is not null)
                {
                    return Fail("CONFLICT", error);
                }
                break;
            default:
                return Fail("VALIDATION", "Thao tác không được hỗ trợ.");
        }

        string historyReason = request.Reason is null ? action
            : request.Reason + ": " + request.Explanation;
        if (order.Status != previous)
        {
            order.OrderStatusHistories.Add(new OrderStatusHistory
            {
                ChangedByUserId = userId, OldStatus = previous, NewStatus = order.Status,
                Reason = historyReason[..Math.Min(historyReason.Length, 500)],
                OccurredAt = now
            });
        }

        FulfillmentRules.Aggregate(order.Order);
        repository.AddAudit(new AuditLog
        {
            PerformedByUserId = userId, EntityType = "SellerOrder", EntityId = id,
            Action = action, TraceId = request.RequestKey, OldValue = previous,
            NewValue = JsonSerializer.Serialize(new { order.Status, request.Explanation }),
            OccurredAt = now
        });
        repository.Notify(order.Order.BuyerUserId, "Cập nhật đơn hàng",
            $"Đơn {order.SellerOrderCode}: {order.Status}.", order.SellerOrderId);
        await unitOfWork.CommitAsync(cancellationToken);
        return FeatureResult<SellerOrderResponse>.Ok(repository.Map(order));
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
                RecordedByUserId = userId, MovementType = "StockOut",
                QuantityChange = -allocation.AllocatedQuantity, Reason = "Simulated dispatch",
                OccurredAt = now, RequestKey = requestKey + ":" + allocation.OrderItemBatchAllocationId
            });
            shipment.ShipmentItems.Add(new ShipmentItem
            {
                OrderItemBatchAllocationId = allocation.OrderItemBatchAllocationId,
                ShippedQuantity = allocation.AllocatedQuantity, ReconciliationStatus = "Pending"
            });
        }

        order.Shipments.Add(shipment);
        order.Status = "Shipping";
    }

    private void Reject(SellerOrder order, DateTime now)
    {
        foreach (var allocation in order.OrderItems.SelectMany(x => x.OrderItemBatchAllocations)
            .Where(x => x.AllocationStatus == "Reserved"))
        {
            allocation.HarvestBatch.ReleaseReservation(allocation.AllocatedQuantity);
            allocation.AllocationStatus = "Released";
            allocation.ReleasedAt = now;
        }

        order.Status = "Cancelled";
        if (rules.VerifiedPayment(order) > 0)
        {
            // A durable notification queues Finance review; no transfer is claimed as completed.
            repository.Notify(order.Order.BuyerUserId, "Chờ xử lý hoàn tiền (MSG162)",
                $"Đơn {order.SellerOrderCode} đã hủy; khoản thu đang chờ Finance đối soát.",
                order.SellerOrderId);
        }
    }

    private void ReportDelivery(SellerOrder order, long userId,
        OrderActionRequest request, DateTime now)
    {
        foreach (var shipment in order.Shipments.Where(x =>
            x.ShipmentStatus is "InTransit" or "Shipping" or "Delivered"))
        {
            shipment.ShipmentStatus = request.Reason == "Return" ? "Returned" : "Failed";
        }

        // Reporting a return never makes the returned quantity saleable again.
        order.Status = request.Reason == "Return" ? "Returned" : "DeliveryFailed";
        var ticket = new DisputeTicket
        {
            SubmittedByUserId = userId, DisputeReason = "DeliveryIssue",
            Description = request.Explanation, TicketStatus = "Open", CreatedAt = now
        };
        foreach (var url in request.EvidenceUrls ?? [])
        {
            ticket.DisputeEvidences.Add(new DisputeEvidence
            {
                SubmittedByUserId = userId, FileUrl = url,
                Description = request.Explanation, SubmittedAt = now
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
            return "Yêu cầu bằng chứng không hợp lệ hoặc đã trả lời (MSG122).";
        }

        if (now > (evidenceRequest.DueAt ?? evidenceRequest.RequestedAt.AddHours(48)))
        {
            return "Đã hết hạn 48 giờ bổ sung bằng chứng (MSG166).";
        }

        foreach (string url in request.EvidenceUrls ?? [])
        {
            repository.AddEvidence(new DisputeEvidence
            {
                DisputeTicketId = evidenceRequest.DisputeTicketId,
                EvidenceRequestId = evidenceRequest.EvidenceRequestId,
                SubmittedByUserId = userId, FileUrl = url,
                Description = request.Explanation, SubmittedAt = now
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




