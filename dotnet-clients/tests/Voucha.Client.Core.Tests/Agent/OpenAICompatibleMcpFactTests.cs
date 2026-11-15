using System.Text.Json;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Chat;
using Xunit;

namespace Voucha.Client.Core.Tests.Agent;

public sealed partial class MemberMcpSessionTests
{
  [Theory]
  [InlineData("native.mcp.read-feed.posts")]
  [InlineData("native.mcp.read-feed.rss-items")]
  [InlineData("native.mcp.read-feed.referral-links")]
  public async Task HttpProviderRendersTheSameFactsAsBuiltInWithoutRawJson(string id)
  {
    var fixture = HttpFactCase.Load(id);
    using var harness = await HttpFactHarness.CreateAsync(
        fixture, new ScriptedResponsesHandler(fixture.Call("call-1"), FinalAnswer));

    var result = await harness.GenerateAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Done", result.AssistantContent);
    Assert.Equal("openai_compatible", result.ModelProvider);
    var post = McpFeedPostReceipt.FromValidatedResult(
        fixture.Tool, fixture.Arguments, fixture.Structured, "account-a", "call-1");
    var rss = McpFeedRssItemReceipt.FromValidatedResult(
        fixture.Tool, fixture.Arguments, fixture.Structured, "account-a", "call-1");
    var referral = McpFeedReferralLinkReceipt.FromValidatedResult(
        fixture.Tool, fixture.Arguments, fixture.Structured, "account-a", "call-1");
    AssertSame(post, result.FeedPostReceipts);
    AssertSame(rss, result.RssItemReceipts);
    AssertSame(referral, result.ReferralLinkReceipts);
    Assert.Equal(1, new object?[] { post, rss, referral }.Count(fact => fact is not null));
    var row = new ChatMessageRow("assistant-1", "assistant", result.AssistantContent, DateTimeOffset.UtcNow,
        Localization: UiLocalization.English, FeedPostReceipts: result.FeedPostReceipts,
        RssItemReceipts: result.RssItemReceipts, ReferralLinkReceipts: result.ReferralLinkReceipts);
    var rendered = row.DisplayFeedPostFacts ?? row.DisplayRssItemFacts ?? row.DisplayReferralLinkFacts;
    Assert.False(string.IsNullOrWhiteSpace(rendered));
    foreach (var raw in new[] { "{", "}", "\"success\"", "structuredContent", "isError" })
      Assert.DoesNotContain(raw, rendered, StringComparison.Ordinal);
    // The complete validated envelope, not the typed fact, is what the model continues from.
    using var envelope = harness.Handler.ContinuationEnvelope("call-1");
    Assert.Equal(fixture.Structured.GetRawText(), envelope.RootElement.GetProperty("structuredContent").GetRawText());
    Assert.Equal(1, harness.ToolCalls);
  }

  [Fact]
  public async Task RefusedResultKeepsItsEnvelopeButProducesNoFact()
  {
    var fixture = HttpFactCase.Load("native.mcp.read-feed.posts");
    using var harness = await HttpFactHarness.CreateAsync(
        fixture, new ScriptedResponsesHandler(fixture.Call("call-1"), FinalAnswer), isError: true);

    var result = await harness.GenerateAsync(TestContext.Current.CancellationToken);

    Assert.Null(result.FeedPostReceipts);
    Assert.Null(result.RssItemReceipts);
    Assert.Null(result.ReferralLinkReceipts);
    using var envelope = harness.Handler.ContinuationEnvelope("call-1");
    Assert.True(envelope.RootElement.GetProperty("isError").GetBoolean());
  }

  [Fact]
  public async Task InvalidResultNeverBecomesAFact()
  {
    var fixture = HttpFactCase.Load("native.mcp.read-feed.posts");
    using var invalid = JsonDocument.Parse("""{"success":true,"results":[{"entity_id":"untrusted text"}]}""");
    using var harness = await HttpFactHarness.CreateAsync(
        fixture, new ScriptedResponsesHandler(fixture.Call("call-1"), FinalAnswer), structured: invalid.RootElement);

    var outcome = await Record.ExceptionAsync(() => harness.GenerateAsync(TestContext.Current.CancellationToken));

    Assert.IsType<PossibleMcpToolSideEffectException>(outcome);
    Assert.Single(harness.Handler.Bodies);
  }

