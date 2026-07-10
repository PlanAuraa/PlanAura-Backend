using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Abstraction.Vendors;
using Planura.Core.Application.Abstraction.Vendors.Contracts;

namespace Planura.Apis.Controller.Controllers.Vendors
{
    [ApiController]
    [Route("api/vendor-categories")]
    public class VendorCategoriesController : ControllerBase
    {
        private readonly IVendorCategoryService _vendorCategoryService;

        public VendorCategoriesController(IVendorCategoryService vendorCategoryService)
        {
            _vendorCategoryService = vendorCategoryService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IReadOnlyList<VendorCategoryDto>>> GetAll(CancellationToken ct)
        {
            var categories = await _vendorCategoryService.GetActiveCategoriesAsync(ct);
            return Ok(categories);
        }
    }
}
