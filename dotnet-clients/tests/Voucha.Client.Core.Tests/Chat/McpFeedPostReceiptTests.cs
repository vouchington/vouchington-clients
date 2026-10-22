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
}
