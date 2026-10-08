using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  public bool IsLoading
  {
    get => isLoading;
    private set
    {
      if (SetProperty(ref isLoading, value))
      {
        NotifyAllSettingsPagination();
        NotifyOAuthGrantPagination();
        OnPropertyChanged(nameof(CanCreateApiKey));
      }
    }
  }

  public IReadOnlyList<ApiKey> ApiKeys
  {
    get => apiKeys;
    private set
    {
      if (!SetProperty(ref apiKeys, value)) return;
      apiKeyPages.ReplaceItems(value);
      OnPropertyChanged(nameof(LocalizedApiKeys));
    }
  }

  public IReadOnlyList<WebPushSubscription> PushSubscriptions
  {
    get => pushSubscriptions;
    private set
    {
      if (SetProperty(ref pushSubscriptions, value)) pushSubscriptionPages.ReplaceItems(value);
    }
  }
}
