using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Planura.Core.Application.Abstraction.AiVisualizer;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Models.AiInvitation;
using Planura.Core.Application.Services.AiInvitation;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;
using Planura.Tests.TestHelpers;
using Xunit;
using AiInvitationEntity = Planura.Core.Domain.Entities.AiInvitation;

namespace Planura.Tests.Services;

public class AiInvitationServiceTests
{
    private const long ClientUserId = 500;
    private const long ClientId = 10;
    private const long EventPlanId = 30;

    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IHuggingFaceApiService> _huggingFaceApiServiceMock = new();
    private readonly Mock<IAttachmentService> _attachmentServiceMock = new();

    private AiInvitationService CreateService() => new(
        _unitOfWorkMock.Object,
        _huggingFaceApiServiceMock.Object,
        _attachmentServiceMock.Object,
        NullLogger<AiInvitationService>.Instance);

    private static Client CreateClient() => new() { Id = ClientId, UserId = ClientUserId };

    private static EventPlan CreateEventPlan(long clientId = ClientId) => new()
    {
        Id = EventPlanId,
        ClientId = clientId,
        EventType = "Wedding"
    };

    private void SetupClientRepo(Client? client)
    {
        var repo = _unitOfWorkMock.SetupRepository<Client, long>();
        repo.Setup(r => r.GetWithSpecAsync(It.IsAny<ISpecification<Client>>())).ReturnsAsync(client);
    }

    private Mock<IGenericRepository<EventPlan, long>> SetupEventPlanRepo(EventPlan? eventPlan)
    {
        var repo = _unitOfWorkMock.SetupRepository<EventPlan, long>();
        repo.Setup(r => r.GetAsync(EventPlanId)).ReturnsAsync(eventPlan);
        return repo;
    }

    private static GenerateInvitationDto CreateDto(string theme = "Elegant", string prompt = "Gold foil accents with floral borders.") => new()
    {
        EventPlanId = EventPlanId,
        Theme = theme,
        Prompt = prompt
    };

