using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

internal static class ModerationDisputeCardPresentation
{
  public static string Heading(ModerationDispute dispute) =>
      $"{UiCopy.Localize(UiMessageKey.NativeSwiftReviewDisputesReviewDispute)} · {dispute.Id}";

  public static string Context(ModerationDispute dispute)
  {
    var staff = dispute.StaffContext;
    var disputant = staff?.Disputant.VerifiedDisplayName;
    if (string.IsNullOrWhiteSpace(disputant)) disputant = staff?.Disputant.Username;
    if (string.IsNullOrWhiteSpace(disputant)) disputant = staff?.Disputant.Id;
    if (string.IsNullOrWhiteSpace(disputant)) disputant = dispute.DisputantUserId;
    var post = staff?.Review.Post.Title;
    if (string.IsNullOrWhiteSpace(post)) post = dispute.PostId;
    var topic = staff?.Review.Topic?.Name;
    if (string.IsNullOrWhiteSpace(topic)) topic = dispute.TopicId;
    var rating = staff is null
        ? UiCopy.Localize(UiMessageKey.NativeSwiftReviewDisputesNotAvailable)
        : staff.Review.Rating.ToString();
    return string.Join(
        Environment.NewLine,
        UiCopy.Format(
            UiMessageKey.NativeSwiftReviewDisputesByActor,
            ("actor", disputant ?? UiCopy.Localize(
                UiMessageKey.NativeSwiftReviewDisputesNotAvailable))),
        post ?? UiCopy.Localize(UiMessageKey.NativeSwiftReviewDisputesNotAvailable),
        Labeled(
            UiMessageKey.NativeSwiftReviewDisputesTopic,
            topic ?? UiCopy.Localize(
                UiMessageKey.NativeSwiftReviewDisputesNotAvailable)),
        UiCopy.Format(UiMessageKey.NativeSwiftReviewDisputesRating, ("rating", rating)));
  }

  public static string Claim(ModerationDispute dispute) =>
      Labeled(
          UiMessageKey.NativeSwiftReviewDisputesClaim,
          dispute.ClaimText ??
              UiCopy.Localize(UiMessageKey.NativeSwiftReviewDisputesNotAvailable));

  public static string Recommendation(ModerationDispute dispute) =>
      dispute.RecommendedAction is { Length: > 0 } recommendation
          ? UiCopy.Format(
              UiMessageKey.NativeSwiftReviewDisputesAiRecommendation,
              ("recommendation", ActionLabel(recommendation)))
          : string.Empty;

  public static string Lifecycle(ModerationDispute dispute)
  {
    var values = new List<string>();
    Add(values, UiMessageKey.NativeSwiftReviewDisputesCreated, dispute.CreatedAt);
    Add(values, UiMessageKey.NativeSwiftReviewDisputesUpdated, dispute.UpdatedAt);
    Add(values, UiMessageKey.NativeSwiftReviewDisputesDrafted, dispute.DraftedAt);
    Add(values, UiMessageKey.NativeSwiftReviewDisputesEdited, dispute.EditedAt);
    Add(values, UiMessageKey.NativeSwiftReviewDisputesApproved, dispute.ApprovedAt);
    Add(values, UiMessageKey.NativeSwiftReviewDisputesDelivered, dispute.SentAt);
    Add(values, UiMessageKey.NativeSwiftReviewDisputesResolved, dispute.ResolvedAt);
    if (dispute.ResolutionAction is { Length: > 0 } action)
      values.Add(UiCopy.Format(
          UiMessageKey.NativeSwiftReviewDisputesResolution,
          ("action", ActionLabel(action))));
    if (dispute.IsOverdue == true)
      values.Add(UiCopy.Localize(UiMessageKey.NativeSwiftReviewDisputesOverdue));
    return string.Join(Environment.NewLine, values);
  }

  public static string Labeled(UiMessageKey key, string value) =>
      $"{UiCopy.Localize(key)}{Environment.NewLine}{value}";

  private static string ActionLabel(string action) =>
      action.ToLowerInvariant() switch
      {
        "no_action" => UiCopy.Localize(
            UiMessageKey.NativeTaxonomyModerationNoAction),
        "remove" => UiCopy.Localize(
            UiMessageKey.NativeTaxonomyModerationRemove),
        "annotate" => UiCopy.Localize(
            UiMessageKey.NativeTaxonomyModerationAnnotate),
        "dismiss" => UiCopy.Localize(
            UiMessageKey.NativeSwiftReviewDisputesDismiss),
        _ => UiCopy.Resolve(UiText.ProtocolValue(action)),
      };

  private static void Add(
      ICollection<string> values,
      UiMessageKey key,
      DateTimeOffset? date)
  {
    if (date is not { } instant) return;
    values.Add(UiCopy.Format(key, ("date", UiCopy.FormatDateTime(instant))));
  }
}
