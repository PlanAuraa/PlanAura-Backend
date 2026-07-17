using System.ClientModel;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Planura.Core.Application.Abstraction.AiChat;
using Planura.Core.Application.Common;
using Planura.Shared.Errors.Models;

namespace Planura.Infrastructure.AiChat
{
    public class OpenAiChatProvider : IAiChatProvider
    {
        private readonly OpenAiOptions _options;
        private readonly ChatClient _chatClient;

        public OpenAiChatProvider(IOptions<OpenAiOptions> options)
        {
            _options = options.Value;
            _chatClient = new ChatClient(model: _options.Model, apiKey: _options.ApiKey);
        }

        public async Task<AiChatCompletionResult> GetChatCompletionAsync(AiChatCompletionRequest request)
        {
            var messages = new List<ChatMessage> { new SystemChatMessage(request.SystemPrompt) };
            foreach (var turn in request.History)
            {
                messages.Add(turn.Role == "assistant"
                    ? new AssistantChatMessage(turn.Content)
                    : new UserChatMessage(turn.Content));
            }

            ChatCompletion completion;
            try
            {
                completion = await _chatClient.CompleteChatAsync(messages);
            }
            catch (ClientResultException ex) when (ex.Status == 429)
            {
                throw new AiProviderRateLimitedExeption("The AI assistant is temporarily busy. Please try again shortly.");
            }
            catch (ClientResultException ex) when (ex.Status == 400)
            {
                throw new AiContentPolicyExeption("Your message couldn't be processed by the AI assistant. Please rephrase.");
            }
            catch (ClientResultException ex)
            {
                throw new AiProviderUnavailableExeption($"The AI assistant is currently unavailable: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                throw new AiProviderTimeoutExeption("The AI assistant took too long to respond. Please try again.");
            }

            return new AiChatCompletionResult
            {
                Content = completion.Content[0].Text,
                ModelUsed = _options.Model
            };
        }
    }
}
