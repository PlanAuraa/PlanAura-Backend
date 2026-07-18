using Planura.Core.Domain.Entities;
using Planura.Core.Domain.Repositories;

namespace Planura.Core.Application.Specifications;

public class AiChatConversationWithMessagesSpecification : BaseSpecification<AiChatConversation>
{
    public AiChatConversationWithMessagesSpecification(long conversationId)
        : base(conversation => conversation.Id == conversationId)
    {
        AddInclude(conversation => conversation.Messages);
    }
}
