using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Planura.Apis.Controllers;
using Planura.Core.Application.Models;
using Planura.Core.Application.Services;
using Planura.Shared.Errors.Models;
using Xunit;

namespace Planura.Tests.Controllers;

public class PaymentsControllerTests
{
    private const long CurrentUserId = 500;

    private readonly Mock<IPaymentService> _paymentServiceMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();

    private PaymentsController CreateController()
    {
        _currentUserServiceMock.Setup(c => c.UserId).Returns(CurrentUserId);
        return new PaymentsController(_paymentServiceMock.Object, _currentUserServiceMock.Object);
    }

    [Fact]
    public async Task MyTransactions_Valid_ReturnsOk()
    {
        var filter = new TransactionsFilterDto();
        var expected = new PagedResult<PaymentDto> { Items = new List<PaymentDto>(), TotalCount = 0, Page = 1, PageSize = 20 };

        _paymentServiceMock
            .Setup(s => s.ListMyTransactionsAsync(CurrentUserId, filter))
            .ReturnsAsync(expected);

        var controller = CreateController();
        var result = await controller.MyTransactions(filter);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expected, okResult.Value);
    }

    [Fact]
    public async Task MyTransactions_NoAuthenticatedUser_ThrowsUnAuthorized()
    {
        _currentUserServiceMock.Setup(c => c.UserId).Returns((long?)null);
        var controller = new PaymentsController(_paymentServiceMock.Object, _currentUserServiceMock.Object);

        await Assert.ThrowsAsync<UnAuthorizedExeption>(() => controller.MyTransactions(new TransactionsFilterDto()));
    }

    [Fact]
    public async Task StripeWebhook_Valid_ReadsBodyAndHeaderThenReturnsOk()
    {
        const string rawJson = "{\"type\":\"payment_intent.succeeded\"}";
        const string signature = "t=123,v1=abc";

        _paymentServiceMock
            .Setup(s => s.HandleStripeWebhookAsync(rawJson, signature))
            .Returns(Task.CompletedTask);

        var controller = new PaymentsController(_paymentServiceMock.Object, _currentUserServiceMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(rawJson));
        httpContext.Request.Headers["Stripe-Signature"] = signature;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await controller.StripeWebhook();

        Assert.IsType<OkResult>(result);
        _paymentServiceMock.Verify(s => s.HandleStripeWebhookAsync(rawJson, signature), Times.Once);
    }

    [Fact]
    public async Task StripeWebhook_InvalidSignature_PropagatesException()
    {
        const string rawJson = "{}";
        _paymentServiceMock
            .Setup(s => s.HandleStripeWebhookAsync(rawJson, It.IsAny<string>()))
            .ThrowsAsync(new BadRequestExeption("Invalid Stripe webhook signature."));

        var controller = new PaymentsController(_paymentServiceMock.Object, _currentUserServiceMock.Object);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(rawJson));
        httpContext.Request.Headers["Stripe-Signature"] = "bad-sig";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        await Assert.ThrowsAsync<BadRequestExeption>(() => controller.StripeWebhook());
    }
}
