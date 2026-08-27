using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Chat;

public sealed class ChatProviderResolver : IChatProviderResolver, ILocalChatProviderResolver, IChatProviderSelectionStore
{
  private readonly ILocalLLMConfigurationStore configurationStore;
  private readonly ILocalLLMSecretStore secretStore;
  private readonly OpenAICompatibleResponsesClient responsesClient;
  private readonly LocalLLMFeaturePolicy featurePolicy;
  private readonly IUiLocalization localization;
  private readonly ChatProviderStatus hostedProvider;
#if WINDOWS
  private readonly IWindowsSystemLanguageModelRuntime windowsRuntime;
#endif

  public ChatProviderResolver(ILocalLLMConfigurationStore configurationStore, ILocalLLMSecretStore secretStore,
      OpenAICompatibleResponsesClient responsesClient, LocalLLMFeaturePolicy featurePolicy, IUiLocalization localization
#if WINDOWS
      , IWindowsSystemLanguageModelRuntime windowsRuntime
#endif
      )
  {
    this.configurationStore = configurationStore; this.secretStore = secretStore; this.responsesClient = responsesClient;
    this.featurePolicy = featurePolicy; this.localization = localization;
#if WINDOWS
    this.windowsRuntime = windowsRuntime;
#endif
    hostedProvider = new(ChatProviderKind.Hosted, UiText.Localized(UiMessageKey.NativeDotnetChatConversationHosted), true,
        UiText.Localized(UiMessageKey.NativeDotnetChatConversationOpenAiHosted), localization);
  }

  public IReadOnlyList<ChatProviderStatus> GetProviderStatuses() =>
      [hostedProvider
#if WINDOWS
      , new WindowsSystemLanguageModelProvider(windowsRuntime, localization).Status
#endif
      , .. configurationStore.Load().Profiles.Select(Create).Select(provider => provider.Status)];

  public ChatProviderStatus GetDefaultProviderStatus()
  {
    var configuration = configurationStore.Load();
    if (configuration.SelectedProviderId is { } id) return GetLocalProvider(id)?.Status ?? new(ChatProviderKind.Local,
        UiText.Localized(UiMessageKey.NativeDotnetChatConversationLocal), false,
        UiText.Localized(UiMessageKey.NativeDotnetChatConversationLocalModelSettingsOff), localization, id);
    return hostedProvider;
  }

  public ILocalChatProvider? GetLocalProvider(string providerId)
  {
#if WINDOWS
    if (providerId == LocalChatProviderIds.WindowsSystemLanguageModel) return new WindowsSystemLanguageModelProvider(windowsRuntime, localization);
#endif
    return configurationStore.Load().Profiles.SingleOrDefault(profile => LocalChatProviderIds.RefersTo(providerId, profile.Id)) is { } profile ? Create(profile) : null;
  }

  public void SaveSelectedProvider(string? providerId) => LocalLLMProviderSelection.Save(configurationStore, providerId);

  private OpenAICompatibleLocalChatProvider Create(LocalLLMEndpointProfile profile) =>
      new(configurationStore, secretStore, responsesClient, featurePolicy, profile.Id, localization);
}
