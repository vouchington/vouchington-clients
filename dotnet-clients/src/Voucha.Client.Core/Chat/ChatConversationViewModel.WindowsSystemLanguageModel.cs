using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Chat;

public sealed partial class ChatConversationViewModel
{
  public bool CanSetUpSelectedWindowsSystemLanguageModel =>
      SelectedProviderStatus.ModelProvider == LocalChatProviderIds.WindowsSystemLanguageModel &&
      providerResolver is ILocalChatProviderResolver resolver &&
      resolver.GetLocalProvider(LocalChatProviderIds.WindowsSystemLanguageModel) is WindowsSystemLanguageModelProvider provider &&
      provider.Readiness == WindowsSystemLanguageModelReadiness.NotReady;

  public async Task SetUpSelectedWindowsSystemLanguageModelAsync(
      IProgress<double>? progress = null,
      CancellationToken cancellationToken = default)
  {
    if (providerResolver is not ILocalChatProviderResolver resolver ||
        resolver.GetLocalProvider(LocalChatProviderIds.WindowsSystemLanguageModel) is not WindowsSystemLanguageModelProvider provider)
    {
      return;
    }

    try
    {
      await provider.EnsureReadyAsync(progress, cancellationToken).ConfigureAwait(true);
      ErrorMessage = null;
      State = ConversationId is { Length: > 0 } ? LoadState.Loaded : LoadState.Idle;
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
    {
      ErrorMessage = ex.Message;
      State = LoadState.Error;
    }
    finally
    {
      RefreshProviderStatuses(LocalChatProviderIds.WindowsSystemLanguageModel);
    }
  }

  private void RefreshProviderStatuses(string selectedProviderId)
  {
    providerStatuses = providerResolver.GetProviderStatuses();
    SelectedProviderStatus = providerStatuses.SingleOrDefault(status => status.ModelProvider == selectedProviderId)
        ?? SelectedProviderStatus;
    OnPropertyChanged(nameof(ProviderStatuses));
  }
}
