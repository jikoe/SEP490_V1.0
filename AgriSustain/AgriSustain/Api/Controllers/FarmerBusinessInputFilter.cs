using AgriSustain.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AgriSustain.Api.Controllers;

public sealed class FarmerBusinessInputFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var message = FarmerBusinessMessages.Text("MSG120");
            var fields = context
                .ModelState.Where(entry =>
                    entry.Value?.Errors.Count > 0 && !string.IsNullOrEmpty(entry.Key)
                )
                .Select(entry => entry.Key.TrimStart('$', '.'))
                .Where(key => key.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(key => key, _ => new[] { message });
            context.Result = new BadRequestObjectResult(
                new ValidationProblemDetails(fields) { Status = 400, Title = message }
            );
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
