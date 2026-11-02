using AgriSustain.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AgriSustain.Api.Controllers;

public sealed class FarmInputFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ModelState.IsValid)
            return;
        var fields = context.ModelState.Where(x => x.Value?.Errors.Count > 0).ToDictionary(x => x.Key, x => new[] { FarmManagementMessages.MSG120 });
        context.Result = new BadRequestObjectResult(new ValidationProblemDetails(fields) { Status = 400, Title = FarmManagementMessages.MSG120 });
    }


    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}
