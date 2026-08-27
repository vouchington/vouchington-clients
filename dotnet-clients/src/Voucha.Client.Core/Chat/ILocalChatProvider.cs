namespace Voucha.Client.Core.Chat;

public interface ILocalChatProvider
{
  string Id { get; }

  ChatProviderStatus Status { get; }

  Task<LocalChatGenerationResult> GenerateAssistantContentAsync(
      string message,
      IReadOnlyList<LocalLLMResponseInput> history,
      CancellationToken cancellationToken = default);
}

public sealed record LocalChatGenerationResult(
    string AssistantContent,
    string ModelProvider,
    string? ModelName);
