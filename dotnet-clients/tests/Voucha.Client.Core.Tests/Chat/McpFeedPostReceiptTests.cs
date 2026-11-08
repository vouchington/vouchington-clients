using System.Text.Json;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class McpFeedPostReceiptTests
{
  [Fact]
  public void CanonicalDeliveryRendersOnlyThePostIdentifier()
  {
    using var fixtures = JsonDocument.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("mcp-results.json")));
    var row = fixtures.RootElement.GetProperty("cases").EnumerateArray().Single(item =>
        item.GetProperty("id").GetString() == "native.mcp.read-feed.posts");
    var receipt = McpFeedPostReceipt.FromValidatedResult(
        row.GetProperty("tool").GetString()!, row.GetProperty("arguments"),
        row.GetProperty("structuredContent"), "account-a", "call-1");
    Assert.NotNull(receipt);
    Assert.Equal(["0198ffff-0000-7000-8000-000000000002"], receipt.PostIds);
    var message = new ChatMessageRow("assistant-1", "assistant", "Model answer", DateTimeOffset.UtcNow,
        Localization: UiLocalization.English, FeedPostReceipts: [receipt]);
    Assert.True(message.HasFeedPostFacts);
    Assert.Contains(UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpCommunitiesPostId),
        message.DisplayFeedPostFacts);
    Assert.Contains(receipt.PostIds[0], message.DisplayFeedPostFacts);
    Assert.DoesNotContain("shared_by_id", message.DisplayFeedPostFacts);
  }

  [Fact]
  public void FailedOrMalformedDeliveryNeverBecomesFact()
  {
    using var arguments = JsonDocument.Parse("{\"option\":\"posts\"}");
    using var failed = JsonDocument.Parse("{\"success\":false,\"results\":[]}");
    using var malformed = JsonDocument.Parse("{\"success\":true,\"results\":[{\"entity_id\":\"unsafe\"}]}");
    Assert.Null(McpFeedPostReceipt.FromValidatedResult(
        "read_feed", arguments.RootElement, failed.RootElement, "account-a", "call-1"));
    Assert.Null(McpFeedPostReceipt.FromValidatedResult(
        "read_feed", arguments.RootElement, malformed.RootElement, "account-a", "call-1"));
  }

  [Fact]
  public void DistinctEntityIdsStayOrderedWhileRepeatsAndOversizeAreRejected()
  {
    const string first = "0198ffff-0000-7000-8000-000000000002";
    const string second = "0198ffff-0000-7000-8000-000000000003";
    using var arguments = JsonDocument.Parse("{\"option\":\"posts\"}");
    using var distinct = JsonDocument.Parse(
        "{\"success\":true,\"results\":[{\"entity_id\":\"" + first + "\"},{\"entity_id\":\"" + second + "\"}]}");
    var receipt = McpFeedPostReceipt.FromValidatedResult(
        "read_feed", arguments.RootElement, distinct.RootElement, "account-a", "call-1");
    Assert.NotNull(receipt);
    Assert.Equal([first, second], receipt.PostIds);

    using var repeated = JsonDocument.Parse(
        "{\"success\":true,\"results\":[{\"entity_id\":\"" + first + "\"},{\"entity_id\":\"" + first + "\"}]}");
    Assert.Null(McpFeedPostReceipt.FromValidatedResult(
        "read_feed", arguments.RootElement, repeated.RootElement, "account-a", "call-1"));

    var acceptedRows = string.Join(',', Enumerable.Range(0, 100).Select(EntityRow));
    using var accepted = JsonDocument.Parse("{\"success\":true,\"results\":[" + acceptedRows + "]}");
    var acceptedReceipt = McpFeedPostReceipt.FromValidatedResult(
        "read_feed", arguments.RootElement, accepted.RootElement, "account-a", "call-1");
    Assert.NotNull(acceptedReceipt);
    Assert.Equal(100, acceptedReceipt.PostIds.Count);
    Assert.Equal(EntityId(0), acceptedReceipt.PostIds[0]);
    Assert.Equal(EntityId(99), acceptedReceipt.PostIds[99]);

    var oversizedRows = acceptedRows + "," + EntityRow(100);
    using var oversized = JsonDocument.Parse("{\"success\":true,\"results\":[" + oversizedRows + "]}");
    Assert.Null(McpFeedPostReceipt.FromValidatedResult(
        "read_feed", arguments.RootElement, oversized.RootElement, "account-a", "call-1"));
  }

  private static string EntityId(int index) =>
      "0198ffff-0000-7000-8000-" + index.ToString("x12");

  private static string EntityRow(int index) =>
      "{\"entity_id\":\"" + EntityId(index) + "\"}";
}
