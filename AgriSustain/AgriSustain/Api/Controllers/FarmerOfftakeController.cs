using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Authorize(Policy = "FarmerOfftake")]
[Route("api/farmer/offtake-proposals")]
public sealed class FarmerOfftakeController(IOfftakeService service) : ApiControllerBase
{
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct) =>
        ToActionResult(await service.FarmerDetailAsync(id, ct));
}