  [Fact]
  public async Task CanceledToolCallPublishesNothingAndIsNotRetained()
  {
    var fixture = HttpFactCase.Load("native.mcp.read-feed.posts");
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

  [Fact]
  public async Task AccountRoundTripBeforeContinuationDropsTheResult()
  {
    var fixture = HttpFactCase.Load("native.mcp.read-feed.posts");
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
    Assert.Equal(1, harness.ToolCalls);
  }

  [Fact]
  public async Task CompletedActionStaysVisibleAndIsNotRedispatchedWhenContinuationFails()
  {
    var fixture = HttpFactCase.Load("native.mcp.read-feed.referral-links");
    using var harness = await HttpFactHarness.CreateAsync(
        fixture, new ScriptedResponsesHandler(fixture.Call("call-1"), FailedContinuation));
    var service = new FakeChatService();
    var viewModel = new ChatConversationViewModel(
        service, new StatusOnlyResolver(harness.Provider.Status), harness.Provider);

    Assert.False(await viewModel.TrySendAsync("Show referral links", TestContext.Current.CancellationToken));

    var retained = Assert.IsType<RetainedWindowsBuiltInToolResult>(viewModel.RetainedWindowsToolResult);
    Assert.Equal("call-1", retained.CallId);
    Assert.Equal("account-a", retained.AccountId);
    Assert.NotNull(retained.ReferralLinkReceipt);
    using var envelope = JsonDocument.Parse(retained.Envelope);
    Assert.Equal(fixture.Structured.GetRawText(), envelope.RootElement.GetProperty("structuredContent").GetRawText());
    var facts = Assert.Single(viewModel.Messages);
    Assert.False(facts.HasContent);
    Assert.True(facts.HasReferralLinkFacts);
    Assert.DoesNotContain("{", facts.DisplayReferralLinkFacts, StringComparison.Ordinal);
    Assert.Null(service.LastClientGeneratedChatBody);
    Assert.True(viewModel.PossibleToolSideEffect);

    Assert.False(await viewModel.TrySendAsync("Show referral links", TestContext.Current.CancellationToken));
    Assert.Equal(2, harness.Handler.Bodies.Count);
    Assert.Equal(1, harness.ToolCalls);
    viewModel.NotifyDraftEdited("another request");
    Assert.Null(viewModel.RetainedWindowsToolResult);
    Assert.Empty(viewModel.Messages);
  }

  [Fact]
  public async Task ReusedCallIdIsRejectedAndTheFirstCompletedCallIsRetained()
  {
    var fixture = HttpFactCase.Load("native.mcp.read-feed.rss-items");
    using var harness = await HttpFactHarness.CreateAsync(
        fixture, new ScriptedResponsesHandler(fixture.Call("same"), fixture.Call("same")));

    var retained = await Assert.ThrowsAsync<WindowsBuiltInResultRetainedException>(() =>
        harness.GenerateAsync(TestContext.Current.CancellationToken));

    Assert.Equal("same", retained.CallId);
    Assert.NotNull(retained.RssItemReceipt);
    Assert.Equal("account-a", retained.AccountId);
    Assert.Equal(1, harness.ToolCalls);
  }

  [Fact]
  public async Task MultiCallTurnRetainsTheLastCompletedCall()
  {
    var fixture = HttpFactCase.Load("native.mcp.read-feed.posts");
    using var harness = await HttpFactHarness.CreateAsync(
        fixture, new ScriptedResponsesHandler(fixture.Call("first"), fixture.Call("second"), FailedContinuation));

    var retained = await Assert.ThrowsAsync<WindowsBuiltInResultRetainedException>(() =>
        harness.GenerateAsync(TestContext.Current.CancellationToken));

    Assert.Equal("second", retained.CallId);
    Assert.Equal("second", retained.Receipt!.CallId);
    Assert.Equal(2, harness.ToolCalls);
  }

  private static void AssertSame<T>(T? expected, IReadOnlyList<T>? actual) where T : class
  {
    if (expected is null) Assert.Null(actual);
    else Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(Assert.Single(actual!)));
  }
}
