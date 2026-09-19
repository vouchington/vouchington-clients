using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Moderation;

public sealed record ReviewQueueRow(
    AdminReviewQueuePost Post,
    AdminReviewQueueClearanceStatus ClearanceStatus,
    IUiLocalization Localization,
    IReadOnlyList<ReviewQueueMediaRow> Media,
    bool IsExposureStale,
    bool IsInExposureCooldown,
    bool IsMediaRevealed,
    bool IsRevealInFlight,
    bool IsRevealAvailable,
    bool IsMutating = false,
    bool RequiresReconciliation = false)
{
  public string Id => Post.Id;
  public string DisplayTitle => Normalize(Post.Title) ?? Localization.Localize(UiMessageKey.NativeSwiftModerationReportsReviewQueueUntitledPost);
  public string UserContentPreview => Normalize(Post.MarkdownPreview) ?? Localization.Localize(UiMessageKey.NativeSwiftModerationReportsReviewQueueNoPreview);
  public AuthoredContentLanguage ContentLanguage => AuthoredContentLanguage.Resolve(Post.DeclaredLanguage, Post.LinguaRsDetectedLanguage);
  public string? TitleFlowDirection => Normalize(Post.Title) is null ? null : ContentLanguage.Direction?.ToString();
  public string? PreviewFlowDirection => Normalize(Post.MarkdownPreview) is null ? null : ContentLanguage.Direction?.ToString();
  public string Author => Normalize(Post.CreatedById) ?? Localization.Localize(UiMessageKey.NativeSwiftModerationReportsReviewQueueAnonymous);
  public string PostType => Post.PostType;
  public DateTimeOffset CreatedAt => Post.CreatedAt;
  public UiMessageKey DispositionKey => Post.ModerationSummary.Disposition switch
  {
    AdminModerationDisposition.Pass => UiMessageKey.NativeModerationSummaryDispositionPass,
    AdminModerationDisposition.Review => UiMessageKey.NativeModerationSummaryDispositionReview,
    AdminModerationDisposition.Reject => UiMessageKey.NativeModerationSummaryDispositionReject,
    AdminModerationDisposition.Incomplete => UiMessageKey.NativeModerationSummaryDispositionIncomplete,
    _ => UiMessageKey.NativeModerationSummaryDispositionNone,
  };
  public string ModerationTitlePresentation => Localization.Localize(UiMessageKey.NativeModerationSummaryTitle);
  public string DispositionPresentation => Localization.Localize(DispositionKey);
  public string FlaggedCategoriesPresentation => Localization.Format(
      UiMessageKey.NativeModerationSummaryEvidenceFlaggedCategories,
      ("count", Post.ModerationSummary.EvidenceSummary.FlaggedCategoryCount));
  public string SignalsPresentation => Localization.Format(
      UiMessageKey.NativeModerationSummaryEvidenceSignals,
      ("count", Post.ModerationSummary.EvidenceSummary.SignalCount));
  public string? RootContext => Post.RootId is null
      ? null
      : string.Join(" · ", new[] { Post.RootPostType, Post.RootSlug, Post.RootId }.Where(value => !string.IsNullOrWhiteSpace(value)));
  public bool CanMarkForReReview =>
      ClearanceStatus == AdminReviewQueueClearanceStatus.Rejected && CanAct;
  public bool CanAct => !IsMutating && !RequiresReconciliation;
  public bool HasMedia => Media.Count > 0;
  public bool RequiresMediaReveal => HasMedia && Post.MediaReveal.RequiresReveal;
  public bool ShowMedia => HasMedia && (!RequiresMediaReveal || IsMediaRevealed);
  public bool ShowRevealGate => RequiresMediaReveal && !IsMediaRevealed;
  public bool ShowExposureStale => RequiresMediaReveal && IsExposureStale;
  public bool ShowExposureCooldown =>
      ShowRevealGate && !IsExposureStale && IsInExposureCooldown;
  public bool CanRevealMedia =>
      ShowRevealGate && !IsExposureStale && !IsInExposureCooldown && IsRevealAvailable;

  public string AuthorPresentation => Localization.Format(
      UiMessageKey.NativeSwiftModerationReportsReviewQueueAuthor,
      ("author", Author));
  public string PostTypePresentation => Localization.Format(
      UiMessageKey.NativeSwiftModerationReportsReviewQueuePostType,
      ("postType", PostType));
  public string? RootContextPresentation => RootContext is { } root
      ? Localization.Format(UiMessageKey.NativeSwiftModerationReportsReviewQueueRootThread, ("root", root))
      : null;
  public string CreatedPresentation => Localization.Format(
      UiMessageKey.NativeSwiftModerationReportsReviewQueueCreated,
      ("date", Localization.FormatDateTime(CreatedAt, TimeZoneInfo.Local)));
  private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
