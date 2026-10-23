using AgriSustain.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

internal static class ApiResults
{
    /// <summary>
    /// Maps successful application values and stable error kinds to HTTP responses/ProblemDetails
    /// with the request trace identifier. Does not inspect localized message text to choose
    /// status.
    /// </summary>
    public static IActionResult From<T>(Result<T> result, HttpContext context)
    {
        if (result.Error == null)
        {
            return new OkObjectResult(result.Value);
        }

        int status = result.Error.Kind switch
        {
            "Unauthorized" => 401,
            "Forbidden" => 403,
            "NotFound" => 404,
            "Conflict" => 409,
            _ => 400
        };
        var problem = new ProblemDetails
        {
            Status = status,
            Title = result.Error.Message,
            Extensions = { ["code"] = result.Error.Code, ["traceId"] = context.TraceIdentifier }
        };
        return new ObjectResult(problem) { StatusCode = status };
    }
}
