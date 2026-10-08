using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed class CommunityAutomodFlagsView : VerticalStackLayout
{
  private readonly VerticalStackLayout rows = new() { Spacing = 8 };
  private readonly Label error = new() { TextColor = Colors.Red, AutomationId = "community-automod-flags-error" };
  private readonly Label notice = new() { AutomationId = "community-automod-flags-notice" };
  private readonly Button more = UiCopy.Bind(new Button { AutomationId = "community-automod-flags-more" }, Button.TextProperty, UiMessageKey.NativeDotnetDirectMessagesLoadMore);

  public CommunityAutomodFlagsView(Func<string, Task> dismiss, Func<string, Task> openPost)
  {
    DismissRequested = dismiss ?? throw new ArgumentNullException(nameof(dismiss));
    OpenPostRequested = openPost ?? throw new ArgumentNullException(nameof(openPost));
    Spacing = 8;
    Children.Add(UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.ExtractedCommunitiesCommunityAutomodFlagsPanelAutomodFlagsB5fc56db));
    Children.Add(UiCopy.Bind(new Label(), Label.TextProperty, UiMessageKey.ExtractedCommunitiesCommunityAutomodFlagsPanelPostsAutomodFlaggedForModeratorReview90005619));
    Children.Add(error);
    Children.Add(notice);
    Children.Add(rows);
    Children.Add(more);
    more.Clicked += OnMoreClicked;
  }

  public event EventHandler? LoadMoreRequested;
  public Func<string, Task> DismissRequested { get; set; }
  public Func<string, Task> OpenPostRequested { get; set; }

  public void Update(
      IReadOnlyList<CommunityModerationQueueEntry> flags,
      Func<string, bool> isDismissing,
      string? errorMessage,
      string? noticeMessage,
      bool hasMore,
      bool isLoading)
  {
    ArgumentNullException.ThrowIfNull(flags);
    ArgumentNullException.ThrowIfNull(isDismissing);
    rows.Children.Clear();
    foreach (var flag in flags)
    {
      if (CommunityAutomodFlagTarget.PostId(flag) is not { Length: > 0 } postId) continue;
      var card = new VerticalStackLayout { Spacing = 3, AutomationId = $"community-automod-flag:{flag.Id}" };
      card.Children.Add(new Label { Text = UiCopy.Resolve(UiText.UserContent(flag.TargetLabel ?? flag.EntityId)), FontAttributes = FontAttributes.Bold });
      if (flag.TargetContent?.Text is { Length: > 0 } content)
        card.Children.Add(new Label { Text = UiCopy.Resolve(UiText.UserContent(content)) });
      if ((flag.Reason ?? flag.FlaggedReason) is { Length: > 0 } reason)
        card.Children.Add(new Label { Text = UiCopy.Resolve(UiText.UserContent(reason)) });
      var open = UiCopy.Bind(new Button
      {
        AutomationId = $"community-automod-open:{postId}",
        IsEnabled = flag.TargetAvailable && !string.IsNullOrWhiteSpace(flag.TargetPath),
      }, Button.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesOpen);
      open.Clicked += async (_, _) =>
      {
        if (open.IsEnabled) await OpenPostRequested(postId).ConfigureAwait(true);
      };
      var dismissButton = UiCopy.Bind(new Button { AutomationId = $"community-automod-dismiss:{postId}", IsEnabled = !isDismissing(postId) }, Button.TextProperty, UiMessageKey.ExtractedCommunitiesCommunityAutomodFlagsPanelDismiss48845bff);
      dismissButton.Clicked += async (_, _) =>
      {
        if (!dismissButton.IsEnabled) return;
        dismissButton.IsEnabled = false;
        try { await DismissRequested(postId).ConfigureAwait(true); }
        finally { dismissButton.IsEnabled = true; }
      };
      card.Children.Add(new HorizontalStackLayout { Spacing = 8, Children = { open, dismissButton } });
      rows.Children.Add(card);
    }
    error.Text = errorMessage;
    error.IsVisible = !string.IsNullOrWhiteSpace(errorMessage);
    notice.Text = noticeMessage;
    notice.IsVisible = !string.IsNullOrWhiteSpace(noticeMessage);
    more.IsVisible = hasMore;
    more.IsEnabled = !isLoading;
  }

  private void OnMoreClicked(object? sender, EventArgs args) => LoadMoreRequested?.Invoke(this, EventArgs.Empty);
}
