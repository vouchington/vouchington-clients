using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ModerationSummaryDetailTests
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task AppealSummaryRequiresTargetedDetailBeforeEditingOrMutating(bool accountRoundTrip)
  {
    var page = JsonSerializer.Deserialize<StaffModerationAppealListResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.appeals.default"), VouchaApiJson.Options)!;
    var summary = Assert.Single(page.Appeals);
    var service = new ModerationAppealsTestService();
    service.SummaryPages.Enqueue(page);
    var owner = new NavigationViewer(true, ["moderator"], IdentityId: "staff-1");
    var provider = new MutableNavigationViewerProvider();
    provider.SetViewer(owner);
    using var viewModel = new ModerationAppealsViewModel(service, owner, viewerProvider: provider);
    if (accountRoundTrip)
    {
      provider.SetViewer(new NavigationViewer(true, ["moderator"], IdentityId: "staff-2"));
      provider.SetViewer(owner);
    }
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("appealing-reviewer", summary.Appellant?.Username);
    Assert.Empty(viewModel.Appeals);
    await viewModel.FetchDetailAsync(summary.Id, TestContext.Current.CancellationToken);
    var detail = Assert.Single(viewModel.Appeals);
    Assert.Contains($"detail:{summary.Id}", service.Calls);
    Assert.NotNull(detail.AiDraftRequestedRunId);
    Assert.True(viewModel.CanEdit(detail));
    Assert.Equal("We reviewed your appeal.", viewModel.DraftFor(detail));
  }

  [Fact]
  public async Task DisputeSummaryRequiresTargetedDetailBeforeEditingOrMutating()
  {
    var page = JsonSerializer.Deserialize<StaffModerationDisputeListResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.disputes.default"), VouchaApiJson.Options)!;
    var summary = Assert.Single(page.Disputes);
    var service = new ModerationDisputesTestService();
    service.SummaryPages.Enqueue(page);
    using var viewModel = new ModerationDisputesViewModel(service, new NavigationViewer(true, ["moderator"]));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("native-claimant", summary.Disputant?.Username);
    Assert.Empty(viewModel.Disputes);
    await viewModel.FetchDetailAsync(summary.Id, TestContext.Current.CancellationToken);
    var detail = Assert.Single(viewModel.Disputes);
    Assert.NotNull(detail.AiDraftRequestedRunId);
    Assert.True(viewModel.CanEdit(detail));
  }

  [Fact]
  public async Task DuplicateSummaryHydrationCannotOverwriteAnEditedDetailDraft()
  {
    var page = JsonSerializer.Deserialize<StaffModerationAppealListResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.appeals.default"), VouchaApiJson.Options)!;
    var summary = Assert.Single(page.Appeals);
    var service = new ModerationAppealsTestService();
    service.SummaryPages.Enqueue(page);
    var held = new TaskCompletionSource<ModerationAppealResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.PendingDetail = held;
    using var viewModel = new ModerationAppealsViewModel(service, new NavigationViewer(true, ["moderator"]));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var first = viewModel.FetchDetailAsync(summary.Id, TestContext.Current.CancellationToken);
    var duplicate = viewModel.FetchDetailAsync(summary.Id, TestContext.Current.CancellationToken);
    Assert.Single(service.Calls, call => call.StartsWith("detail:", StringComparison.Ordinal));
    Assert.Empty(viewModel.Appeals);
    held.SetResult(new ModerationAppealResponse(service.Current));
    await first;
    var detail = Assert.Single(viewModel.Appeals);
    Assert.True(viewModel.CanEdit(detail));
    viewModel.SetDraft(detail, "Moderator local edit");
    await duplicate;

    Assert.Equal("Moderator local edit", viewModel.DraftFor(Assert.Single(viewModel.Appeals)));
    Assert.Single(service.Calls, call => call.StartsWith("detail:", StringComparison.Ordinal));
  }

  [Fact]
  public async Task HeldDetailCannotReplaceStateWhileDeliveryIsInFlight()
  {
    var service = new ModerationAppealsTestService();
    service.Current = service.Current with { PublicResponse = "Approved response", ApprovedAt = DateTimeOffset.UtcNow };
    using var viewModel = new ModerationAppealsViewModel(service, new NavigationViewer(true, ["moderator"]));
    await viewModel.LoadWithDetailAsync(TestContext.Current.CancellationToken);
    var original = Assert.Single(viewModel.Appeals);
    var heldRead = new TaskCompletionSource<ModerationAppealResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var heldDelivery = new TaskCompletionSource<ModerationAppealResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.PendingDetail = heldRead;
    service.PendingDelivery = heldDelivery;
    var detailRead = viewModel.FetchDetailAsync(original.Id, TestContext.Current.CancellationToken);
    var delivery = viewModel.DeliverAsync(original, TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsMutating);

    heldRead.SetResult(new ModerationAppealResponse(original with { AiPublicResponse = "Stale detail" }));

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => detailRead);
    Assert.Same(original, Assert.Single(viewModel.Appeals));
    heldDelivery.SetResult(new ModerationAppealResponse(original with { SentAt = DateTimeOffset.UtcNow }));
    await delivery;
    Assert.NotNull(Assert.Single(viewModel.Appeals).SentAt);
  }

  [Fact]
  public async Task HeldDisputeDetailCannotRepopulateAChangedStatusQueue()
  {
    var service = new ModerationDisputesTestService();
    using var viewModel = new ModerationDisputesViewModel(service, new NavigationViewer(true, ["moderator"]));
    await viewModel.LoadWithDetailAsync(TestContext.Current.CancellationToken);
    var original = Assert.Single(viewModel.Disputes);
    var held = new TaskCompletionSource<ModerationDisputeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.PendingDetail = held;
    var detailRead = viewModel.FetchDetailAsync(original.Id, TestContext.Current.CancellationToken);
    await viewModel.SelectStatusAsync(ModerationDisputeStatus.Resolved, TestContext.Current.CancellationToken);

    held.SetResult(new ModerationDisputeResponse(original));

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => detailRead);
    Assert.Empty(viewModel.Disputes);
    Assert.Equal(ModerationDisputeStatus.Resolved, Assert.Single(viewModel.Summaries).Status);
  }

  [Theory]
  [InlineData("native.moderation.appeals.default", "appeals", true)]
  [InlineData("native.moderation.disputes.default", "disputes", false)]
  public void SummaryShapesCannotBeDecodedAsStaffDetail(string fixtureId, string collection, bool appeal)
  {
    using var document = JsonDocument.Parse(ApiFixtureLoader.LoadResponse(fixtureId));
    var row = document.RootElement.GetProperty(collection)[0];
    if (appeal)
      Assert.Throws<JsonException>(() => row.Deserialize<ModerationAppeal>(VouchaApiJson.Options));
    else
      Assert.Throws<JsonException>(() => row.Deserialize<ModerationDispute>(VouchaApiJson.Options));
  }
}
