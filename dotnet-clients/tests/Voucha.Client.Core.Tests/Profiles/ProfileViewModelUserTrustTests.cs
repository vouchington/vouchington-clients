using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Profiles;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Profiles;

public sealed partial class ProfileViewModelSafetyTests
{
  [Fact]
  public async Task LoadPublicAsyncLoadsUserTrustCountsAndExistingChoice()
  {
    var handler = new RecordingHandler(UserTrustContextJson(ElectionVoteChoice.Like));
    var viewModel = NewUserTrustViewModel(handler);

    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal("/api/v1/users/user-2/vouch-context", handler.PathAndQuery);
    Assert.Equal(4, viewModel.PositiveSignalsFromFollowingCount);
    Assert.Equal(2, viewModel.NegativeSignalsFromFollowingCount);
    Assert.Equal("4 positive signals from people you follow", viewModel.LocalizedPositiveSignalsFromFollowing);
    Assert.Equal("2 negative signals from people you follow", viewModel.LocalizedNegativeSignalsFromFollowing);
    Assert.Equal(ElectionVoteChoice.Like, viewModel.UserTrustChoice);
    Assert.False(viewModel.CanClearUserTrustVote);
  }

  [Fact]
  public async Task LoadPublicAsyncClearsUserTrustStateWhenContextRequestFails()
  {
    var handler = new RecordingHandler("{}", HttpStatusCode.InternalServerError);
    var viewModel = NewUserTrustViewModel(handler);

    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    Assert.Equal(0, viewModel.PositiveSignalsFromFollowingCount);
    Assert.Equal(0, viewModel.NegativeSignalsFromFollowingCount);
    Assert.Null(viewModel.UserTrustChoice);
    Assert.False(viewModel.CanClearUserTrustVote);
  }

