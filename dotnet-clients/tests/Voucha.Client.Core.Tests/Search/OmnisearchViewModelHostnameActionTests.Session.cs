using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Search;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Search;

public sealed partial class OmnisearchViewModelHostnameActionTests
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task MissingOrSignedOutSessionCannotClearHydratedHostnameVote(bool signedOut)
  {
    var handler = new RecordingHandler(HostnameDetailWithCurrentVoteJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    using var viewModel = new OmnisearchViewModel(client, viewerProvider: ViewerProvider([]),
        sessionStore: signedOut ? new AnonymousSessionStore() : null);
    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanVoteSelectedHostname);
    Assert.False(viewModel.CanClearSelectedHostnameVote);
    await viewModel.VoteSelectedHostnameAsync(null, TestContext.Current.CancellationToken);
    Assert.Single(handler.Requests);
  }

  private sealed class AnonymousSessionStore : ISessionStore
  {
    public event EventHandler<SessionChangedEventArgs>? SessionChanged { add { } remove { } }
    public SessionSnapshot Current => SessionSnapshot.Anonymous;
    public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SignOutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
  }

}
