using AgriSustain.Application.DTOs.Checkout;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>
/// Goship always has provinces, so an empty list means the catalog could not be loaded (for example an
/// expired Goship access token). It is reported as 503 instead of an empty 200, so the address screens
/// show an error rather than empty dropdowns. 503, not 401, so the caller's own session is kept.
/// </summary>
public static class AddressCatalogResults
{
    public const string UnavailableCode = "ADDRESS_CATALOG_UNAVAILABLE";

    public const string UnavailableMessage =
        "Hiện chưa tải được danh mục tỉnh / thành phố từ Goship. Vui lòng thử lại sau.";

    public static IActionResult Provinces(IReadOnlyList<ProvinceOption> provinces, HttpContext httpContext) =>
        provinces.Count > 0
            ? new OkObjectResult(provinces)
            : new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = UnavailableMessage,
                Extensions =
                {
                    ["code"] = UnavailableCode, ["traceId"] = httpContext.TraceIdentifier
                }
            }) { StatusCode = StatusCodes.Status503ServiceUnavailable };
}
