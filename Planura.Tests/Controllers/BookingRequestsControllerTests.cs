using Microsoft.AspNetCore.Mvc;
using Moq;
using Planura.Apis.Controllers;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;
using Planura.Core.Application.Services.Booking;
using Planura.Core.Domain.Enums;
using Planura.Shared.Errors.Models;
using Xunit;

namespace Planura.Tests.Controllers;

public class BookingRequestsControllerTests
{
    private const long CurrentUserId = 500;

    private readonly Mock<IBookingService> _bookingServiceMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();

    private BookingRequestsController CreateController()
    {
        _currentUserServiceMock.Setup(c => c.UserId).Returns(CurrentUserId);
        return new BookingRequestsController(_bookingServiceMock.Object, _currentUserServiceMock.Object);
    }

    private static BookingRequestDto CreateDto(long id = 1) => new()
    {
        Id = id,
        ClientId = 10,
        VendorId = 20,
        EventPlanId = 30,
        Status = BookingStatus.Pending,
        PaymentStatus = BookingPaymentStatus.Unpaid
    };

    [Fact]
    public async Task Create_Valid_ReturnsCreatedAtActionWithDto()
    {
        var dto = new CreateBookingRequestDto { EventPlanId = 30, AvailabilityId = 40 };
        var expected = CreateDto();

        _bookingServiceMock
            .Setup(s => s.CreateBookingRequestAsync(CurrentUserId, dto))
            .ReturnsAsync(expected);

        var controller = CreateController();
        var result = await controller.Create(dto);

        var createdAtAction = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(BookingRequestsController.GetById), createdAtAction.ActionName);
        Assert.Equal(expected, createdAtAction.Value);
    }

    [Fact]
    public async Task List_Valid_ReturnsOkWithPagedResult()
    {
        var filter = new BookingRequestFilterDto();
        var expected = new PagedResult<BookingRequestDto>
        {
            Items = new List<BookingRequestDto> { CreateDto() },
            TotalCount = 1,
            Page = 1,
            PageSize = 20
        };

        _bookingServiceMock
            .Setup(s => s.ListMyBookingRequestsAsync(CurrentUserId, filter))
            .ReturnsAsync(expected);

        var controller = CreateController();
        var result = await controller.List(filter);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expected, okResult.Value);
    }

    [Fact]
    public async Task GetById_Valid_ReturnsOkWithDto()
    {
        var expected = CreateDto();
        _bookingServiceMock
            .Setup(s => s.GetBookingRequestAsync(1, CurrentUserId))
            .ReturnsAsync(expected);

        var controller = CreateController();
        var result = await controller.GetById(1);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expected, okResult.Value);
    }

    [Fact]
    public async Task GetById_NotFound_PropagatesException()
    {
        _bookingServiceMock
            .Setup(s => s.GetBookingRequestAsync(1, CurrentUserId))
            .ThrowsAsync(new NotFoundExeption("BookingRequest", 1));

        var controller = CreateController();

        await Assert.ThrowsAsync<NotFoundExeption>(() => controller.GetById(1));
    }

    [Fact]
    public async Task Cancel_Valid_ReturnsOkWithDto()
    {
        var expected = CreateDto();
        expected.Status = BookingStatus.Cancelled;

        _bookingServiceMock
            .Setup(s => s.CancelBookingRequestAsync(1, CurrentUserId))
            .ReturnsAsync(expected);

        var controller = CreateController();
        var result = await controller.Cancel(1);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expected, okResult.Value);
    }

    [Fact]
    public async Task Cancel_NotPending_PropagatesException()
    {
        _bookingServiceMock
            .Setup(s => s.CancelBookingRequestAsync(1, CurrentUserId))
            .ThrowsAsync(new BadRequestExeption("Cannot cancel."));

        var controller = CreateController();

        await Assert.ThrowsAsync<BadRequestExeption>(() => controller.Cancel(1));
    }

    [Fact]
    public async Task Dispute_Valid_ReturnsOkWithDto()
    {
        var dto = new FlagDisputeDto { Reason = "Vendor never showed up." };
        var expected = CreateDto();
        expected.DisputeStatus = DisputeStatus.Open;

        _bookingServiceMock
            .Setup(s => s.FlagDisputeAsync(1, CurrentUserId, dto.Reason))
            .ReturnsAsync(expected);

        var controller = CreateController();
        var result = await controller.Dispute(1, dto);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expected, okResult.Value);
    }

    [Fact]
    public async Task Dispute_EmptyReason_PropagatesException()
    {
        var dto = new FlagDisputeDto { Reason = "" };

        _bookingServiceMock
            .Setup(s => s.FlagDisputeAsync(1, CurrentUserId, dto.Reason))
            .ThrowsAsync(new BadRequestExeption("A dispute reason is required."));

        var controller = CreateController();

        await Assert.ThrowsAsync<BadRequestExeption>(() => controller.Dispute(1, dto));
    }

    [Fact]
    public async Task GetById_NoAuthenticatedUser_ThrowsUnAuthorized()
    {
        _currentUserServiceMock.Setup(c => c.UserId).Returns((long?)null);
        var controller = new BookingRequestsController(_bookingServiceMock.Object, _currentUserServiceMock.Object);

        await Assert.ThrowsAsync<UnAuthorizedExeption>(() => controller.GetById(1));
    }
}
