using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class McpFactReceiptTests
{
  public static TheoryData<string> Families =>
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
  [MemberData(nameof(Families))]
  public void CanonicalFixtureBecomesATypedOwnedFactWithoutRawJsonOrIdentifiers(string id)
  {
    var row = Case(id);

    var receipt = Receipt(row, row.Structured);

    Assert.NotNull(receipt);
    Assert.True(receipt.IsOwnedBy("account-a", "call-1"));
    Assert.False(receipt.IsOwnedBy("account-b", "call-1"));
    Assert.False(receipt.IsOwnedBy("account-a", "call-2"));
    var text = McpFactReceipt.Display([receipt], UiLocalization.English);
    Assert.False(string.IsNullOrWhiteSpace(text));
    foreach (var raw in new[] { "{", "}", "\"success\"", "structuredContent", "isError", "_id" })
      Assert.DoesNotContain(raw, text, StringComparison.Ordinal);
    Assert.DoesNotMatch(new Regex("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}"), text);
    Assert.Null(McpFactReceipt.Display(null, UiLocalization.English));
    Assert.Null(McpFactReceipt.Display([], UiLocalization.English));
  }

  [Theory]
  [MemberData(nameof(Families))]
  public void AResultWithoutTheExpectedShapeProducesNoFact(string id)
  {
    var row = Case(id);
    using var empty = JsonDocument.Parse("""{"success":true}""");
    using var wrong = JsonDocument.Parse("""{"success":true,"results":[7],"items":[7],"post":1,"profile":[]}""");
    var node = JsonNode.Parse(row.Structured.GetRawText())!;
    node["success"] = false;
    using var unsuccessful = JsonDocument.Parse(node.ToJsonString());
    using var array = JsonDocument.Parse("[]");

    Assert.Null(Receipt(row, empty.RootElement));
    Assert.Null(Receipt(row, wrong.RootElement));
    Assert.Null(Receipt(row, unsuccessful.RootElement));
    Assert.Null(Receipt(row, array.RootElement));
    Assert.Null(McpFactReceipt.FromValidatedResult(row.Tool, row.Arguments, row.Structured, "", "call-1"));
    Assert.Null(McpFactReceipt.FromValidatedResult(row.Tool, row.Arguments, row.Structured, "account-a", ""));
  }

  [Theory]
  [InlineData("read_posts", """{"option":"other"}""")]
  [InlineData("read_my_profile", """{"option":7}""")]
  [InlineData("manage_bookmark", """{"option":"set"}""")]
  [InlineData("get_my_financial_profile", """{}""")]
  [InlineData("search_copyright_notices_for_association", """{}""")]
  [InlineData("list_repeat_infringer_accounts", """{}""")]
  public void UnsupportedToolsAndOptionsNeverProduceFacts(string tool, string arguments)
  {
    using var args = JsonDocument.Parse(arguments);
    using var structured = JsonDocument.Parse("""{"success":true,"results":[],"items":[]}""");

    Assert.Null(McpFactReceipt.FromValidatedResult(tool, args.RootElement, structured.RootElement, "a", "c"));
    Assert.Null(McpFactReceipt.FromValidatedResult(
        tool, JsonDocument.Parse("[]").RootElement, structured.RootElement, "a", "c"));
  }

  [Fact]
  public void StoryWithNoItemsKeepsTheHeadingAndTheEmptyState()
  {
    using var args = JsonDocument.Parse("{}");
    using var structured = JsonDocument.Parse("""{"success":true,"items":[]}""");

    var receipt = McpFactReceipt.FromValidatedResult("get_story", args.RootElement, structured.RootElement, "a", "c");

    var lines = McpFactReceipt.Display([receipt!], UiLocalization.English)!.Split('\n');
    Assert.Equal(2, lines.Length);
  }

  [Theory]
  [InlineData("""{"id":"%1","title":"One"},{"id":"%1","title":"Two"}""")]
  [InlineData("""{"id":"not-a-guid","title":"One"}""")]
  [InlineData("""{"id":"%1","title":"  "}""")]
  [InlineData("""{"id":"%1","title":"One","hacker_news_references":{"story_count":-1,"comment_count":0}}""")]
  [InlineData("""{"id":"%1","title":"One","hacker_news_references":"nope"}""")]
  public void MalformedPostRowsRejectTheWholeFact(string rows)
  {
    var guid = "11111111-1111-1111-1111-111111111111";
    using var args = JsonDocument.Parse("""{"option":"search"}""");
    using var structured = JsonDocument.Parse(
        "{\"success\":true,\"results\":[" + rows.Replace("%1", guid) + "]}");

    Assert.Null(McpFactReceipt.FromValidatedResult(
        "read_posts", args.RootElement, structured.RootElement, "a", "c"));
  }

  [Fact]
  public void TooManyRowsRejectTheWholeFact()
  {
    var rows = string.Join(",", Enumerable.Range(0, 101).Select(index =>
        $$"""{"id":"{{Guid.NewGuid()}}","title":"T{{index}}"}"""));
    using var args = JsonDocument.Parse("""{"option":"search"}""");
    using var structured = JsonDocument.Parse("{\"success\":true,\"results\":[" + rows + "]}");

    Assert.Null(McpFactReceipt.FromValidatedResult(
        "read_posts", args.RootElement, structured.RootElement, "a", "c"));
  }

  [Fact]
  public void RssRowsNeedAFeedTitleAndPostsDoNot()
  {
    var guid = "11111111-1111-1111-1111-111111111111";
    using var rss = JsonDocument.Parse("{\"option\":\"list\"}");
    using var missing = JsonDocument.Parse(
        $$"""{"success":true,"results":[{"id":"{{guid}}","title":"One"}]}""");
    using var present = JsonDocument.Parse(
        $$"""{"success":true,"results":[{"id":"{{guid}}","title":"One","rss_feed_title":"Feed"}]}""");

    Assert.Null(McpFactReceipt.FromValidatedResult("read_rss_feed_item", rss.RootElement, missing.RootElement, "a", "c"));
    var receipt = McpFactReceipt.FromValidatedResult(
        "read_rss_feed_item", rss.RootElement, present.RootElement, "a", "c");
    Assert.Contains("Feed", McpFactReceipt.Display([receipt!], UiLocalization.English));
  }

  [Theory]
  [InlineData("""{"id":"1","link_type":"unknown","name":"x"}""")]
  [InlineData("""{"id":"","link_type":"github","name":"x"}""")]
  [InlineData("""{"id":"1","link_type":"github","name":7}""")]
  [InlineData("""{"id":"1","link_type":"github"}""")]
  public void MalformedOrEmptyProfileLinksProduceNoFact(string row)
  {
    using var args = JsonDocument.Parse("""{"option":"links"}""");
    using var structured = JsonDocument.Parse("{\"success\":true,\"results\":[" + row + "]}");

    Assert.Null(McpFactReceipt.FromValidatedResult(
        "read_my_profile", args.RootElement, structured.RootElement, "a", "c"));
  }

  [Fact]
  public void PreferencesWithAnUnknownOptionOrBadDaysProduceNoFact()
  {
    var row = Case("native.mcp.read-my-preferences.email");
    var general = Case("native.mcp.read-my-preferences.general");
    var badDays = JsonNode.Parse(row.Structured.GetRawText())!;
    badDays["email_preferences"]!["moderation_email_days_of_week"] = new JsonArray(9);
    var badAudience = JsonNode.Parse(general.Structured.GetRawText())!;
    badAudience["settings"]!["follows_visibility"] = "zz";

    using var days = JsonDocument.Parse(badDays.ToJsonString());
    using var audience = JsonDocument.Parse(badAudience.ToJsonString());

    Assert.Null(Receipt(row, days.RootElement));
    Assert.Null(Receipt(general, audience.RootElement));
  }

  private static McpFactReceipt? Receipt(CaseRow row, JsonElement structured) =>
      McpFactReceipt.FromValidatedResult(row.Tool, row.Arguments, structured, "account-a", "call-1");

  private static CaseRow Case(string id)
  {
    using var fixtures = JsonDocument.Parse(File.ReadAllText(ContractPaths.ApiFixture("mcp-results.json")));
    var item = fixtures.RootElement.GetProperty("cases").EnumerateArray()
        .Single(row => row.GetProperty("id").GetString() == id);
    return new(item.GetProperty("tool").GetString()!, item.GetProperty("arguments").Clone(),
        item.GetProperty("structuredContent").Clone());
  }

  private sealed record CaseRow(string Tool, JsonElement Arguments, JsonElement Structured);
}
