using AgriSustain.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[ApiController]
public abstract class UseCaseControllerBase : ControllerBase
{
    protected IActionResult Respond<T>(UseCaseResult<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }
        var error = result.Error!;
        var status = error.Kind switch
        {
            "Forbidden" => 403, "NotFound" => 404, "Conflict" => 409, _ => 400
        };
        if (error.Fields is not null)
        {
            return new ObjectResult(new ValidationProblemDetails(error.Fields.ToDictionary(
                x => x.Key, x => x.Value))
            {
                Status = 400, Title = error.Message,
                Extensions =
                {
                    ["code"] = error.Code, ["traceId"] = HttpContext.TraceIdentifier
                }
            }) { StatusCode = 400 };
        }
        return new ObjectResult(new ProblemDetails
        {
            Status = status, Title = error.Message,
            Extensions = new Dictionary<string, object?>
            {
                ["code"] = error.Code, ["traceId"] = HttpContext.TraceIdentifier
            }
        }) { StatusCode = status };
    }
}