  [Fact]
  public async Task FollowReloadsServerHydratedTrustBallot()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(UserTrustContextJson(null)),
        new RecordedResponse(UserTrustContextJson(ElectionVoteChoice.Like)),
      ]);
    var viewModel = NewUserTrustViewModel(handler, safety: new RecordingProfileSafetyService());
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.ToggleFollowUserAsync(TestContext.Current.CancellationToken);

    Assert.Equal(ElectionVoteChoice.Like, viewModel.UserTrustChoice);
    Assert.False(viewModel.CanClearUserTrustVote);
    Assert.Equal(2, handler.Requests.Count);
    Assert.All(handler.Requests, request => Assert.Equal("/api/v1/users/user-2/vouch-context", request.PathAndQuery));
  }

  [Fact]
  public async Task VoteUserTrustAsyncSubmitsSemanticChoiceAndClearsExistingVote()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(UserTrustContextJson(ElectionVoteChoice.Like)),
        new RecordedResponse(string.Empty, HttpStatusCode.NoContent),
        new RecordedResponse(string.Empty, HttpStatusCode.NoContent),
      ]);
    var viewModel = NewUserTrustViewModel(handler);
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.VoteUserTrustAsync(ElectionVoteChoice.Disavow, TestContext.Current.CancellationToken);
    Assert.Equal(ElectionVoteChoice.Disavow, viewModel.UserTrustChoice);
    Assert.False(viewModel.IsVotingUserTrust);
    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal(HttpMethod.Put, handler.Requests[1].Method);
    Assert.Equal("/api/v1/users/user-2/vouch-vote", handler.Requests[1].PathAndQuery);
    Assert.Contains("\"choice\":\"disavow\"", handler.Requests[1].Body!, StringComparison.Ordinal);

    await viewModel.VoteUserTrustAsync(ElectionVoteChoice.Neutral, TestContext.Current.CancellationToken);
    Assert.Equal(ElectionVoteChoice.Neutral, viewModel.UserTrustChoice);
    Assert.False(viewModel.IsVotingUserTrust);
    Assert.Equal(HttpMethod.Put, handler.Requests[2].Method);
    Assert.Equal("/api/v1/users/user-2/vouch-vote", handler.Requests[2].PathAndQuery);
    Assert.Contains("\"choice\":\"neutral\"", handler.Requests[2].Body!, StringComparison.Ordinal);
  }

  [Fact]
  public async Task VoteUserTrustAsyncRestoresExistingChoiceAfterRequestFailure()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(UserTrustContextJson(ElectionVoteChoice.Vouch)),
        new RecordedResponse("vote failed", HttpStatusCode.InternalServerError),
      ]);
    var safety = FollowingUnmutedSafety();
    var viewModel = NewUserTrustViewModel(handler, safety: safety);
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.VoteUserTrustAsync(ElectionVoteChoice.Disavow, TestContext.Current.CancellationToken);

    Assert.Equal(ElectionVoteChoice.Vouch, viewModel.UserTrustChoice);
    Assert.Equal("Voucha API request failed with HTTP 500.", viewModel.ErrorMessage);
    Assert.False(viewModel.IsVotingUserTrust);
    Assert.True(viewModel.IsFollowingUser);
    Assert.False(viewModel.IsMutedUser);
  }

  [Fact]
  public async Task DisavowUserTrustVoteAppliesFollowAndMuteSideEffectsAfterSuccess()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(UserTrustContextJson(null)),
        new RecordedResponse(string.Empty, HttpStatusCode.NoContent),
      ]);
    var viewModel = NewUserTrustViewModel(handler, safety: FollowingUnmutedSafety());
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.VoteUserTrustAsync(ElectionVoteChoice.Disavow, TestContext.Current.CancellationToken);

    Assert.Equal(ElectionVoteChoice.Disavow, viewModel.UserTrustChoice);
    Assert.False(viewModel.IsFollowingUser);
    Assert.True(viewModel.IsMutedUser);
  }

  [Fact]
  public async Task VoteUserTrustAsyncRestoresChoiceAndRequestsVerificationRecovery()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(UserTrustContextJson(ElectionVoteChoice.Vouch)),
        new RecordedResponse("{\"code\":\"EMAIL_VERIFICATION_REQUIRED\"}", HttpStatusCode.Forbidden),
      ]);
    var viewModel = NewUserTrustViewModel(handler, safety: FollowingUnmutedSafety());
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.VoteUserTrustAsync(ElectionVoteChoice.Disavow, TestContext.Current.CancellationToken);

    Assert.Equal(ElectionVoteChoice.Vouch, viewModel.UserTrustChoice);
    Assert.False(viewModel.IsVotingUserTrust);
    Assert.True(viewModel.IsFollowingUser);
    Assert.False(viewModel.IsMutedUser);
    Assert.True(await viewModel.EmailVerificationGate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
    Assert.False(await viewModel.EmailVerificationGate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task ClearUserTrustVoteAsyncRestoresChoiceAndRequestsVerificationRecovery()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(UserTrustContextJson(ElectionVoteChoice.Neutral)),
        new RecordedResponse("{\"code\":\"EMAIL_VERIFICATION_REQUIRED\"}", HttpStatusCode.Forbidden),
      ]);
    var viewModel = NewUserTrustViewModel(handler, canCastPublicVotes: false);
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.VoteUserTrustAsync(null, TestContext.Current.CancellationToken);

    Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
    Assert.Equal(ElectionVoteChoice.Neutral, viewModel.UserTrustChoice);
    Assert.False(viewModel.IsVotingUserTrust);
    Assert.True(await viewModel.EmailVerificationGate.ConsumeRecoveryRequestAsync(
        _ => Task.CompletedTask,
        TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task ClearUserTrustVoteDoesNotApplyDisavowFollowAndMuteSideEffects()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(UserTrustContextJson(ElectionVoteChoice.Disavow)),
        new RecordedResponse(string.Empty, HttpStatusCode.NoContent),
      ]);
    var viewModel = NewUserTrustViewModel(handler, safety: FollowingUnmutedSafety());
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    await viewModel.VoteUserTrustAsync(ElectionVoteChoice.Neutral, TestContext.Current.CancellationToken);

    Assert.Equal(ElectionVoteChoice.Neutral, viewModel.UserTrustChoice);
    Assert.True(viewModel.IsFollowingUser);
    Assert.False(viewModel.IsMutedUser);
  }

  [Fact]
  public async Task VoteUserTrustAsyncAllowsClearForOfficialViewerButNotNewVotes()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(UserTrustContextJson(ElectionVoteChoice.Neutral)),
        new RecordedResponse(string.Empty, HttpStatusCode.NoContent),
      ]);
    var viewModel = NewUserTrustViewModel(handler, canCastPublicVotes: false);
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanCreateUserTrustVote);
    Assert.True(viewModel.CanClearUserTrustVote);
    await viewModel.VoteUserTrustAsync(ElectionVoteChoice.Vouch, TestContext.Current.CancellationToken);
    Assert.Single(handler.Requests);

    await viewModel.VoteUserTrustAsync(null, TestContext.Current.CancellationToken);
    Assert.Equal(2, handler.Requests.Count);
    Assert.Equal(HttpMethod.Delete, handler.Requests[1].Method);
    Assert.Null(viewModel.UserTrustChoice);
  }

  [Fact]
  public async Task VoteUserTrustAsyncRejectsNonSentimentChoicesBeforeSubmitting()
  {
    var handler = new RecordingHandler(UserTrustContextJson(null));
    var viewModel = NewUserTrustViewModel(handler);
    await viewModel.LoadPublicAsync("bob", cancellationToken: TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
        () => viewModel.VoteUserTrustAsync(ElectionVoteChoice.Support, TestContext.Current.CancellationToken));

    Assert.Single(handler.Requests);
    Assert.Null(viewModel.UserTrustChoice);
    Assert.False(viewModel.IsVotingUserTrust);
  }

  private static ProfileViewModel NewUserTrustViewModel(
      RecordingHandler handler,
      bool canCastPublicVotes = true,
      RecordingProfileSafetyService? safety = null)
  {
    var viewModel = NewViewModel(new User("user-2", "bob", "Hello"), safety);
    viewModel.ConfigureUserTrustClient(
        new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));
    viewModel.SetCurrentViewer("user-1", "alice", canCastPublicVotes);
    return viewModel;
  }

  private static RecordingProfileSafetyService FollowingUnmutedSafety() => new()
  {
    Bookmarks = new BookmarkPredicates(Follow: true),
  };

  private static string UserTrustContextJson(ElectionVoteChoice? choice) => $$"""
      {
        "positive_by_following": { "total": 4, "users": [] },
        "negative_by_following": { "total": 2, "users": [] },
        "election_vote": {{ElectionVoteJson(choice)}}
      }
      """;

  private static string ElectionVoteJson(ElectionVoteChoice? choice) => choice is { } selected
      ? $$"""{ "__entity_type": "election_vote", "entity_id": "user-2", "user_id": "user-1", "choice": "{{selected.ToString().ToLowerInvariant()}}", "created_at": "2026-01-01T00:00:00Z" }"""
      : "null";
}
