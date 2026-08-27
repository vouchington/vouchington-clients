using System.Collections.ObjectModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly ISettingsService settingsService;
  private readonly ILocalLLMConfigurationStore localLLMConfigurationStore;
  private readonly ILocalLLMSecretStore localLLMSecretStore;
  private readonly OpenAICompatibleResponsesClient localLLMResponsesClient;
  private readonly bool ownsLocalLLMResponsesClient;
  private readonly LocalLLMFeaturePolicy localLLMFeaturePolicy;
  private readonly IUiLocaleController? uiLocaleController;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private string username = string.Empty;
  private string displayNameSource = "username";
  private string? profileImageId;
  private string profileMarkdown = string.Empty;
  private string apiKeyLabel = string.Empty;
  private string apiKeyType = "rss";
  private string? currentUserIdOrSlug;
  private string? identitySummary;
  private string? profileSummary;
  private string? apiKeySecret;
  private string? apiKeySecretDisplay;
  private string? dataRequestStatus;
  private string? membershipSummary;
  private string? errorMessage;
  private bool isLoading;
  private IReadOnlyList<SettingsSelectionRowViewModel> privacySelections = [];
  private IReadOnlyList<SettingsToggleRowViewModel> privacyToggles = [];
  private IReadOnlyList<ApiKey> apiKeys = [];
  private IReadOnlyList<WebPushSubscription> pushSubscriptions = [];
  private Membership? membership;
  private UserDataRequestResponse? dataRequest;
  private User? loadedUser;

  public SettingsViewModel(
      ISettingsService settingsService,
      ILocalLLMConfigurationStore? localLLMConfigurationStore = null,
      ILocalLLMSecretStore? localLLMSecretStore = null,
      OpenAICompatibleResponsesClient? localLLMResponsesClient = null,
      LocalLLMFeaturePolicy? localLLMFeaturePolicy = null,
      IUiLocaleController? uiLocaleController = null,
      IUiLocalization? localization = null)
  {
    this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    this.localLLMConfigurationStore = localLLMConfigurationStore ?? new InMemoryLocalLLMConfigurationStore();
    this.localLLMSecretStore = localLLMSecretStore ?? new InMemoryLocalLLMSecretStore();
    ownsLocalLLMResponsesClient = localLLMResponsesClient is null;
    this.localLLMResponsesClient = localLLMResponsesClient ?? new OpenAICompatibleResponsesClient();
    this.localLLMFeaturePolicy = localLLMFeaturePolicy ?? new LocalLLMFeaturePolicy();
    this.uiLocaleController = uiLocaleController;
    this.localization = localization ?? UiLocalization.English;
    membershipActionNotice = this.localization.Localize(UiMessageKey.NativeDotnetResidualBillingUnavailable);
    localeSubscription = uiLocaleController?.SubscribeLocaleChanges(this);
  }

  public string Username
  {
    get => username;
    set => SetProperty(ref username, value ?? string.Empty);
  }

  public string DisplayNameSource
  {
    get => displayNameSource;
    set
    {
      if (SetProperty(ref displayNameSource, value ?? "username"))
      {
        OnPropertyChanged(nameof(SelectedDisplayNameSourceOption));
      }
    }
  }

  public string? ProfileImageId
  {
    get => profileImageId;
    set => SetProperty(ref profileImageId, value);
  }

  public string ProfileMarkdown
  {
    get => profileMarkdown;
    set => SetProperty(ref profileMarkdown, value ?? string.Empty);
  }

  public string? IdentitySummary
  {
    get => identitySummary;
    private set => SetProperty(ref identitySummary, value);
  }

  public string? ProfileSummary
  {
    get => profileSummary;
    private set => SetProperty(ref profileSummary, value);
  }

  public string? ApiKeySecret
  {
    get => apiKeySecret;
    private set
    {
      if (SetProperty(ref apiKeySecret, value))
      {
        ApiKeySecretDisplay = value is { Length: > 0 }
            ? localization.Format(
                UiMessageKey.NativeDotnetSettingsApiKeyCreatedNotice,
                ("maskedKey", MaskSecret(value)))
            : null;
        OnPropertyChanged(nameof(HasApiKeySecret));
      }
    }
  }

  public string? ApiKeySecretDisplay
  {
    get => apiKeySecretDisplay;
    private set => SetProperty(ref apiKeySecretDisplay, value);
  }

  public bool HasApiKeySecret => ApiKeySecret is { Length: > 0 };

  public string? LocalizedDataRequestStatus
  {
    get => dataRequestStatus;
    private set => SetProperty(ref dataRequestStatus, value);
  }

  public string? MembershipSummary
  {
    get => membershipSummary;
    private set => SetProperty(ref membershipSummary, value);
  }

  public string? ErrorMessage
  {
    get => errorMessage;
    private set => SetProperty(ref errorMessage, value);
  }

  public IReadOnlyList<SettingsSelectionRowViewModel> PrivacySelections
  {
    get => privacySelections;
    private set
    {
      SetProperty(ref privacySelections, value);
    }
  }

  public IReadOnlyList<SettingsToggleRowViewModel> PrivacyToggles
  {
    get => privacyToggles;
    private set
    {
      SetProperty(ref privacyToggles, value);
    }
  }

  public Membership? Membership
  {
    get => membership;
    private set => SetProperty(ref membership, value);
  }

  public UserDataRequestResponse? DataRequest
  {
    get => dataRequest;
    private set
    {
      SetProperty(ref dataRequest, value);
      UpdateDataRequestState();
    }
  }

  private static UiText T(UiMessageKey key) => UiText.Localized(key);
}
