using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Search;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Search;

public sealed partial class OmnisearchViewModelTests
{
  [Fact]
  public async Task ViewerChangeDisablesLoadedUrlCrawlTrigger()
  {
    var viewerProvider = new MutableNavigationViewerProvider();
    viewerProvider.SetViewer(new NavigationViewer(true, ["administrator"]));
    var handler = new RecordingHandler(AdminUrlDetailJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: viewerProvider);

    await viewModel.LoadUrlDetailAsync("url-1", TestContext.Current.CancellationToken);
    viewerProvider.SetViewer(NavigationViewer.Anonymous);
    await viewModel.TriggerSelectedUrlCrawlAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasSelectedUrl);
    Assert.False(viewModel.CanTriggerSelectedUrlCrawl);
    Assert.Single(handler.Requests);
  }

  [Theory]
  [InlineData(null, "administrator", true)]
  [InlineData(null, "investor", true)]
  [InlineData(AccountType.Official, null, false)]
  [InlineData(AccountType.System, null, false)]
  [InlineData(AccountType.AiAgent, null, false)]
  public async Task HostnameVotingUsesAccountTypeInsteadOfRoles(AccountType? accountType, string? role, bool canVote)
  {
    var roles = role is null ? Array.Empty<string>() : [role];
    var viewerProvider = new MutableNavigationViewerProvider();
    viewerProvider.SetViewer(new NavigationViewer(true, roles));
    var sessionStore = new VotingSessionStore();
    sessionStore.SetSession(new(new User("user-1", "alice", Roles: roles, AccountType: accountType)));
    var handler = new RecordingHandler([new RecordedResponse(HostnameDetailJson), new RecordedResponse("{}")]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    using var viewModel = new OmnisearchViewModel(client, viewerProvider: viewerProvider, sessionStore: sessionStore);

    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);
    Assert.Equal(canVote, viewModel.CanVoteSelectedHostname);
    await viewModel.VoteSelectedHostnameAsync(ElectionVoteChoice.Like, TestContext.Current.CancellationToken);

    Assert.Equal(canVote ? 2 : 1, handler.Requests.Count);
    if (canVote) Assert.Equal("/api/v1/hostnames/hostname-1/vote", handler.Requests[1].PathAndQuery);
  }

  [Fact]
  public async Task SessionChangesRefreshHostnameVotingAndDisposeUnsubscribes()
  {
    var sessionStore = new VotingSessionStore();
    var handler = new RecordingHandler(HostnameDetailJson);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new OmnisearchViewModel(client, viewerProvider: AuthenticatedViewerProvider(), sessionStore: sessionStore);
    await viewModel.LoadDomainDetailAsync("hostname-1", TestContext.Current.CancellationToken);
    var changes = new List<string?>();
    viewModel.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

    sessionStore.SetSession(new(new User("user-1", "alice", AccountType: AccountType.System)));
    Assert.False(viewModel.CanVoteSelectedHostname);
    Assert.Contains(nameof(viewModel.CanVoteSelectedHostname), changes);
    sessionStore.SetSession(new(new User("user-1", "alice", Roles: ["investor"])));
    Assert.True(viewModel.CanVoteSelectedHostname);
    await sessionStore.SignOutAsync(TestContext.Current.CancellationToken);
    Assert.False(viewModel.CanVoteSelectedHostname);
    viewModel.Dispose();
    changes.Clear();
    sessionStore.SetSession(new(new User("user-1", "alice")));
    Assert.Empty(changes);
  }

  private const string AdminUrlDetailJson = """
      {
        "url": {
          "__entity_type": "url",
          "id": "url-1",
          "url": "https://example.com/native",
          "pathname": "/native",
          "hostname": {
            "__entity_type": "hostname",
            "id": "hostname-1",
            "hostname": "example.com"
          }
        },
        "latest_crawl": null,
        "can_view_latest_crawl": true,
        "can_view_crawl_history": true,
        "can_trigger_crawl": true,
        "url_type": "web",
        "rss_feed_id": null
      }
      """;
}
