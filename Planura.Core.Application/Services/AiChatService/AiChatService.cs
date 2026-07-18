using AutoMapper;
using Microsoft.Extensions.Logging;
using Planura.Core.Application.Abstraction.AiChat;
using Planura.Core.Application.Models;
using Planura.Core.Application.Specifications;
using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;
using Planura.Shared.Errors.Models;

namespace Planura.Core.Application.Services.AiChat;

public class AiChatService : IAiChatService
{
    private const string SystemPromptText = """
        You are the Planura AI Assistant, a friendly and knowledgeable event-planning
        guide built into the Planura platform. Planura helps clients plan events
        (weddings, birthdays, corporate events, and similar occasions) and connects
        them with local vendors (catering, photography, venues, decor, and more).

        Your job in this conversation:
        - Help the client think through their event by asking clarifying questions
          about event type, date, city, guest count, budget, and style/theme when
          that information isn't yet known.
        - Offer concrete, practical planning advice: timelines, checklists, budget
          breakdowns, vendor categories they should consider, and questions they
          should ask vendors.
        - Keep a warm, professional, concise tone. Use short paragraphs or bullet
          points. Avoid generic filler.

        Strict boundaries (important):
        - You do NOT currently have access to Planura's live database of vendors,
          packages, or pricing. Never claim a specific vendor, package, price, or
          availability exists on Planura, and never invent vendor names, ratings, or
          contact details.
        - If the client asks you to recommend or compare actual vendors/packages on
          Planura, explain that you can't browse live listings yet, and instead
          direct them to the Vendors/Marketplace section of the app, or describe in
          general terms what to look for in that category.
        - You may speak generally about typical price ranges, timelines, or
          planning norms, but always frame this as general guidance, not Planura
          platform data.
        - Do not discuss topics unrelated to event planning, Planura, or vendor
          categories. Politely redirect off-topic requests back to event planning.
        - Do not request or store sensitive personal data (payment details, national
          ID numbers, passwords).
        """;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly IAiChatProvider _aiChatProvider;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(
        IUnitOfWork unitOfWork,
        IMapper mapper,
        IAiChatProvider aiChatProvider,
        ILogger<AiChatService> logger)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _aiChatProvider = aiChatProvider;
        _logger = logger;
    }

    public async Task<ChatMessageResponseDto> SendMessageAsync(long clientUserId, SendChatMessageDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Message))
        {
            throw new BadRequestExeption("Message cannot be empty.");
        }

        var clientId = await ResolveClientIdAsync(clientUserId);

        var conversation = dto.ConversationId is long existingId
            ? await LoadOwnedConversationAsync(existingId, clientId)
            : await CreateConversationAsync(clientId, dto.EventPlanId);

        var userMessage = new AiChatMessage
        {
            ConversationId = conversation.Id,
            Role = "user",
            Content = dto.Message
        };
        conversation.Messages.Add(userMessage);

        var history = conversation.Messages
            .OrderBy(message => message.CreatedAt)
            .Select(message => new AiChatTurn(message.Role, message.Content))
            .ToList();

        _logger.LogInformation(
            "Sending AI chat completion request for conversation {ConversationId} with {MessageCount} messages of history.",
            conversation.Id, history.Count);

        var completion = await _aiChatProvider.GetChatCompletionAsync(new AiChatCompletionRequest
        {
            SystemPrompt = SystemPromptText,
            History = history
        });

        var assistantMessage = new AiChatMessage
        {
            ConversationId = conversation.Id,
            Role = "assistant",
            Content = completion.Content
        };

        await _unitOfWork.Repository<AiChatMessage, long>().AddAsync(userMessage);
        await _unitOfWork.Repository<AiChatMessage, long>().AddAsync(assistantMessage);
        conversation.UpdatedAt = DateTimeOffset.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        return new ChatMessageResponseDto
        {
            ConversationId = conversation.Id,
            UserMessage = _mapper.Map<AiChatMessageDto>(userMessage),
            AssistantMessage = _mapper.Map<AiChatMessageDto>(assistantMessage)
        };
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

    private async Task<AiChatConversation> LoadOwnedConversationAsync(long conversationId, long clientId)
    {
        var conversation = await _unitOfWork.Repository<AiChatConversation, long>()
            .GetWithSpecAsync(new AiChatConversationWithMessagesSpecification(conversationId));

        if (conversation is null || conversation.ClientId != clientId)
        {
            throw new NotFoundExeption(nameof(AiChatConversation), conversationId);
        }

        return conversation;
    }

    private async Task<AiChatConversation> CreateConversationAsync(long clientId, long? eventPlanId)
    {
        var conversation = new AiChatConversation
        {
            ClientId = clientId,
            EventPlanId = eventPlanId
        };

        await _unitOfWork.Repository<AiChatConversation, long>().AddAsync(conversation);
        await _unitOfWork.SaveChangesAsync();

        return conversation;
    }
}
