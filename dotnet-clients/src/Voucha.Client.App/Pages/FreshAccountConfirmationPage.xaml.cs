using Voucha.Client.Core.Auth;

namespace Voucha.Client.App.Pages;

public partial class FreshAccountConfirmationPage : ContentPage
{
  private readonly FreshAccountConfirmationViewModel model;
  public FreshAccountConfirmationPage(FreshAccountConfirmationViewModel model)
  {
    InitializeComponent();
    this.model = model;
    BindingContext = model;
  }
  private void OnEmailSelected(object? sender, EventArgs args) => model.SelectEmail();
  private void OnTotpSelected(object? sender, EventArgs args) => model.SelectTotp();
  private async void OnSendEmail(object? sender, EventArgs args) => await model.SendEmailAsync();
  private async void OnVerifyCode(object? sender, EventArgs args) => await model.VerifyCodeAsync();
  private async void OnLoadPasskeys(object? sender, EventArgs args) => await model.LoadPasskeysAsync();
  private async void OnVerifyPasskey(object? sender, EventArgs args)
  {
    if (sender is Button { CommandParameter: string id }) await model.VerifyPasskeyAsync(id);
  }
  private async void OnVerifyProvider(object? sender, EventArgs args)
  {
    if (sender is Button { CommandParameter: string provider }) await model.VerifyProviderAsync(provider);
  }
  private void OnCancel(object? sender, EventArgs args) => model.Cancel();
  protected override void OnDisappearing() { model.Cancel(); base.OnDisappearing(); }
}
