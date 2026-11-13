using System.Net;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class ConfirmationBoundaryTests
{
  [Theory]
  [InlineData("identity")]
  [InlineData("device")]
  [InlineData("session")]
  public void MissingSessionBindingComponentCannotAuthorizeConfirmation(string missing)
  {
    var origin = new Uri("https://api.example.test");
    var session = new ConfirmationSessionStore();
    if (missing == "identity") session.Change(null);
    var cookies = new CookieContainer();
    if (missing != "device") cookies.Add(origin, new Cookie("dt", "device"));
    if (missing != "session") cookies.Add(origin, new Cookie("st", "session"));
    using var jar = new SessionCookieJar(cookies, origin, new TestSessionCookiePersistence());
    Assert.Null(AccountChangeSessionBinding.Capture(session, jar, origin));
  }

  [Theory]
  [InlineData(null)]
  [InlineData("12345/")]
  [InlineData("12345:")]
  public void InvalidTotpCannotReachRequestEncoding(string? code)
  {
    Assert.False(TotpVerificationCode.IsValid(code));
    Assert.Throws<ArgumentException>(() => TotpVerificationCode.RequireValid(code!));
  }

  [Fact]
  public async Task DialogStartsWithNoAdvertisedMethodsAndLocaleSubscriptionStopsAfterDisposal()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en-US"));
    var service = new FreshAccountProofServiceStub();
    var model = new FreshAccountConfirmationViewModel(service, () => "session", new ConfirmationSessionStore(),
        new UiLocalization(controller), controller);
    Assert.False(model.CanEmail);
    Assert.False(model.CanTotp);
    Assert.False(model.CanPasskey);
    Assert.False(model.HasMorePasskeys);
    Assert.Empty(model.Providers);
    Assert.Null(model.ErrorMessage);
    model.SelectEmail();
    model.SelectTotp();
    await model.VerifyCodeAsync();
    Assert.Empty(service.Codes);
    var notifications = new List<string?>();
    model.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
    controller.ApplySavedLocale("fr");
    Assert.Contains(nameof(model.VerifyLabel), notifications);
    model.Dispose();
    notifications.Clear();
    controller.ApplySavedLocale("en");
    Assert.Empty(notifications);
    Assert.Throws<OperationCanceledException>(() => new FreshAccountConfirmationViewModel(service, () => null,
        new ConfirmationSessionStore(), UiLocalization.English));
  }

  [Fact]
  public async Task InFlightVerificationDisplaysCanonicalBusyLabelAndKeepsOneRequest()
  {
    var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FreshAccountProofServiceStub { Verification = () => pending.Task };
    using var model = new FreshAccountConfirmationViewModel(service, () => "session", new ConfirmationSessionStore(), UiLocalization.English);
    await model.InitializeAsync();
    model.Code = "ABCDEFGH23";
    var verification = model.VerifyCodeAsync();
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.ExtractedMfaReauthDialogVerificationPanesVerifying2ec1ac7d), model.VerifyLabel);
    await model.VerifyCodeAsync();
    Assert.Single(service.Codes);
    pending.SetResult("proof");
    await verification;
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.ExtractedMfaReauthDialogVerificationPanesVerifyEea2745e), model.VerifyLabel);
  }
}
