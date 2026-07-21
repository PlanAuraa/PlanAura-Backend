using Microsoft.AspNetCore.Mvc;
using Moq;
using Planura.Apis.Controllers;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;
//using Planura.Core.Application.Services;
using Planura.Shared.Errors.Models;
using Xunit;

namespace Planura.Tests.Controllers;

public class EventPlansControllerTests
{
    private const long CurrentUserId = 500;

    private readonly Mock<IEventPlanService> _eventPlanServiceMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();

    private EventPlansController CreateController()
    {
        _currentUserServiceMock.Setup(c => c.UserId).Returns(CurrentUserId);
        return new EventPlansController(_eventPlanServiceMock.Object, _currentUserServiceMock.Object);
    }

    private static EventPlanDto CreateDto(long id = 1) => new()
    {
        Id = id,
        ClientId = 10,
        EventType = "Wedding",
        Status = "draft"
    };

    [Fact]
    public async Task Create_Valid_ReturnsCreatedAtActionWithDto()
    {
        var dto = new CreateEventPlanDto { EventType = "Wedding" };
        var expected = CreateDto();

        _eventPlanServiceMock
            .Setup(s => s.CreateEventPlanAsync(CurrentUserId, dto))
            .ReturnsAsync(expected);

        var controller = CreateController();
        var result = await controller.Create(dto);

        var createdAtAction = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(EventPlansController.GetById), createdAtAction.ActionName);
        Assert.Equal(expected, createdAtAction.Value);
    }

    [Fact]
    public async Task List_Valid_ReturnsOkWithPlans()
    {
        var expected = new List<EventPlanDto> { CreateDto() };
        _eventPlanServiceMock
            .Setup(s => s.ListMyEventPlansAsync(CurrentUserId))
            .ReturnsAsync(expected);

        var controller = CreateController();
        var result = await controller.List();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expected, okResult.Value);
    }

    [Fact]
    public async Task GetById_Valid_ReturnsOkWithDto()
    {
        var expected = CreateDto();
        _eventPlanServiceMock
            .Setup(s => s.GetEventPlanAsync(1, CurrentUserId))
            .ReturnsAsync(expected);

        var controller = CreateController();
        var result = await controller.GetById(1);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expected, okResult.Value);
    }

    [Fact]
    public async Task GetById_NotFound_PropagatesException()
    {
        _eventPlanServiceMock
            .Setup(s => s.GetEventPlanAsync(1, CurrentUserId))
            .ThrowsAsync(new NotFoundExeption("EventPlan", 1));

        var controller = CreateController();

        await Assert.ThrowsAsync<NotFoundExeption>(() => controller.GetById(1));
    }

    [Fact]
    public async Task Update_Valid_ReturnsOkWithDto()
    {
        var dto = new UpdateEventPlanDto { EventType = "Wedding" };
        var expected = CreateDto();

        _eventPlanServiceMock
            .Setup(s => s.UpdateEventPlanAsync(1, CurrentUserId, dto))
            .ReturnsAsync(expected);

        var controller = CreateController();
        var result = await controller.Update(1, dto);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expected, okResult.Value);
    }

    [Fact]
    public async Task Update_NotFound_PropagatesException()
    {
        var dto = new UpdateEventPlanDto { EventType = "Wedding" };
        _eventPlanServiceMock
            .Setup(s => s.UpdateEventPlanAsync(1, CurrentUserId, dto))
            .ThrowsAsync(new NotFoundExeption("EventPlan", 1));

        var controller = CreateController();

        await Assert.ThrowsAsync<NotFoundExeption>(() => controller.Update(1, dto));
    }

    [Fact]
    public async Task Delete_Valid_ReturnsNoContent()
    {
        _eventPlanServiceMock
            .Setup(s => s.DeleteEventPlanAsync(1, CurrentUserId))
            .Returns(Task.CompletedTask);

        var controller = CreateController();
        var result = await controller.Delete(1);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_HasLinkedBookings_PropagatesException()
    {
        _eventPlanServiceMock
            .Setup(s => s.DeleteEventPlanAsync(1, CurrentUserId))
            .ThrowsAsync(new BadRequestExeption("Cannot delete an event plan that has booking requests linked to it."));

        var controller = CreateController();

        await Assert.ThrowsAsync<BadRequestExeption>(() => controller.Delete(1));
    }

    [Fact]
    public async Task GetById_NoAuthenticatedUser_ThrowsUnAuthorized()
    {
        _currentUserServiceMock.Setup(c => c.UserId).Returns((long?)null);
        var controller = new EventPlansController(_eventPlanServiceMock.Object, _currentUserServiceMock.Object);

        await Assert.ThrowsAsync<UnAuthorizedExeption>(() => controller.GetById(1));
    }
}
