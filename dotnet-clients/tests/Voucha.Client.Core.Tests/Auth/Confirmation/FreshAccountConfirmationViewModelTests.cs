using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Auth;

public sealed class FreshAccountConfirmationViewModelTests
{
  [Fact]
  public async Task EmailIsExplicitTenCharacterProofAndDoesNotRefreshSessionOrMfa()
  {
    var service = new FreshAccountProofServiceStub();
    var session = new ConfirmationSessionStore();
    var initial = session.Current;
    using var model = New(service, session);
    await model.InitializeAsync();
    model.Code = "abcdefgh23";
    Assert.Empty(service.Codes);
    Assert.False(model.Completion.IsCompleted);
    await model.SendEmailAsync();
    await model.SendEmailAsync();
    Assert.Equal(2, service.EmailRequests);
    Assert.True(model.HasSentEmail);

    await model.VerifyCodeAsync();

    Assert.Equal([(true, "ABCDEFGH23")], service.Codes);
    var proof = await model.Completion;
    Assert.Equal("proof-token", proof.Consume());
    Assert.Throws<OperationCanceledException>(() => proof.Consume());
    Assert.Same(initial, session.Current);
    Assert.Equal(0, session.RefreshCalls);
  }

  [Theory]
  [InlineData("123456")]
  [InlineData("ABCDEFGH01")]
  [InlineData("ABCDEFGH2!")]
  [InlineData("ABCDEFGHIß")]
  [InlineData(" ABCDEFGH23")]
  public async Task MalformedEmailCodeNeverRequestsVerification(string code)
  {
    var service = new FreshAccountProofServiceStub();
    using var model = New(service);
    await model.InitializeAsync();
    model.Code = code;
    await model.VerifyCodeAsync();
    Assert.Empty(service.Codes);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.ExtractedAuthMfaStepInvalidVerificationCode4ed23ad3), model.ErrorMessage);
  }

  [Theory]
  [InlineData("ABCDEFGH23", false)]
  [InlineData("１２３４５６", false)]
  [InlineData("12345", false)]
  [InlineData("1234567", false)]
  [InlineData("123456", true)]
  public async Task TotpStaysExactlySixAsciiDigits(string code, bool valid)
  {
    var service = new FreshAccountProofServiceStub();
    using var model = New(service);
    await model.InitializeAsync();
    model.SelectTotp();
    model.Code = code;
    await model.VerifyCodeAsync();
    if (valid) Assert.Equal([(false, "123456")], service.Codes);
    else Assert.Empty(service.Codes);
    Assert.Equal(valid, model.Completion.IsCompletedSuccessfully);
  }

  [Theory]
  [InlineData(HttpStatusCode.BadRequest)]
  [InlineData(HttpStatusCode.Unauthorized)]
  [InlineData(HttpStatusCode.Gone)]
  [InlineData(HttpStatusCode.Conflict)]
  [InlineData(HttpStatusCode.Forbidden)]
  [InlineData(HttpStatusCode.TooManyRequests)]
  public async Task RejectedExpiredReplayedOrQuotaProofDoesNotRetryOrResend(HttpStatusCode status)
  {
    var service = new FreshAccountProofServiceStub
    {
      Verification = () => Task.FromException<string>(new VouchaApiException(status, """{"message":"untrusted server text"}""")),
    };
    using var model = New(service);
    await model.InitializeAsync();
    model.Code = "ABCDEFGH23";
    await model.VerifyCodeAsync();
    Assert.Single(service.Codes);
    Assert.Equal(0, service.EmailRequests);
    Assert.False(model.Completion.IsCompleted);
    Assert.NotNull(model.ErrorMessage);
    Assert.DoesNotContain("untrusted", model.ErrorMessage, StringComparison.Ordinal);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("account-b")]
  public async Task LogoutOrAccountSwitchCancelsPendingVerification(string? account)
  {
    var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FreshAccountProofServiceStub { Verification = () => pending.Task };
    var session = new ConfirmationSessionStore();
    using var model = New(service, session);
    await model.InitializeAsync();
    model.Code = "ABCDEFGH23";
    var verify = model.VerifyCodeAsync();
    session.Change(account);
    pending.SetResult("late-proof");
    await verify;
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => model.Completion);
    Assert.Equal(0, session.RefreshCalls);
  }

  [Fact]
  public async Task DeviceOrSessionChangeAfterDialogSuccessInvalidatesOneUseProof()
  {
    var binding = "account/device/session-a";
    using var model = new FreshAccountConfirmationViewModel(new FreshAccountProofServiceStub(), () => binding,
        new ConfirmationSessionStore(), UiLocalization.English);
    await model.InitializeAsync();
    model.Code = "ABCDEFGH23";
    await model.VerifyCodeAsync();
    var proof = await model.Completion;
    binding = "account/device/session-b";
    Assert.Throws<OperationCanceledException>(() => proof.Consume());
  }

  [Fact]
  public async Task ExplicitCancellationAndConcurrentSubmitCannotProduceTwoProofs()
  {
    var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FreshAccountProofServiceStub { Verification = () => pending.Task };
    using var model = New(service);
    await model.InitializeAsync();
    model.Code = "ABCDEFGH23";
    var first = model.VerifyCodeAsync();
    await model.VerifyCodeAsync();
    model.Cancel();
    pending.SetResult("late");
    await first;
    Assert.Single(service.Codes);
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => model.Completion);
  }

  [Fact]
  public async Task DisposedDialogRejectsLateProofAndDisposalIsIdempotent()
  {
    var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FreshAccountProofServiceStub { Verification = () => pending.Task };
    var session = new ConfirmationSessionStore();
    var model = New(service, session);
    await model.InitializeAsync();
    model.Code = "ABCDEFGH23";
    var verify = model.VerifyCodeAsync();
    model.Dispose();
    model.Dispose();
    session.Change("other-account");
    pending.SetResult("late-proof");
    await verify;
    await model.VerifyCodeAsync();
    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => model.Completion);
    Assert.Single(service.Codes);
  }

  [Fact]
  public async Task SuccessfulProofSurvivesDialogCleanupButRemainsOneUseAndBoundToSession()
  {
    var model = New(new FreshAccountProofServiceStub());
    await model.InitializeAsync();
    model.Code = "ABCDEFGH23";
    await model.VerifyCodeAsync();
    var proof = await model.Completion;
    model.Dispose();
    Assert.Equal("proof-token", proof.Consume());
    Assert.Throws<OperationCanceledException>(() => proof.Consume());
  }

  [Fact]
  public async Task PasskeyOnlyDiscoverySupportsOwnedSelectionAndBoundedCursorPages()
  {
    var service = new FreshAccountProofServiceStub
    {
      Methods = new(HasEmail: false, HasPasskey: true, HasTotp: false, Providers: []),
      Page = after => after is null ? new([new("first", "First")], new("cursor-1", true, null))
          : new([new("first", "First"), new("second", "Second")], new(null, false, null)),
    };
    using var model = New(service);
    await model.InitializeAsync();
    Assert.False(model.HasCodeMethod);
    Assert.False(model.CanEmail);
    Assert.False(model.CanTotp);
    await model.LoadPasskeysAsync();
    await model.LoadPasskeysAsync();
    await model.LoadPasskeysAsync();
    Assert.Equal([null, "cursor-1"], service.Afters);
    Assert.Equal(2, model.Passkeys.Count);
    await model.VerifyPasskeyAsync("not-owned");
    Assert.Empty(service.PasskeyIds);
    await model.VerifyPasskeyAsync("second");
    Assert.Equal(["second"], service.PasskeyIds);
    Assert.Equal("proof-token", (await model.Completion).Consume());
  }

  [Theory]
  [InlineData("apple")]
  [InlineData("google")]
  [InlineData("facebook")]
  [InlineData("x")]
  [InlineData("github")]
  [InlineData("linkedin")]
  [InlineData("microsoft")]
  [InlineData("future-provider")]
  public async Task AdvertisedProviderMethodsHaveLocalizedLabelsAndRefreshWithoutVerifying(string provider)
  {
    var service = new FreshAccountProofServiceStub { Methods = new(false, false, false, [provider]) };
    using var model = New(service);
    await model.InitializeAsync();
    var row = Assert.Single(model.Providers);
    Assert.Equal(provider, row.Provider);
    Assert.False(string.IsNullOrWhiteSpace(row.Label));
    var notifications = new List<string?>();
    model.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
    model.OnUiLocaleChanged();
    Assert.Contains(nameof(model.Providers), notifications);
    Assert.Contains(nameof(model.VerifyLabel), notifications);
    Assert.Empty(service.Providers);
    Assert.False(model.Completion.IsCompleted);
  }

  [Fact]
  public async Task ProviderOnlyAccountOffersOnlyAdvertisedProvidersAndFailsClosedOnMismatch()
  {
    var service = new FreshAccountProofServiceStub
    {
      Methods = new(false, false, false, ["google"]),
      Verification = () => Task.FromException<string>(new VouchaApiException(HttpStatusCode.Forbidden, null)),
    };
    using var model = New(service);
    await model.InitializeAsync();
    Assert.False(model.HasCodeMethod);
    Assert.Equal("google", Assert.Single(model.Providers).Provider);
    await model.VerifyProviderAsync("github");
    Assert.Empty(service.Providers);
    await model.VerifyProviderAsync("google");
    Assert.Equal(["google"], service.Providers);
    Assert.False(model.Completion.IsCompleted);
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.ExtractedAuthMfaStepUnableToVerifyPleaseTryAgainB7979b96), model.ErrorMessage);
  }

  private static FreshAccountConfirmationViewModel New(FreshAccountProofServiceStub service, ConfirmationSessionStore? session = null)
  {
    session ??= new ConfirmationSessionStore();
    return new(service, () => session.Current.Identity?.Id, session, UiLocalization.English);
  }
}
