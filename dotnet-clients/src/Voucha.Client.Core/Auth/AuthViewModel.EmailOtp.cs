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
