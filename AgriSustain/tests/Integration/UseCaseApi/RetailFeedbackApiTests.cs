using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Services.Complaints;
using AgriSustain.Domain.Entities;
using AgriSustain.IntegrationTests.UT1X;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.UseCaseApi;

public sealed class RetailFeedbackApiTests
{
    [CommerceSqlFact]
    [Trait("UseCase", "UC-RC-05")]
    public async Task Review_UpdatesEligibilityAndPersistsOneVerifiedReview()
    {
        await using var api = await ApiHost.StartAsync();
        await using var db = api.Open();
        var order = await db.Orders.Include(order => order.SellerOrders).SingleAsync();
        order.OrderStatus = "Completed";
        var seller = order.SellerOrders.Single(seller => seller.SellerOrderId == api.SellerOrderId);
        seller.Status = "Completed";
        seller.ReceivedAt = api.Clock.Now.UtcDateTime;
        await db.SaveChangesAsync();

        string feedbackPath = $"api/orders/{order.OrderId}/feedback";
        string reviewPath = $"api/orders/{order.OrderId}/reviews/{api.SpinachItemId}";
        using var owner = api.NewClient(api.OtherBuyerId, "RetailCustomer");
        var before = await owner.GetFromJsonAsync<OrderFeedbackContext>(feedbackPath);
        Assert.True(before!.Items.Single(item => item.Id == api.SpinachItemId).CanReview);

        using var invalid = ReviewForm("0", "Good produce");
        await ApiAssert.Problem(await owner.PostAsync(reviewPath, invalid),
            HttpStatusCode.BadRequest, "INVALID_REVIEW");
        using var foreign = ReviewForm("5", "Good produce");
        await ApiAssert.Problem(await api.Buyer().PostAsync(reviewPath, foreign),
            HttpStatusCode.Conflict, "REVIEW_NOT_ELIGIBLE");
        Assert.Empty(await db.ProductReviews.ToListAsync());

        using var valid = ReviewForm("5", "Good produce");
        var submitted = await ApiAssert.Status(await owner.PostAsync(reviewPath, valid), HttpStatusCode.OK);
        long id = submitted.GetProperty("id").GetInt64();
        var saved = await db.ProductReviews.AsNoTracking().SingleAsync();
        Assert.Equal(id, saved.ProductReviewId);
        Assert.Equal(api.OtherBuyerId, saved.UserId);
        Assert.Equal(api.SpinachItemId, saved.OrderItemId);
        Assert.True(saved.IsPurchasedVerified);
        Assert.Equal("Approved", saved.ModerationStatus);

        using var duplicate = ReviewForm("1", "Duplicate review");
        await ApiAssert.Problem(await owner.PostAsync(reviewPath, duplicate),
            HttpStatusCode.Conflict, "REVIEW_NOT_ELIGIBLE");
        using var modified = ReviewForm("4", "Updated review");
        await ApiAssert.Status(await owner.PutAsync(reviewPath, modified), HttpStatusCode.OK);
        var after = await owner.GetFromJsonAsync<OrderFeedbackContext>(feedbackPath);
        var item = after!.Items.Single(item => item.Id == api.SpinachItemId);
        Assert.False(item.CanReview);
        Assert.True(item.CanEditReview);
        Assert.Equal(new ExistingReview(4, "Updated review"), item.Review);
        var updated = await db.ProductReviews.AsNoTracking().SingleAsync();
        Assert.Equal(id, updated.ProductReviewId);
        Assert.Equal(saved.ReviewDate, updated.ReviewDate);
    }

