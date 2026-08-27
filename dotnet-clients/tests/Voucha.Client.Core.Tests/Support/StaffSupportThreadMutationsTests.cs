using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Support;

public sealed class StaffSupportThreadMutationsTests
{
  [Fact]
  public void ApprovedDraftsCanOnlyBeSent()
  {
    var editable = StaffSupportTestService.MessageFor("draft", "thread", "editable", true);
    var approved = StaffSupportTestService.MessageFor("approved", "thread", "approved", true, true);
    var sent = StaffSupportTestService.MessageFor("sent", "thread", "sent", true, true, true);

    Assert.True(StaffSupportThreadViewModel.CanEditDraft(editable));
    Assert.False(StaffSupportThreadViewModel.CanSendDraft(editable));
    Assert.False(StaffSupportThreadViewModel.CanEditDraft(approved));
    Assert.True(StaffSupportThreadViewModel.CanSendDraft(approved));
    Assert.False(StaffSupportThreadViewModel.CanEditDraft(sent));
    Assert.False(StaffSupportThreadViewModel.CanSendDraft(sent));
  }

  [Fact]
  public async Task ThreadMutationsReplaceStateAndPersistOutboundReply()
  {
    var initial = StaffSupportTestService.MessageFor("draft", "thread", "before", true);
    var service = new StaffSupportTestService { MessageResults = [initial] };
    var viewModel = new StaffSupportThreadViewModel(service, "administrator");
    await viewModel.LoadAsync("thread", TestContext.Current.CancellationToken);

    await viewModel.AssignAsync(TestContext.Current.CancellationToken);
    Assert.Equal(SupportThreadStatus.Assigned, viewModel.Thread?.Status);
    await viewModel.SetResolvedAsync(true, TestContext.Current.CancellationToken);
    Assert.Equal(SupportThreadStatus.Resolved, viewModel.Thread?.Status);
    await viewModel.SetResolvedAsync(false, TestContext.Current.CancellationToken);
    Assert.Equal(SupportThreadStatus.Open, viewModel.Thread?.Status);

    viewModel.ReplyText = "  saved reply  ";
    await viewModel.SaveOutboundReplyAsync(TestContext.Current.CancellationToken);
    await viewModel.SaveDraftAsync(initial, "edited", TestContext.Current.CancellationToken);
    await viewModel.ApproveAsync(initial, TestContext.Current.CancellationToken);
    await viewModel.SendAsync(initial, TestContext.Current.CancellationToken);

    Assert.Contains("assign", service.Calls);
    Assert.Contains("resolve:True", service.Calls);
    Assert.Contains("resolve:False", service.Calls);
    Assert.Contains("create:saved reply", service.Calls);
    Assert.Contains("update", service.Calls);
    Assert.Contains("approve", service.Calls);
    Assert.Contains("send", service.Calls);
    Assert.Equal(string.Empty, viewModel.ReplyText);
    Assert.Contains(viewModel.Messages, message => message.Id == "created");
    Assert.True(viewModel.Messages.Single(message => message.Id == "draft").SentAt is not null);
  }

  [Fact]
  public async Task OlderMessagesPrependDistinctItemsAndExpectedErrorsRender()
  {
    var existing = StaffSupportTestService.MessageFor("existing", "thread", "old");
    var service = new StaffSupportTestService
    {
      MessageResults = [existing],
      MessagePageInfo = new("existing", true, "older"),
    };
    var viewModel = new StaffSupportThreadViewModel(service, "administrator");
    await viewModel.LoadAsync("thread", TestContext.Current.CancellationToken);
    service.MessageResults = [StaffSupportTestService.MessageFor("older", "thread", "older"), existing];
    service.MessagePageInfo = new("older", false, null);
    await viewModel.LoadOlderAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["older", "existing"], viewModel.Messages.Select(message => message.Id));
    Assert.Equal([null, "existing"], service.MessageAfters);
    Assert.False(viewModel.HasOlderMessages);

