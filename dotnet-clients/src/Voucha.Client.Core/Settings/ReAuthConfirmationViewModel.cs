using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Settings;

public enum ReAuthConfirmationState
{
  SelectingMethod,
  Expired,
  Canceled,

  /// <summary>The provider proof belongs to another session; only a different method can recover.</summary>
  Rejected,
}

/// <summary>
/// Outcome of a provider re-authentication attempt and its recovery: confirm again after an
/// expiry, or return to method selection after a cancel or a rejected (other-session) proof.
/// </summary>
public sealed class ReAuthConfirmationViewModel : ObservableObject
{
  private readonly IUiLocalization localization;
  private ReAuthConfirmationState state;
  private string? prompt;

  public ReAuthConfirmationViewModel(IUiLocalization? localization = null)
  {
    this.localization = localization ?? UiLocalization.English;
  }

  public ReAuthConfirmationState State
  {
    get => state;
    private set
    {
      if (SetProperty(ref state, value)) OnPropertyChanged(nameof(Message));
    }
  }

  public string? Message => State switch
  {
    ReAuthConfirmationState.Expired => localization.Localize(UiMessageKey.SharedReauthConfirmationExpired),
    ReAuthConfirmationState.Canceled => localization.Localize(UiMessageKey.SharedReauthConfirmationCanceled),
    ReAuthConfirmationState.Rejected =>
        localization.Localize(UiMessageKey.SharedReauthConfirmationChooseAnotherMethod),
    _ => null,
  };

  /// <summary>Set when the user is sent back to method selection after a failed attempt.</summary>
  public string? Prompt
  {
    get => prompt;
    private set => SetProperty(ref prompt, value);
  }

  /// <summary>Failures without a confirmation code keep the caller's generic presentation.</summary>
  public Exception? UnrecognizedError { get; private set; }

  public void RecordFailure(Exception error)
  {
    ArgumentNullException.ThrowIfNull(error);
    UnrecognizedError = null;
    if (error is not VouchaApiException { IsMfaReauthRequired: true } api)
    {
      UnrecognizedError = error;
      return;
    }

    State = api.StatusCode == HttpStatusCode.Forbidden
        ? ReAuthConfirmationState.Rejected
        : ReAuthConfirmationState.Expired;
  }

  public void RecordCancellation()
  {
    UnrecognizedError = null;
    State = ReAuthConfirmationState.Canceled;
  }

  /// <summary>Returns to method selection with the choose-another-method prompt.</summary>
  public void ChooseAnotherMethod()
  {
    UnrecognizedError = null;
    State = ReAuthConfirmationState.SelectingMethod;
    Prompt = localization.Localize(UiMessageKey.SharedReauthConfirmationChooseAnotherMethod);
  }

  public void BeginAttempt()
  {
    UnrecognizedError = null;
    State = ReAuthConfirmationState.SelectingMethod;
    Prompt = null;
  }
}
