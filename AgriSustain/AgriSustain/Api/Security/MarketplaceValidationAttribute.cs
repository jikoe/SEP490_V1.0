using AgriSustain.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AgriSustain.Api.Security;

public sealed class MarketplaceValidationAttribute : ActionFilterAttribute
{
    public MarketplaceValidationAttribute()
    {
        Order = -3000;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(Result.Fail(
                "MSG40: Vui lòng nhập từ khóa hoặc giá trị bộ lọc hợp lệ."));
        }
    }
}
