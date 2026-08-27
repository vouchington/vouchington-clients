using Voucha.Client.Core.Settings;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed class EmailVerificationRecoveryPage : ContentPage, IDisposable
{
  private readonly EmailAddressManagerViewModel viewModel;

  public EmailVerificationRecoveryPage(EmailAddressManagerViewModel viewModel)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    Unloaded += (_, _) => Dispose();
    SetDynamicResource(TitleProperty, UiMessageKey.NativeDotnetCsharpVerificationTitle.Value);
    Content = new VerticalStackLayout
    {
      Padding = 20,
      Spacing = 16,
      Children =
      {
        UiCopy.Bind(
            new Label(),
            Label.TextProperty,
            UiMessageKey.NativeDotnetCsharpVerificationDescription),
        new EmailAddressManagerView { BindingContext = viewModel },
        UiCopy.Bind(
            new Button { Command = new Command(() => _ = Navigation.PopModalAsync()) },
            Button.TextProperty,
            UiMessageKey.NativeDotnetCsharpClose),
      },
    };
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await viewModel.LoadAsync();
  }

  public void Dispose() => viewModel.Dispose();
}
