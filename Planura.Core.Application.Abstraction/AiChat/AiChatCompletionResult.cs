namespace Planura.Core.Application.Abstraction.AiChat
{
    public class AiChatCompletionResult
    {
        public string Content { get; set; } = null!;
        public string ModelUsed { get; set; } = null!;
    }
}
