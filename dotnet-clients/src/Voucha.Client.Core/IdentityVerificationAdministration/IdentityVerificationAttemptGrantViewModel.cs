using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.IdentityVerificationAdministration;

public sealed class IdentityVerificationAttemptGrantViewModel : ObservableObject
{
  private readonly IIdentityVerificationAdministrationService service;
  private readonly IUiLocalization localization;
  private string note = string.Empty;
  private string? targetUserId;
  private UiText? loadError;
  private UiText? submission;
  private bool loading;
  private bool submitting;

  public IdentityVerificationAttemptGrantViewModel(
      IIdentityVerificationAdministrationService service,
      IUiLocalization? localization = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.localization = localization ?? UiLocalization.English;
  }

  public string IdOrUsername { get; private set; } = string.Empty;
  public bool IsAdministrator { get; private set; }
  public string Note
  {
    get => note;
    set
    {
      if (SetProperty(ref note, value ?? string.Empty)) OnPropertyChanged(nameof(CanSubmit));
    }
  }
  public bool IsLoading { get => loading; private set { if (SetProperty(ref loading, value)) OnPropertyChanged(nameof(CanSubmit)); } }
  public bool IsSubmitting { get => submitting; private set { if (SetProperty(ref submitting, value)) OnPropertyChanged(nameof(CanSubmit)); } }
  public UiText? LoadError { get => loadError; private set => SetProperty(ref loadError, value); }
  public UiText? Submission { get => submission; private set => SetProperty(ref submission, value); }
  public bool CanSubmit =>
      IsAdministrator
      && targetUserId is not null
      && !string.IsNullOrWhiteSpace(Note)
      && Note.Trim().Length <= 2000
      && !IsSubmitting
      && !IsLoading;

  public void Configure(string idOrUsername, bool isAdministrator)
  {
    IdOrUsername = idOrUsername ?? string.Empty;
    IsAdministrator = isAdministrator;
    targetUserId = null;
    Note = string.Empty;
    LoadError = null;
    Submission = null;
    OnPropertyChanged(nameof(IdOrUsername));
    OnPropertyChanged(nameof(IsAdministrator));
    OnPropertyChanged(nameof(CanSubmit));
  }

  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    if (!IsAdministrator || string.IsNullOrWhiteSpace(IdOrUsername)) return;
    IsLoading = true;
    LoadError = null;
    try
    {
      var response = await service.FetchUserAsync(IdOrUsername, cancellationToken).ConfigureAwait(true);
      targetUserId = response.User.Id;
      OnPropertyChanged(nameof(CanSubmit));
    }
    catch (OperationCanceledException) { throw; }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      targetUserId = null;
      LoadError = UiText.Localized(UiMessageKey.NativeSwiftIdentityVerificationLoadFailure);
    }
    finally { IsLoading = false; }
  }

  public async Task<bool> GrantAsync(CancellationToken cancellationToken = default)
  {
    var trimmed = Note.Trim();
    Submission = null;
    if (!IsAdministrator || targetUserId is null || trimmed.Length == 0 || trimmed.Length > 2000)
    {
      Submission = UiText.Localized(UiMessageKey.NativeSwiftIdentityVerificationValidation);
      return false;
    }
    IsSubmitting = true;
    try
    {
      var response = await service.GrantAsync(targetUserId, trimmed, cancellationToken).ConfigureAwait(true);
      if (!response.Granted)
      {
        Submission = UiText.Localized(UiMessageKey.NativeSwiftIdentityVerificationFailure);
        return false;
      }
      Note = string.Empty;
      Submission = UiText.Localized(UiMessageKey.NativeSwiftIdentityVerificationSuccess);
      return true;
    }
    catch (OperationCanceledException) { throw; }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      Submission = Failure(ex);
      return false;
    }
    finally { IsSubmitting = false; }
  }

  private static UiText Failure(Exception error)
  {
    if (error is VouchaApiException { StatusCode: { } status, ApiMessage: { } message }
        && (int)status is >= 400 and < 500
        && !string.IsNullOrWhiteSpace(message))
    {
      return UiText.UserContent(message);
    }

    return UiText.Localized(UiMessageKey.NativeSwiftIdentityVerificationFailure);
  }
}
