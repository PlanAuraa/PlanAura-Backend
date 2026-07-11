using Planura.Shared.Errors.Response;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace EventManagment.Apis.Controller.Controllers.Common
{
    [ApiController]
    [Route("Errors/{Code}")]
    [ApiExplorerSettings(IgnoreApi = false)]
    public class ErrorsController : ControllerBase
    {
        // Responds to every verb: UseStatusCodePagesWithReExecute re-executes the original request
        // (which may be POST/PUT/DELETE) against this route, so restricting to GET would yield a 405.
        [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE")]
        public IActionResult Error(int Code)
        {
            if (Code == (int)HttpStatusCode.NotFound)
            {
                var respnse = new ApiResponse((int)HttpStatusCode.NotFound, $"the requested endpoint  is not found");
                return NotFound(respnse);
            }
            else if (Code == (int)HttpStatusCode.Forbidden)
            {
                var respnse = new ApiResponse((int)HttpStatusCode.Forbidden, $"You Are Not Authorized");
                return StatusCode((int)HttpStatusCode.Forbidden, respnse);

            }
            else if (Code == (int)HttpStatusCode.MethodNotAllowed)
            {
                var respnse = new ApiResponse((int)HttpStatusCode.MethodNotAllowed, $"Method Not Allowed");
                return StatusCode(Code, respnse);

            }
            return StatusCode(Code, new ApiResponse(Code));

        }

    }
}
