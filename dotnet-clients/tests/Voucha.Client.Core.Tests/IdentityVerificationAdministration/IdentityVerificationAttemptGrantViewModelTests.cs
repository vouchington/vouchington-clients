using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.IdentityVerificationAdministration;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.IdentityVerificationAdministration;

public sealed class IdentityVerificationAttemptGrantViewModelTests
{
  [Fact]
  public async Task BlankNoteDoesNotPost()
  {
    var service = new Service();
    var model = new IdentityVerificationAttemptGrantViewModel(service);
    model.Configure("alice", true);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    model.Note = "   ";

    Assert.False(await model.GrantAsync(TestContext.Current.CancellationToken));
    Assert.Equal(0, service.GrantCalls);
    Assert.Equal(UiMessageKey.NativeSwiftIdentityVerificationValidation, model.Submission?.Key);
  }

  [Fact]
  public async Task GrantSuccessClearsNote()
  {
    var service = new Service();
    var model = new IdentityVerificationAttemptGrantViewModel(service);
    model.Configure("alice", true);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    model.Note = "Terminal Free attempt reviewed.";

    Assert.True(await model.GrantAsync(TestContext.Current.CancellationToken));
    Assert.Equal(1, service.GrantCalls);
    Assert.Equal(string.Empty, model.Note);
    Assert.Equal(UiMessageKey.NativeSwiftIdentityVerificationSuccess, model.Submission?.Key);
  }

  [Fact]
  public async Task GrantShows409AsUserContent()
  {
    var service = new Service { GrantError = new VouchaApiException(HttpStatusCode.Conflict, "{\"message\":\"Support retries require a completed terminal Free identity verification attempt.\"}") };
    var model = new IdentityVerificationAttemptGrantViewModel(service);
    model.Configure("alice", true);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    model.Note = "Please retry.";

    Assert.False(await model.GrantAsync(TestContext.Current.CancellationToken));
    Assert.Equal("Please retry.", model.Note);
    Assert.Equal("Support retries require a completed terminal Free identity verification attempt.", model.Submission?.VerbatimValue);
  }

  [Fact]
  public async Task NonAdministratorDoesNotLoadOrSubmit()
  {
    var service = new Service();
    var model = new IdentityVerificationAttemptGrantViewModel(service);
    model.Configure("alice", false);

    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.False(await model.GrantAsync(TestContext.Current.CancellationToken));
    Assert.Equal(0, service.FetchCalls);
    Assert.Equal(0, service.GrantCalls);
  }

  private sealed class Service : IIdentityVerificationAdministrationService
  {
    public int FetchCalls { get; private set; }
    public int GrantCalls { get; private set; }
    public Exception? GrantError { get; init; }

    public Task<UserResponse> FetchUserAsync(string idOrSlug, CancellationToken cancellationToken = default)
    {
      FetchCalls++;
      return Task.FromResult(new UserResponse(new User("user-1", "alice")));
    }

    public Task<GrantIdentityVerificationAttemptResponse> GrantAsync(
        string userId,
        string note,
        CancellationToken cancellationToken = default)
    {
      GrantCalls++;
      if (GrantError is not null) return Task.FromException<GrantIdentityVerificationAttemptResponse>(GrantError);
      return Task.FromResult(new GrantIdentityVerificationAttemptResponse(true));
    }
  }
}
