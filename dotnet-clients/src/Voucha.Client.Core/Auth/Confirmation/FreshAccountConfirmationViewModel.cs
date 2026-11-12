using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Auth;

public sealed partial class FreshAccountConfirmationViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly IFreshAccountProofService service;
  private readonly Func<string?> captureBinding;
  private readonly string binding;
  private readonly ISessionStore sessionStore;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private readonly CancellationTokenSource cancellation;
  private readonly DateTimeOffset started = DateTimeOffset.UtcNow;
  private readonly TaskCompletionSource<AccountChangeProof> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
  private bool busy;
  private bool emailMode = true;
  private bool hasSentEmail;
  private string code = string.Empty;
  private UiText? error;
  private ReAuthMethodsResponse? methods;
  private IReadOnlyList<ConfirmationPasskeyRow> passkeys = [];
  private PageInfo? passkeyPage;

  public FreshAccountConfirmationViewModel(IFreshAccountProofService service, Func<string?> captureBinding,
      ISessionStore sessionStore, IUiLocalization localization, IUiLocaleController? localeController = null,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(service);
    ArgumentNullException.ThrowIfNull(captureBinding);
    ArgumentNullException.ThrowIfNull(sessionStore);
    ArgumentNullException.ThrowIfNull(localization);
    this.service = service;
    this.captureBinding = captureBinding;
    this.sessionStore = sessionStore;
    this.localization = localization;
    binding = captureBinding() ?? throw new OperationCanceledException();
    cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    sessionStore.SessionChanged += OnSessionChanged;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public Task<AccountChangeProof> Completion => completion.Task;
  public bool IsBusy { get => busy; private set { SetProperty(ref busy, value); OnPropertyChanged(nameof(VerifyLabel)); } }
  public bool CanEmail => methods?.HasEmail == true;
  public bool CanTotp => methods?.HasTotp == true;
  public bool CanPasskey => methods?.HasPasskey == true;
  public bool IsEmailMode { get => emailMode && CanEmail; private set { SetProperty(ref emailMode, value); OnPropertyChanged(nameof(IsTotpMode)); OnPropertyChanged(nameof(CanSendEmail)); } }
  public bool IsTotpMode => !emailMode && CanTotp;
  public bool HasCodeMethod => CanEmail || CanTotp;
  public bool HasSentEmail { get => hasSentEmail; private set { SetProperty(ref hasSentEmail, value); OnPropertyChanged(nameof(CanSendEmail)); } }
  public bool CanSendEmail => CanEmail && IsEmailMode && !HasSentEmail;
  public string Code { get => code; set => SetProperty(ref code, IsEmailMode ? EmailVerificationCode.Normalize(value) : value); }
  public string? ErrorMessage => error is { } text ? localization.Resolve(text) : null;
  public string VerifyLabel => localization.Localize(IsBusy
      ? UiMessageKey.ExtractedMfaReauthDialogVerificationPanesVerifying2ec1ac7d
      : UiMessageKey.ExtractedMfaReauthDialogVerificationPanesVerifyEea2745e);
  public IReadOnlyList<ConfirmationPasskeyRow> Passkeys { get => passkeys; private set => SetProperty(ref passkeys, value); }
  public bool HasMorePasskeys => passkeyPage?.HasNextPage == true;
  public IReadOnlyList<ConfirmationProviderRow> Providers => (methods?.Providers ?? []).Select(ProviderRow).ToArray();

  public Task InitializeAsync() => RunAsync(async () =>
  {
    methods = await service.DiscoverAsync(cancellation.Token).ConfigureAwait(true);
    IsEmailMode = CanEmail || !CanTotp;
    OnPropertyChanged(nameof(CanEmail));
    OnPropertyChanged(nameof(CanTotp));
    OnPropertyChanged(nameof(CanPasskey));
    OnPropertyChanged(nameof(IsEmailMode));
    OnPropertyChanged(nameof(IsTotpMode));
    OnPropertyChanged(nameof(HasCodeMethod));
    OnPropertyChanged(nameof(CanSendEmail));
    OnPropertyChanged(nameof(Providers));
  });

  public void SelectEmail() { if (CanEmail) SelectMode(true); }
  public void SelectTotp() { if (CanTotp) SelectMode(false); }
  private void SelectMode(bool email) { IsEmailMode = email; Code = string.Empty; error = null; OnPropertyChanged(nameof(ErrorMessage)); }

  public Task SendEmailAsync() => RunAsync(async () =>
  {
    if (!CanEmail || !IsEmailMode) throw new InvalidOperationException();
    await service.RequestEmailAsync(cancellation.Token).ConfigureAwait(true);
    HasSentEmail = true;
  });

  public async Task VerifyCodeAsync()
  {
    if (IsEmailMode ? !CanEmail || !EmailVerificationCode.IsValid(Code)
        : !CanTotp || !TotpVerificationCode.IsValid(Code))
    {
      SetError(UiMessageKey.ExtractedAuthMfaStepInvalidVerificationCode4ed23ad3);
      return;
    }
    await RunAsync(async () => Finish(await service.VerifyCodeAsync(IsEmailMode, Code, cancellation.Token)
        .ConfigureAwait(true)), emailVerification: IsEmailMode).ConfigureAwait(true);
  }

  public Task LoadPasskeysAsync() => RunAsync(async () =>
  {
    if (!CanPasskey) throw new InvalidOperationException();
    if (passkeyPage is not null && !HasMorePasskeys) return;
    var page = await service.FetchPasskeysAsync(passkeyPage?.EndCursor, cancellation.Token).ConfigureAwait(true);
    Passkeys = Passkeys.Concat(page.Results.Select(item => new ConfirmationPasskeyRow(item.Id,
        string.IsNullOrWhiteSpace(item.Name) ? UiText.Localized(UiMessageKey.ExtractedMyPasskeyManagerPasskeysC87ccdb4)
            : UiText.UserContent(item.Name), localization))).DistinctBy(item => item.Id).ToArray();
    passkeyPage = page.PageInfo;
    OnPropertyChanged(nameof(HasMorePasskeys));
  });

  public Task VerifyPasskeyAsync(string id) => RunAsync(async () =>
  {
    if (!CanPasskey || !Passkeys.Any(item => item.Id == id)) throw new InvalidOperationException();
    Finish(await service.VerifyPasskeyAsync(id, cancellation.Token).ConfigureAwait(true));
  });

  public Task VerifyProviderAsync(string provider) => RunAsync(async () =>
  {
    if (methods?.Providers.Contains(provider, StringComparer.Ordinal) != true) throw new InvalidOperationException();
    Finish(await service.VerifyProviderAsync(provider, cancellation.Token).ConfigureAwait(true));
  });

  private bool IsCurrent() => !cancellation.IsCancellationRequested && captureBinding() == binding &&
      DateTimeOffset.UtcNow - started < TimeSpan.FromMinutes(5);
  private void EnsureCurrent() { if (!IsCurrent()) throw new OperationCanceledException(); }
  private void Finish(string token)
  {
    EnsureCurrent();
    if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException();
    var originalBinding = binding;
    completion.TrySetResult(new AccountChangeProof(token, () => captureBinding() == originalBinding &&
        DateTimeOffset.UtcNow - started < TimeSpan.FromMinutes(5)));
  }

  private async Task RunAsync(Func<Task> action, bool emailVerification = false)
  {
    if (IsBusy || completion.Task.IsCompleted) return;
    IsBusy = true;
    error = null;
    OnPropertyChanged(nameof(ErrorMessage));
    try { EnsureCurrent(); await action().ConfigureAwait(true); EnsureCurrent(); }
    catch (OperationCanceledException) { Cancel(); }
    catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or NotSupportedException)
    {
      SetError(emailVerification && exception is VouchaApiException { StatusCode: System.Net.HttpStatusCode.BadRequest or
          System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Gone }
          ? UiMessageKey.ExtractedMyEmailManagerInvalidOrExpiredVerificationCode9b5b98fe
          : UiMessageKey.ExtractedAuthMfaStepUnableToVerifyPleaseTryAgainB7979b96);
    }
    finally { IsBusy = false; }
  }

  private void SetError(UiMessageKey key) { error = UiText.Localized(key); OnPropertyChanged(nameof(ErrorMessage)); }
  private void OnSessionChanged(object? sender, SessionChangedEventArgs args) { if (!IsCurrent()) Cancel(); }
  public void Cancel() { cancellation.Cancel(); completion.TrySetCanceled(); }
  public void OnUiLocaleChanged() { OnPropertyChanged(nameof(ErrorMessage)); OnPropertyChanged(nameof(VerifyLabel)); OnPropertyChanged(nameof(Providers)); }
  public void Dispose() { Cancel(); sessionStore.SessionChanged -= OnSessionChanged; localeSubscription?.Dispose(); cancellation.Dispose(); }
}
