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
  }

  public IReadOnlyList<ChatProviderStatus> GetProviderStatuses()
  {
    var configured = configurationStore.Load().Profiles.Select(Create).Select(provider => provider.Status).ToArray();
#if WINDOWS
    return [new WindowsSystemLanguageModelProvider(windowsRuntime, localization).Status, .. configured];
#else
    return [UnavailableStatus(), .. configured];
#endif
  }

  public ChatProviderStatus GetDefaultProviderStatus()
  {
    var configuration = configurationStore.Load();
    if (configuration.SelectedProviderId is { } id) return GetLocalProvider(id)?.Status ?? new(ChatProviderKind.Local,
        UiText.Localized(UiMessageKey.NativeDotnetChatConversationLocal), false,
        UiText.Localized(UiMessageKey.NativeDotnetChatConversationLocalModelSettingsOff), localization, id);
#if WINDOWS
    return new WindowsSystemLanguageModelProvider(windowsRuntime, localization).Status;
#else
    return UnavailableStatus();
#endif
  }

  private ChatProviderStatus UnavailableStatus() => new(ChatProviderKind.Local,
      UiText.Localized(UiMessageKey.NativeDotnetChatConversationLocal), false,
      UiText.Localized(UiMessageKey.NativeDotnetChatConversationLocalChatUnavailable), localization);

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
