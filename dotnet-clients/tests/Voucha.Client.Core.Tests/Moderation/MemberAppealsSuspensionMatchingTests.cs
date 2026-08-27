using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class MemberAppealsSuspensionMatchingTests
{
  [Fact]
  public async Task HistoricalSuspensionAppealDoesNotBlockCurrentSuspension()
  {
    var currentDate = DateTimeOffset.Parse("2026-07-02T09:00:00Z");
    var service = Service(currentDate, "old-suspension", "2026-07-01T09:00:00Z");
    var viewModel = ViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var target = Assert.Single(viewModel.EligibleTargets);
    Assert.Equal(currentDate, target.CreatedAt);
  }

  [Fact]
  public async Task CurrentSuspensionAppealBlocksOnlyItsExactSuspension()
  {
    var currentDate = DateTimeOffset.Parse("2026-07-02T09:00:00Z");
    var service = Service(currentDate, "current-suspension", "2026-07-02T09:00:00Z");
    var viewModel = ViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.EligibleTargets);
  }

  [Fact]
  public async Task ContextLessPendingSuspensionAppealBlocksCurrentSuspension()
  {
    var currentDate = DateTimeOffset.Parse("2026-07-02T09:00:00Z");
    var service = new MemberAppealsTestService
    {
      Identity = new MyIdentityResponse(new User(
          "user-1", "member", SuspendedAt: currentDate)),
    };
    service.AppealPages[ModerationAppealStatus.Pending].Enqueue(new(
        [MemberAppealsFixtures.Appeal(
            "suspension-appeal", suspensionId: "current-suspension")],
        MemberAppealsFixtures.Page()));
    var viewModel = ViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Empty(viewModel.EligibleTargets);
  }

  [Fact]
  public async Task SuspensionContextWithoutAppealSuspensionIdDoesNotBlockCurrentSuspension()
  {
    var currentDate = DateTimeOffset.Parse("2026-07-02T09:00:00Z");
    var service = new MemberAppealsTestService
    {
      Identity = new MyIdentityResponse(new User(
          "user-1", "member", SuspendedAt: currentDate)),
    };
    service.AppealPages[ModerationAppealStatus.Pending].Enqueue(new(
        [MemberAppealsFixtures.Appeal("suspension-appeal") with
        {
          TargetContext = new ModerationAppealSuspensionContext(
              "current-suspension", null, currentDate),
        }],
        MemberAppealsFixtures.Page()));
    var viewModel = ViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Single(viewModel.EligibleTargets);
  }

  private static MemberAppealsViewModel ViewModel(MemberAppealsTestService service) =>
      new(service, new NavigationViewer(true, [], IdentityId: "user-1"),
          MemberAppealsRoute.Suspension, new MemberAppealDraftStore());

  private static MemberAppealsTestService Service(
      DateTimeOffset currentDate,
      string suspensionId,
      string appealSuspensionDate)
  {
    var service = new MemberAppealsTestService
    {
      Identity = new MyIdentityResponse(new User("user-1", "member", SuspendedAt: currentDate)),
    };
    var target = new ModerationAppealSuspensionContext(
        suspensionId, null, DateTimeOffset.Parse(appealSuspensionDate));
    service.AppealPages[ModerationAppealStatus.Pending].Enqueue(new(
        [MemberAppealsFixtures.Appeal("suspension-appeal", suspensionId: suspensionId) with
        {
          TargetContext = target,
        }],
        MemberAppealsFixtures.Page()));
    return service;
  }
}
