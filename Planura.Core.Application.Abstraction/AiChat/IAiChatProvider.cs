namespace Planura.Core.Application.Abstraction.AiChat
{
    public interface IAiChatProvider
    {
        Task<AiChatCompletionResult> GetChatCompletionAsync(AiChatCompletionRequest request);
    }
}
