using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  private void ApplySendFailure(
      Exception exception,
      ChatProviderStatus selectedProvider,
      bool streamAccepted)
  {
    var localizedError = exception is ChatStreamIncompleteException
        ? localization.Localize(UiMessageKey.NativeDotnetChatConversationResponseInterrupted)
        : exception.Message;
    ErrorMessage = localizedError;
    if (selectedProvider.Kind != ChatProviderKind.Local && streamAccepted)
    {
      MarkStreamingAssistantMessageFailed(localizedError);
    }
    State = LoadState.Error;
  }

  private void MarkStreamingAssistantMessageFailed(string error)
  {
    if (streamingAssistantMessageId is null) return;

    var index = messages.FindLastIndex(row =>
        row.Role == "assistant" && row.Id == streamingAssistantMessageId);
    if (index < 0) return;

    messages[index] = messages[index] with
    {
      Content = StreamContent ?? messages[index].Content,
      HasError = true,
      Error = error,
    };
    OnPropertyChanged(nameof(Messages));
  }
}
