using System.Text.Json;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Chat;
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

  private sealed class RecordingBuiltInRuntime : IWindowsSystemLanguageModelRuntime, IWindowsBuiltInToolRuntime
  {
    public int PlainCalls { get; private set; }
    public List<string> Generated { get; } = [];
    public string BuiltInText { get; init; } = """{"answer":"kept"}""";
    public string Status { get; init; } = "Complete";
    public ulong? UsableIndex { get; init; }

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
        Task.FromResult(new WindowsBuiltInPromptObservation(UsableIndex ?? (ulong)prompt.Length, false, null));
    public Task<WindowsBuiltInModelTurn> GenerateBuiltInAsync(string prompt, CancellationToken cancellationToken = default)
    {
      Generated.Add(prompt);
      return Task.FromResult(new WindowsBuiltInModelTurn(Status, BuiltInText));
    }
  }
}
