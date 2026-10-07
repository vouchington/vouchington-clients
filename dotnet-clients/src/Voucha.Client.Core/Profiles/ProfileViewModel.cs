using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private const long MaxAvatarUploadBytes = 50L * 1024L * 1024L;
  private readonly ISettingsService settingsService;
  private readonly ILandingPagesService landingPagesService;
  private readonly IPostsService postsService;
  private readonly IImageUploadService imageUploadService;
  private readonly AppConfig config;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private User? identity;
  private User? user;
  private UserMetrics? userMetrics;
  private string bioMarkdown = string.Empty;
  private IReadOnlyList<ProfileLink> profileLinks = [];
  private IReadOnlyList<PostRow> historyItems = [];
  private IReadOnlyList<ProfileHistoryTabRow> historyTabs = [];
  private ProfileHistoryTab selectedHistoryTab = ProfileHistoryTab.All;
  private bool canEdit;
  private bool isUploadingAvatar;
  private bool isSavingBio;
  private LoadState state = LoadState.Idle;
  private string? errorMessage;
  private string? loadedIdOrUsername;

  public ProfileViewModel(
      ISettingsService settingsService,
      ILandingPagesService landingPagesService,
      IPostsService postsService,
      IImageUploadService imageUploadService,
      AppConfig config,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    this.landingPagesService = landingPagesService ?? throw new ArgumentNullException(nameof(landingPagesService));
    this.postsService = postsService ?? throw new ArgumentNullException(nameof(postsService));
    this.imageUploadService = imageUploadService ?? throw new ArgumentNullException(nameof(imageUploadService));
    this.config = config ?? throw new ArgumentNullException(nameof(config));
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
    historyTabs = BuildTabs(null, ProfileHistoryTab.All);
  }

  public User? Identity
  {
    get => identity;
    private set
    {
      if (SetProperty(ref identity, value)) RefreshHeaderProperties();
    }
  }

  public User? User
  {
    get => user;
    private set
    {
      if (SetProperty(ref user, value)) RefreshHeaderProperties();
    }
  }

  public string DisplayName =>
      SelectedDisplayAccount()?.Name ??
      User?.VerifiedDisplayName ??
      User?.Name ??
      Identity?.Username ??
      User?.Username ??
      User?.Id ??
      localization.Localize(UiMessageKey.NativeDotnetResidualProfile);

  public string UsernameLabel => User?.Username is { Length: > 0 } username ? $"@{username}" : User?.Id ?? string.Empty;

  public string? AccountTypeLabel => AccountTypeLabels.Resolve(User?.AccountType, localization);

  public string? ProfileImageId => Identity?.ProfileImageId ?? User?.ProfileImageId;

  public Uri? AvatarUrl => config.ImageUrlForImageId(ProfileImageId, 144);

  public string BioMarkdown
  {
    get => bioMarkdown;
    set => SetProperty(ref bioMarkdown, value);
  }

  public IReadOnlyList<ProfileLink> ProfileLinks
  {
    get => profileLinks;
    private set => SetProperty(ref profileLinks, value);
  }

  public IReadOnlyList<PostRow> HistoryItems
  {
    get => historyItems;
    private set => SetProperty(ref historyItems, value);
  }

  public IReadOnlyList<ProfileHistoryTabRow> HistoryTabs
  {
    get => historyTabs;
    private set => SetProperty(ref historyTabs, value);
  }

  public ProfileHistoryTab SelectedHistoryTab
  {
    get => selectedHistoryTab;
    private set => SetProperty(ref selectedHistoryTab, value);
  }

  public bool CanEdit
  {
    get => canEdit;
    private set => SetProperty(ref canEdit, value);
  }

  public bool IsUploadingAvatar
  {
    get => isUploadingAvatar;
    private set => SetProperty(ref isUploadingAvatar, value);
  }

  public bool IsSavingBio
  {
    get => isSavingBio;
    private set => SetProperty(ref isSavingBio, value);
  }

  public LoadState State
  {
    get => state;
    private set
    {
      if (SetProperty(ref state, value)) OnPropertyChanged(nameof(IsLoading));
    }
  }

  public bool IsLoading => State == LoadState.Loading;

  public string? ErrorMessage
  {
    get => errorMessage;
    private set => SetProperty(ref errorMessage, value);
  }

  private void RefreshHeaderProperties()
  {
    OnPropertyChanged(nameof(DisplayName));
    OnPropertyChanged(nameof(UsernameLabel));
    OnPropertyChanged(nameof(AccountTypeLabel));
    OnPropertyChanged(nameof(ProfileImageId));
    OnPropertyChanged(nameof(AvatarUrl));
  }

  private UserDisplayAccount? SelectedDisplayAccount()
  {
    if (User?.DisplayAccount is { } displayAccount) return displayAccount;

    return User?.UseDisplayNameFrom switch
    {
      "facebook" => User.FacebookAccount,
      "apple" => User.AppleAccount,
      "google" => User.GoogleAccount,
      "x" => User.XAccount,
      "linkedin" => User.LinkedinAccount,
      "microsoft" => User.MicrosoftAccount,
      "github" => User.GithubAccount,
      _ => null,
    };
  }

}
