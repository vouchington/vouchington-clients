using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  private sealed class UnavailableOnlyChatProviderResolver : IChatProviderResolver
  {
    internal static readonly UnavailableOnlyChatProviderResolver Instance = new();

    private static readonly ChatProviderStatus UnavailableProvider = new(
        ChatProviderKind.Local,
        UiText.Localized(UiMessageKey.NativeDotnetChatConversationLocal),
        false,
        UiText.Localized(UiMessageKey.NativeDotnetChatConversationLocalChatUnavailable),
        UiLocalization.English);

    public IReadOnlyList<ChatProviderStatus> GetProviderStatuses() => [UnavailableProvider];

    public ChatProviderStatus GetDefaultProviderStatus() => UnavailableProvider;
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
