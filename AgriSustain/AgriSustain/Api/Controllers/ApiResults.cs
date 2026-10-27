using AgriSustain.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

internal static class ApiResults
{
    public static IActionResult From<T>(Result<T> result, HttpContext context)
    {
        if (result.IsSuccess)
        {
            return new OkObjectResult(result.Value);
        }

        int status = result.Error?.Kind switch
        {
            "Unauthorized" => 401,
            "Forbidden" => 403,
            "NotFound" => 404,
            "Conflict" => 409,
            "TooManyRequests" => 429,
            "ServiceUnavailable" => 503,
            "Error" => 500,
            _ => 400
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = result.Error?.Message ?? result.Message,
        };

        problem.Extensions["code"] = result.Error?.Code ?? "ERROR";
        problem.Extensions["traceId"] = context.TraceIdentifier;

        // Include field-level validation errors if available
        if (result.Error?.FieldErrors is { Count: > 0 } fieldErrors)
        {
            problem.Extensions["errors"] = fieldErrors;
        }

        return new ObjectResult(problem) { StatusCode = status };
    }
}