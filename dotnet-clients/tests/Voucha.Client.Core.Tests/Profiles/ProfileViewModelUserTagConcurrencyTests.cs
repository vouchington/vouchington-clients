using System.Net;
using System.Text;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed partial class ProfileViewModelSafetyTests
{
  [Fact]
  public async Task AdministratorRelationPolicyCanCreateUserTagVotesWithoutPublicVoteEligibility()
  {
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"));
    viewModel.SetCurrentViewer("administrator-1", "admin", canCastPublicVotes: false, canCreateUserTagVotes: true);

    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    Assert.True(viewModel.CanCreateUserTagVotes);
  }

  [Fact]
  public async Task ConcurrentUserTagVoteForSameRelationIsIgnored()
  {
    var handler = new DeferredUserTagVoteHandler();
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"));
    viewModel.SetCurrentViewer("user-1", "alice");
    viewModel.ConfigureUserTagClient(client);
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    var firstVote = viewModel.VoteUserTagAsync("relation-1", ElectionVoteChoice.Confirm, TestContext.Current.CancellationToken);
    await handler.VoteStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    await viewModel.VoteUserTagAsync("relation-1", ElectionVoteChoice.Dispute, TestContext.Current.CancellationToken);

    Assert.Equal(1, handler.VoteRequestCount);
    handler.ReleaseVote.TrySetResult();
    await firstVote;
    Assert.Equal(new ProfileUserTagRow("relation-1", "Bot", 3, 0, ElectionVoteChoice.Confirm), Assert.Single(viewModel.UserTags));
  }

  [Fact]
  public async Task RefreshingPublicProfileReloadsUserTags()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(UserTagsJson("Bot", "confirm")),
        new RecordedResponse(UserTagsJson("Spammer", "dispute")),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"));
    viewModel.SetCurrentViewer("user-1", "alice");
    viewModel.ConfigureUserTagClient(client);
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);
    await viewModel.LoadUserTagsAsync(TestContext.Current.CancellationToken);
    Assert.Equal("Bot", Assert.Single(viewModel.UserTags).Title);

    await viewModel.RefreshAsync(TestContext.Current.CancellationToken);

    Assert.Equal(new ProfileUserTagRow("relation-1", "Spammer", 3, 0, ElectionVoteChoice.Dispute), Assert.Single(viewModel.UserTags));
    Assert.Equal(2, handler.Requests.Count);
  }

  [Fact]
  public async Task UserTagContinuationAppendsRowsAndForwardsTheOpaqueCursor()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(UserTagPage("relation-1", "Bot", "next", true)),
        new RecordedResponse(UserTagPage("relation-2", "Helpful", null, false)),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"));
    viewModel.SetCurrentViewer("user-1", "alice");
    viewModel.ConfigureUserTagClient(client);
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);
    await viewModel.LoadUserTagsAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreUserTagsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["relation-1", "relation-2"], viewModel.UserTags.Select(row => row.Id));
    Assert.Contains("after=next", handler.Requests[^1].PathAndQuery, StringComparison.Ordinal);
    Assert.False(viewModel.HasMoreUserTags);
  }

  private static string UserTagsJson(string title, string vote) =>
      $$$$"""
        {"results":[{"id":"relation-1"}],"page_info":{"has_next_page":false},
        "entity_relations":{"relation-1":{"id":"relation-1","object_id":"user-tag-bot",
        "object_data":{"label":"{{{{title}}}}"},"votes_score_net":3,"votes_count_up":3,"votes_count_down":0}},
        "election_votes":{"relation-1":{"choice":"{{{{vote}}}}"}}}
        """;

  private static string UserTagPage(string id, string title, string? endCursor, bool hasNextPage) =>
      JsonSerializer.Serialize(new
      {
        results = new[] { new { id } },
        page_info = new { has_next_page = hasNextPage, end_cursor = endCursor },
        entity_relations = new Dictionary<string, object>
        {
          [id] = new { id, object_id = $"user-tag-{id}", object_data = new { label = title }, votes_score_net = 3, votes_count_up = 3, votes_count_down = 0 },
        },
        election_votes = new Dictionary<string, object> { [id] = new { choice = "confirm" } },
      });

  private sealed class DeferredUserTagVoteHandler : HttpMessageHandler
  {
    public TaskCompletionSource VoteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource ReleaseVote { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int VoteRequestCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      var isVote = request.RequestUri?.AbsolutePath.EndsWith("/vote", StringComparison.Ordinal) == true;
      if (isVote)
      {
        VoteRequestCount++;
        VoteStarted.TrySetResult();
        await ReleaseVote.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
      }

      const string tags = """
          {"results":[{"id":"relation-1"}],"page_info":{"has_next_page":false},
          "entity_relations":{"relation-1":{"id":"relation-1","object_id":"user-tag-bot",
          "object_data":{"label":"Bot"},"votes_score_net":3,"votes_count_up":3,"votes_count_down":0}},
          "election_votes":{"relation-1":{"choice":"confirm"}}}
          """;
      return new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(isVote ? "{}" : tags, Encoding.UTF8, "application/json"),
        RequestMessage = request,
      };
    }
  }
}