    service.Failure = new InvalidOperationException("bad request");
    await viewModel.SaveDraftAsync(existing, "nope", TestContext.Current.CancellationToken);
    Assert.Equal("bad request", viewModel.ErrorMessage);
    Assert.False(viewModel.IsBusy);
  }

  [Fact]
  public async Task GeneratedDraftChecksImmediatelyAndPreservesLoadedOlderMessages()
  {
    var newest = StaffSupportTestService.MessageFor("newest", "thread", "latest", direction: "inbound");
    var service = new StaffSupportTestService
    {
      MessageResults = [newest],
      MessagePageInfo = new("newest", true, "older"),
    };
    var viewModel = new StaffSupportThreadViewModel(service, "administrator");
    await viewModel.LoadAsync("thread", TestContext.Current.CancellationToken);
    var older = StaffSupportTestService.MessageFor("older", "thread", "older");
    service.MessageResults = [older];
    service.MessagePageInfo = new("older", false, null);
    await viewModel.LoadOlderAsync(TestContext.Current.CancellationToken);
    service.MessageResults =
    [
      newest,
      StaffSupportTestService.MessageFor("draft-two", "thread", "new", true),
    ];

    using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
    await viewModel.GenerateDraftAsync(cancellation.Token);

    Assert.Contains("draft", service.Calls);
    Assert.Equal(["older", "newest", "draft-two"], viewModel.Messages.Select(message => message.Id));
    Assert.False(viewModel.HasOlderMessages);
    Assert.False(viewModel.IsWaitingForDraft);
    Assert.False(viewModel.IsBusy);
  }

  [Fact]
  public async Task AmbiguousDraftQueueFailureReconcilesTheAuthoritativeDraft()
  {
    var inbound = StaffSupportTestService.MessageFor("inbound", "thread", "received", direction: "inbound");
    var generated = StaffSupportTestService.MessageFor("generated", "thread", "draft", true);
    var service = new StaffSupportTestService { MessageResults = [inbound], QueueFailure = new HttpRequestException("offline") };
    var viewModel = new StaffSupportThreadViewModel(service, "administrator");
    await viewModel.LoadAsync("thread", TestContext.Current.CancellationToken);
    service.MessageResults = [inbound, generated];

    await viewModel.GenerateDraftAsync(TestContext.Current.CancellationToken);

    Assert.Null(viewModel.ErrorMessage);
    Assert.Contains(viewModel.Messages, message => message.Id == generated.Id);
    Assert.False(viewModel.IsWaitingForDraft);
  }

  [Theory]
  [InlineData(SupportThreadStatus.Resolved)]
  [InlineData(SupportThreadStatus.Closed)]
  public async Task ResolvedOrClosedThreadsRejectDraftMutations(SupportThreadStatus status)
  {
    var draft = StaffSupportTestService.MessageFor("draft", "thread", "before", true);
    var service = new StaffSupportTestService { Thread = StaffSupportTestService.ThreadFor("thread", status), MessageResults = [draft] };
    var viewModel = new StaffSupportThreadViewModel(service, "administrator");
    await viewModel.LoadAsync("thread", TestContext.Current.CancellationToken);

    await viewModel.SaveDraftAsync(draft, "edited", TestContext.Current.CancellationToken);
    await viewModel.ApproveAsync(draft, TestContext.Current.CancellationToken);
    await viewModel.SendAsync(draft, TestContext.Current.CancellationToken);

    Assert.DoesNotContain("update", service.Calls);
    Assert.DoesNotContain("approve", service.Calls);
    Assert.DoesNotContain("send", service.Calls);
  }

  [Fact]
  public async Task DraftGenerationRequiresAnInboundMessageAndPublishesPollingStateImmediately()
  {
    var service = new StaffSupportTestService
    {
      MessageResults = [StaffSupportTestService.MessageFor("outbound", "thread", "sent")],
    };
    var viewModel = new StaffSupportThreadViewModel(service, "administrator");
    await viewModel.LoadAsync("thread", TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanGenerateDraft);
    await viewModel.GenerateDraftAsync(TestContext.Current.CancellationToken);
    Assert.DoesNotContain("draft", service.Calls);

    service.MessagePageInfo = new("outbound", true, "older");
    await viewModel.LoadAsync("thread", TestContext.Current.CancellationToken);
    Assert.True(viewModel.CanGenerateDraft);
    service.MessagePageInfo = new("outbound", false, null);
    await viewModel.LoadOlderAsync(TestContext.Current.CancellationToken);
    Assert.False(viewModel.CanGenerateDraft);

    service.MessageResults = [StaffSupportTestService.MessageFor("inbound", "thread", "received", direction: "inbound")];
    await viewModel.LoadAsync("thread", TestContext.Current.CancellationToken);
    var pollingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    service.MessageFetch = async (_, _, _, token) =>
    {
      pollingStarted.TrySetResult();
      await Task.Delay(Timeout.InfiniteTimeSpan, token);
      return new SupportMessageListResponse([], new PageInfo(null, false, null));
    };

    using var cancellation = new CancellationTokenSource();
    var generation = viewModel.GenerateDraftAsync(cancellation.Token);
    await pollingStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.CanGenerateDraft);
    Assert.True(viewModel.IsWaitingForDraft);
    Assert.True(viewModel.IsBusy);
    cancellation.Cancel();
    await generation;
    Assert.False(viewModel.IsWaitingForDraft);
    Assert.False(viewModel.IsBusy);
  }

  [Fact]
  public async Task DraftGenerationWaitsForAnExistingUnsentDraftToBeSent()
  {
    var service = new StaffSupportTestService
    {
      MessageResults =
      [
        StaffSupportTestService.MessageFor("inbound", "thread", "received", direction: "inbound"),
        StaffSupportTestService.MessageFor("draft", "thread", "awaiting review", true),
      ],
    };
    var viewModel = new StaffSupportThreadViewModel(service, "administrator");
    await viewModel.LoadAsync("thread", TestContext.Current.CancellationToken);

    await viewModel.GenerateDraftAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.CanGenerateDraft);
    Assert.DoesNotContain("draft", service.Calls);
  }
}
