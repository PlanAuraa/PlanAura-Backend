namespace Planura.Core.Application.Abstraction.AiChat
{
    public class AiChatCompletionRequest
    {
        public string SystemPrompt { get; set; } = null!;
        public IReadOnlyList<AiChatTurn> History { get; set; } = new List<AiChatTurn>();
    }
}
