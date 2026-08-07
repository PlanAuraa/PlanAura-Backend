using Microsoft.Extensions.Logging;
using Planura.Core.Application.Abstraction.AiVisualizer;
using Planura.Core.Application.Abstraction.AttachementService;
using Planura.Core.Application.Models.AiInvitation;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;
using AiInvitationEntity = Planura.Core.Domain.Entities.AiInvitation;
using Client = Planura.Core.Domain.Entities.Client;
using EventPlan = Planura.Core.Domain.Entities.EventPlan;

namespace Planura.Core.Application.Services.AiInvitation;

public class AiInvitationService : IAiInvitationService
{
    private const int MaxThemeLength = 100;
    private const int MaxPromptLength = 500;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IHuggingFaceApiService _huggingFaceApiService;
    private readonly IAttachmentService _attachmentService;
    private readonly ILogger<AiInvitationService> _logger;

    public AiInvitationService(
        IUnitOfWork unitOfWork,
        IHuggingFaceApiService huggingFaceApiService,
        IAttachmentService attachmentService,
        ILogger<AiInvitationService> logger)
    {
        _unitOfWork = unitOfWork;
        _huggingFaceApiService = huggingFaceApiService;
        _attachmentService = attachmentService;
        _logger = logger;
    }

    public async Task<InvitationDto> GenerateInvitationAsync(long clientUserId, GenerateInvitationDto dto)
    {
        var theme = ValidateRequest(dto);

        var clientId = await ResolveClientIdAsync(clientUserId);
        var eventPlan = await _unitOfWork.Repository<EventPlan, long>().GetAsync(dto.EventPlanId);
        if (eventPlan is null || eventPlan.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(EventPlan), dto.EventPlanId);
        }

        var prompt = dto.Prompt.Trim();
        var combinedPrompt = $"Elegant {theme} themed wedding invitation card design. {prompt}";

        _logger.LogInformation(
            "Requesting AI invitation image for event plan {EventPlanId} with theme {Theme}.",
            dto.EventPlanId, theme);

        var generated = await _huggingFaceApiService.GenerateImageAsync(new HuggingFaceImageRequest
        {
            Prompt = combinedPrompt
        });

        var generatedImagePath = await _attachmentService.UploadGeneratedFileAsync(
            generated.ImageBytes,
            ExtensionFromContentType(generated.ContentType),
            "ai-invitations/generated")
            ?? throw new AiProviderUnavailableExeption("The generated invitation image could not be stored.");

        var invitation = new AiInvitationEntity
        {
            EventPlanId = dto.EventPlanId,
            Theme = theme,
            Prompt = prompt,
            ImageUrl = generatedImagePath
        };

        await _unitOfWork.Repository<AiInvitationEntity, long>().AddAsync(invitation);
        await _unitOfWork.SaveChangesAsync();

        return ToDto(invitation);
    }

    public async Task<IEnumerable<InvitationDto>> ListInvitationsAsync(long clientUserId, long eventPlanId)
    {
        var clientId = await ResolveClientIdAsync(clientUserId);
        var eventPlan = await _unitOfWork.Repository<EventPlan, long>().GetAsync(eventPlanId);
        if (eventPlan is null || eventPlan.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(EventPlan), eventPlanId);
        }

        var invitations = await _unitOfWork.Repository<AiInvitationEntity, long>()
            .GetAllWithSpecAsync(new AiInvitationsByEventPlanSpecification(eventPlanId));

        return invitations.Select(ToDto);
    }

    private InvitationDto ToDto(AiInvitationEntity invitation) => new()
    {
        Id = invitation.Id,
        EventPlanId = invitation.EventPlanId,
        Theme = invitation.Theme ?? string.Empty,
        ImageUrl = _attachmentService.ToAbsoluteUrl(invitation.ImageUrl) ?? invitation.ImageUrl ?? string.Empty,
        Prompt = invitation.Prompt ?? string.Empty,
        CreatedAt = invitation.CreatedAt
    };

    private static string ValidateRequest(GenerateInvitationDto dto)
    {
        if (dto.EventPlanId <= 0)
        {
            throw new BadRequestExeption("A valid event plan is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Theme))
        {
            throw new BadRequestExeption("A theme for the invitation is required.");
        }

        if (dto.Theme.Trim().Length > MaxThemeLength)
        {
            throw new BadRequestExeption($"The theme must be {MaxThemeLength} characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(dto.Prompt))
        {
            throw new BadRequestExeption("A description of the desired invitation is required.");
        }

        if (dto.Prompt.Trim().Length > MaxPromptLength)
        {
            throw new BadRequestExeption($"The prompt must be {MaxPromptLength} characters or fewer.");
        }

        return dto.Theme.Trim();
    }

    private async Task<long> ResolveClientIdAsync(long userId)
    {
        var client = await _unitOfWork.Repository<Client, long>()
            .GetWithSpecAsync(new ClientByUserIdSpecification(userId));

        if (client is null)
        {
            throw new NotFoundExeption(nameof(Client), userId);
        }

        return client.Id;
    }

    private static string ExtensionFromContentType(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".jpg"
    };
}
