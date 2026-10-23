using AgriSustain.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[ApiController]
public abstract class FeatureControllerBase : ControllerBase
{
    protected IActionResult Respond<T>(FeatureResult<T> result)
    {
        if (result.Error is not { } error)
        {
            return Ok(result.Value);
        }

        int status = error.Code switch
        {
            "NOT_FOUND" => 404, "FORBIDDEN" => 403, "CONFLICT" => 409, _ => 400
        };
        ProblemDetails problem = error.Fields is null
            ? new ProblemDetails() : new ValidationProblemDetails(error.Fields.ToDictionary());
        problem.Status = status;
        problem.Title = error.Message;
        problem.Extensions["code"] = error.Code;
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return StatusCode(status, problem);
    }
}

