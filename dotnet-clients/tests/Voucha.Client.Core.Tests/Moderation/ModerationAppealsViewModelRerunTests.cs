using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Xunit;

namespace Voucha.Client.Core.Tests.Moderation;

public sealed partial class ModerationAppealsViewModelTests
{
  [Fact]
  public async Task RerunRefreshesTheAppealAndPreservesServerConfirmation()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.Details.Enqueue(service.Current);
    service.Details.Enqueue(service.Current with
    {
      AiPublicResponse = "New AI draft",
      AiDraftedAt = service.Current.AiDraftedAt?.AddMinutes(1),
      LatestLifecycleChangeId = "new-change",
    });

    await viewModel.RerunAsync(viewModel.Appeals.Single(), TestContext.Current.CancellationToken);

    Assert.Equal("New AI draft", viewModel.Appeals.Single().AiPublicResponse);
    var id = service.Current.Id;
    Assert.Equal([$"rerun:{id}", $"detail:{id}", $"detail:{id}"], service.Calls.Skip(1));
  }

  [Fact]
  public async Task RerunTimeoutPreservesTheExistingRowAndDirtyDraft()
  {
    var service = new ModerationAppealsTestService();
    var viewModel = PollingStaffViewModel(service, attempts: 2);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();
    viewModel.SetDraft(appeal, "Local edit");

    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);

    Assert.Equal(appeal.AiPublicResponse, viewModel.Appeals.Single().AiPublicResponse);
    Assert.Equal("Local edit", viewModel.DraftFor(viewModel.Appeals.Single()));
    Assert.Equal("The AI draft is still processing. Refresh to check again.", viewModel.ErrorMessage);
  }

  [Fact]
  public async Task RerunCancellationStopsPollingAndPreservesTheExistingRow()
  {
    var service = new ModerationAppealsTestService();
    var delayStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var viewModel = new ModerationAppealsViewModel(
        service,
        new NavigationViewer(true, ["administrator"]),
        20,
        async token =>
        {
          delayStarted.SetResult();
          await Task.Delay(Timeout.InfiniteTimeSpan, token);
        });
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var original = viewModel.Appeals.Single();
    using var cancellation = new CancellationTokenSource();
    var rerun = viewModel.RerunAsync(original, cancellation.Token);
    await delayStarted.Task;

    cancellation.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rerun);
    Assert.Equal(original.AiPublicResponse, viewModel.Appeals.Single().AiPublicResponse);
    Assert.Single(service.Calls, call => call == $"detail:{original.Id}");
    Assert.False(viewModel.IsMutating);
  }

  [Theory]
  [InlineData(false, true)]
  [InlineData(true, false)]
  public async Task ApprovedOrSentAppealsCannotRerunResolutionDrafts(bool approved, bool sent)
  {
    var service = new ModerationAppealsTestService
    {
      Current = new ModerationAppealsTestService().Appeal(approved: approved) with
      {
        SentAt = sent ? DateTimeOffset.UtcNow : null,
      },
    };
    var viewModel = StaffViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var appeal = viewModel.Appeals.Single();

    Assert.False(viewModel.CanRerun(appeal));
    await viewModel.RerunAsync(appeal, TestContext.Current.CancellationToken);
    Assert.DoesNotContain(service.Calls, call => call.StartsWith("rerun:", StringComparison.Ordinal));
  }

  private static ModerationAppealsViewModel PollingStaffViewModel(
      ModerationAppealsTestService service,
      int attempts = 20) =>
      new(service, new NavigationViewer(true, ["administrator"]), attempts, _ => Task.CompletedTask);
}
