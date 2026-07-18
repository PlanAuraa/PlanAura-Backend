namespace Planura.Core.Application.Abstraction.AiChat
{
    public class AiChatTurn
    {
        public string Role { get; set; } = null!;
        public string Content { get; set; } = null!;

        public AiChatTurn()
        {
        }

        public AiChatTurn(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}
