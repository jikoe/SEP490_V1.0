using AgriSustain.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AgriSustain.Api.Controllers;

public sealed class FarmerBusinessInputFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
            context.Result = new BadRequestObjectResult(new ProblemDetails
            { Status = 400, Title = FarmerBusinessMessages.Text("MSG120") });
    }
    public void OnActionExecuted(ActionExecutedContext context) { }
}