    [CommerceSqlFact]
    [Trait("UseCase", "UC-RC-06")]
    public async Task SubmitComplaint_StoresOneTicketPerSellerAndKeepsEvidencePrivate()
    {
        await using var api = await ApiHost.StartAsync();
        await using var db = api.Open();
        var existing = await db.DisputeTickets.SingleAsync();
        existing.TicketStatus = DisputeTicket.StatusRejected;
        var order = await db.Orders.Include(order => order.SellerOrders)
            .ThenInclude(seller => seller.OrderItems).SingleAsync();
        var first = order.SellerOrders.Single(seller => seller.SellerOrderId == api.SellerOrderId);
        var second = order.SellerOrders.Single(seller => seller.SellerOrderId != api.SellerOrderId);
        first.DeliveredAt = api.Clock.Now.UtcDateTime.AddHours(-48);
        await db.SaveChangesAsync();
        long secondItem = Assert.Single(second.OrderItems).OrderItemId;
        string path = $"api/orders/{order.OrderId}/complaints";
        byte[] image = [255, 216, 255, 217];
        byte[] video = Convert.FromHexString("000000146674797069736F6D000000006D703432000000086D646174");

        using var noVideo = ComplaintForm([api.SpinachItemId], image, null);
        using var owner = api.NewClient(api.OtherBuyerId, "RetailCustomer");
        await ApiAssert.Problem(await owner.PostAsync(path, noVideo),
            HttpStatusCode.BadRequest, "INVALID_EVIDENCE");
        using var foreign = ComplaintForm([api.SpinachItemId], image, video);
        await ApiAssert.Problem(await api.Buyer().PostAsync(path, foreign),
            HttpStatusCode.Conflict, "COMPLAINT_NOT_ELIGIBLE");
        Assert.Equal(1, await db.DisputeTickets.CountAsync());
        Assert.Empty(await db.DisputeEvidences.ToListAsync());

        using var form = ComplaintForm([api.SpinachItemId, secondItem], image, video);
        using var response = await owner.PostAsync(path, form);
        await ApiAssert.Status(response, HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<ComplaintSubmissionResponse>())!;
        Assert.Equal(2, result.Ids.Count);
        var tickets = await db.DisputeTickets.AsNoTracking()
            .Include(ticket => ticket.DisputeEvidences).Include(ticket => ticket.DisputeEvidenceRequests)
            .Include(ticket => ticket.SellerOrder).ThenInclude(seller => seller!.FarmerProfile)
            .Where(ticket => result.Ids.Contains(ticket.DisputeTicketId)).ToListAsync();
        var selections = new Dictionary<long, long>
        {
            [first.SellerOrderId] = api.SpinachItemId,
            [second.SellerOrderId] = secondItem
        };
        Assert.Equal(selections.Keys.Order(), tickets.Select(ticket => ticket.SellerOrderId!.Value).Order());
        foreach (var ticket in tickets)
        {
            Assert.Equal(DisputeTicket.StatusWaitingInfo, ticket.TicketStatus);
            Assert.Contains($"#{selections[ticket.SellerOrderId!.Value]})", ticket.Description);
            Assert.Equal(2, ticket.DisputeEvidences.Count);
            var request = Assert.Single(ticket.DisputeEvidenceRequests);
            Assert.Equal(ticket.SellerOrder!.FarmerProfile.UserId, request.RequestedPartyUserId);
            Assert.Equal(2, await db.Notifications.CountAsync(notice => notice.RelatedRecordType == "DisputeTicket"
                && notice.RelatedRecordId == ticket.DisputeTicketId && notice.UserId == request.RequestedPartyUserId));
        }

        string url = tickets[0].DisputeEvidences.Single(file => file.FileUrl.EndsWith(".mp4")).FileUrl;
        Assert.Equal(video, await owner.GetByteArrayAsync(url));
        using var otherDownload = await api.Buyer().GetAsync(url);
        Assert.Equal(HttpStatusCode.NotFound, otherDownload.StatusCode);
        api.Clock.Advance(TimeSpan.FromSeconds(1));
        long unselected = first.OrderItems.Single(item => item.OrderItemId != api.SpinachItemId).OrderItemId;
        using var late = ComplaintForm([unselected], image, video);
        await ApiAssert.Problem(await owner.PostAsync(path, late),
            HttpStatusCode.Conflict, "COMPLAINT_NOT_ELIGIBLE");
        Assert.Equal(3, await db.DisputeTickets.CountAsync());
    }

