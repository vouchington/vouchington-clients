using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed class ModerationPaginationResilienceTests
{
  [Fact]
  public async Task HasMoreAliasDrivesContinuationAndDeduplicatesRows()
  {
    var first = AppealFixture() with { PageInfo = new PageInfo("appeals-next", false, null, true) };
    var duplicate = first.Appeals[0];
    var service = new RacingModerationService
    {
      AppealResponses = new Queue<Func<CancellationToken, Task<StaffModerationAppealListResponse>>>([
        _ => Task.FromResult(first),
        _ => Task.FromResult(new StaffModerationAppealListResponse(
            [duplicate, duplicate with { Id = "appeal-2" }],
            new PageInfo(null, false, null))),
      ]),
    };
    var viewModel = ViewModel(service, "/appeals");

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.HasMore);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal([duplicate.Id, "appeal-2"], viewModel.Items.Select(row => row.Id));
    Assert.Equal([null, "appeals-next"], service.AppealCursors);
    Assert.False(viewModel.HasMore);
  }

  [Fact]
  public async Task CanceledContinuationRetainsRowsAndRetriesTheSameCursor()
  {
    var first = AppealFixture() with { PageInfo = new PageInfo("appeals-next", true, null) };
    var service = new RacingModerationService
    {
      AppealResponses = new Queue<Func<CancellationToken, Task<StaffModerationAppealListResponse>>>([
        _ => Task.FromResult(first),
        token => Task.FromCanceled<StaffModerationAppealListResponse>(token),
        _ => Task.FromResult(new StaffModerationAppealListResponse([], new PageInfo(null, false, null))),
      ]),
    };
    var viewModel = ViewModel(service, "/appeals");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreAsync(new CancellationToken(true));

    Assert.Equal(first.Appeals.Select(appeal => appeal.Id), viewModel.Items.Select(row => row.Id));
    Assert.False(viewModel.HasPaginationError);
    Assert.True(viewModel.HasMore);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal([null, "appeals-next", "appeals-next"], service.AppealCursors);
  }

  [Fact]
  public async Task ContextChangeRejectsStaleContinuationCompletion()
  {
    var continuation = new TaskCompletionSource<StaffModerationAppealListResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var first = AppealFixture() with { PageInfo = new PageInfo("appeals-next", true, null) };
    var disputes = DisputeFixture();
    var service = new RacingModerationService
    {
      AppealResponses = new Queue<Func<CancellationToken, Task<StaffModerationAppealListResponse>>>([
        _ => Task.FromResult(first),
        _ => continuation.Task,
      ]),
      DisputeResponse = disputes,
    };
    var viewModel = ViewModel(service, "/appeals");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var staleLoad = viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.True(ModerationRoutes.TryResolve("/disputes", out var disputesContext));
    viewModel.SetContext(disputesContext);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    continuation.SetResult(new StaffModerationAppealListResponse(
        [first.Appeals[0] with { Id = "stale-appeal" }], new PageInfo(null, false, null)));
    await staleLoad;

    Assert.Equal(disputes.Disputes.Select(dispute => dispute.Id), viewModel.Items.Select(row => row.Id));
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.False(viewModel.HasPaginationError);
  }

  [Fact]
  public async Task UnexpectedCancellationSurfacesAsRetryablePaginationFailure()
  {
    var first = AppealFixture() with { PageInfo = new PageInfo("appeals-next", true, null) };
    var service = new RacingModerationService
    {
      AppealResponses = new Queue<Func<CancellationToken, Task<StaffModerationAppealListResponse>>>([
        _ => Task.FromResult(first),
        _ => Task.FromCanceled<StaffModerationAppealListResponse>(new CancellationToken(true)),
        _ => Task.FromResult(new StaffModerationAppealListResponse([], new PageInfo(null, false, null))),
      ]),
    };
    var viewModel = ViewModel(service, "/appeals");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasPaginationError);
    Assert.True(viewModel.HasMore);

    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasPaginationError);
    Assert.False(viewModel.HasMore);
    Assert.Equal([null, "appeals-next", "appeals-next"], service.AppealCursors);
  }

  private static ModerationViewModel ViewModel(RacingModerationService service, string path)
  {
    Assert.True(ModerationRoutes.TryResolve(path, out var context));
    var viewModel = new ModerationViewModel(service);
    viewModel.SetContext(context);
    return viewModel;
  }

  private static StaffModerationAppealListResponse AppealFixture() =>
      JsonSerializer.Deserialize<StaffModerationAppealListResponse>(
          ApiFixtureLoader.LoadResponse("native.moderation.appeals.default"),
          VouchaApiJson.Options)!;

  private static StaffModerationDisputeListResponse DisputeFixture() =>
      JsonSerializer.Deserialize<StaffModerationDisputeListResponse>(
          ApiFixtureLoader.LoadResponse("native.moderation.disputes.default"),
          VouchaApiJson.Options)!;

  private sealed class RacingModerationService : ReviewQueueModerationServiceStub
  {
    public required Queue<Func<CancellationToken, Task<StaffModerationAppealListResponse>>>
        AppealResponses
    { get; init; }
    public StaffModerationDisputeListResponse? DisputeResponse { get; init; }
    public List<string?> AppealCursors { get; } = [];

    public override Task<StaffModerationAppealListResponse> FetchStaffAppealsAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
      AppealCursors.Add(after);
      return AppealResponses.Dequeue()(cancellationToken);
    }

    public override Task<StaffModerationDisputeListResponse> FetchStaffDisputesAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(DisputeResponse ?? throw new InvalidOperationException("Missing disputes."));
  }
}
