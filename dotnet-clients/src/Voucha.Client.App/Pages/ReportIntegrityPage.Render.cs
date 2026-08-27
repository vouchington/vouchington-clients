using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed partial class ReportIntegrityPage
{
  private void Render()
  {
    errorLabel.Text = viewModel.ErrorMessage;
    retryButton.IsVisible = viewModel.State == Voucha.Client.Core.Support.LoadState.Error;
    statusPicker.IsEnabled = !viewModel.IsLoading;
    loadMoreButton.IsVisible = viewModel.HasMore;
    loadMoreButton.IsEnabled = viewModel.CanLoadMore;
    applyingStatus = true;
    statusPicker.ItemsSource = viewModel.AvailableStatuses.Select(StatusLabel).ToArray();
    statusPicker.SelectedIndex = Array.IndexOf(
        viewModel.AvailableStatuses.ToArray(),
        viewModel.Status);
    applyingStatus = false;
    queue.Children.Clear();
    if (viewModel.IsLoading && viewModel.Items.Count == 0)
    {
      queue.Children.Add(new ActivityIndicator
      {
        IsRunning = true,
        AutomationId = "report-integrity-loading",
      });
      return;
    }
    if (viewModel.IsEmpty)
    {
      queue.Children.Add(UiCopy.Bind(
          new Label { AutomationId = "report-integrity-empty" }, Label.TextProperty,
          UiMessageKey.NativeSwiftIntegrityNoFlagsMessage));
      return;
    }
    foreach (var row in viewModel.Items) queue.Children.Add(FlagCard(row));
  }

  private View FlagCard(ReportIntegrityRow row)
  {
    var flag = row.Flag;
    var content = IntegrityPageViews.CardLines(
        IntegrityPageViews.FlagType(flag.FlagType),
        UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityFlagId), flag.Id,
        UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityCreated),
        UiCopy.FormatDateTime(flag.CreatedAt),
        UiCopy.Resolve(row.ReporterCountText),
        UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityNewAccountReporters),
        UiCopy.FormatPercent((decimal)row.NewAccountReporterPct),
        string.IsNullOrWhiteSpace(row.Evidence) ? string.Empty :
            UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityEvidence),
        row.Evidence);
    AddResolution(content, flag.Resolution, flag.ResolvedAt, flag.ResolvedById);
    content.Children.Insert(1, IntegrityPageViews.Entity(row.Entity));
    if (flag.Resolution is null && flag.ResolvedAt is null)
    {
      var dismiss = UiCopy.Bind(new Button
      {
        AutomationId = $"report-integrity-dismiss-{flag.Id}",
        IsEnabled = viewModel.CanDismiss(flag.Id),
      }, Button.TextProperty, UiMessageKey.NativeSwiftIntegrityDismiss);
      dismiss.Clicked += async (_, _) =>
          await RunActionAsync(token => viewModel.DismissAsync(flag.Id, token))
              .ConfigureAwait(true);
      var actions = new HorizontalStackLayout { Spacing = 8 };
      actions.Children.Add(dismiss);
      var penalize = UiCopy.Bind(new Button
      {
        AutomationId = $"report-integrity-penalize-{flag.Id}",
        IsEnabled = viewModel.CanPenalizeReporters(flag.Id),
      }, Button.TextProperty, UiMessageKey.NativeSwiftIntegrityPenalizeReportersAction);
      penalize.Clicked += (_, _) =>
      {
        if (!viewModel.CanPenalizeReporters(flag.Id)) return;
        confirmingPenaltyIds.Add(flag.Id);
        Render();
      };
      actions.Children.Add(penalize);
      if (confirmingPenaltyIds.Contains(flag.Id))
      {
        var confirm = UiCopy.Bind(new Button
        {
          AutomationId = $"report-integrity-penalize-confirm-{flag.Id}",
          IsEnabled = viewModel.CanPenalizeReporters(flag.Id),
        }, Button.TextProperty, UiMessageKey.NativeSwiftIntegrityConfirmAction);
        confirm.Clicked += async (_, _) =>
        {
          confirmingPenaltyIds.Remove(flag.Id);
          await RunActionAsync(token => viewModel.PenalizeReportersAsync(flag.Id, token))
              .ConfigureAwait(true);
        };
        var cancel = UiCopy.Bind(new Button
        {
          AutomationId = $"report-integrity-penalize-cancel-{flag.Id}",
        }, Button.TextProperty, UiMessageKey.NativeSwiftIntegrityCancel);
        cancel.Clicked += (_, _) =>
        {
          confirmingPenaltyIds.Remove(flag.Id);
          Render();
        };
        actions.Children.Add(confirm);
        actions.Children.Add(cancel);
      }
      content.Children.Add(actions);
    }
    if (viewModel.PenalizedUserCount(flag.Id) is { } count)
      content.Children.Add(new Label { Text = UiCopy.Format(
          UiMessageKey.NativeSwiftIntegrityPenalizedReporters, ("count", count)) });
    if (viewModel.ActionError(flag.Id) is { } error)
      content.Children.Add(new Label { Text = error, TextColor = Colors.IndianRed });
    if (viewModel.NeedsReconciliation(flag.Id))
    {
      var reconcile = UiCopy.Bind(new Button
      {
        AutomationId = $"report-integrity-reconcile-{flag.Id}",
      }, Button.TextProperty, UiMessageKey.NativeSwiftIntegrityReconcile);
      reconcile.Clicked += async (_, _) =>
          await viewModel.ReconcileAsync(flag.Id).ConfigureAwait(true);
      content.Children.Add(reconcile);
    }
    return IntegrityPageViews.Card(content, $"report-integrity-flag-{flag.Id}");
  }

  private static void AddResolution(
      VerticalStackLayout content, string? resolution, DateTimeOffset? at, string? by)
  {
    if (resolution is null && at is null && by is null)
    {
      content.Children.Add(new Label { Text = UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityPending) });
      return;
    }
    if (resolution is not null) IntegrityPageViews.AddPair(
        content, UiMessageKey.NativeSwiftIntegrityResolution,
        IntegrityPageViews.Resolution(resolution));
    if (at is not null) IntegrityPageViews.AddPair(
        content, UiMessageKey.NativeSwiftIntegrityResolvedAt, UiCopy.FormatDateTime(at.Value));
    if (by is not null) IntegrityPageViews.AddPair(
        content, UiMessageKey.NativeSwiftIntegrityResolvedBy, by);
  }
}
