using System.Text.Json;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class McpFeedRssItemReceiptTests
{
  [Fact]
  public void CanonicalDeliveryRendersOnlyTheRssItemIdentifier()
  {
    using var fixtures = JsonDocument.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("mcp-results.json")));
    var row = fixtures.RootElement.GetProperty("cases").EnumerateArray().Single(item =>
        item.GetProperty("id").GetString() == "native.mcp.read-feed.rss-items");
    var receipt = McpFeedRssItemReceipt.FromValidatedResult(
        row.GetProperty("tool").GetString()!, row.GetProperty("arguments"),
        row.GetProperty("structuredContent"), "account-a", "call-1");
    Assert.NotNull(receipt);
    Assert.Equal(["0198ffff-0000-7000-8000-000000000002"], receipt.ItemIds);
    var message = new ChatMessageRow("assistant-1", "assistant", "Model answer", DateTimeOffset.UtcNow,
        Localization: UiLocalization.English, RssItemReceipts: [receipt]);
    Assert.True(message.HasRssItemFacts);
    Assert.Contains(UiLocalization.English.Localize(UiMessageKey.NativeTaxonomyListsRssFeedItem),
        message.DisplayRssItemFacts);
    Assert.Contains(UiLocalization.English.Format(
        UiMessageKey.NativeSwiftIntegrityRssItemId, ("id", UiText.ProtocolValue(receipt.ItemIds[0]))),
        message.DisplayRssItemFacts);
    Assert.DoesNotContain("shared_by_id", message.DisplayRssItemFacts);
    Assert.Null(McpFeedRssItemReceipt.FromValidatedResult(
        "read_feed", PostsArguments(), row.GetProperty("structuredContent"), "account-a", "call-1"));
  }

  [Fact]
  public void EmptyDeliveryUsesExistingNoResultsCopyAndRefreshesLocale()
  {
    using var arguments = JsonDocument.Parse("{\"option\":\"rss_items\"}");
    using var empty = JsonDocument.Parse("{\"success\":true,\"results\":[]}");
    var receipt = McpFeedRssItemReceipt.FromValidatedResult(
        "read_feed", arguments.RootElement, empty.RootElement, "account-a", "call-1");
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    var message = new ChatMessageRow("assistant-1", "assistant", "Model answer", DateTimeOffset.UtcNow,
        Localization: localization, RssItemReceipts: [receipt!]);
    Assert.Contains(localization.Localize(UiMessageKey.NativeSwiftRouteSurfaceNoResults),
        message.DisplayRssItemFacts);
    controller.ApplySavedLocale("fr");
    message.NotifyLocalizationChanged();
    Assert.Contains("Aucun résultat", message.DisplayRssItemFacts);
    Assert.Contains("Élément de flux RSS", message.DisplayRssItemFacts);
    Assert.DoesNotContain("No results", message.DisplayRssItemFacts);
  }

  [Fact]
  public void FailedOrMalformedDeliveryNeverBecomesFact()
  {
    using var arguments = JsonDocument.Parse("{\"option\":\"rss_items\"}");
    using var failed = JsonDocument.Parse("{\"success\":false,\"results\":[]}");
    using var malformed = JsonDocument.Parse("{\"success\":true,\"results\":[{\"entity_id\":\"unsafe\"}]}");
    Assert.Null(McpFeedRssItemReceipt.FromValidatedResult(
        "read_feed", arguments.RootElement, failed.RootElement, "account-a", "call-1"));
    Assert.Null(McpFeedRssItemReceipt.FromValidatedResult(
        "read_feed", arguments.RootElement, malformed.RootElement, "account-a", "call-1"));
  }

  private static JsonElement PostsArguments()
  {
    using var arguments = JsonDocument.Parse("{\"option\":\"posts\"}");
    return arguments.RootElement.Clone();
  }
}
