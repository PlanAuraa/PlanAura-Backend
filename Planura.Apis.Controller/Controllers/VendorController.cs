using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planura.Core.Application.Common;
using Planura.Core.Application.Models.Vendor;
using Planura.Core.Application.Services;
using Planura.Shared.Errors.Models;

namespace Planura.Apis.Controllers;

[ApiController]
[Route("api/vendors")]
public class VendorController : ControllerBase
{
    private readonly IVendorService _vendorService;
    private readonly ICurrentUserService _currentUserService;

    public VendorController(IVendorService vendorService, ICurrentUserService currentUserService)
    {
        _vendorService = vendorService;
        _currentUserService = currentUserService;
    }

    [AllowAnonymous]
    [HttpGet("{id:long}")]
    public async Task<ActionResult<VendorDto>> GetById(long id)
    {
        return Ok(await _vendorService.GetByIdAsync(id));
    }

    [Authorize(Policy = AuthorizationPolicies.VendorOnly)]
    [HttpGet("me")]
    public async Task<ActionResult<VendorDto>> GetMyProfile()
    {
        var vendorId = _currentUserService.VendorId
            ?? throw new UnAuthorizedExeption("No vendor profile is associated with this account.");

        return Ok(await _vendorService.GetByIdAsync(vendorId));
    }

    [Authorize(Policy = AuthorizationPolicies.VendorOnly)]
    [HttpPut("me")]
    public async Task<ActionResult<VendorDto>> UpdateMyProfile([FromForm] UpdateVendorProfileDto dto)
    {
        var vendorId = _currentUserService.VendorId
            ?? throw new UnAuthorizedExeption("No vendor profile is associated with this account.");

        return Ok(await _vendorService.UpdateProfileAsync(vendorId, dto));
    }

    [AllowAnonymous]
    [HttpGet("{id:long}/portfolio/media")]
    public async Task<ActionResult<IEnumerable<PortfolioMediaItemDto>>> GetPortfolioMedia(long id)
    {
        return Ok(await _vendorService.GetPortfolioMediaAsync(id));
    }

    [AllowAnonymous]
    [HttpGet("{id:long}/portfolio/links")]
    public async Task<ActionResult<IEnumerable<PortfolioLinkDto>>> GetPortfolioLinks(long id)
    {
        return Ok(await _vendorService.GetPortfolioLinksAsync(id));
    }

    [Authorize(Policy = AuthorizationPolicies.VendorOnly)]
    [HttpPost("me/portfolio/media")]
    public async Task<ActionResult<PortfolioMediaItemDto>> AddMyPortfolioMedia([FromForm] AddPortfolioMediaDto dto)
    {
        var vendorId = _currentUserService.VendorId
            ?? throw new UnAuthorizedExeption("No vendor profile is associated with this account.");

        var result = await _vendorService.AddPortfolioMediaAsync(vendorId, dto);
        return Ok(result);
    }

    [Authorize(Policy = AuthorizationPolicies.VendorOnly)]
    [HttpDelete("me/portfolio/media/{mediaId:long}")]
    public async Task<ActionResult> RemoveMyPortfolioMedia(long mediaId)
    {
        var vendorId = _currentUserService.VendorId
            ?? throw new UnAuthorizedExeption("No vendor profile is associated with this account.");

        await _vendorService.RemovePortfolioMediaAsync(vendorId, mediaId);
        return NoContent();
    }

    [Authorize(Policy = AuthorizationPolicies.VendorOnly)]
    [HttpPut("me/portfolio/media/reorder")]
    public async Task<ActionResult> ReorderMyPortfolioMedia([FromBody] ReorderPortfolioMediaDto dto)
    {
        var vendorId = _currentUserService.VendorId
            ?? throw new UnAuthorizedExeption("No vendor profile is associated with this account.");

        await _vendorService.ReorderPortfolioMediaAsync(vendorId, dto);
        return NoContent();
    }

    [Authorize(Policy = AuthorizationPolicies.VendorOnly)]
    [HttpPost("me/portfolio/links")]
    public async Task<ActionResult<PortfolioLinkDto>> AddMyPortfolioLink([FromBody] CreatePortfolioLinkDto dto)
    {
        var vendorId = _currentUserService.VendorId
            ?? throw new UnAuthorizedExeption("No vendor profile is associated with this account.");

        var result = await _vendorService.AddPortfolioLinkAsync(vendorId, dto);
        return Ok(result);
    }

    [Authorize(Policy = AuthorizationPolicies.VendorOnly)]
    [HttpDelete("me/portfolio/links/{linkId:long}")]
    public async Task<ActionResult> RemoveMyPortfolioLink(long linkId)
    {
        var vendorId = _currentUserService.VendorId
            ?? throw new UnAuthorizedExeption("No vendor profile is associated with this account.");

        await _vendorService.RemovePortfolioLinkAsync(vendorId, linkId);
        return NoContent();
    }
}
