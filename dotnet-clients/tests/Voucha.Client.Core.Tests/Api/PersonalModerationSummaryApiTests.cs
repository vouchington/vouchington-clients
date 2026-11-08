using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class PersonalModerationSummaryApiTests
{
  [Theory]
  [InlineData("native.moderation.appeals.default", true)]
  [InlineData("native.moderation.appeals.member.default", false)]
  public async Task PersonalAppealReadAcceptsTheCallersPublishedSummaryProjection(string fixture, bool staff)
  {
    using var handler = new RecordingHandler(ApiFixtureLoader.LoadResponse(fixture));
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var client = new VouchaApiClient(http);

    var response = await client.FetchMemberAppealsAsync(ModerationAppealStatus.Pending, cancellationToken: TestContext.Current.CancellationToken);

    var row = Assert.Single(response.Appeals);
    Assert.Equal("/api/v1/appeals?limit=25&mine=true&status=pending", handler.PathAndQuery);
    Assert.Equal("post-1", row.PostId);
    Assert.IsType<ModerationAppealPostRemovalContext>(row.TargetContext);
    Assert.Equal(staff ? ModerationAppealAction.Reduce : null, row.RecommendedAction);
    Assert.Equal(staff ? true : null, row.HasAiDraft);
    Assert.DoesNotContain("ai_draft_requested_run_id", JsonSerializer.Serialize(response, VouchaApiJson.Options), StringComparison.Ordinal);
  }

  [Theory]
  [InlineData("native.moderation.disputes.default", true)]
  [InlineData("native.moderation.disputes.member.default", false)]
  public async Task PersonalDisputeReadAcceptsTheCallersPublishedSummaryProjection(string fixture, bool staff)
  {
    using var handler = new RecordingHandler(ApiFixtureLoader.LoadResponse(fixture));
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var client = new VouchaApiClient(http);

    var response = await client.FetchMemberDisputesAsync("pending", cancellationToken: TestContext.Current.CancellationToken);

    var row = Assert.Single(response.Disputes);
    Assert.Equal("/api/v1/disputes?limit=25&mine=true&status=pending", handler.PathAndQuery);
    Assert.Equal("A disputed review", row.PostLabel);
    Assert.Equal(staff ? "remove" : null, row.RecommendedAction);
    Assert.Equal(staff ? false : null, row.HasAiDraft);
    Assert.DoesNotContain("ai_draft_requested_run_id", JsonSerializer.Serialize(response, VouchaApiJson.Options), StringComparison.Ordinal);
  }
}
