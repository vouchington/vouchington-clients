using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class McpAccountFactReceiptTests
{
  private const string Id = "0198ffff-0000-7000-8000-000000000031";

  public static TheoryData<string> Families =>
  [
    "native.mcp.get-my-financial-profile.null",
    "native.mcp.get-my-financial-profile.populated",
    "native.mcp.manage-bookmark.set",
    "native.mcp.manage-bookmark.remove",
    "native.mcp.search-copyright-notices-for-association.results",
    "native.mcp.list-repeat-infringer-accounts.accounts",
  ];

  [Theory]
  [MemberData(nameof(Families))]
  public void FixtureBecomesATypedOwnedFactWithoutRawJsonOrIdentifiers(string id)
  {
    var row = Case(id);

    var receipt = McpFactReceipt.FromValidatedResult(row.Tool, row.Arguments, row.Structured, "account-a", "call-1");

    Assert.NotNull(receipt);
    Assert.True(receipt.IsOwnedBy("account-a", "call-1"));
    Assert.False(receipt.IsOwnedBy("account-b", "call-1"));
    Assert.False(receipt.IsOwnedBy("account-a", "call-2"));
    var text = McpFactReceipt.Display([receipt], UiLocalization.English)!;
    foreach (var raw in new[] { "{", "}", "\"success\"", "structuredContent", "individual_id", "_id", "us_dmca" })
      Assert.DoesNotContain(raw, text, StringComparison.Ordinal);
    Assert.DoesNotMatch(new Regex("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}"), text);
  }

  [Fact]
  public void FinancialProfileShowsOnlyTheUsersOwnFields()
  {
    var empty = Lines("native.mcp.get-my-financial-profile.null");
    var populated = Lines("native.mcp.get-my-financial-profile.populated");

    Assert.Equal(["No financial profile"], empty);
    Assert.Equal(3, populated.Count);
    Assert.Equal("Credit score range: 670-739", populated[0]);
    Assert.StartsWith("Stated income range: ", populated[1]);
    Assert.Contains("75,000", populated[1]);
    Assert.Contains("100,000", populated[1]);
    Assert.Equal("Years of credit history: 7", populated[2]);
    Assert.DoesNotContain(populated, line => line.Contains("Total credit limit", StringComparison.Ordinal));
  }

  [Fact]
  public void BookmarkFactsLabelPredicateAndEntityTypes()
  {
    Assert.Equal(["Bookmark set", "Follow · Topic"], Lines("native.mcp.manage-bookmark.set"));
    Assert.Equal(["Bookmark removed", "Follow · Topic"], Lines("native.mcp.manage-bookmark.remove"));
  }

  [Theory]
  [InlineData("save", "Save")]
  [InlineData("follow", "Follow")]
  [InlineData("mute", "Mute")]
  [InlineData("block", "Block")]
  public void EveryBookmarkPredicateHasALabel(string predicate, string label)
  {
    var receipt = Bookmark("set", predicate, "topic", "set");

    Assert.Equal(label + " · Topic", McpFactReceipt.Display([receipt!], UiLocalization.English)!.Split('\n')[1]);
  }

  [Theory]
  [InlineData("post", "Post")]
  [InlineData("topic", "Topic")]
  [InlineData("user", "User")]
  [InlineData("community", "Community")]
  [InlineData("rss_feed", "RSS feed")]
  [InlineData("rss_feed_item", "RSS feed item")]
  [InlineData("url", "URL")]
  [InlineData("url_hostname", "Hostname")]
  public void EveryBookmarkEntityTypeHasALabel(string type, string label)
  {
    var receipt = Bookmark("set", "save", type, "set");

    Assert.Equal("Save · " + label, McpFactReceipt.Display([receipt!], UiLocalization.English)!.Split('\n')[1]);
  }

  [Fact]
  public void UnknownBookmarkTokensAndUnsetResultsProduceNoFact()
  {
    Assert.Null(Bookmark("set", "pin", "topic", "set"));
    Assert.Null(Bookmark("set", "save", "widget", "set"));
    Assert.Null(Bookmark("set", "save", "topic", "skipped"));
    Assert.Null(Bookmark("remove", "pin", "topic", "set"));
    Assert.Null(Bookmark("remove", "save", "widget", "set"));
    Assert.Null(Receipt("manage_bookmark", """{"option":"remove"}""", """{"success":true}"""));
    Assert.Null(Receipt("manage_bookmark", """{"option":"set","arguments":{"predicate":"save"}}""", """{"success":true}"""));
    Assert.Null(Receipt("manage_bookmark", """{"option":"set","arguments":{"predicate":"save"}}""",
        """{"success":true,"results":[{"entity_type":"widget","entity":"x","status":"set"}]}"""));
  }

  [Fact]
  public void PartialSetResultsKeepOnlyTheTypesTheServerSet()
  {
    var receipt = Receipt("manage_bookmark", """{"option":"set","arguments":{"predicate":"save"}}""",
        """
        {"success":true,"results":[
          {"entity_type":"topic","entity":"a","status":"set"},
          {"entity_type":"topic","entity":"b","status":"set"},
          {"entity_type":"user","entity":"c","status":"error"},
          {"entity_type":"post","entity":"d","status":"set"}]}
        """);

    Assert.Equal("Save · Topic · Post", McpFactReceipt.Display([receipt!], UiLocalization.English)!.Split('\n')[1]);
  }

  [Fact]
  public void CopyrightNoticesRenderLabelsAndNoIdentifiers()
  {
    var lines = Lines("native.mcp.search-copyright-notices-for-association.results");

    Assert.Equal("Copyright notices", lines[0]);
    Assert.Equal("Claimant: Example Claimant", lines[1]);
    Assert.Equal("Jurisdiction: DMCA", lines[2]);
    Assert.StartsWith("Received ", lines[3]);
    Assert.Equal("Work description: Example work", lines[4]);
  }

  [Theory]
  [InlineData("""{"id":"%1","claimant_display_name":null,"jurisdiction":"uk","received_at":"2026-10-09T00:00:00.000Z","work_description":null}""")]
  [InlineData("""{"id":"nope","claimant_display_name":null,"jurisdiction":"us_dmca","received_at":"2026-10-09T00:00:00.000Z","work_description":null}""")]
  [InlineData("""{"id":"%1","claimant_display_name":null,"jurisdiction":"us_dmca","received_at":"yesterday","work_description":null}""")]
  [InlineData("""{"id":"%1","claimant_display_name":7,"jurisdiction":"us_dmca","received_at":"2026-10-09T00:00:00.000Z","work_description":null}""")]
  [InlineData("""{"id":"%1","claimant_display_name":null,"jurisdiction":"us_dmca","received_at":"2026-10-09T00:00:00.000Z","work_description":null},{"id":"%1","claimant_display_name":null,"jurisdiction":"us_dmca","received_at":"2026-10-09T00:00:00.000Z","work_description":null}""")]
  public void MalformedCopyrightRowsRejectTheWholeFact(string rows) =>
      Assert.Null(Receipt("search_copyright_notices_for_association", "{}",
          "{\"copyright_notices\":[" + rows.Replace("%1", Id) + "]}"));

  [Fact]
  public void CopyrightNoticesWithoutMatchesShowTheEmptyStateAndEuLabel()
  {
    var empty = Receipt("search_copyright_notices_for_association", "{}", """{"copyright_notices":[]}""");
    Assert.Equal(["Copyright notices", "No results"], McpFactReceipt.Display([empty!], UiLocalization.English)!.Split('\n'));
    var eu = Receipt("search_copyright_notices_for_association", "{}", $$"""
        {"copyright_notices":[{"id":"{{Id}}","claimant_display_name":" ","jurisdiction":"eu_dsa","received_at":"2026-10-09T00:00:00Z","work_description":""}]}
        """);
    var text = McpFactReceipt.Display([eu!], UiLocalization.English)!;
    Assert.Contains("Jurisdiction: DSA", text);
    Assert.DoesNotContain("Claimant", text);
    Assert.DoesNotContain("Work description", text);
  }

  [Fact]
  public void TooManyCopyrightNoticesRejectTheWholeFact()
  {
    var rows = string.Join(",", Enumerable.Range(0, 101).Select(_ => $$"""
        {"id":"{{Guid.NewGuid()}}","claimant_display_name":null,"jurisdiction":"us_dmca","received_at":"2026-10-09T00:00:00Z","work_description":null}
        """));

    Assert.Null(Receipt("search_copyright_notices_for_association", "{}", "{\"copyright_notices\":[" + rows + "]}"));
  }

  [Fact]
  public void RepeatInfringersRenderLabelsAndPickSingularOrPluralByCount()
  {
    var single = Lines("native.mcp.list-repeat-infringer-accounts.accounts");

    Assert.Equal(
        ["Repeat infringer accounts", "Username: example-account", "Operative: Yes", "Termination in effect: No",
          "Open review: Yes", "Incident: 1"], single);
    var second = "0198ffff-0000-7000-8000-000000000099";
    var plural = Receipt("list_repeat_infringer_accounts", "{}", $$"""
        {"copyright_repeat_infringer_accounts":[
          {{Account(Id, Id, null)}},{{Account(Id, second, "x")}}]}
        """);

    var lines = McpFactReceipt.Display([plural!], UiLocalization.English)!.Split('\n');
    Assert.Equal("Incidents: 2", lines[^1]);
    Assert.Equal("Username: x", lines[1]);
    Assert.Equal(6, lines.Length);
  }

  [Fact]
  public void MalformedRepeatInfringerRowsRejectTheWholeFact()
  {
    Assert.Null(Receipt("list_repeat_infringer_accounts", "{}", """{"copyright_repeat_infringer_accounts":[{}]}"""));
    Assert.Null(Receipt("list_repeat_infringer_accounts", "{}",
        $$"""{"copyright_repeat_infringer_accounts":[{{Account("nope", Id, null)}}]}"""));
    Assert.Null(Receipt("list_repeat_infringer_accounts", "{}",
        $$"""{"copyright_repeat_infringer_accounts":[{{Account(Id, "nope", null)}}]}"""));
    Assert.Null(Receipt("list_repeat_infringer_accounts", "{}", """{"success":false,"copyright_repeat_infringer_accounts":[]}"""));
    var empty = Receipt("list_repeat_infringer_accounts", "{}", """{"copyright_repeat_infringer_accounts":[]}""");
    Assert.Equal(["Repeat infringer accounts", "No results"],
        McpFactReceipt.Display([empty!], UiLocalization.English)!.Split('\n'));
    var rows = string.Join(",", Enumerable.Range(0, 501).Select(_ => Account(Id, Id, null)));
    Assert.Null(Receipt("list_repeat_infringer_accounts", "{}", "{\"copyright_repeat_infringer_accounts\":[" + rows + "]}"));
  }

  [Theory]
  [InlineData("""{"success":true,"result":{"financial_profile":{"credit_score_range":"999-999","stated_income_range":null,"total_credit_limit":null,"years_of_credit_history":null,"hard_inquiries_12m":null,"cards_opened_24m":null}}}""")]
  [InlineData("""{"success":true,"result":{"financial_profile":{"credit_score_range":null,"stated_income_range":null,"total_credit_limit":{"amount":1,"currency":"xyz"},"years_of_credit_history":null,"hard_inquiries_12m":null,"cards_opened_24m":null}}}""")]
  [InlineData("""{"success":true,"result":{"financial_profile":{"credit_score_range":null,"stated_income_range":{"minimum":{"amount":5,"currency":"usd"},"maximum":{"amount":1,"currency":"usd"}},"total_credit_limit":null,"years_of_credit_history":null,"hard_inquiries_12m":null,"cards_opened_24m":null}}}""")]
  [InlineData("""{"success":true,"result":{"financial_profile":{"credit_score_range":null,"stated_income_range":{"minimum":{"amount":1,"currency":"usd"},"maximum":{"amount":5,"currency":"eur"}},"total_credit_limit":null,"years_of_credit_history":null,"hard_inquiries_12m":null,"cards_opened_24m":null}}}""")]
  [InlineData("""{"success":true,"result":{"financial_profile":{"credit_score_range":null,"stated_income_range":"x","total_credit_limit":null,"years_of_credit_history":null,"hard_inquiries_12m":null,"cards_opened_24m":null}}}""")]
  [InlineData("""{"success":true,"result":{"financial_profile":{"credit_score_range":null,"stated_income_range":null,"total_credit_limit":null,"years_of_credit_history":-1,"hard_inquiries_12m":null,"cards_opened_24m":null}}}""")]
  [InlineData("""{"success":true,"result":{"financial_profile":{"credit_score_range":null,"stated_income_range":null,"total_credit_limit":null,"years_of_credit_history":"7","hard_inquiries_12m":null,"cards_opened_24m":null}}}""")]
  [InlineData("""{"success":true,"result":{"financial_profile":{"credit_score_range":null}}}""")]
  [InlineData("""{"success":true,"result":{"financial_profile":7}}""")]
  [InlineData("""{"success":true,"result":{}}""")]
  [InlineData("""{"success":true}""")]
  public void MalformedFinancialProfilesProduceNoFact(string content) =>
      Assert.Null(Receipt("get_my_financial_profile", "{}", content));

  [Fact]
  public void FullFinancialProfileShowsEveryNamedField()
  {
    var receipt = Receipt("get_my_financial_profile", "{}", """
        {"success":true,"result":{"financial_profile":{"individual_id":"secret","credit_score_range":"800-850",
        "stated_income_range":{"minimum":{"amount":100,"currency":"jpy"},"maximum":{"amount":200,"currency":"jpy"}},
        "total_credit_limit":{"amount":500000,"currency":"usd"},"currency":"usd",
        "years_of_credit_history":12,"hard_inquiries_12m":0,"cards_opened_24m":3,"updated_at":"2026-10-09T00:00:00.000Z"}}}
        """);

    var lines = McpFactReceipt.Display([receipt!], UiLocalization.English)!.Split('\n');

    Assert.Equal(6, lines.Length);
    Assert.Contains("Total credit limit: ", lines[2]);
    Assert.Equal("Years of credit history: 12", lines[3]);
    Assert.Equal("Hard inquiries (12 months): 0", lines[4]);
    Assert.Equal("Cards opened (24 months): 3", lines[5]);
    Assert.DoesNotContain("secret", string.Join("", lines));
  }

  [Fact]
  public void AllNullFinancialProfileUsesTheEmptyState()
  {
    var receipt = Receipt("get_my_financial_profile", "{}", """
        {"success":true,"result":{"financial_profile":{"credit_score_range":null,"stated_income_range":null,
        "total_credit_limit":null,"years_of_credit_history":null,"hard_inquiries_12m":null,"cards_opened_24m":null}}}
        """);

    Assert.Equal("No financial profile", McpFactReceipt.Display([receipt!], UiLocalization.English));
  }

  [Fact]
  public void EmailPreferencesNowShowModerationDaysAndGeneralShowsCountry()
  {
    var email = Lines("native.mcp.read-my-preferences.email");
    var general = Lines("native.mcp.read-my-preferences.general");

    Assert.Contains(email, line => line.StartsWith("Moderation days: ", StringComparison.Ordinal));
    Assert.Contains(general, line => line.StartsWith("Country: ", StringComparison.Ordinal));
    var node = JsonNode.Parse(Case("native.mcp.read-my-preferences.general").Structured.GetRawText())!;
    node["settings"]!["country"] = null;
    var none = Receipt("read_my_preferences", """{"option":"general"}""", node.ToJsonString());
    Assert.Contains("Country: No country preference", McpFactReceipt.Display([none!], UiLocalization.English));
  }

  private static string Account(string account, string incident, string? username) => $$"""
      {"account_user_id":"{{account}}","account_username":{{(username is null ? "null" : "\"" + username + "\"")}},
      "incident_id":"{{incident}}","is_operative":true,"open_review_id":null,"termination_in_effect":false}
      """;

  private static McpFactReceipt? Bookmark(string option, string predicate, string type, string status) => option == "set"
      ? Receipt("manage_bookmark", "{\"option\":\"set\",\"arguments\":{\"predicate\":\"" + predicate + "\"}}",
          "{\"success\":true,\"results\":[{\"entity_type\":\"" + type + "\",\"entity\":\"x\",\"status\":\"" +
          status + "\"}]}")
      : Receipt("manage_bookmark",
          "{\"option\":\"remove\",\"arguments\":{\"predicate\":\"" + predicate + "\",\"entity_type\":\"" + type +
          "\",\"entity_id\":\"x\"}}",
          """{"success":true}""");

  private static McpFactReceipt? Receipt(string tool, string arguments, string content)
  {
    using var args = JsonDocument.Parse(arguments);
    using var structured = JsonDocument.Parse(content);
    return McpFactReceipt.FromValidatedResult(tool, args.RootElement.Clone(), structured.RootElement.Clone(), "a", "c");
  }

  private static List<string> Lines(string id)
  {
    var row = Case(id);
    var receipt = McpFactReceipt.FromValidatedResult(row.Tool, row.Arguments, row.Structured, "account-a", "call-1");
    return [.. McpFactReceipt.Display([receipt!], UiLocalization.English)!.Split('\n')];
  }

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
