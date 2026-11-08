using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces;
using AgriSustain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/business-notifications")]
public sealed class BusinessNotificationController(AgriSustainContext db, ICurrentActor actor) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (page < 1 || page > 100000) return BadRequest(new { message = FarmerBusinessMessages.Text("MSG120") });
        var owned = db.Notifications.AsNoTracking().Where(x => x.UserId == actor.UserId
            && x.DeliveryChannel == "InApp" && x.DeliveryStatus == "Sent");
        return Ok(new
        {
            total = await owned.CountAsync(ct),
            unread = await owned.CountAsync(x => !x.IsRead, ct),
            page,
            items = await owned.OrderByDescending(x => x.NotificationId).Skip((page - 1) * 20).Take(20)
                .Select(x => new
                {
                    id = x.NotificationId,
                    x.Title,
                    x.Content,
                    x.SentAt,
                    x.IsRead,
                    x.RelatedRecordType,
                    x.RelatedRecordId
                }).ToListAsync(ct)
        });
    }

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
    {
        int updated = await db.Notifications.Where(x => x.NotificationId == id && x.UserId == actor.UserId
            && x.DeliveryChannel == "InApp").ExecuteUpdateAsync(s => s.SetProperty(x => x.IsRead, true), ct);
        return updated == 1 ? Ok() : NotFound();
    }
}
