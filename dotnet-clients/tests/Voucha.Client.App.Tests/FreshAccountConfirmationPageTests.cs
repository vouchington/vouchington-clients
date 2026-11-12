using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Support;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class FreshAccountConfirmationPageTests
{
  [Fact]
  public async Task VisibleEmailInputWaitsForExplicitVerificationAndShowsCanonicalProgress()
  {
    Prepare();
    var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FreshAccountProofServiceStub { Verification = () => pending.Task };
    using var model = Model(service);
    var page = new FreshAccountConfirmationPage(model);
    await model.InitializeAsync();
    var entry = Find<Entry>(page, "account-confirmation-code");
    var verify = Find<Button>(page, "account-confirmation-verify");
    entry.Text = "abcdefgh23";
    Assert.True(entry.IsVisible);
    Assert.Equal("ABCDEFGH23", model.Code);
    Assert.Empty(service.Codes);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.ExtractedMfaReauthDialogVerificationPanesVerifyEea2745e), verify.Text);

    verify.SendClicked();

    Assert.Single(service.Codes);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.ExtractedMfaReauthDialogVerificationPanesVerifying2ec1ac7d), verify.Text);
    pending.SetResult("one-use-proof");
    Assert.Equal("one-use-proof", (await model.Completion.WaitAsync(TestContext.Current.CancellationToken)).Consume());
  }

  [Fact]
  public async Task PasskeyOnlyAccountHidesCodeControlsAndRendersOnlyOwnedPasskeys()
  {
    Prepare();
    var service = new FreshAccountProofServiceStub { Methods = new(HasEmail: false, HasPasskey: true, HasTotp: false, Providers: []) };
    using var model = Model(service);
    var page = new FreshAccountConfirmationPage(model);
    await model.InitializeAsync();
    Assert.False(Find<Entry>(page, "account-confirmation-code").IsVisible);
    Assert.False(Find<Button>(page, "account-confirmation-verify").IsVisible);
    var passkey = page.GetVisualTreeDescendants().OfType<Button>().Single(button =>
        button.Text == UiLocalization.English.Localize(UiMessageKey.ExtractedMyPasskeyManagerPasskeysC87ccdb4));
    passkey.SendClicked();
    var owned = page.GetVisualTreeDescendants().OfType<Button>().Single(button => button.CommandParameter as string == "owned-passkey");
    Assert.Equal("Device", owned.Text);
    owned.SendClicked();
    Assert.Equal("proof-token", (await model.Completion.WaitAsync(TestContext.Current.CancellationToken)).Consume());
    Assert.Equal(["owned-passkey"], service.PasskeyIds);
    Assert.Empty(service.Codes);
  }

  [Fact]
  public async Task CancelLeavesVerificationUntouched()
  {
    Prepare();
    var service = new FreshAccountProofServiceStub();
    using var model = Model(service);
    var page = new FreshAccountConfirmationPage(model);
    await model.InitializeAsync();
    page.GetVisualTreeDescendants().OfType<Button>().Single(button =>
        button.Text == UiLocalization.English.Localize(UiMessageKey.CommonCancel)).SendClicked();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => model.Completion);
    Assert.Empty(service.Codes);
    Assert.Empty(service.Providers);
    Assert.Empty(service.PasskeyIds);
  }

  [Fact]
  public async Task MethodSelectorsAndSendResendButtonsKeepEmailAndTotpPurposeSeparate()
  {
    Prepare();
    var service = new FreshAccountProofServiceStub();
    using var model = Model(service);
    var page = new FreshAccountConfirmationPage(model);
    await model.InitializeAsync();
    Button ButtonFor(UiMessageKey key) => page.GetVisualTreeDescendants().OfType<Button>().Single(button =>
        button.Text == UiLocalization.English.Localize(key));
    ButtonFor(UiMessageKey.ExtractedMfaReauthDialogVerificationPanesSendCode66a5b409).SendClicked();
    Assert.Equal(1, service.EmailRequests);
    ButtonFor(UiMessageKey.ExtractedMfaReauthDialogVerificationPanesResend1f948437).SendClicked();
    Assert.Equal(2, service.EmailRequests);
    ButtonFor(UiMessageKey.ExtractedMfaReauthDialogMethodSelectorAuthenticatorApp09a12ace).SendClicked();
    Assert.True(model.IsTotpMode);
    var entry = Find<Entry>(page, "account-confirmation-code");
    Assert.Same(Keyboard.Numeric, entry.Keyboard);
    entry.Text = "123456";
    ButtonFor(UiMessageKey.ExtractedMfaReauthDialogMethodSelectorEmailVerification4d225196).SendClicked();
    Assert.True(model.IsEmailMode);
    Assert.Empty(entry.Text);
    Assert.Empty(service.Codes);
  }

  [Fact]
  public async Task LinkedProviderButtonUsesOnlyTheAdvertisedFreshProvider()
  {
    Prepare();
    var service = new FreshAccountProofServiceStub { Methods = new(false, false, false, ["google"]) };
    using var model = Model(service);
    var page = new FreshAccountConfirmationPage(model);
    await model.InitializeAsync();
    page.GetVisualTreeDescendants().OfType<Button>().Single(button => button.CommandParameter as string == "google").SendClicked();
    Assert.Equal("proof-token", (await model.Completion.WaitAsync(TestContext.Current.CancellationToken)).Consume());
    Assert.Equal(["google"], service.Providers);
    Assert.Empty(service.Codes);
  }

  [Fact]
  public async Task NavigatingAwayCancelsDialogBeforeLateVerificationReturns()
  {
    Prepare();
    var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FreshAccountProofServiceStub { Verification = () => pending.Task };
    using var model = Model(service);
    var page = new DisappearingConfirmationPage(model);
    await model.InitializeAsync();
    Find<Entry>(page, "account-confirmation-code").Text = "ABCDEFGH23";
    Find<Button>(page, "account-confirmation-verify").SendClicked();
    page.Disappear();
    pending.SetResult("late-proof");
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => model.Completion);
    Assert.Single(service.Codes);
  }

  private sealed class DisappearingConfirmationPage(FreshAccountConfirmationViewModel model) : FreshAccountConfirmationPage(model)
  {
    public void Disappear() => base.OnDisappearing();
  }

  private static T Find<T>(Page page, string id) where T : View =>
      page.GetVisualTreeDescendants().OfType<T>().Single(view => view.AutomationId == id);

  private static FreshAccountConfirmationViewModel Model(FreshAccountProofServiceStub service) =>
      new(service, () => "account/device/session", new ConfirmationSessionStore(), UiLocalization.English);

  private static void Prepare()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var application = new Application();
    foreach (var key in new[] { "Body", "Headline", "Metadata" }) application.Resources[key] = new Style(typeof(Label));
    foreach (var key in UiMessageKey.All) application.Resources[key.Value] = UiLocalization.English.Localize(key);
  }
}
