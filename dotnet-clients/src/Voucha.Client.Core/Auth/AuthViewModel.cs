using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Auth;

public sealed partial class AuthViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly AuthenticationService authenticationService;
  private readonly ISessionStore sessionStore;
  private readonly IUiLocaleController? uiLocaleController;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private readonly SynchronizationContext? syncContext;
  private string email = "";
  private string code = "";
  private string turnstileToken = "";
  private string? uiLocale;
  private string totpCode = "";
  private MfaChallenge? mfaChallenge;
  private string? statusMessage;
  private UiText? localizedStatusMessage;
  private bool isLoading;

  public AuthViewModel(
      AuthenticationService authenticationService,
      ISessionStore sessionStore,
      IUiLocaleController? uiLocaleController = null,
      IUiLocalization? localization = null)
  {
    this.authenticationService = authenticationService ??
        throw new ArgumentNullException(nameof(authenticationService));
    this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    this.uiLocaleController = uiLocaleController;
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = uiLocaleController?.SubscribeLocaleChanges(this);
    syncContext = SynchronizationContext.Current;
    sessionStore.SessionChanged += (_, _) =>
    {
      if (syncContext is not null)
      {
        syncContext.Post(_ => NotifySessionStateChanged(), null);
      }
      else
      {
        NotifySessionStateChanged();
      }
    };
  }

  public string Email
  {
    get => email;
    set => SetProperty(ref email, value);
  }

  public string Code
  {
    get => code;
    set => SetProperty(ref code, value);
  }

  public string TurnstileToken
  {
    get => turnstileToken;
    set => SetProperty(ref turnstileToken, value);
  }

  public string? UiLocale
  {
    get => uiLocaleController?.EffectiveLocale ?? uiLocale;
    set => SetProperty(ref uiLocale, value);
  }

  public string TotpCode
  {
    get => totpCode;
    set => SetProperty(ref totpCode, value);
  }

  public bool IsLoading
  {
    get => isLoading;
    private set => SetProperty(ref isLoading, value);
  }

  public string? StatusMessage
  {
    get => localizedStatusMessage is UiText text ? localization.Resolve(text) : statusMessage;
    private set
    {
      localizedStatusMessage = null;
      SetProperty(ref statusMessage, value, nameof(StatusMessage));
    }
  }

  public void ClearStatusMessage() => StatusMessage = null;

  public void SetStatusMessage(string message) => StatusMessage = message;

  public void ClearMfaChallenge()
  {
    mfaChallenge = null;
    TotpCode = "";
    ClearStatusMessage();
  }

  public void AcceptMfaChallenge(string loginAttemptId)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(loginAttemptId);
    mfaChallenge = new MfaChallenge(loginAttemptId);
    statusMessage = null;
    localizedStatusMessage = UiText.Localized(UiMessageKey.NativeDotnetAuthMfaCodeRequired);
    OnPropertyChanged(nameof(StatusMessage));
  }

  public bool IsAuthenticated => sessionStore.Current.IsAuthenticated;

  public string SessionLabel =>
      sessionStore.Current.Identity?.Username is { Length: > 0 } username
          ? localization.Format(UiMessageKey.NativeDotnetAuthSignedInAs, ("username", username))
          : localization.Localize(UiMessageKey.NativeDotnetDynamicSignedOut);

  public async Task RequestEmailOtpAsync(CancellationToken cancellationToken = default)
  {
    await RunAsync(async () =>
    {
      await authenticationService
          .RequestEmailOtpAsync(Email, TurnstileToken, UiLocale, cancellationToken)
          .ConfigureAwait(true);
      MarkEmailOtpRequested();
    }).ConfigureAwait(true);
  }

  public async Task VerifyEmailOtpAsync(CancellationToken cancellationToken = default)
  {
    await RunAsync(async () =>
    {
      mfaChallenge = await authenticationService
          .VerifyEmailOtpAsync(Email, Code, cancellationToken)
          .ConfigureAwait(true);
      StatusMessage = mfaChallenge is null
          ? localization.Localize(UiMessageKey.NativeDotnetCsharpSignedIn)
          : localization.Localize(UiMessageKey.NativeDotnetAuthMfaCodeRequired);
      OnPropertyChanged(nameof(SessionLabel));
    }).ConfigureAwait(true);
  }

  public async Task VerifyTotpAsync(CancellationToken cancellationToken = default)
  {
    await RunAsync(async () =>
    {
      if (mfaChallenge is null)
      {
        throw new InvalidOperationException(localization.Localize(UiMessageKey.NativeDotnetAuthNoMfaChallenge));
      }

      await authenticationService
          .VerifyMfaTotpAsync(mfaChallenge.LoginAttemptId, TotpCode, cancellationToken)
          .ConfigureAwait(true);
      mfaChallenge = null;
      StatusMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpSignedIn);
    }).ConfigureAwait(true);
  }

  public async Task SignInWithPasskeyAsync(CancellationToken cancellationToken = default)
  {
    await RunAsync(async () =>
    {
      mfaChallenge = await authenticationService
          .SignInWithPasskeyAsync(cancellationToken)
          .ConfigureAwait(true);
      StatusMessage = mfaChallenge is null
          ? localization.Localize(UiMessageKey.NativeDotnetAuthSignedInWithPasskey)
          : localization.Localize(UiMessageKey.NativeDotnetAuthMfaCodeRequired);
    }).ConfigureAwait(true);
  }

  public async Task SignInWithAppleAsync(CancellationToken cancellationToken = default)
  {
    await RunAsync(async () =>
    {
      mfaChallenge = await authenticationService
          .SignInWithAppleAsync(cancellationToken)
          .ConfigureAwait(true);
      StatusMessage = mfaChallenge is null
          ? localization.Localize(UiMessageKey.NativeDotnetAuthSignedInWithApple)
          : localization.Localize(UiMessageKey.NativeDotnetAuthMfaCodeRequired);
    }).ConfigureAwait(true);
  }

  public async Task SignOutAsync(CancellationToken cancellationToken = default)
  {
    await RunAsync(async () =>
    {
      await sessionStore.SignOutAsync(cancellationToken).ConfigureAwait(true);
      StatusMessage = localization.Localize(UiMessageKey.NativeDotnetCsharpSignedOut);
    }).ConfigureAwait(true);
  }
}
