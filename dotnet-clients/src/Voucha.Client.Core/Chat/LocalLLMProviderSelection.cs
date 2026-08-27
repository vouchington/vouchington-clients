namespace Voucha.Client.Core.Chat;

public static class LocalLLMProviderSelection
{
  public static void Save(ILocalLLMConfigurationStore configurationStore, string? providerId)
  {
    ArgumentNullException.ThrowIfNull(configurationStore);
    var normalized = LocalChatProviderIds.Normalize(providerId);
    var endpointId = LocalChatProviderIds.TryGetEndpointId(providerId);
    configurationStore.Update(configuration => configuration with
    {
      SelectedProviderId = normalized,
      SelectedEndpointId = endpointId ?? configuration.SelectedEndpointId,
    });
  }
}
