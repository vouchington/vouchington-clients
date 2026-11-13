using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed class AccountChangeFailureMessageTests
{
  private static VouchaApiException Error(HttpStatusCode status, string? code) =>
      new(status, code is null ? "{}" : $$"""{"code":"{{code}}","message":"server text"}""");

  [Fact]
  public void DecodesStableCodesFromTheErrorBody()
  {
    Assert.True(Error(HttpStatusCode.BadRequest, "LAST_SIGN_IN_METHOD_REMOVAL_FORBIDDEN")
        .IsLastSignInMethodRemovalForbidden);
    Assert.True(Error(HttpStatusCode.Forbidden, "ADMINISTRATOR_MFA_REQUIRED").IsAdministratorMfaRequired);
    Assert.True(Error(HttpStatusCode.Unauthorized, "MFA_REAUTH_REQUIRED").IsMfaReauthRequired);
    Assert.False(Error(HttpStatusCode.Forbidden, "OTHER").IsMfaReauthRequired);
  }

  public static TheoryData<HttpStatusCode, string, AccountChangeContext, UiMessageKey> Cases => new()
  {
    { HttpStatusCode.BadRequest, "LAST_SIGN_IN_METHOD_REMOVAL_FORBIDDEN",
        AccountChangeContext.SignInMethodRemoval, UiMessageKey.SharedSignInMethodRemovalLastMethod },
    { HttpStatusCode.Forbidden, "ADMINISTRATOR_MFA_REQUIRED",
        AccountChangeContext.SignInMethodRemoval, UiMessageKey.SharedSignInMethodRemovalAdministratorMfa },
    { HttpStatusCode.Forbidden, "MFA_REAUTH_REQUIRED",
        AccountChangeContext.SignInMethodRemoval, UiMessageKey.SharedReauthConfirmationExpired },
    { HttpStatusCode.Unauthorized, "MFA_REAUTH_REQUIRED",
        AccountChangeContext.ProviderReAuth, UiMessageKey.SharedReauthConfirmationExpired },
    { HttpStatusCode.Gone, "MFA_REAUTH_REQUIRED",
        AccountChangeContext.ProviderReAuth, UiMessageKey.SharedReauthConfirmationExpired },
    { HttpStatusCode.Forbidden, "MFA_REAUTH_REQUIRED",
        AccountChangeContext.ProviderReAuth, UiMessageKey.SharedReauthConfirmationChooseAnotherMethod },
  };

  [Theory]
  [MemberData(nameof(Cases))]
  public void MapsCodesToMessages(
      HttpStatusCode status,
      string code,
      AccountChangeContext context,
      UiMessageKey expected) =>
      Assert.Equal(expected, AccountChangeFailureMessage.KeyFor(Error(status, code), context));

  [Fact]
  public void UnknownCodesAndNonApiErrorsFallBack()
  {
    Assert.Null(AccountChangeFailureMessage.KeyFor(
        Error(HttpStatusCode.BadRequest, "SOMETHING_NEW"), AccountChangeContext.SignInMethodRemoval));
    Assert.Null(AccountChangeFailureMessage.KeyFor(
        Error(HttpStatusCode.BadRequest, null), AccountChangeContext.ProviderReAuth));
    Assert.Null(AccountChangeFailureMessage.KeyFor(
        new InvalidOperationException("x"), AccountChangeContext.ProviderReAuth));
  }

  [Fact]
  public void ReAuthRecoveryReturnsToMethodSelection()
  {
    var model = new ReAuthConfirmationViewModel();
    Assert.Null(model.Message);

    model.RecordFailure(Error(HttpStatusCode.Gone, "MFA_REAUTH_REQUIRED"));
    Assert.Equal(ReAuthConfirmationState.Expired, model.State);
    Assert.NotNull(model.Message);

    model.RecordCancellation();
    Assert.Equal(ReAuthConfirmationState.Canceled, model.State);
    Assert.NotNull(model.Message);

    model.RecordFailure(Error(HttpStatusCode.Forbidden, "MFA_REAUTH_REQUIRED"));
    Assert.Equal(ReAuthConfirmationState.Rejected, model.State);
    Assert.NotNull(model.Message);

    model.ChooseAnotherMethod();
    Assert.Equal(ReAuthConfirmationState.SelectingMethod, model.State);
    Assert.Null(model.Message);
    Assert.NotNull(model.Prompt);

    model.BeginAttempt();
    Assert.Null(model.Prompt);
  }

  [Fact]
  public void UnrecognizedFailuresKeepGenericPresentation()
  {
    var model = new ReAuthConfirmationViewModel();
    var error = Error(HttpStatusCode.BadRequest, "SOMETHING_NEW");

    model.RecordFailure(error);

    Assert.Same(error, model.UnrecognizedError);
    Assert.Equal(ReAuthConfirmationState.SelectingMethod, model.State);
    Assert.Throws<ArgumentNullException>(() => model.RecordFailure(null!));
  }
}