    [CommerceSqlFact]
    [Trait("UseCase", "UC-RC-07")]
    public async Task RespondToComplaint_PersistsEvidenceAndNotifiesSupportOnlyOnce()
    {
        await using var api = await ApiHost.StartAsync();
        await using var db = api.Open();
        var ticket = await db.DisputeTickets.Include(ticket => ticket.DisputeEvidenceRequests).SingleAsync();
        var request = ticket.RequestInformation(api.OtherBuyerId, "Please provide a receipt.", api.SupportId,
            api.Clock.Now.UtcDateTime, TimeSpan.FromHours(48));
        await db.SaveChangesAsync();

        const string root = "api/customer/complaints";
        string path = $"{root}/{ticket.DisputeTicketId}";
        using var owner = api.NewClient(api.OtherBuyerId, "RetailCustomer");
        using var opened = await owner.GetAsync(path);
        Assert.True(opened.IsSuccessStatusCode,
            string.Join(Environment.NewLine, api.Logs.Lines.Where(line => line.Contains("Exception"))));
        var detail = (await opened.Content.ReadFromJsonAsync<Result<CustomerComplaintDetail>>())!.Data!;
        Assert.True(Assert.Single(detail.Requests).CanRespond);
        byte[] pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n");
        using var evidence = new MultipartFormDataContent();
        evidence.Add(new ByteArrayContent(pdf), "file", "receipt.pdf");
        var uploaded = await ApiAssert.Status(await owner.PostAsync(root + "/evidence", evidence),
            HttpStatusCode.OK);
        string fileUrl = uploaded.GetProperty("fileUrl").GetString()!;
        const string explanation = "The receipt shows the delivered parcel.";
        var input = new CustomerComplaintResponseRequest(request.EvidenceRequestId, explanation, [fileUrl],
            Guid.NewGuid().ToString("D"), detail.Version);

        string responsesPath = path + "/responses";
        await AssertComplaintError(await api.Buyer().PostAsJsonAsync(responsesPath, input),
            HttpStatusCode.Forbidden, "MSG121");
        await AssertComplaintError(await owner.PostAsJsonAsync(responsesPath,
            input with { Version = new string('A', 64) }), HttpStatusCode.Conflict, "MSG122");
        api.Clock.Advance(TimeSpan.FromHours(49));
        await AssertComplaintError(await owner.PostAsJsonAsync(responsesPath, input),
            HttpStatusCode.Conflict, "MSG166");
        api.Clock.Now = ApiHost.T0;
        Assert.Empty(await db.DisputeEvidences.ToListAsync());
        await ApiAssert.Status(await owner.PostAsJsonAsync(responsesPath, input), HttpStatusCode.OK);
        await AssertComplaintError(await owner.PostAsJsonAsync(responsesPath, input),
            HttpStatusCode.Conflict, "MSG122");

        var saved = (await owner.GetFromJsonAsync<Result<CustomerComplaintDetail>>(path))!.Data!;
        Assert.Equal(DisputeTicket.StatusUnderReview, saved.Status);
        var answered = Assert.Single(saved.Requests);
        Assert.Equal("Responded", answered.Status);
        Assert.False(answered.CanRespond);
        Assert.Equal(explanation, answered.Explanation);
        var file = Assert.Single(answered.Files);
        Assert.Equal(pdf, await owner.GetByteArrayAsync(path + $"/evidence/{file.Id}"));
        var stored = await db.DisputeEvidences.AsNoTracking().SingleAsync();
        Assert.Equal(request.EvidenceRequestId, stored.EvidenceRequestId);
        Assert.Equal(fileUrl, stored.FileUrl);
        Assert.Equal(1, await db.AuditLogs.CountAsync(log => log.Action == CustomerComplaintService.ResponseAction));
        Assert.Equal(new[] { "Email", "InApp" }, await db.Notifications.Where(notice =>
                notice.UserId == api.SupportId && notice.RelatedRecordType == "DisputeTicket"
                && notice.RelatedRecordId == ticket.DisputeTicketId)
            .OrderBy(notice => notice.DeliveryChannel).Select(notice => notice.DeliveryChannel).ToListAsync());
    }

    private static FormUrlEncodedContent ReviewForm(string rating, string comment) =>
        new(new Dictionary<string, string> { ["Rating"] = rating, ["Comment"] = comment });

    private static async Task AssertComplaintError(HttpResponseMessage response, HttpStatusCode status,
        string code)
    {
        using (response)
        {
            var problem = await ApiAssert.Status(response, status);
            Assert.Equal(code, problem.GetProperty("error").GetProperty("code").GetString());
        }
    }

    private static MultipartFormDataContent ComplaintForm(IEnumerable<long> items, byte[] image, byte[]? video)
    {
        var form = new MultipartFormDataContent();
        foreach (long item in items)
        {
            form.Add(new StringContent(item.ToString(CultureInfo.InvariantCulture)), "OrderItemIds");
        }
        form.Add(new StringContent("Damaged"), "Reason");
        form.Add(new StringContent("The selected produce arrived damaged."), "Description");
        form.Add(new ByteArrayContent(image), "Files", "parcel.jpg");
        if (video is not null)
        {
            form.Add(new ByteArrayContent(video), "Files", "parcel.mp4");
        }
        return form;
    }
}
