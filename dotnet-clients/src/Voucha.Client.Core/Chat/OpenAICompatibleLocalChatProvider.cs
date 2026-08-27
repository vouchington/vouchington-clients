using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Chat;

public sealed class OpenAICompatibleLocalChatProvider : ILocalChatProvider
{
  private readonly ILocalLLMConfigurationStore configurationStore;
  private readonly ILocalLLMSecretStore secretStore;
  private readonly OpenAICompatibleResponsesClient responsesClient;
  private readonly LocalLLMFeaturePolicy featurePolicy;
  private readonly IUiLocalization localization;
  private readonly Guid profileId;

  public OpenAICompatibleLocalChatProvider(
      ILocalLLMConfigurationStore configurationStore,
      ILocalLLMSecretStore secretStore,
      OpenAICompatibleResponsesClient responsesClient,
      LocalLLMFeaturePolicy featurePolicy,
      Guid profileId,
      IUiLocalization? localization = null)
  {
    this.configurationStore = configurationStore ?? throw new ArgumentNullException(nameof(configurationStore));
    this.secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    this.responsesClient = responsesClient ?? throw new ArgumentNullException(nameof(responsesClient));
    this.featurePolicy = featurePolicy ?? throw new ArgumentNullException(nameof(featurePolicy));
    this.profileId = profileId;
    this.localization = localization ?? UiLocalization.English;
  }

  public string Id => LocalChatProviderIds.OpenAICompatible(profileId);

  public ChatProviderStatus Status
  {
    get
    {
      var profile = configurationStore.Load().Profiles.SingleOrDefault(candidate => candidate.Id == profileId);
      if (!featurePolicy.IsEnabled)
      {
        return Unavailable(profile, UiMessageKey.NativeDotnetChatConversationLocalModelsOffPlatform);
      }
      if (profile is null)
      {
        return Unavailable(null, UiMessageKey.NativeDotnetChatConversationLocalModelSettingsOff);
      }
      if (!profile.IsEnabled)
      {
        return Unavailable(profile, UiMessageKey.NativeDotnetChatConversationLocalModelSettingsOff);
      }
      if (profile.ResponsesUri is null)
      {
        return Unavailable(profile, UiMessageKey.NativeDotnetChatConversationEnterLocalResponsesEndpoint);
      }
      var model = profile.SelectedModel;
      if (model is null)
      {
        return Unavailable(profile, UiMessageKey.NativeDotnetChatConversationChooseLocalModel);
      }
      return new(
          ChatProviderKind.Local,
          DisplayName(profile),
          true,
          UiText.Verbatim(model),
          localization,
          Id,
          model);
    }
  }

  public async Task<LocalChatGenerationResult> GenerateAssistantContentAsync(
      string message,
      IReadOnlyList<LocalLLMResponseInput> history,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(history);

    if (!featurePolicy.IsEnabled)
    {
      throw LocalizedException(UiMessageKey.NativeDotnetChatConversationLocalModelsOffPlatform);
    }
    var profile = configurationStore.Load().Profiles.SingleOrDefault(candidate => candidate.Id == profileId)
        ?? throw LocalizedException(UiMessageKey.NativeDotnetChatConversationLocalModelSettingsOff);
    if (!profile.IsEnabled)
    {
      throw LocalizedException(UiMessageKey.NativeDotnetChatConversationLocalModelSettingsOff);
    }
    var model = profile.SelectedModel
        ?? throw LocalizedException(UiMessageKey.NativeDotnetChatConversationChooseLocalModel);
    var response = await responsesClient.GenerateAssistantContentAsync(
        message,
        history,
        profile,
        await secretStore.ReadApiKeyAsync(profileId, cancellationToken).ConfigureAwait(false),
        cancellationToken).ConfigureAwait(false);
    if (string.IsNullOrWhiteSpace(response))
    {
      throw LocalizedException(UiMessageKey.NativeDotnetChatConversationLocalModelEmptyResponse);
    }
    return new(response, "openai_compatible", model);
  }

  private ChatProviderStatus Unavailable(LocalLLMEndpointProfile? profile, UiMessageKey reason) =>
      new(
          ChatProviderKind.Local,
          profile is null ? UiText.Localized(UiMessageKey.NativeDotnetChatConversationLocal) : DisplayName(profile),
          false,
          UiText.Localized(reason),
          localization,
          Id,
          null);

  private static UiText DisplayName(LocalLLMEndpointProfile profile) =>
      !string.IsNullOrWhiteSpace(profile.DisplayName) ? UiText.Verbatim(profile.DisplayName.Trim()) :
      profile.ResponsesUri is { } uri ? UiText.Verbatim(uri.GetLeftPart(UriPartial.Authority)) :
      UiText.Localized(UiMessageKey.NativeDotnetChatConversationLocal);

  private InvalidOperationException LocalizedException(UiMessageKey key) =>
      new(localization.Localize(key));
}

public static class LocalChatProviderIds
{
  public const string WindowsSystemLanguageModel = "windows-system-language-model";
  private const string OpenAICompatiblePrefix = "openai_compatible:";
  private const string LegacyOpenAICompatiblePrefix = "openai-compatible:";

  public static string OpenAICompatible(Guid profileId) => $"{OpenAICompatiblePrefix}{profileId:D}";

  public static Guid? TryGetEndpointId(string? providerId) =>
      (TryParse(providerId, OpenAICompatiblePrefix, out var profileId) ||
      TryParse(providerId, LegacyOpenAICompatiblePrefix, out profileId)) ? profileId : null;

  public static string? Normalize(string? providerId) =>
      TryParse(providerId, LegacyOpenAICompatiblePrefix, out var profileId) ? OpenAICompatible(profileId) : providerId;

  public static bool RefersTo(string? providerId, Guid profileId) => TryGetEndpointId(providerId) == profileId;

  private static bool TryParse(string? providerId, string prefix, out Guid profileId)
  {
    profileId = default;
    return providerId is { } value && value.StartsWith(prefix, StringComparison.Ordinal) &&
        Guid.TryParse(value[prefix.Length..], out profileId);
  }
}
