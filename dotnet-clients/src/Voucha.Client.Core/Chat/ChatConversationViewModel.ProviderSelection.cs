using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  public Task PersistSelectedProviderAsync(CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();
    if (providerResolver is not IChatProviderSelectionStore selectionStore) return Task.CompletedTask;
    try
    {
      selectionStore.SaveSelectedProvider(SelectedProviderStatus.ModelProvider);
      ErrorMessage = null;
      State = ConversationId is { Length: > 0 } ? LoadState.Loaded : LoadState.Idle;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
      State = LoadState.Error;
    }
    return Task.CompletedTask;
  }
}
