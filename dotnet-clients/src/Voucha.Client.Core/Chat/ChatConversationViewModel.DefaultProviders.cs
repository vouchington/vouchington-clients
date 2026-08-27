using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  private sealed class HostedOnlyChatProviderResolver : IChatProviderResolver
  {
    internal static readonly HostedOnlyChatProviderResolver Instance = new();

    private static readonly ChatProviderStatus HostedProvider = new(
        ChatProviderKind.Hosted,
        UiText.Localized(UiMessageKey.NativeDotnetChatConversationHosted),
        true,
        UiText.Localized(UiMessageKey.NativeDotnetChatConversationOpenAiHosted),
        UiLocalization.English);

    public IReadOnlyList<ChatProviderStatus> GetProviderStatuses() => [HostedProvider];

    public ChatProviderStatus GetDefaultProviderStatus() => HostedProvider;
  }

  private sealed class UnavailableLocalChatProvider : ILocalChatProvider
  {
    internal static readonly UnavailableLocalChatProvider Instance = new();

    private static readonly ChatProviderStatus StatusValue = new(
        ChatProviderKind.Local,
        UiText.Localized(UiMessageKey.NativeDotnetChatConversationLocal),
        false,
        UiText.Localized(UiMessageKey.NativeDotnetChatConversationLocalChatUnavailable),
        UiLocalization.English);

    public ChatProviderStatus Status => StatusValue;

    public string Id => "unavailable-local";

    public Task<LocalChatGenerationResult> GenerateAssistantContentAsync(
        string message,
        IReadOnlyList<LocalLLMResponseInput> history,
        CancellationToken cancellationToken = default) =>
        Task.FromException<LocalChatGenerationResult>(new InvalidOperationException(StatusValue.StatusText));
  }
}
