using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class ModerationAppealCardPresentationTests
{
  private static readonly DateTimeOffset Instant =
      DateTimeOffset.Parse("2026-07-01T10:00:00Z");

  [Fact]
  public void TypedTargetContextsRenderEnrichedValuesWithoutRawIds()
  {
    using var scope = UiCopy.PushLocalization(UiLocalization.English);
    var contexts = new (ModerationAppealTargetContext Context, string[] Expected)[]
    {
      (new ModerationAppealWarningContext(
          "warning-rich", "Please keep replies civil",
          new("community-1", "Gardeners"), Instant),
        ["Warning appeal", "Decision: Please keep replies civil", "Community: Gardeners"]),
      (new ModerationAppealCommunityBanContext(
          "ban-rich", new("community-2", "Book Club"),
          "Repeated personal attacks", null, Instant),
        ["Community ban appeal", "Decision: Repeated personal attacks", "Community: Book Club"]),
      (new ModerationAppealPostRemovalContext(
          "post-rich", "A thoughtful title", ModerationAppealPostRemovalKind.Platform,
          null, "Violates the site rules", Instant),
        ["Platform post removal appeal", "Decision: Violates the site rules"]),
      (new ModerationAppealPostRemovalContext(
          "community-post-rich", "Local announcement",
          ModerationAppealPostRemovalKind.Community,
          new("community-3", "Neighborhood"), "Off topic", Instant),
        ["Community post removal appeal", "Decision: Off topic", "Community: Neighborhood"]),
      (new ModerationAppealSuspensionContext(
          "suspension-rich", "Repeated policy violations", Instant),
        ["Suspension appeal", "Decision: Repeated policy violations"]),
    };

    foreach (var (context, expected) in contexts)
    {
      var rendered = ModerationAppealCardPresentation.Context(Appeal(context));

      Assert.All(expected, value => Assert.Contains(value, rendered));
      Assert.DoesNotContain(context switch
      {
        ModerationAppealWarningContext warning => warning.Id,
        ModerationAppealCommunityBanContext ban => ban.Id,
        ModerationAppealPostRemovalContext removal => removal.Id,
        ModerationAppealSuspensionContext suspension => suspension.Id,
        _ => throw new InvalidOperationException(),
      }, rendered, StringComparison.Ordinal);
    }
  }

  [Fact]
  public void StaffContextUsesDisplayNamesAndOriginalDecisionDetails()
  {
    using var scope = UiCopy.PushLocalization(UiLocalization.English);
    var staff = new ModerationAppealStaffContext(
        new("appellant-id", "appellant-user", "Alicia Reviewer", null),
        new(
            new("moderator-id", "moderator-user", null, null),
            "Internal policy rationale"));
    var rendered = ModerationAppealCardPresentation.Context(Appeal(
        new ModerationAppealSuspensionContext(
            "suspension-rich", "Repeated policy violations", Instant),
        staff));

    Assert.Contains("Appellant Alicia Reviewer", rendered);
    Assert.Contains("Original decision reason: Internal policy rationale", rendered);
    Assert.Contains("Original decision by moderator-user", rendered);
    Assert.DoesNotContain("appellant-id", rendered, StringComparison.Ordinal);
    Assert.DoesNotContain("moderator-id", rendered, StringComparison.Ordinal);
  }

  [Fact]
  public void RawIdsRemainAsLegacyBackendFallback()
  {
    using var scope = UiCopy.PushLocalization(UiLocalization.English);
    var rendered = ModerationAppealCardPresentation.Context(Appeal(
        targetContext: null,
        warningId: "legacy-warning",
        appellantId: "legacy-appellant"));

    Assert.Contains(
        "Appellant legacy-appellant | Warning legacy-warning | Pending",
        rendered);
  }

  [Fact]
  public void EnrichedContextUsesTheActiveLocale()
  {
    using var controller = new UiLocaleController(new Languages());
    controller.ApplySavedLocale("es");
    using var scope = UiCopy.PushLocalization(new UiLocalization(controller));
    var staff = new ModerationAppealStaffContext(
        new("appellant-id", "alicia", "Alicia", null),
        new(null, "Motivo interno"));
    var rendered = ModerationAppealCardPresentation.Context(Appeal(
        new ModerationAppealPostRemovalContext(
            "post-rich", "Un título", ModerationAppealPostRemovalKind.Community,
            new("community-id", "Vecindario"), "Fuera de tema", Instant),
        staff));

    Assert.Contains("Apelación por eliminación de publicación de comunidad", rendered);
    Assert.Contains("Decisión: Fuera de tema", rendered);
    Assert.Contains("Comunidad: Vecindario", rendered);
    Assert.Contains("Motivo de la decisión original: Motivo interno", rendered);
  }

  private static ModerationAppeal Appeal(
      ModerationAppealTargetContext? targetContext,
      ModerationAppealStaffContext? staffContext = null,
      string? warningId = "legacy-warning",
      string? appellantId = "legacy-appellant") =>
      new(
          "appeal-1", "case-1", appellantId, warningId, null, null, null, null,
          "Please reconsider", ModerationAppealStatus.Pending, null, null, null, null,
          null, null, null, null, null, null, null, null, null, null, null, null, null,
          Instant, Instant, false, null, targetContext, staffContext);

  private sealed class Languages : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages => ["en"];
  }
}
