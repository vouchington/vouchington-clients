using Voucha.Client.Core.Api;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed partial class ChatTitleGenerationTests
{
  [Fact]
  public async Task SecondGenerateWhileLoadingIsIgnoredAndRaisesCanGenerateChanged()
  {
    var (viewModel, _, provider) = await LoadAsync("Generated", ("user", "hello"));
    var changed = new List<string?>();
    viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
    var gate = new TaskCompletionSource();
    provider.Gate = gate.Task;

    var first = viewModel.GenerateTitleAsync(TestContext.Current.CancellationToken);

    Assert.Contains(nameof(ChatConversationViewModel.CanGenerateTitle), changed);
    Assert.False(viewModel.CanGenerateTitle);
    await viewModel.GenerateTitleAsync(TestContext.Current.CancellationToken);
    Assert.Single(provider.Calls);
    gate.SetResult();
    await first;
  }

  [Fact]
  public async Task CancellationDuringGenerationRestoresTitleAndStateThenRethrows()
  {
    var (viewModel, service, provider) = await LoadAsync("Generated", ("user", "hello"));
    using var cts = new CancellationTokenSource();
    var gate = new TaskCompletionSource();
    provider.Gate = gate.Task;

    var pending = viewModel.GenerateTitleAsync(cts.Token);
    await cts.CancelAsync();
    gate.SetCanceled(cts.Token);

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    Assert.Equal(string.Empty, viewModel.Title);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Empty(service.UpdatedTitles);
  }

  [Fact]
  public async Task CancellationDuringPatchRestoresTheOptimisticTitle()
  {
    var (viewModel, service, _) = await LoadAsync("Generated", ("user", "hello"));
    using var cts = new CancellationTokenSource();
    var patch = new TaskCompletionSource<ChatConversationResponse>();
    service.UpdateConversationTitleAsyncOverride = (_, _) => patch.Task;

    var pending = viewModel.GenerateTitleAsync(cts.Token);
    Assert.Equal("Generated", viewModel.Title);
    await cts.CancelAsync();
    patch.SetCanceled(cts.Token);

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    Assert.Equal(string.Empty, viewModel.Title);
    Assert.Equal(LoadState.Loaded, viewModel.State);
  }

  [Fact]
  public async Task TitleWritesAreSerializedAndTheUsersTitleIsLastOnTheServer()
  {
    var (viewModel, service, _) = await LoadAsync("Generated", ("user", "hello"));
    var writes = new WriteLog(service, "Generated");
    var pending = viewModel.GenerateTitleAsync(TestContext.Current.CancellationToken);
    var rename = viewModel.RenameAsync("Mine", TestContext.Current.CancellationToken);
    writes.Release("Generated");
    await pending;
    await rename;

    Assert.Equal(1, writes.MaxInFlight);
    Assert.Equal("Mine", writes.Committed[^1]);
    Assert.Equal("Generated", writes.Committed[0]);
  }

  [Fact]
  public async Task RepairKeepsPatchingUntilTheSavedTitleEqualsTheCurrentTitle()
  {
    var (viewModel, service, _) = await LoadAsync("Generated", ("user", "hello"));
    var writes = new WriteLog(service, "Generated", "Mine");
    var pending = viewModel.GenerateTitleAsync(TestContext.Current.CancellationToken);
    var first = viewModel.RenameAsync("Mine", TestContext.Current.CancellationToken);
    writes.Release("Generated");
    await writes.WaitForStartAsync("Mine");
    var second = viewModel.RenameAsync("Mine2", TestContext.Current.CancellationToken);
    writes.Release("Mine");
    await Task.WhenAll(pending, first, second);

    Assert.Equal(["Generated", "Mine", "Mine2"], writes.Committed.Take(3));
    Assert.Equal("Mine2", writes.Committed[^1]);
  }

  [Fact]
  public async Task FailedRepairLeavesTheTitleVisiblyUnsavedUntilTheUserRenames()
  {
    var (viewModel, service, _) = await LoadAsync("Generated", ("user", "hello"));
    var calls = 0;
    (LoadState State, string? Error) atRename = default;
    var patch = new TaskCompletionSource<ChatConversationResponse>();
    service.UpdateConversationTitleAsyncOverride = (_, _) => ++calls switch
    {
      1 => patch.Task,
      2 => Task.FromException<ChatConversationResponse>(new HttpRequestException("boom")),
      _ => Capture(),
    };
    Task<ChatConversationResponse> Capture()
    {
      atRename = (viewModel.State, viewModel.ErrorMessage);
      return Task.FromResult(service.UpdateConversationTitleResult);
    }

    var pending = viewModel.GenerateTitleAsync(TestContext.Current.CancellationToken);
    var rename = viewModel.RenameAsync("Mine", TestContext.Current.CancellationToken);
    patch.SetResult(service.UpdateConversationTitleResult);
    await pending;
    await rename;

    Assert.Equal((LoadState.Error, "boom"), atRename);
    Assert.Equal(LoadState.Loaded, viewModel.State);
    Assert.Null(viewModel.ErrorMessage);
    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task FailedRepairAfterSwitchingConversationsDoesNotShowOnTheNewConversation()
  {
    var (viewModel, service, _) = await LoadAsync("Generated", ("user", "hello"));
    var calls = 0;
    var patch = new TaskCompletionSource<ChatConversationResponse>();
    var repair = new TaskCompletionSource<ChatConversationResponse>();
    var repairStarted = new TaskCompletionSource();
    service.UpdateConversationTitleAsyncOverride = (_, _) =>
    {
      switch (++calls)
      {
        case 1: return patch.Task;
        case 2: repairStarted.SetResult(); return repair.Task;
        default: return Task.FromResult(service.UpdateConversationTitleResult);
      }
    };

    var pending = viewModel.GenerateTitleAsync(TestContext.Current.CancellationToken);
    var rename = viewModel.RenameAsync("Mine", TestContext.Current.CancellationToken);
    patch.SetResult(service.UpdateConversationTitleResult);
    await repairStarted.Task;
    await viewModel.LoadAsync("conversation-2", "Other", TestContext.Current.CancellationToken);
    repair.SetException(new HttpRequestException("boom"));
    await pending;
    await rename;

    Assert.Equal("conversation-2", viewModel.ConversationId);
    Assert.Null(viewModel.ErrorMessage);
    Assert.False(viewModel.HasError);
    Assert.Equal(LoadState.Loaded, viewModel.State);
  }

  [Fact]
  public async Task DeleteDuringPatchSkipsTheRepair()
  {
    var (viewModel, service, _) = await LoadAsync("Generated", ("user", "hello"));
    var patch = new TaskCompletionSource<ChatConversationResponse>();
    service.UpdateConversationTitleAsyncOverride = (_, _) => patch.Task;

    var pending = viewModel.GenerateTitleAsync(TestContext.Current.CancellationToken);
    await viewModel.DeleteAsync(TestContext.Current.CancellationToken);
    patch.SetResult(service.UpdateConversationTitleResult);
    await pending;

    Assert.Single(service.UpdatedTitles);
  }

  private sealed class WriteLog
  {
    private readonly Dictionary<string, TaskCompletionSource> gates = [];
    private readonly Dictionary<string, TaskCompletionSource> started = [];
    private readonly FakeChatService service;
    private int inFlight;

    internal WriteLog(FakeChatService service, params string[] held)
    {
      this.service = service;
      foreach (var title in held) gates[title] = new(TaskCreationOptions.RunContinuationsAsynchronously);
      service.UpdateConversationTitleAsyncOverride = WriteAsync;
    }

    internal List<string> Committed { get; } = [];

    internal int MaxInFlight { get; private set; }

    internal void Release(string title) => Gate(gates, title).SetResult();

    internal Task WaitForStartAsync(string title) => Gate(started, title).Task;

    private static TaskCompletionSource Gate(Dictionary<string, TaskCompletionSource> map, string title)
    {
      if (!map.TryGetValue(title, out var tcs)) map[title] = tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
      return tcs;
    }

    private async Task<ChatConversationResponse> WriteAsync(string id, string title)
    {
      MaxInFlight = Math.Max(MaxInFlight, ++inFlight);
      Gate(started, title).TrySetResult();
      if (gates.TryGetValue(title, out var hold)) await hold.Task;
      await Task.Yield();
      Committed.Add(title);
      inFlight--;
      return service.UpdateConversationTitleResult with
      {
        Conversation = service.UpdateConversationTitleResult.Conversation with { Title = title },
      };
    }
  }
}
