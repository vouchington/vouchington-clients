namespace Voucha.Client.Core.Auth;

using Voucha.Client.Core.Localization;

public sealed partial class AuthViewModel
{
  private bool hasRequestedEmailOtp;

  public bool HasRequestedEmailOtp => hasRequestedEmailOtp;

  public bool CanResendEmailOtp => hasRequestedEmailOtp;

  public void ApplyEmailOtpPrefill(string? email, string? code)
  {
    Email = email ?? "";
    Code = code ?? "";
  }

  public async Task VerifyEmailOtpAsync(CancellationToken cancellationToken = default)
  {
    if (!EmailVerificationCode.IsValid(Code))
    {
      SetLocalizedStatusMessage(UiMessageKey.NativeDotnetAuthOneTimeCode);
      return;
    }
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

  private void MarkEmailOtpRequested()
  {
    var wasRequested = hasRequestedEmailOtp;
    hasRequestedEmailOtp = true;
    OnPropertyChanged(nameof(HasRequestedEmailOtp));
    OnPropertyChanged(nameof(CanResendEmailOtp));
    SetLocalizedStatusMessage(
        wasRequested
            ? UiMessageKey.NativeDotnetAuthEmailCodeResent
            : UiMessageKey.NativeDotnetAuthEmailCodeRequested);
  }
}
