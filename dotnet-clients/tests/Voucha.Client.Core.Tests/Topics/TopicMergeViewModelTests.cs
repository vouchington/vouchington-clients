using System.Reflection;
using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.Core.Tests.Topics;

public sealed class TopicMergeViewModelTests
{
  [Fact]
  public async Task PendingSubmissionCannotBeRepeatedAndStatusRetriesOnlyGet()
  {
    var (model, service) = Create();
    var pending = new TaskCompletionSource<TopicMergeResponse>();
    service.Submit = () => pending.Task;
    var first = model.SubmitAsync("source", "destination");
    await model.SubmitAsync("source", "destination");
    Assert.False(model.CanSubmit);
    Assert.Equal(1, service.Posts);
    pending.SetResult(Fixture("accepted"));
    await first;
    service.Read = () => Task.FromException<TopicMergeResponse>(new HttpRequestException("offline"));
    await model.CheckAsync();
    Assert.Equal(UiMessageKey.SettingsTopicsMergeStatusFailed, model.Message?.Key);
    Assert.True(model.CanCheck);
    var accepted = model.Operation!;
    service.Read = () => Task.FromResult(Fixture("status.running"));
    await model.SubmitAsync("source", "destination");
    await model.CheckAsync();
    Assert.Equal(1, service.Posts);
    Assert.Equal(2, service.Gets);
    Assert.Equal((accepted.SourceTopicId, accepted.Id), service.LastRead);
    Assert.Equal(accepted.StatusUrl, model.Operation?.StatusUrl);
    Assert.Equal("running", model.Operation?.Status);
  }

  [Fact]
  public async Task PreviousAccountSubmissionCannotReplaceNewOperation()
  {
    var (model, service) = Create();
    model.SetAccount("first");
    var pending = new TaskCompletionSource<TopicMergeResponse>();
    service.Submit = () => pending.Task;
    var old = model.SubmitAsync("source", "destination");
    model.SetAccount("second");
    Assert.Null(model.Operation);
    service.Submit = () => Task.FromResult(Fixture("accepted") with
    {
      TopicMerge = Fixture("accepted").TopicMerge with { Id = "new-operation" },
    });
    await model.SubmitAsync("source", "destination");
    pending.SetResult(Fixture("accepted"));
    await old;
    Assert.Equal("new-operation", model.Operation?.Id);
    Assert.False(model.IsBusy);
  }

  [Fact]
  public async Task PreviousAccountStatusCannotRestoreClearedOperation()
  {
    var (model, service) = Create();
    model.SetAccount("first");
    await model.SubmitAsync("source", "destination");
    var pending = new TaskCompletionSource<TopicMergeResponse>();
    service.Read = () => pending.Task;
    var read = model.CheckAsync();
    model.SetAccount(null);
    pending.SetResult(Fixture("status.completed"));
    await read;
    Assert.Null(model.Operation);
    Assert.Null(model.Message);
    Assert.False(model.IsBusy);
  }

  [Fact]
  public async Task CurrentAccountFencesResponseEvenWithoutSessionNotification()
  {
    var proxy = DispatchProxy.Create<ITopicsService, ServiceProxy>();
    var service = (ServiceProxy)(object)proxy;
    string? account = "first";
    var model = new TopicMergeViewModel(proxy, () => account);
    var pending = new TaskCompletionSource<TopicMergeResponse>();
    service.Submit = () => pending.Task;
    var submit = model.SubmitAsync("source", "destination");
    account = "second";
    pending.SetResult(Fixture("accepted"));
    await submit;
    Assert.Null(model.Operation);
    Assert.Null(model.Message);
    Assert.False(model.IsBusy);
  }

  [Theory]
  [InlineData("queued", true)]
  [InlineData("running", true)]
  [InlineData("completed", false)]
  [InlineData("failed", false)]
  public async Task StatusPreservesProducerDetailsAndStopsAtTerminalState(string state, bool canCheck)
  {
    var (model, service) = Create();
    await model.SubmitAsync("source", "destination");
    service.Read = () => Task.FromResult(Fixture($"status.{state}"));
    await model.CheckAsync();
    Assert.Equal(state, model.Operation?.Status);
    Assert.Equal(canCheck, model.CanCheck);
    Assert.Equal(Fixture($"status.{state}").TopicMerge.Stage, model.Operation?.Stage);
    if (!canCheck)
    {
      await model.CheckAsync();
      Assert.Equal(1, service.Gets);
    }
  }

  [Fact]
  public async Task StatusForDifferentOperationIsIgnored()
  {
    var (model, service) = Create();
    await model.SubmitAsync("source", "destination");
    service.Read = () => Task.FromResult(Fixture("status.completed") with
    {
      TopicMerge = Fixture("status.completed").TopicMerge with { Id = "unrelated" },
    });
    await model.CheckAsync();
    Assert.Equal("queued", model.Operation?.Status);
    Assert.True(model.CanCheck);
  }

  private static TopicMergeResponse Fixture(string suffix) => JsonSerializer.Deserialize<TopicMergeResponse>(
      ApiFixtureLoader.LoadResponse($"native.topics.merge.{suffix}"), VouchaApiJson.Options)!;

  private static (TopicMergeViewModel Model, ServiceProxy Service) Create()
  {
    var proxy = DispatchProxy.Create<ITopicsService, ServiceProxy>();
    var service = (ServiceProxy)(object)proxy;
    return (new TopicMergeViewModel(proxy), service);
  }

  public class ServiceProxy : DispatchProxy
  {
    public Func<Task<TopicMergeResponse>> Submit { get; set; } = () => Task.FromResult(Fixture("accepted"));
    public Func<Task<TopicMergeResponse>> Read { get; set; } = () => Task.FromResult(Fixture("status.running"));
    public int Posts { get; private set; }
    public int Gets { get; private set; }
    public (string Source, string Id)? LastRead { get; private set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
      if (targetMethod?.Name == nameof(ITopicsService.MergeTopicAliasesAsync))
      {
        Posts++;
        return Submit();
      }
      if (targetMethod?.Name == nameof(ITopicsService.FetchTopicMergeStatusAsync))
      {
        Gets++;
        LastRead = ((string)args![0]!, (string)args[1]!);
        return Read();
      }
      throw new InvalidOperationException($"Unexpected call: {targetMethod?.Name}");
    }
  }
}
