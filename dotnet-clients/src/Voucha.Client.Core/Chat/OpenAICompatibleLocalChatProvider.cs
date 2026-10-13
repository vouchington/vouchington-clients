using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Agent;

namespace Voucha.Client.Core.Chat;

public sealed class OpenAICompatibleLocalChatProvider : IMcpLocalChatProvider
{
  private readonly ILocalLLMConfigurationStore configurationStore;
  private readonly ILocalLLMSecretStore secretStore;
  private readonly OpenAICompatibleResponsesClient responsesClient;
  private readonly LocalLLMFeaturePolicy featurePolicy;
  private readonly IUiLocalization localization;
  private readonly Guid profileId;
  private readonly MemberMcpSession? mcpSession;

  public OpenAICompatibleLocalChatProvider(
      ILocalLLMConfigurationStore configurationStore,
      ILocalLLMSecretStore secretStore,
      OpenAICompatibleResponsesClient responsesClient,
      LocalLLMFeaturePolicy featurePolicy,
      Guid profileId,
      IUiLocalization? localization = null, MemberMcpSession? mcpSession = null)
  {
    this.configurationStore = configurationStore ?? throw new ArgumentNullException(nameof(configurationStore));
    this.secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    this.responsesClient = responsesClient ?? throw new ArgumentNullException(nameof(responsesClient));
    this.featurePolicy = featurePolicy ?? throw new ArgumentNullException(nameof(featurePolicy));
    this.profileId = profileId;
    this.localization = localization ?? UiLocalization.English;
    this.mcpSession = mcpSession;
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
      => await GenerateAssistantContentAsync(message, history, null, cancellationToken).ConfigureAwait(false);

  public MemberMcpConnectedSnapshot? ConnectedMcpSnapshot() => mcpSession?.ConnectedSnapshot();

  public bool IsCurrent(MemberMcpConnectedSnapshot snapshot) => mcpSession?.IsCurrent(snapshot) == true;

  public bool MatchesAccount(string accountId) => mcpSession?.MatchesAccount(accountId) == true;

  public async Task<LocalChatGenerationResult> GenerateAssistantContentAsync(
      string message, IReadOnlyList<LocalLLMResponseInput> history,
      MemberMcpConnectedSnapshot? snapshot, CancellationToken cancellationToken)
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
    var key = await secretStore.ReadApiKeyAsync(profileId, cancellationToken).ConfigureAwait(false);
    string? response;
    if (snapshot is not null)
    {
      var session = mcpSession ?? throw new OperationCanceledException("MCP session is unavailable.");
      if (!session.IsCurrent(snapshot)) throw new OperationCanceledException("MCP account or catalog changed.");
      var bridge = new MemberMcpToolBridge(snapshot.Catalog);
      response = await responsesClient.GenerateAssistantContentWithToolsAsync(
          message, history, profile, key, bridge.Tools,
          () => session.IsCurrent(snapshot),
          (name, arguments, _) =>
          {
            bridge.Validate(name, arguments);
            return Task.CompletedTask;
          },
          (name, arguments, token) => bridge.ExecuteAsync(name, arguments, session, snapshot, token),
          cancellationToken).ConfigureAwait(false);
    }
    else
    {
      response = await responsesClient.GenerateAssistantContentAsync(
          message, history, profile, key, cancellationToken).ConfigureAwait(false);
    }
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
          profile?.SelectedModel);

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
