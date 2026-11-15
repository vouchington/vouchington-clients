using System.Net;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.ModerationIntegrity;

internal sealed class PenaltyReviewTestService : IIntegrityPenaltyReviewService
{
  public Queue<Func<Task<ReportIntegrityPenaltiesResponse>>> ReportDue { get; } = new();
  public Queue<Func<Task<VoteIntegrityPenaltiesResponse>>> VoteDue { get; } = new();
  public Queue<Func<Task<ReportAbusePenaltyResponse>>> ReportReviews { get; } = new();
  public Queue<Func<Task<VoteWeightPenaltyResponse>>> VoteReviews { get; } = new();
  public List<string?> DueCursors { get; } = [];
  public List<string> ReviewCalls { get; } = [];

  public Task<ReportIntegrityPenaltiesResponse> FetchReportPenaltyReviewsDueAsync(
      string? after = null, int limit = 25, CancellationToken cancellationToken = default)
  {
    DueCursors.Add(after);
    Assert.Equal(25, limit);
    return ReportDue.Dequeue()();
  }

  public Task<ReportAbusePenaltyResponse> ReviewReportPenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default)
  {
    ReviewCalls.Add(penaltyId);
    return ReportReviews.Dequeue()();
  }

  public Task<VoteIntegrityPenaltiesResponse> FetchVotePenaltyReviewsDueAsync(
      string? after = null, int limit = 25, CancellationToken cancellationToken = default)
  {
    DueCursors.Add(after);
    Assert.Equal(25, limit);
    return VoteDue.Dequeue()();
  }

  public Task<VoteWeightPenaltyResponse> ReviewVotePenaltyAsync(
      string penaltyId, CancellationToken cancellationToken = default)
  {
    ReviewCalls.Add(penaltyId);
    return VoteReviews.Dequeue()();
  }
}

internal static class PenaltyReviewFixtures
{
  public static readonly NavigationViewer Admin = new(true, ["administrator"]);

  public static T Load<T>(string id) =>
      JsonSerializer.Deserialize<T>(ApiFixtureLoader.LoadResponse(id), VouchaApiJson.Options)!;

  public static ReportIntegrityPenaltiesResponse ReportDue(string suffix = "") =>
      Load<ReportIntegrityPenaltiesResponse>(
          "native.moderation.report-integrity.penalties.review-due" + suffix);

  public static VoteIntegrityPenaltiesResponse VoteDue(string suffix = "") =>
      Load<VoteIntegrityPenaltiesResponse>(
          "native.moderation.vote-integrity.penalties.review-due" + suffix);

  public static ReportAbusePenaltyResponse ReportReviewed() =>
      Load<ReportAbusePenaltyResponse>("native.moderation.report-integrity.penalties.review");

  public static VoteWeightPenaltyResponse VoteReviewed() =>
      Load<VoteWeightPenaltyResponse>("native.moderation.vote-integrity.penalties.review");

  public static VouchaApiException Http(HttpStatusCode status) => new(status, null);
}
