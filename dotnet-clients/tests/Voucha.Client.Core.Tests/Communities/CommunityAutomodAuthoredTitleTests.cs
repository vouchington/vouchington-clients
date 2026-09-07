using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Tests.Api;
using Xunit;
using static Voucha.Client.Core.Tests.Communities.CommunityDetailViewModelCoverageFixtures;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityAutomodAuthoredTitleTests
{
  [Fact]
  public async Task RecentActionCarriesAuthoredTitleLanguageIntoTheSummary()
  {
    var service = new ScriptedCommunitiesService();
    SeedLoadResponseSet(service, CreateDetailResponse(true, false, membershipRole: "moderator"));
    var response = JsonSerializer.Deserialize<CommunityAutomodActionsResponse>(
        ApiFixtureLoader.LoadResponse("web.communities.automod-recent-actions.default"),
        VouchaApiJson.Options)!;
    service.AutomodRecentActionsResponses.Enqueue(response with
    {
      AutomodActions = [response.AutomodActions[0] with
      {
        AuthoredTitle = "عنوان",
        DeclaredLanguage = "ar",
        LinguaRsDetectedLanguage = null,
      }],
    });
    var viewModel = new CommunityDetailViewModel(
        service,
        new TestSessionStore(SessionSnapshotForTests.Authenticated));
    await viewModel.LoadAsync("community-1", TestContext.Current.CancellationToken);

    await viewModel.LoadAutomodRecentActionsAsync(
        cancellationToken: TestContext.Current.CancellationToken);

    var summary = Assert.Single(viewModel.Moderation);
    Assert.Equal("عنوان", summary.Title);
    Assert.Equal("RightToLeft", summary.TitleFlowDirection);
  }
}
