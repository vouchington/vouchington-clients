using System.Text.Json;
using Voucha.Client.Core.Agent;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  public static TheoryData<string> AccountFactCases =>
  [
    "native.mcp.get-my-financial-profile.null",
    "native.mcp.get-my-financial-profile.populated",
    "native.mcp.manage-bookmark.set",
    "native.mcp.manage-bookmark.remove",
    "native.mcp.search-copyright-notices-for-association.results",
    "native.mcp.list-repeat-infringer-accounts.accounts",
  ];

  [Theory]
  [MemberData(nameof(AccountFactCases))]
  public async Task HttpProviderPublishesAccountFactsOwnedByTheTrustedCall(string id)
  {
    var fixture = HttpFactCase.Load(id);
    using var harness = await HttpFactHarness.CreateAsync(
        fixture, new ScriptedResponsesHandler(fixture.Call("call-1"), FinalAnswer));

    var result = await harness.GenerateAsync(TestContext.Current.CancellationToken);

    var receipt = Assert.Single(result.FactReceipts!);
    Assert.True(receipt.IsOwnedBy("account-a", "call-1"));
    Assert.False(receipt.IsOwnedBy("account-b", "call-1"));
    var row = new ChatMessageRow("assistant-1", "assistant", result.AssistantContent, DateTimeOffset.UtcNow,
        Localization: UiLocalization.English, FactReceipts: result.FactReceipts);
    Assert.True(row.HasMcpFacts);
    foreach (var raw in new[]
        { "{", "}", "\"success\"", "structuredContent", "isError", "individual_id", "0198ffff", "7500000", "us_dmca" })
      Assert.DoesNotContain(raw, row.DisplayMcpFacts, StringComparison.Ordinal);
    // The complete validated envelope, not the typed fact, is what the model continues from.
    using var envelope = harness.Handler.ContinuationEnvelope("call-1");
    Assert.Equal(fixture.Structured.GetRawText(), envelope.RootElement.GetProperty("structuredContent").GetRawText());
    Assert.Equal(1, harness.ToolCalls);
  }

  [Theory]
  [MemberData(nameof(AccountFactCases))]
  public async Task RefusedAndInvalidAccountResultsPublishNoFacts(string id)
  {
    var fixture = HttpFactCase.Load(id);
    using var refused = await HttpFactHarness.CreateAsync(
        fixture, new ScriptedResponsesHandler(fixture.Call("call-1"), FinalAnswer), isError: true);
    Assert.Null((await refused.GenerateAsync(TestContext.Current.CancellationToken)).FactReceipts);

    using var unexpected = JsonDocument.Parse("""{"success":true,"unexpected":{"individual_id":"x"}}""");
    using var invalid = await HttpFactHarness.CreateAsync(
        fixture, new ScriptedResponsesHandler(fixture.Call("call-1"), FinalAnswer),
        structured: unexpected.RootElement);
    LocalChatGenerationResult? generated = null;
    await Record.ExceptionAsync(async () =>
        generated = await invalid.GenerateAsync(TestContext.Current.CancellationToken));
    Assert.Null(generated?.FactReceipts);
  }

  [Theory]
  [MemberData(nameof(AccountFactCases))]
  public async Task CanceledAccountToolCallPublishesNothing(string id)
  {
    var fixture = HttpFactCase.Load(id);
    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    using var harness = await HttpFactHarness.CreateAsync(
        fixture, new ScriptedResponsesHandler(fixture.Call("call-1"), FinalAnswer), holdToolCall: true);
    var generation = harness.GenerateAsync(cancellation.Token);
    await harness.Factory.Connections.Single().ToolEntered.Task;
    await cancellation.CancelAsync();

    var error = await Assert.ThrowsAnyAsync<Exception>(() => generation);

    Assert.IsNotType<WindowsBuiltInResultRetainedException>(error);
    Assert.Single(harness.Handler.Bodies);
  }

  [Theory]
  [MemberData(nameof(AccountFactCases))]
  public async Task AccountRoundTripBeforeContinuationDropsAccountFacts(string id)
  {
    var fixture = HttpFactCase.Load(id);
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
  [MemberData(nameof(AccountFactCases))]
  public async Task AccountFactStaysVisibleWhenContinuationFails(string id)
  {
    var fixture = HttpFactCase.Load(id);
    using var harness = await HttpFactHarness.CreateAsync(
        fixture, new ScriptedResponsesHandler(fixture.Call("call-1"), FailedContinuation));
    var viewModel = new ChatConversationViewModel(
        new FakeChatService(), new StatusOnlyResolver(harness.Provider.Status), harness.Provider);

    Assert.False(await viewModel.TrySendAsync("Show it", TestContext.Current.CancellationToken));

    var retained = Assert.IsType<RetainedWindowsBuiltInToolResult>(viewModel.RetainedWindowsToolResult);
    Assert.Equal("call-1", retained.CallId);
    Assert.NotNull(retained.FactReceipt);
    var facts = Assert.Single(viewModel.Messages);
    Assert.True(facts.HasMcpFacts);
    Assert.DoesNotContain("{", facts.DisplayMcpFacts, StringComparison.Ordinal);
    Assert.Equal(1, harness.ToolCalls);
  }

  [Theory]
  [MemberData(nameof(AccountFactCases))]
  public async Task BuiltInProviderPublishesAccountFactsLikeTheHttpProvider(string id)
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
    var expected = McpFactReceipt.FromValidatedResult(
        fixture.Tool, fixture.Arguments, fixture.Structured, "account-a", "call-1");
    Assert.Equal(
        McpFactReceipt.Display([expected!], UiLocalization.English),
        McpFactReceipt.Display([receipt], UiLocalization.English));
  }
}
