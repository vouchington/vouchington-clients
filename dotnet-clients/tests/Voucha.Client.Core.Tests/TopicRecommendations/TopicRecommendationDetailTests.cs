using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Tests.Api;
using Voucha.Client.Core.TopicRecommendations;
using Xunit;

namespace Voucha.Client.Core.Tests.TopicRecommendations;

public sealed class TopicRecommendationDetailTests
{
  private const string FixtureId = "native.topic-recommendation.detail.default";
  private const string RecommendationId = "recommendation 1";

  [Fact]
  public async Task ViewModelExposesVoteCapabilitiesFromBallotAndVotingState()
  {
    var pendingVote = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new StubService { PendingVote = pendingVote };
    var viewModel = new TopicRecommendationDetailViewModel(service, "recommendation-1");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.CanCastVote);
    Assert.True(viewModel.CanClearVote);

    var vote = viewModel.VoteAsync(ElectionVoteChoice.Oppose, TestContext.Current.CancellationToken);
    await service.VoteStarted.Task;

    Assert.False(viewModel.CanCastVote);
    Assert.False(viewModel.CanClearVote);

    pendingVote.SetResult();
    await vote;

    Assert.True(viewModel.CanCastVote);
    Assert.True(viewModel.CanClearVote);
  }

  [Fact]
  public async Task ApiServiceUsesEncodedRecommendationEndpointAndFixture()
  {
    var handler = new RecordingHandler(ApiFixtureLoader.LoadResponse(FixtureId));
    ITopicRecommendationDetailService service = new ApiTopicRecommendationDetailService(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));

    var response = await service.FetchAsync(RecommendationId, TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/topic-recommendations/recommendation%201", handler.PathAndQuery);
    Assert.Equal("Native recommendation", response.Post.Title);
    Assert.Equal("<p>Recommendation body</p>", response.Html);
    Assert.Equal(1, response.PostElection?.VotesCountUp);
    Assert.Equal(ElectionVoteChoice.Support, response.ElectionVote?.Choice);
  }

  [Fact]
  public async Task ViewModelPresentsDetailAndRetriesFailures()
  {
    var service = new StubService { Error = new HttpRequestException("Unavailable", null, HttpStatusCode.ServiceUnavailable) };
    var viewModel = new TopicRecommendationDetailViewModel(service, "recommendation-1");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal("Unavailable", viewModel.ErrorMessage);

    service.Error = null;
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Native recommendation", viewModel.Title);
    Assert.Equal("<p>Recommendation body</p>", viewModel.Html);
    Assert.Equal("Recommendation body", viewModel.Markdown);
    Assert.Equal(1, viewModel.VoteCountUp);
    Assert.Equal(ElectionVoteChoice.Support, viewModel.CurrentVoteChoice);
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task ViewModelCancellationDoesNotPresentAnError()
  {
    var service = new StubService { Error = new OperationCanceledException() };
    var viewModel = new TopicRecommendationDetailViewModel(service, "recommendation-1");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Null(viewModel.ErrorMessage);
    Assert.False(viewModel.HasError);
    Assert.False(viewModel.IsLoading);
  }

  [Fact]
  public async Task ViewModelOptimisticallyChangesAndClearsRecommendationVotes()
  {
    var service = new StubService();
    var viewModel = new TopicRecommendationDetailViewModel(service, "recommendation-1");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.VoteAsync(ElectionVoteChoice.Oppose, TestContext.Current.CancellationToken);
    Assert.Equal(ElectionVoteChoice.Oppose, viewModel.CurrentVoteChoice);
    Assert.Equal(0, viewModel.VoteCountUp);
    Assert.Equal(1, viewModel.VoteCountDown);

    await viewModel.VoteAsync(null, TestContext.Current.CancellationToken);
    Assert.Null(viewModel.CurrentVoteChoice);
    Assert.Equal(0, viewModel.VoteCountDown);
    Assert.Equal([ElectionVoteChoice.Oppose], service.SubmittedChoices);
    Assert.Equal(1, service.ClearCount);
  }

  [Fact]
  public async Task ViewModelRestoresRecommendationVoteAfterMutationFailure()
  {
    var service = new StubService { VoteError = new InvalidOperationException("Vote failed") };
    var viewModel = new TopicRecommendationDetailViewModel(service, "recommendation-1");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.VoteAsync(ElectionVoteChoice.Oppose, TestContext.Current.CancellationToken);

    Assert.Equal(ElectionVoteChoice.Support, viewModel.CurrentVoteChoice);
    Assert.Equal(1, viewModel.VoteCountUp);
    Assert.Equal(0, viewModel.VoteCountDown);
    Assert.Equal("Vote failed", viewModel.ErrorMessage);
    Assert.False(viewModel.IsVoting);
  }

  [Fact]
  public async Task ViewModelRestoresRecommendationVoteAfterCancellation()
  {
    var service = new StubService { VoteError = new OperationCanceledException() };
    var viewModel = new TopicRecommendationDetailViewModel(service, "recommendation-1");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.VoteAsync(ElectionVoteChoice.Oppose, TestContext.Current.CancellationToken);

    Assert.Equal(ElectionVoteChoice.Support, viewModel.CurrentVoteChoice);
    Assert.Null(viewModel.ErrorMessage);
    Assert.False(viewModel.IsVoting);
  }

  [Fact]
  public async Task ViewModelStoresEmailVerificationRecoveryAndRestoresVote()
  {
    var service = new StubService
    {
      VoteError = new VouchaApiException(
          HttpStatusCode.Forbidden,
          """{ "code": "EMAIL_VERIFICATION_REQUIRED", "message": "Verify email" }"""),
    };
    var viewModel = new TopicRecommendationDetailViewModel(service, "recommendation-1");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.VoteAsync(ElectionVoteChoice.Oppose, TestContext.Current.CancellationToken);

    Assert.Equal(ElectionVoteChoice.Support, viewModel.CurrentVoteChoice);
    Assert.Equal("Voucha API request failed with HTTP 403.", viewModel.ErrorMessage);
    Assert.True(await viewModel.EmailVerificationGate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
    Assert.False(viewModel.IsVoting);
  }

  [Fact]
  public async Task ViewModelDoesNotVoteBeforeLoadingDetail()
  {
    var service = new StubService();
    var viewModel = new TopicRecommendationDetailViewModel(service, "recommendation-1");

    await viewModel.VoteAsync(ElectionVoteChoice.Oppose, TestContext.Current.CancellationToken);

    Assert.Empty(service.SubmittedChoices);
    Assert.Equal(0, service.ClearCount);
    Assert.False(viewModel.IsVoting);
  }

  private sealed class StubService : ITopicRecommendationDetailService
  {
    public Exception? Error { get; set; }
    public Exception? VoteError { get; set; }
    public TaskCompletionSource? PendingVote { get; init; }
    public TaskCompletionSource VoteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public List<ElectionVoteChoice> SubmittedChoices { get; } = [];
    public int ClearCount { get; private set; }

    public Task<PostResponse> FetchAsync(string recommendationId, CancellationToken cancellationToken = default)
    {
      if (Error is not null) return Task.FromException<PostResponse>(Error);
      return Task.FromResult(new PostResponse(
          new Post(recommendationId, "topic_recommendation", "Native recommendation", "Recommendation body", "user-1"),
          "<p>Recommendation body</p>",
          PostElection: new PostElection(null, recommendationId, 1, 1, 0),
          ElectionVote: new ElectionVote("election_vote", recommendationId, "user-1", ElectionVoteChoice.Support, DateTimeOffset.UnixEpoch)));
    }

    public Task VoteAsync(string recommendationId, ElectionVoteChoice choice, CancellationToken cancellationToken = default)
    {
      SubmittedChoices.Add(choice);
      VoteStarted.TrySetResult();
      if (PendingVote is not null) return PendingVote.Task;
      return VoteError is null ? Task.CompletedTask : Task.FromException(VoteError);
    }

    public Task ClearVoteAsync(string recommendationId, CancellationToken cancellationToken = default)
    {
      ClearCount++;
      return VoteError is null ? Task.CompletedTask : Task.FromException(VoteError);
    }
  }
}
