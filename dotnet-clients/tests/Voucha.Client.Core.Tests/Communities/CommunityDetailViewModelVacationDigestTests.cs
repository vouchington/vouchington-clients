using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Communities;
using Xunit;
using static Voucha.Client.Core.Tests.Communities.CommunityDetailViewModelCoverageFixtures;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityDetailViewModelVacationDigestTests
{
  [Fact]
  public async Task HydratedVacationDigestSuppressionCanBeDisabledOnFirstToggle()
  {
    var service = new ScriptedCommunitiesService();
    service.ModeratorVacationResponses.Enqueue(new(null, true));
    service.ModeratorVacationResponses.Enqueue(new(null, false));
    SeedLoadResponseSet(service, CreateDetailResponse(hasMembership: true, hasPendingApplication: false));
    var viewModel = new CommunityDetailViewModel(service, new TestSessionStore(SessionSnapshotForTests.Authenticated));

    await viewModel.LoadSurfaceAsync("community-1", CommunityDetailSurfaceSection.ModeratorVacation, TestContext.Current.CancellationToken);

    Assert.True(viewModel.SuppressCommunityDigestsWhileOnVacation);
    Assert.True(await viewModel.SetSuppressCommunityDigestsWhileOnVacationAsync(false, TestContext.Current.CancellationToken));
    Assert.Equal(["False"], service.MutationDetails);
    Assert.False(viewModel.SuppressCommunityDigestsWhileOnVacation);
  }
}
