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
    var service = new FreshAccountProofServiceStub { Methods = new(false, false, true, []) };
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
