using Voucha.Client.Core.Localization;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Support;

namespace Voucha.Client.App.Pages;

public sealed class IntegrityPenaltyReviewSection : VerticalStackLayout
{
  private readonly IIntegrityPenaltyReviewModel queue;
  private readonly string prefix;
  private readonly Label error = new() { TextColor = Colors.IndianRed };
  private readonly VerticalStackLayout rows = new() { Spacing = 8 };
  private readonly Button refresh = new();
  private readonly Button loadMore = new();
  private readonly Label more = new();
  private CancellationTokenSource? cancellation;

  public IntegrityPenaltyReviewSection(IIntegrityPenaltyReviewModel queue, string domain)
  {
    this.queue = queue;
    prefix = $"{domain}-integrity-penalty-reviews";
    Spacing = 8;
    var title = new Label { FontAttributes = FontAttributes.Bold };
    title.SetDynamicResource(Label.TextProperty, UiMessageKey.ModerationPenaltyReviewsTitle.Value);
    var description = new Label();
    description.SetDynamicResource(Label.TextProperty, UiMessageKey.ModerationPenaltyReviewsDescription.Value);
    refresh.AutomationId = $"{prefix}-refresh";
    refresh.SetDynamicResource(Button.TextProperty, UiMessageKey.ModerationPenaltyReviewsRefresh.Value);
    refresh.Clicked += async (_, _) => await queue.RefreshAsync(Token()).ConfigureAwait(true);
    more.AutomationId = $"{prefix}-more";
    more.SetDynamicResource(Label.TextProperty, UiMessageKey.ModerationPenaltyReviewsMore.Value);
    loadMore.AutomationId = $"{prefix}-load-more";
    loadMore.SetDynamicResource(Button.TextProperty, UiMessageKey.NativeSwiftIntegrityLoadMore.Value);
    loadMore.Clicked += async (_, _) => await queue.LoadMoreAsync(Token()).ConfigureAwait(true);
    Children.Add(title);
    Children.Add(description);
    Children.Add(refresh);
    Children.Add(error);
    Children.Add(rows);
    Children.Add(more);
    Children.Add(loadMore);
    queue.PropertyChanged += (_, _) => Render();
    Render();
  }

  public Task LoadAsync() => queue.LoadAsync(Token());

  public void Cancel()
  {
    cancellation?.Cancel();
    cancellation?.Dispose();
    cancellation = null;
  }

  private void Render()
  {
    IsVisible = queue.IsAuthorized || queue.ErrorMessage is not null;
    refresh.IsVisible = queue.IsAuthorized;
    error.Text = queue.ErrorMessage;
    more.IsVisible = loadMore.IsVisible = queue.IsAuthorized && queue.HasMore;
    loadMore.IsEnabled = queue.CanLoadMore;
    rows.Children.Clear();
    if (!queue.IsAuthorized) return;
    if (queue.State == LoadState.Loading)
      rows.Children.Add(new ActivityIndicator { IsRunning = true, AutomationId = $"{prefix}-loading" });
    else if (queue.IsEmpty)
    {
      var empty = new Label { AutomationId = $"{prefix}-empty" };
      empty.SetDynamicResource(Label.TextProperty, UiMessageKey.ModerationPenaltyReviewsEmpty.Value);
      rows.Children.Add(empty);
    }
    else foreach (var row in queue.Rows) rows.Children.Add(Card(row));
  }

  private View Card(IntegrityPenaltyRow row)
  {
    var content = IntegrityPageViews.CardLines(
        UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityReason), LocalizedReason(row.Reason),
        UiCopy.Localize(UiMessageKey.ModerationPenaltyReviewsDue),
        row.ReviewDueAt is { } due ? UiCopy.FormatDateTime(due) : string.Empty);
    content.Children.Insert(2, IntegrityPageViews.Entity(new(row.UserLabel, row.UserId, $"/user/{row.UserId}")));
    if (queue.CanConfirm(row.Id))
      content.Children.Add(Action(row.Id, "confirm", UiMessageKey.ModerationPenaltyReviewsConfirm,
          () => queue.ConfirmAsync(row.Id, Token())));
    if (queue.CanRevoke(row.Id))
      content.Children.Add(Action(row.Id, "revoke", UiMessageKey.NativeSwiftIntegrityRevoke,
          () => queue.RevokeAsync(row.Id, Token())));
    if (queue.NeedsReconciliation(row.Id))
      content.Children.Add(Action(row.Id, "reconcile", UiMessageKey.NativeSwiftIntegrityReconcile,
          () => queue.ReconcileAsync(row.Id)));
    if (queue.ActionError(row.Id) is { } actionError)
      content.Children.Add(new Label { Text = actionError, TextColor = Colors.IndianRed });
    return IntegrityPageViews.Card(content, $"{prefix}-{row.Id}");
  }

  private Button Action(string id, string name, UiMessageKey key, Func<Task> run)
  {
    var button = new Button { AutomationId = $"{prefix}-{name}-{id}" };
    button.SetDynamicResource(Button.TextProperty, key.Value);
    button.Clicked += async (_, _) => await run().ConfigureAwait(true);
    return button;
  }

  private static string LocalizedReason(string reason) => reason switch
  {
    "mass_report_campaign" => UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityMassReportCampaign),
    "voting_ring" => UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityVotingRing),
    _ => reason,
  };

  private CancellationToken Token() => (cancellation ??= new()).Token;
}
