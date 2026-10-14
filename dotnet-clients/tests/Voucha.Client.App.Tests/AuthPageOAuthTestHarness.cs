using Voucha.Client.Core.Auth;

namespace Voucha.Client.App.Pages;

// Compile the production OAuth handlers against the actual login XAML in headless App.Tests.
public sealed partial class AuthPage : ContentPage
{
  private readonly NativeOAuthAuthorizationCoordinator oauthCoordinator;
  private readonly AuthViewModel viewModel = null!;

  public AuthPage(NativeOAuthAuthorizationCoordinator oauthCoordinator)
  {
    InitializeComponent();
    this.oauthCoordinator = oauthCoordinator;
  }

  public void RefreshOAuthForTest() => RefreshOAuthPresentation();

  private void OnSendEmailCodeClicked(object? sender, EventArgs args) { }
  private void OnResendEmailCodeClicked(object? sender, EventArgs args) { }
  private void OnVerifyEmailOtpClicked(object? sender, EventArgs args) { }
  private void OnVerifyTotpClicked(object? sender, EventArgs args) { }
  private void OnAppleSignInClicked(object? sender, EventArgs args) { }
  private void OnPasskeyClicked(object? sender, EventArgs args) { }
  private void OnPrivacyPolicyClicked(object? sender, EventArgs args) { }
  private void OnTermsOfServiceClicked(object? sender, EventArgs args) { }
  private void OnCommunityGuidelinesClicked(object? sender, EventArgs args) { }
  private void OnSignOutClicked(object? sender, EventArgs args) { }
}
