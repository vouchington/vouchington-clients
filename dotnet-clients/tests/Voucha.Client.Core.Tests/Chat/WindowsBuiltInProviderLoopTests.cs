using System.Text.Json;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Tests.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  [Fact]
  public async Task WindowsProviderKeepsTheToolPromptAndReturnsTheClosedAnswer()
  {
    using var catalog = JsonDocument.Parse("""
        {"tools":[{"name":"read_post","description":"Read","inputSchema":{"type":"object","required":["id"]}}]}
        """);
    var factory = new FakeFactory { Catalog = catalog.RootElement.Clone() };
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    var snapshot = Assert.IsType<MemberMcpConnectedSnapshot>(session.ConnectedSnapshot());
    var runtime = new RecordingBuiltInRuntime { BuiltInText = """{"answer":"kept"}""" };
    var provider = new WindowsSystemLanguageModelProvider(runtime, mcpSession: session);
    var message = new string('m', 13_000);

    var result = await provider.GenerateAssistantContentAsync(
        message, [new("assistant", "earlier")], snapshot, TestContext.Current.CancellationToken);

    Assert.Equal("kept", result.AssistantContent);
    Assert.Equal("windows_foundry", result.ModelProvider);
    Assert.Equal(0, runtime.PlainCalls);
    Assert.Single(runtime.Generated);
    Assert.Contains(message, runtime.Generated[0], StringComparison.Ordinal);
    Assert.Contains("{\"type\":\"object\",\"required\":[\"id\"]}", runtime.Generated[0], StringComparison.Ordinal);
    Assert.Equal(0, factory.Connections[0].ToolCalls);
  }

  [Fact]
  public async Task WindowsProviderUsesThePlainPromptWhenTheCatalogHasNoTools()
  {
    var factory = new FakeFactory();
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    var snapshot = Assert.IsType<MemberMcpConnectedSnapshot>(session.ConnectedSnapshot());
    var runtime = new RecordingBuiltInRuntime();
    var provider = new WindowsSystemLanguageModelProvider(runtime, mcpSession: session);

    var result = await provider.GenerateAssistantContentAsync(
        "hello", [], snapshot, TestContext.Current.CancellationToken);

    Assert.Equal("plain", result.AssistantContent);
    Assert.Equal(1, runtime.PlainCalls);
    Assert.Empty(runtime.Generated);
  }

  [Fact]
  public async Task WindowsProviderDoesNotGenerateAPromptThatDoesNotFitOrExecuteFilteredText()
  {
    using var catalog = JsonDocument.Parse("""
        {"tools":[{"name":"read_post","description":"Read","inputSchema":{"type":"object","required":["id"]}}]}
        """);
    var factory = new FakeFactory { Catalog = catalog.RootElement.Clone() };
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    var snapshot = Assert.IsType<MemberMcpConnectedSnapshot>(session.ConnectedSnapshot());
    var declined = new RecordingBuiltInRuntime { UsableIndex = 0 };
    var declinedProvider = new WindowsSystemLanguageModelProvider(declined, mcpSession: session);
    var declinedError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        declinedProvider.GenerateAssistantContentAsync("hello", [], snapshot, TestContext.Current.CancellationToken));
    Assert.Contains("does not fit", declinedError.Message, StringComparison.Ordinal);
    Assert.Empty(declined.Generated);

    var filtered = new RecordingBuiltInRuntime
    {
      Status = "ResponseBlockedByContentModeration",
      BuiltInText = """{"call":{"id":"call-1","name":"read_post","arguments":{"id":"post"}}}""",
    };
    var filteredProvider = new WindowsSystemLanguageModelProvider(filtered, mcpSession: session);
    var filteredError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
        filteredProvider.GenerateAssistantContentAsync("hello", [], snapshot, TestContext.Current.CancellationToken));
    Assert.Contains("ResponseBlockedByContentModeration", filteredError.Message, StringComparison.Ordinal);
    Assert.DoesNotContain("read_post", filteredError.Message, StringComparison.Ordinal);
    Assert.Equal(0, factory.Connections.Single().ToolCalls);
  }

  [Fact]
  public async Task WindowsProviderRejectsABlankClosedAnswer()
  {
    using var catalog = JsonDocument.Parse("""
        {"tools":[{"name":"read_post","description":"Read","inputSchema":{"type":"object","required":["id"]}}]}
        """);
    var factory = new FakeFactory { Catalog = catalog.RootElement.Clone() };
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    var snapshot = Assert.IsType<MemberMcpConnectedSnapshot>(session.ConnectedSnapshot());
    var runtime = new RecordingBuiltInRuntime { BuiltInText = """{"answer":"   "}""" };
    var provider = new WindowsSystemLanguageModelProvider(runtime, mcpSession: session);

    await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GenerateAssistantContentAsync(
        "hello", [], snapshot, TestContext.Current.CancellationToken));
    Assert.Equal(0, factory.Connections.Single().ToolCalls);
  }

  [Fact]
  public async Task RetainedWindowsToolResultSurvivesTheSendHandlerAndBlocksAnotherDispatch()
  {
    const string postId = "11111111-1111-1111-1111-111111111111";
    using var catalog = JsonDocument.Parse("""
        {"tools":[{"name":"read_feed","description":"Read","inputSchema":{"type":"object"}}]}
        """);
    using var structured = JsonDocument.Parse(
        $$"""{"success":true,"results":[{"entity_id":"{{postId}}"}]}""");
    using var content = JsonDocument.Parse("""[{"type":"text","text":"kept"}]""");
    var factory = new FakeFactory { Catalog = catalog.RootElement.Clone() };
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    var dispatch = Assert.IsType<MemberMcpConnectedSnapshot>(session.ConnectedSnapshot());
    factory.Connections.Single().ToolResult = new McpToolResult(
        content.RootElement.Clone(), structured.RootElement.Clone(), false);
    var runtime = new RecordingBuiltInRuntime
    {
      BuiltInText = """{"call":{"id":"call-1","name":"read_feed","arguments":{"option":"posts"}}}""",
      UsablePromptIndex = prompt => prompt.Contains("result:", StringComparison.Ordinal) ? 0 : (ulong)prompt.Length,
    };
    var provider = new WindowsSystemLanguageModelProvider(runtime, mcpSession: session);
    var service = new FakeChatService();
    var viewModel = new ChatConversationViewModel(service, new StatusOnlyResolver(provider.Status), provider);

    Assert.False(await viewModel.TrySendAsync("Show the feed", TestContext.Current.CancellationToken));

    Assert.NotNull(viewModel.RetainedWindowsToolResult);
    var retained = viewModel.RetainedWindowsToolResult;
    Assert.Equal("conversation-1", retained.ConversationId);
    Assert.Equal("Show the feed", retained.RequestText);
    Assert.Equal("account-a", retained.AccountId);
    Assert.Equal(dispatch.AccountEpoch, retained.AccountEpoch);
    Assert.Equal("call-1", retained.CallId);
    Assert.Contains(postId, retained.Envelope, StringComparison.Ordinal);
    Assert.Equal(postId, Assert.Single(retained.FeedPostReceipt!.PostIds));
    Assert.Null(retained.RssItemReceipt);
    var facts = Assert.Single(viewModel.Messages);
    Assert.False(facts.HasContent);
    Assert.DoesNotContain(retained.Envelope, facts.Content, StringComparison.Ordinal);
    Assert.Contains(postId, facts.DisplayFeedPostFacts, StringComparison.Ordinal);
    Assert.Null(service.LastClientGeneratedChatBody);
    Assert.Single(runtime.Generated);
    Assert.Equal(1, factory.Connections.Single().ToolCalls);
    Assert.True(viewModel.PossibleToolSideEffect);

    Assert.False(await viewModel.TrySendAsync("Show the feed", TestContext.Current.CancellationToken));
    Assert.Single(runtime.Generated);
    Assert.Equal(1, factory.Connections.Single().ToolCalls);
    Assert.Equal(retained.Envelope, viewModel.RetainedWindowsToolResult!.Envelope);
    Assert.Equal(postId, Assert.Single(viewModel.RetainedWindowsToolResult.FeedPostReceipt!.PostIds));
    Assert.Contains(postId, Assert.Single(viewModel.Messages).DisplayFeedPostFacts, StringComparison.Ordinal);

    viewModel.NotifyDraftEdited("a different request");
    Assert.Null(viewModel.RetainedWindowsToolResult);
    Assert.Empty(viewModel.Messages);
  }

  [Fact]
  public async Task EmptyFeedReceiptSurvivesAnOverBudgetContinuation()
  {
    using var catalog = JsonDocument.Parse("""
        {"tools":[{"name":"read_feed","description":"Read","inputSchema":{"type":"object"}}]}
        """);
    using var structured = JsonDocument.Parse("""{"success":true,"results":[]}""");
    using var content = JsonDocument.Parse("""[{"type":"text","text":"none"}]""");
    var factory = new FakeFactory { Catalog = catalog.RootElement.Clone() };
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    factory.Connections.Single().ToolResult = new McpToolResult(
        content.RootElement.Clone(), structured.RootElement.Clone(), false);
    var runtime = new RecordingBuiltInRuntime
    {
      BuiltInText = """{"call":{"id":"call-1","name":"read_feed","arguments":{"option":"posts"}}}""",
      UsablePromptIndex = prompt => prompt.Contains("result:", StringComparison.Ordinal) ? 0 : (ulong)prompt.Length,
    };
    var provider = new WindowsSystemLanguageModelProvider(runtime, mcpSession: session);
    var viewModel = new ChatConversationViewModel(
        new FakeChatService(), new StatusOnlyResolver(provider.Status), provider);

    Assert.False(await viewModel.TrySendAsync("Show the feed", TestContext.Current.CancellationToken));

    var retained = viewModel.RetainedWindowsToolResult;
    Assert.NotNull(retained);
    Assert.Empty(retained.FeedPostReceipt!.PostIds);
    Assert.Contains("\"results\":[]", retained.Envelope, StringComparison.Ordinal);
    var facts = Assert.Single(viewModel.Messages);
    Assert.False(facts.HasContent);
    Assert.True(facts.HasFeedPostFacts);
    Assert.DoesNotContain(retained.Envelope, facts.Content, StringComparison.Ordinal);
    Assert.Equal(1, factory.Connections.Single().ToolCalls);
  }

  [Fact]
  public async Task RetainedWindowsToolResultStaysWithTheDispatchAccount()
  {
    using var catalog = JsonDocument.Parse("""{"tools":[]}""");
    var element = catalog.RootElement.Clone();
    var dispatch = new MemberMcpConnectedSnapshot("account-a", 1, element, CancellationToken.None, 4);
    var switched = new MemberMcpConnectedSnapshot("account-b", 2, element, CancellationToken.None, 5);
    var provider = new AccountSwitchingRetainedProvider(dispatch, switched);
    var viewModel = new ChatConversationViewModel(
        new FakeChatService(), new StatusOnlyResolver(provider.Status), provider);

    Assert.False(await viewModel.TrySendAsync("Show the feed", TestContext.Current.CancellationToken));

    Assert.Null(viewModel.RetainedWindowsToolResult);
    Assert.True(viewModel.PossibleToolSideEffect);
  }

  private sealed class AccountSwitchingRetainedProvider(
      MemberMcpConnectedSnapshot dispatch, MemberMcpConnectedSnapshot switched) : IMcpLocalChatProvider
  {
    private int snapshots;

    public string Id => LocalChatProviderIds.WindowsSystemLanguageModel;

    public ChatProviderStatus Status { get; } = new(
        ChatProviderKind.Local, "Windows local", true, "Ready.", LocalChatProviderIds.WindowsSystemLanguageModel);

    public MemberMcpConnectedSnapshot? ConnectedMcpSnapshot() => snapshots++ == 0 ? dispatch : switched;

    public bool IsCurrent(MemberMcpConnectedSnapshot snapshot) =>
        snapshot.AccountId == switched.AccountId && snapshot.AccountEpoch == switched.AccountEpoch ||
        snapshots == 1 && snapshot.AccountId == dispatch.AccountId && snapshot.AccountEpoch == dispatch.AccountEpoch;

    public bool MatchesAccount(string accountId) => accountId == switched.AccountId || snapshots == 1 && accountId == dispatch.AccountId;

    public bool MatchesAccountEpoch(MemberMcpConnectedSnapshot snapshot) => IsCurrent(snapshot);

    public Task<LocalChatGenerationResult> GenerateAssistantContentAsync(
        string message, IReadOnlyList<LocalLLMResponseInput> history, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The snapshot overload is required.");

    public Task<LocalChatGenerationResult> GenerateAssistantContentAsync(
        string message, IReadOnlyList<LocalLLMResponseInput> history, MemberMcpConnectedSnapshot? snapshot,
        CancellationToken cancellationToken = default) =>
        throw new WindowsBuiltInResultRetainedException(
            """{"structuredContent":null,"content":[],"isError":false}""", null, null, "call-1",
            dispatch.AccountId, dispatch.AccountEpoch);
  }

  private sealed class StatusOnlyResolver(ChatProviderStatus status) : IChatProviderResolver
  {
    public IReadOnlyList<ChatProviderStatus> GetProviderStatuses() => [status];
    public ChatProviderStatus GetDefaultProviderStatus() => status;
  }

  private sealed class RecordingBuiltInRuntime : IWindowsSystemLanguageModelRuntime, IWindowsBuiltInToolRuntime
  {
    public int PlainCalls { get; private set; }
    public List<string> Generated { get; } = [];
    public string BuiltInText { get; init; } = """{"answer":"kept"}""";
    public string Status { get; init; } = "Complete";
    public ulong? UsableIndex { get; init; }
    public Func<string, ulong>? UsablePromptIndex { get; init; }

    public WindowsSystemLanguageModelState GetState() => new(WindowsSystemLanguageModelReadiness.Ready);
    public Task EnsureReadyAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    public Task<string?> GenerateAssistantContentAsync(
        string message, IReadOnlyList<LocalLLMResponseInput> history, CancellationToken cancellationToken = default)
    {
      PlainCalls++;
      return Task.FromResult<string?>("plain");
    }
    public Task<WindowsBuiltInPromptObservation> ObservePromptAsync(string prompt, CancellationToken cancellationToken = default) =>
        Task.FromResult(new WindowsBuiltInPromptObservation(
            UsablePromptIndex?.Invoke(prompt) ?? UsableIndex ?? (ulong)prompt.Length, false, null));
    public Task<WindowsBuiltInModelTurn> GenerateBuiltInAsync(string prompt, CancellationToken cancellationToken = default)
    {
      Generated.Add(prompt);
      return Task.FromResult(new WindowsBuiltInModelTurn(Status, BuiltInText));
    }
  }
}
