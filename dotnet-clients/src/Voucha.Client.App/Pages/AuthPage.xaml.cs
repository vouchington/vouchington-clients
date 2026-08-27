using Voucha.Client.App;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
#if DEBUG
using Microsoft.Maui.Controls.Shapes;
#endif

namespace Voucha.Client.App.Pages;

public partial class AuthPage : ContentPage
{
  private readonly AuthLinkPrefillStore prefillStore;
  private readonly AuthViewModel viewModel;
  private readonly ITurnstileTokenProvider turnstileTokenProvider;
  private readonly NativeOAuthAuthorizationCoordinator oauthCoordinator;
  private bool emailOtpRequestInProgress;

  public AuthPage(
      AuthViewModel viewModel,
      AuthLinkPrefillStore prefillStore,
      ITurnstileTokenProvider turnstileTokenProvider,
      NativeOAuthAuthorizationCoordinator oauthCoordinator)
  {
    InitializeComponent();
    this.prefillStore = prefillStore;
    this.viewModel = viewModel;
    this.turnstileTokenProvider = turnstileTokenProvider;
    this.oauthCoordinator = oauthCoordinator;

    BindingContext = viewModel;
#if DEBUG
    AddDevelopmentSessionControls();
#endif
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    AttachOAuthObservers();
    if (prefillStore.Take() is { } prefill)
    {
      viewModel.ApplyEmailOtpPrefill(prefill.EmailAddress, prefill.Otp);
    }
    await RefreshOAuthAsync().ConfigureAwait(true);
  }

  protected override void OnDisappearing()
  {
    DetachOAuthObservers();
    base.OnDisappearing();
  }

  private async Task RequestEmailOtpAsync()
  {
    if (emailOtpRequestInProgress)
    {
      return;
    }

    emailOtpRequestInProgress = true;
    try
    {
      viewModel.TurnstileToken = await turnstileTokenProvider.GetTokenAsync();
      await viewModel.RequestEmailOtpAsync();
    }
    catch (OperationCanceledException)
    {
      viewModel.ClearStatusMessage();
    }
    catch (InvalidOperationException ex)
    {
      viewModel.SetStatusMessage(ex.Message);
    }
    finally
    {
      viewModel.TurnstileToken = "";
      emailOtpRequestInProgress = false;
    }
  }

  private async void OnSendEmailCodeClicked(object? sender, EventArgs e) =>
      await RequestEmailOtpAsync();

  private async void OnResendEmailCodeClicked(object? sender, EventArgs e) =>
      await RequestEmailOtpAsync();

  private async void OnVerifyTotpClicked(object? sender, EventArgs e)
    => await viewModel.VerifyTotpAsync();

  private async void OnVerifyEmailOtpClicked(object? sender, EventArgs e) =>
      await viewModel.VerifyEmailOtpAsync();

  private async void OnAppleSignInClicked(object? sender, EventArgs e) =>
      await viewModel.SignInWithAppleAsync();

  private async void OnPasskeyClicked(object? sender, EventArgs e) =>
      await viewModel.SignInWithPasskeyAsync();

  private async void OnSignOutClicked(object? sender, EventArgs e) =>
      await viewModel.SignOutAsync();

#if DEBUG
  private void AddDevelopmentSessionControls()
  {
    var section = new Border
    {
      Padding = 14,
      Stroke = Color.FromArgb("#D8DEE9"),
      StrokeThickness = 1,
      StrokeShape = new RoundRectangle { CornerRadius = 8 },
      Content = new VerticalStackLayout
      {
        Spacing = 10,
        Children =
        {
          UiCopy.Bind(new Label { Style = FindHeadlineStyle() }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpDevelopmentSession),
          BoundEntry("dt cookie", nameof(AuthViewModel.DeviceToken), isPassword: false),
          BoundEntry("st cookie", nameof(AuthViewModel.SessionToken), isPassword: true),
          UiCopy.Bind(new Button { Command = new Command(() => _ = viewModel.InjectDevelopmentCookiesAsync()) }, Button.TextProperty, UiMessageKey.NativeDotnetCsharpLoadCookies),
        },
      },
    };
    ContentStack.Children.Insert(ContentStack.Children.Count - 1, section);
  }

  private static Style? FindHeadlineStyle() =>
      Application.Current?.Resources.TryGetValue("Headline", out var style) == true
          ? (Style)style
          : null;

  private static Entry BoundEntry(string placeholder, string path, bool isPassword)
  {
    var entry = new Entry { Placeholder = placeholder, IsPassword = isPassword };
    entry.SetBinding(Entry.TextProperty, path);
    return entry;
  }
#endif
}
