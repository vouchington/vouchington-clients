using System.Text.Json;
using Voucha.Client.Core.Chat;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Chat;

public sealed class McpFeedReferralLinkReceiptTests
{
  [Fact]
  public void CanonicalEmptyDeliveryRendersSharedEmptyCopy()
  {
    using var fixtures = JsonDocument.Parse(File.ReadAllText(FilamentsContractPaths.ApiFixture("mcp-results.json")));
    var row = fixtures.RootElement.GetProperty("cases").EnumerateArray().Single(item =>
        item.GetProperty("id").GetString() == "native.mcp.read-feed.referral-links");
    var receipt = McpFeedReferralLinkReceipt.FromValidatedResult(
        row.GetProperty("tool").GetString()!, row.GetProperty("arguments"),
        row.GetProperty("structuredContent"), "account-a", "call-1");
    var message = new ChatMessageRow("assistant-1", "assistant", "Model answer", DateTimeOffset.UtcNow,
        Localization: UiLocalization.English, ReferralLinkReceipts: [receipt!]);
    Assert.True(message.HasReferralLinkFacts);
    Assert.Contains(UiLocalization.English.Localize(UiMessageKey.NativeSwiftLandingPagesReferralLinks),
        message.DisplayReferralLinkFacts);
    Assert.Contains(UiLocalization.English.Localize(UiMessageKey.NativeSwiftRouteSurfaceNoResults),
        message.DisplayReferralLinkFacts);
    Assert.Null(McpFeedReferralLinkReceipt.FromValidatedResult(
        "read_feed", PostsArguments(), row.GetProperty("structuredContent"), "account-a", "call-1"));
  }

  [Fact]
  public void EmptyDeliveryRefreshesToFrenchNoResults()
  {
    using var arguments = JsonDocument.Parse("{\"option\":\"referral_links\"}");
    using var empty = JsonDocument.Parse("{\"success\":true,\"results\":[]}");
    var receipt = McpFeedReferralLinkReceipt.FromValidatedResult(
        "read_feed", arguments.RootElement, empty.RootElement, "account-a", "call-1");
    using var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    var message = new ChatMessageRow("assistant-1", "assistant", "Model answer", DateTimeOffset.UtcNow,
        Localization: localization, ReferralLinkReceipts: [receipt!]);
    controller.ApplySavedLocale("fr");
    message.NotifyLocalizationChanged();
    Assert.Contains("Liens de parrainage", message.DisplayReferralLinkFacts);
    Assert.Contains("Aucun résultat", message.DisplayReferralLinkFacts);
    Assert.DoesNotContain("No results", message.DisplayReferralLinkFacts);
  }

  [Fact]
  public void SyntheticPopulatedLinkRendersProgramAndUrlOnly()
  {
    using var arguments = JsonDocument.Parse("{\"option\":\"referral_links\"}");
    using var payload = JsonDocument.Parse("""
        {"success":true,"results":[{"id":"link-1","user_id":"user-1","referral_program_id":"program-1","referral_program_name":"Example Card","referral_program_slug":"example-card","url":"https://example.test/r/card","label":null}]}
        """);
    var receipt = McpFeedReferralLinkReceipt.FromValidatedResult(
        "read_feed", arguments.RootElement, payload.RootElement, "account-a", "call-1");
    var message = new ChatMessageRow("assistant-1", "assistant", "Model answer", DateTimeOffset.UtcNow,
        Localization: UiLocalization.English, ReferralLinkReceipts: [receipt!]);
    Assert.Contains(UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpDialogsReferralProgram),
        message.DisplayReferralLinkFacts);
    Assert.Contains("Example Card", message.DisplayReferralLinkFacts);
    Assert.Contains(UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpDialogsReferralUrl),
        message.DisplayReferralLinkFacts);
    Assert.Contains("https://example.test/r/card", message.DisplayReferralLinkFacts);
    Assert.DoesNotContain("user-1", message.DisplayReferralLinkFacts);
    Assert.DoesNotContain("program-1", message.DisplayReferralLinkFacts);
    Assert.Null(McpFeedReferralLinkReceipt.FromValidatedResult(
        "read_feed", arguments.RootElement, Failed(), "account-a", "call-1"));
    Assert.Null(McpFeedReferralLinkReceipt.FromValidatedResult(
        "read_feed", arguments.RootElement, Malformed(), "account-a", "call-1"));
  }

  private static JsonElement PostsArguments()
  {
    using var document = JsonDocument.Parse("{\"option\":\"posts\"}");
    return document.RootElement.Clone();
  }

  private static JsonElement Failed()
  {
    using var document = JsonDocument.Parse("{\"success\":false,\"results\":[]}");
    return document.RootElement.Clone();
  }

  private static JsonElement Malformed()
  {
    using var document = JsonDocument.Parse(
        "{\"success\":true,\"results\":[{\"id\":\"link-1\",\"url\":\"not a url\"}]}");
    return document.RootElement.Clone();
  }
}
