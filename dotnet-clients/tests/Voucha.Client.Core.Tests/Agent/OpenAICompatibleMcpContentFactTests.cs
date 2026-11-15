using System.Text.Json;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  public static TheoryData<string> ContentFactCases =>
  [
    "native.mcp.read-my-preferences.email",
    "native.mcp.read-my-preferences.general",
    "native.mcp.read-my-profile.bio",
    "native.mcp.read-my-profile.links",
    "native.mcp.read-posts.hacker-news-search",
    "native.mcp.read-posts.hacker-news-details",
    "native.mcp.read-rss-item.hacker-news-list",
    "native.mcp.read-rss-item.hacker-news-details",
    "native.mcp.get-story.hacker-news-members",
  ];

  [Theory]
  [MemberData(nameof(ContentFactCases))]
  public async Task HttpProviderPublishesTypedFactsOwnedByTheTrustedCall(string id)
  {
    var fixture = HttpFactCase.Load(id);
    using var harness = await HttpFactHarness.CreateAsync(
        fixture, new ScriptedResponsesHandler(fixture.Call("call-1"), FinalAnswer));

    var result = await harness.GenerateAsync(TestContext.Current.CancellationToken);

    var receipt = Assert.Single(result.FactReceipts!);
    Assert.True(receipt.IsOwnedBy("account-a", "call-1"));
    var row = new ChatMessageRow("assistant-1", "assistant", result.AssistantContent, DateTimeOffset.UtcNow,
        Localization: UiLocalization.English, FactReceipts: result.FactReceipts);
    Assert.True(row.HasMcpFacts);
    foreach (var raw in new[] { "{", "}", "\"success\"", "structuredContent", "isError" })
      Assert.DoesNotContain(raw, row.DisplayMcpFacts, StringComparison.Ordinal);
    using var envelope = harness.Handler.ContinuationEnvelope("call-1");
    Assert.Equal(fixture.Structured.GetRawText(), envelope.RootElement.GetProperty("structuredContent").GetRawText());
  }

  [Fact]
  public async Task RefusedAndInvalidFactResultsPublishNoFactReceipts()
  {
    var fixture = HttpFactCase.Load("native.mcp.read-posts.hacker-news-search");
    using var refused = await HttpFactHarness.CreateAsync(
        fixture, new ScriptedResponsesHandler(fixture.Call("call-1"), FinalAnswer), isError: true);

    var result = await refused.GenerateAsync(TestContext.Current.CancellationToken);

    Assert.Null(result.FactReceipts);
    using var envelope = refused.Handler.ContinuationEnvelope("call-1");
    Assert.True(envelope.RootElement.GetProperty("isError").GetBoolean());
  }

  [Fact]
  public async Task CompletedFactStaysVisibleAndIsNotRedispatchedWhenContinuationFails()
  {
    var fixture = HttpFactCase.Load("native.mcp.read-my-profile.bio");
    using var harness = await HttpFactHarness.CreateAsync(
        fixture, new ScriptedResponsesHandler(fixture.Call("call-1"), FailedContinuation));
    var viewModel = new ChatConversationViewModel(
        new FakeChatService(), new StatusOnlyResolver(harness.Provider.Status), harness.Provider);

    Assert.False(await viewModel.TrySendAsync("Show my bio", TestContext.Current.CancellationToken));

    var retained = Assert.IsType<RetainedWindowsBuiltInToolResult>(viewModel.RetainedWindowsToolResult);
    Assert.Equal("call-1", retained.CallId);
    Assert.NotNull(retained.FactReceipt);
    var facts = Assert.Single(viewModel.Messages);
    Assert.False(facts.HasContent);
    Assert.True(facts.HasMcpFacts);
    Assert.DoesNotContain("{", facts.DisplayMcpFacts, StringComparison.Ordinal);
    Assert.Equal(1, harness.ToolCalls);
  }

  [Fact]
  public async Task AccountRoundTripBeforeContinuationDropsTheFact()
  {
    var fixture = HttpFactCase.Load("native.mcp.read-posts.hacker-news-search");
    HttpFactHarness? harness = null;
    var handler = new ScriptedResponsesHandler(fixture.Call("call-1"), FinalAnswer)
    {
      OnRequest = count =>
      {
        if (count != 2) return;
        harness!.Accounts.SetAccount("account-b");
        harness.Accounts.SetAccount("account-a");
      },
    };
    harness = await HttpFactHarness.CreateAsync(fixture, handler);
    using var disposable = harness;

    var error = await Assert.ThrowsAsync<PossibleMcpToolSideEffectException>(() =>
        harness.GenerateAsync(TestContext.Current.CancellationToken));

    Assert.IsNotType<WindowsBuiltInResultRetainedException>(error);
  }

  [Theory]
  [InlineData("native.mcp.read-rss-item.hacker-news-list")]
  [InlineData("native.mcp.get-story.hacker-news-members")]
  public async Task BuiltInProviderPublishesTheSameFactAsTheHttpProvider(string id)
  {
    var fixture = HttpFactCase.Load(id);
    var factory = new FakeFactory { Catalog = HttpFactCase.PublishedCatalog() };
    using var session = new MemberMcpSession(new FakeSessionStore("account-a"), new FakeStore(), factory);
    await session.ConnectAsync(TestContext.Current.CancellationToken);
    using var content = JsonDocument.Parse("[]");
    factory.Connections.Single().ToolResult = new McpToolResult(
        content.RootElement.Clone(), fixture.Structured.Clone(), false);
    var call = JsonSerializer.Serialize(new
    {
      call = new { id = "call-1", name = fixture.Tool, arguments = fixture.Arguments },
    });
    var runtime = new RecordingBuiltInRuntime
    {
      BuiltInText = call,
      ContinuationText = """{"answer":"Done"}""",
      UsablePromptIndex = prompt => (ulong)prompt.Length,
    };
    var provider = new WindowsSystemLanguageModelProvider(runtime, mcpSession: session);

    var result = await provider.GenerateAssistantContentAsync(
        "Read it", [], session.ConnectedSnapshot(), TestContext.Current.CancellationToken);

    Assert.Equal("Done", result.AssistantContent);
    var receipt = Assert.Single(result.FactReceipts!);
    Assert.True(receipt.IsOwnedBy("account-a", "call-1"));
    Assert.Equal(1, factory.Connections.Single().ToolCalls);
  }
}