    // ---------------- GenerateInvitationAsync: validation ----------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateInvitationAsync_BlankTheme_ThrowsBadRequestWithoutCallingProvider(string theme)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(
            () => service.GenerateInvitationAsync(ClientUserId, CreateDto(theme: theme)));

        _huggingFaceApiServiceMock.Verify(
            p => p.GenerateImageAsync(It.IsAny<HuggingFaceImageRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateInvitationAsync_BlankPrompt_ThrowsBadRequestExeption(string prompt)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BadRequestExeption>(
            () => service.GenerateInvitationAsync(ClientUserId, CreateDto(prompt: prompt)));
    }

    [Fact]
    public async Task GenerateInvitationAsync_PromptTooLong_ThrowsBadRequestExeption()
    {
        var service = CreateService();
        var dto = CreateDto(prompt: new string('a', 501));

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.GenerateInvitationAsync(ClientUserId, dto));
    }

    [Fact]
    public async Task GenerateInvitationAsync_ThemeTooLong_ThrowsBadRequestExeption()
    {
        var service = CreateService();
        var dto = CreateDto(theme: new string('a', 101));

        await Assert.ThrowsAsync<BadRequestExeption>(() => service.GenerateInvitationAsync(ClientUserId, dto));
    }

    // ---------------- GenerateInvitationAsync: ownership ----------------

    [Fact]
    public async Task GenerateInvitationAsync_EventPlanNotFound_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        SetupEventPlanRepo(null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.GenerateInvitationAsync(ClientUserId, CreateDto()));

        _huggingFaceApiServiceMock.Verify(
            p => p.GenerateImageAsync(It.IsAny<HuggingFaceImageRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateInvitationAsync_EventPlanOwnedByAnotherClient_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        SetupEventPlanRepo(CreateEventPlan(clientId: 999));

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.GenerateInvitationAsync(ClientUserId, CreateDto()));
    }

    // ---------------- GenerateInvitationAsync: happy path ----------------

    [Fact]
    public async Task GenerateInvitationAsync_Valid_CallsProviderStoresImageAndPersistsInvitation()
    {
        SetupClientRepo(CreateClient());
        SetupEventPlanRepo(CreateEventPlan());

        HuggingFaceImageRequest? capturedRequest = null;
        _huggingFaceApiServiceMock
            .Setup(p => p.GenerateImageAsync(It.IsAny<HuggingFaceImageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<HuggingFaceImageRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new GeneratedImageResult
            {
                ImageBytes = new byte[] { 1, 2, 3 },
                ContentType = "image/png"
            });

        _attachmentServiceMock
            .Setup(a => a.UploadGeneratedFileAsync(It.IsAny<byte[]>(), ".png", "ai-invitations/generated"))
            .ReturnsAsync("images/ai-invitations/generated/invite.png");
        _attachmentServiceMock
            .Setup(a => a.ToAbsoluteUrl("images/ai-invitations/generated/invite.png"))
            .Returns("https://api.planura.local/images/ai-invitations/generated/invite.png");

        var invitationRepo = _unitOfWorkMock.SetupRepository<AiInvitationEntity, long>();
        AiInvitationEntity? captured = null;
        invitationRepo.Setup(r => r.AddAsync(It.IsAny<AiInvitationEntity>()))
            .Callback<AiInvitationEntity>(i => captured = i)
            .Returns(Task.CompletedTask);

        var service = CreateService();
        var dto = CreateDto(theme: "Elegant", prompt: "  Gold foil accents with floral borders.  ");
        var result = await service.GenerateInvitationAsync(ClientUserId, dto);

        Assert.NotNull(capturedRequest);
        Assert.Contains("Elegant", capturedRequest!.Prompt);
        Assert.Contains("Gold foil accents with floral borders.", capturedRequest.Prompt);

        Assert.NotNull(captured);
        Assert.Equal(EventPlanId, captured!.EventPlanId);
        Assert.Equal("Elegant", captured.Theme);
        Assert.Equal("images/ai-invitations/generated/invite.png", captured.ImageUrl);

        Assert.Equal("https://api.planura.local/images/ai-invitations/generated/invite.png", result.ImageUrl);
        Assert.Equal("Elegant", result.Theme);
        Assert.Equal(EventPlanId, result.EventPlanId);

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateInvitationAsync_ProviderThrowsAiException_PropagatesWithoutStoringOrPersisting()
    {
        SetupClientRepo(CreateClient());
        SetupEventPlanRepo(CreateEventPlan());

        _huggingFaceApiServiceMock
            .Setup(p => p.GenerateImageAsync(It.IsAny<HuggingFaceImageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AiProviderUnavailableExeption("The image generation service is currently unavailable."));

        var invitationRepo = _unitOfWorkMock.SetupRepository<AiInvitationEntity, long>();

        var service = CreateService();

        await Assert.ThrowsAsync<AiProviderUnavailableExeption>(
            () => service.GenerateInvitationAsync(ClientUserId, CreateDto()));

        _attachmentServiceMock.Verify(
            a => a.UploadGeneratedFileAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        invitationRepo.Verify(r => r.AddAsync(It.IsAny<AiInvitationEntity>()), Times.Never);
    }

    // ---------------- ListInvitationsAsync ----------------

    [Fact]
    public async Task ListInvitationsAsync_EventPlanOwnedByAnotherClient_ThrowsNotFound()
    {
        SetupClientRepo(CreateClient());
        SetupEventPlanRepo(CreateEventPlan(clientId: 999));

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundExeption>(() => service.ListInvitationsAsync(ClientUserId, EventPlanId));
    }

    [Fact]
    public async Task ListInvitationsAsync_Valid_ReturnsMappedInvitationsNewestFirst()
    {
        SetupClientRepo(CreateClient());
        SetupEventPlanRepo(CreateEventPlan());

        var invitations = new List<AiInvitationEntity>
        {
            new()
            {
                Id = 2,
                EventPlanId = EventPlanId,
                Theme = "Rustic",
                Prompt = "Wood and burlap textures.",
                ImageUrl = "images/ai-invitations/generated/2.png",
                CreatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = 1,
                EventPlanId = EventPlanId,
                Theme = "Elegant",
                Prompt = "Gold foil accents.",
                ImageUrl = "images/ai-invitations/generated/1.png",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            }
        };

        var invitationRepo = _unitOfWorkMock.SetupRepository<AiInvitationEntity, long>();
        invitationRepo
            .Setup(r => r.GetAllWithSpecAsync(It.IsAny<ISpecification<AiInvitationEntity>>(), It.IsAny<bool>()))
            .ReturnsAsync(invitations);

        _attachmentServiceMock
            .Setup(a => a.ToAbsoluteUrl(It.IsAny<string>()))
            .Returns<string>(path => $"https://api.planura.local/{path}");

        var service = CreateService();
        var result = (await service.ListInvitationsAsync(ClientUserId, EventPlanId)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result[0].Id);
        Assert.Equal("Rustic", result[0].Theme);
        Assert.Equal("https://api.planura.local/images/ai-invitations/generated/2.png", result[0].ImageUrl);
    }
}
