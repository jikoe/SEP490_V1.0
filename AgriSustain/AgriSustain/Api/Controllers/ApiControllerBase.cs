using AgriSustain.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers
{
    [ApiController]
    public class ApiControllerBase : Controller
    {
        protected IActionResult ToActionResult(Result result) =>
            StatusCode(result.StatusCode, result);

        protected IActionResult ToActionResult<T>(Result<T> result) =>
            StatusCode(result.StatusCode, result);
    }
}
