using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

public enum CommunityActionKind
{
  Create,
  Apply,
  Invite,
  RedeemInvite,
}

public sealed partial class CommunityActionViewModel :
    ObservableObject,
    IDisposable,
    IUiLocaleChangeListener
{
  private readonly ICommunitiesService service;
  private readonly CommunityActionKind kind;
  private readonly AppConfig appConfig;
  private readonly IUiLocalization localization;
  private string communitySlug = "";
  private string name = "";
  private string slug = "";
  private string? markdown;
  private string turnstileToken = "";
  private string? email;
  private string? username;
  private string? code;
  private string? message;
  private IReadOnlyList<CommunityApplicationQuestion> applicationQuestions = [];
  private IReadOnlyDictionary<string, object> applicationAnswers = new Dictionary<string, object>();
  private LoadState state = LoadState.Idle;
  private string? errorMessage;
  private readonly IDisposable? localeSubscription;

  public CommunityActionViewModel(
      ICommunitiesService service,
      CommunityActionKind kind,
      AppConfig? appConfig = null,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.kind = kind;
    this.appConfig = appConfig ?? AppConfig.FromEnvironment();
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public CommunityActionKind Kind => kind;

  public string CommunitySlug
  {
    get => communitySlug;
    set => SetProperty(ref communitySlug, value);
  }

  public string Name
  {
    get => name;
    set => SetProperty(ref name, value);
  }

  public string Slug
  {
    get => slug;
    set => SetProperty(ref slug, value);
  }

  public string? Markdown
  {
    get => markdown;
    set => SetProperty(ref markdown, value);
  }

  public string TurnstileToken
  {
    get => turnstileToken;
    set => SetProperty(ref turnstileToken, value);
  }

  public string? Email
  {
    get => email;
    set => SetProperty(ref email, value);
  }

  public string? Username
  {
    get => username;
    set => SetProperty(ref username, value);
  }

  public string? Code
  {
    get => code;
    set => SetProperty(ref code, value);
  }

  public string? Message
  {
    get => message;
    set => SetProperty(ref message, value);
  }

  public LoadState State
  {
    get => state;
    private set
    {
      if (SetProperty(ref state, value))
      {
        OnPropertyChanged(nameof(HasError));
      }
    }
  }

  public string? ErrorMessage
  {
    get => errorMessage;
    private set
    {
      if (SetProperty(ref errorMessage, value))
      {
        OnPropertyChanged(nameof(HasError));
      }
    }
  }

  public bool HasError => State == LoadState.Error || !string.IsNullOrWhiteSpace(ErrorMessage);

  public bool CanUseCaptchaBypass => appConfig.AllowCommunityCreateCaptchaBypass;

  public void ReportTurnstileChallengeFailure(string message)
  {
    TurnstileToken = "";
    ErrorMessage = string.IsNullOrWhiteSpace(message)
        ? localization.Localize(UiMessageKey.NativeDotnetCsharpTurnstileChallengeFailed)
        : message;
    State = LoadState.Error;
  }

  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(ErrorMessage));
    OnPropertyChanged(nameof(ApplicationQuestions));
  }

  public void Dispose() => localeSubscription?.Dispose();
}
