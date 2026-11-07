using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Tests.Api;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ModerationSummaryDetailTests
{
  [Fact]
  public async Task AppealSummaryRequiresTargetedDetailBeforeEditingOrMutating()
  {
    var page = JsonSerializer.Deserialize<ModerationAppealListResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.appeals.default"), VouchaApiJson.Options)!;
    var summary = Assert.Single(page.Appeals);
    var service = new ModerationAppealsTestService();
    service.Pages.Enqueue(page);
    using var viewModel = new ModerationAppealsViewModel(service, new NavigationViewer(true, ["moderator"]));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Null(summary.AiDraftRequestedRunId);
    Assert.Equal("appealing-reviewer", summary.Appellant?.Username);
    Assert.False(viewModel.CanEdit(summary));
    Assert.False(viewModel.CanRerun(summary));
    viewModel.SetDraft(summary, "Premature draft");
    Assert.Equal(string.Empty, viewModel.DraftFor(summary));
    await viewModel.ApproveAsync(summary, TestContext.Current.CancellationToken);
    Assert.DoesNotContain(service.Calls, call => call.StartsWith("approve:", StringComparison.Ordinal));

    await viewModel.RefreshDeliveryStatusAsync(summary, TestContext.Current.CancellationToken);
    var detail = Assert.Single(viewModel.Appeals);
    Assert.Contains($"detail:{summary.Id}", service.Calls);
    Assert.NotNull(detail.AiDraftRequestedRunId);
    Assert.True(viewModel.CanEdit(detail));
    Assert.Equal("We reviewed your appeal.", viewModel.DraftFor(detail));
  }

  [Fact]
  public async Task DisputeSummaryRequiresTargetedDetailBeforeEditingOrMutating()
  {
    var page = JsonSerializer.Deserialize<ModerationDisputeListResponse>(
        ApiFixtureLoader.LoadResponse("native.moderation.disputes.default"), VouchaApiJson.Options)!;
    var summary = Assert.Single(page.Disputes);
    var service = new ModerationDisputesTestService();
    service.Pages.Enqueue(page);
    using var viewModel = new ModerationDisputesViewModel(service, new NavigationViewer(true, ["moderator"]));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Null(summary.AiDraftRequestedRunId);
    Assert.Equal("native-claimant", summary.Disputant?.Username);
    Assert.False(viewModel.CanEdit(summary));
    Assert.False(viewModel.CanRerun(summary));
    viewModel.SetPublicResponseDraft(summary, "Premature draft");
    Assert.Equal(string.Empty, viewModel.PublicResponseDraftFor(summary));
    await viewModel.ApproveAsync(summary, TestContext.Current.CancellationToken);
    Assert.DoesNotContain(service.Calls, call => call.StartsWith("approve:", StringComparison.Ordinal));

    await viewModel.RefreshAmbiguousAsync(summary.Id, TestContext.Current.CancellationToken);
    var detail = Assert.Single(viewModel.Disputes);
    Assert.NotNull(detail.AiDraftRequestedRunId);
    Assert.True(viewModel.CanEdit(detail));
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
