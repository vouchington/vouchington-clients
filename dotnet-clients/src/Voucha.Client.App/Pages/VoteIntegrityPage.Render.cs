using Voucha.Client.Core.Api;
using Voucha.Client.Core.ModerationIntegrity;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed partial class VoteIntegrityPage
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
        AutomationId = "vote-integrity-loading",
      });
      return;
    }
    if (viewModel.IsEmpty)
    {
      queue.Children.Add(UiCopy.Bind(
          new Label { AutomationId = "vote-integrity-empty" }, Label.TextProperty,
          UiMessageKey.NativeSwiftIntegrityNoFlagsMessage));
      return;
    }
    foreach (var row in viewModel.Items) queue.Children.Add(FlagCard(row));
  }

  private View FlagCard(VoteIntegrityRow row)
  {
    var flag = row.Flag;
    var content = IntegrityPageViews.CardLines(
        IntegrityPageViews.FlagType(flag.FlagType),
        UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityFlagId), flag.Id,
        UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityCreated),
        UiCopy.FormatDateTime(flag.CreatedAt),
        string.IsNullOrWhiteSpace(row.Evidence) ? string.Empty :
            UiCopy.Localize(UiMessageKey.NativeSwiftIntegrityEvidence),
        row.Evidence);
    AddResolution(content, flag.Resolution, flag.ResolvedAt, flag.ResolvedById);
    content.Children.Insert(1, IntegrityPageViews.Entity(row.Entity));
    if (flag.Resolution is null && flag.ResolvedAt is null) AddActions(content, flag.Id);
    if (viewModel.PenalizedUserCount(flag.Id) is { } count)
      content.Children.Add(new Label { Text = UiCopy.Format(
          UiMessageKey.NativeSwiftIntegrityPenalizedUsers, ("count", count)) });
    if (viewModel.ActionError(flag.Id) is { } error)
      content.Children.Add(new Label { Text = error, TextColor = Colors.IndianRed });
    if (viewModel.NeedsReconciliation(flag.Id))
    {
      var reconcile = UiCopy.Bind(new Button
      {
        AutomationId = $"vote-integrity-reconcile-{flag.Id}",
      }, Button.TextProperty, UiMessageKey.NativeSwiftIntegrityReconcile);
      reconcile.Clicked += async (_, _) =>
          await viewModel.ReconcileVotePenaltyAsync(flag.Id).ConfigureAwait(true);
      content.Children.Add(reconcile);
    }
    return IntegrityPageViews.Card(content, $"vote-integrity-flag-{flag.Id}");
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

  private void AddActions(VerticalStackLayout content, string flagId)
  {
    var actions = new HorizontalStackLayout { Spacing = 8 };
    actions.Children.Add(ResolutionButton(flagId, UiMessageKey.NativeSwiftIntegrityDismiss, VoteIntegrityResolution.Dismissed));
    actions.Children.Add(ResolutionButton(
        flagId, UiMessageKey.NativeSwiftIntegrityMarkPenalized, VoteIntegrityResolution.Penalized));
    actions.Children.Add(ResolutionButton(flagId, UiMessageKey.NativeSwiftIntegritySuspend, VoteIntegrityResolution.Suspended));
    var penalty = UiCopy.Bind(new Button
    {
      AutomationId = $"vote-integrity-penalty-{flagId}",
      IsEnabled = viewModel.CanApplyVoteRingPenalty(flagId),
    }, Button.TextProperty, UiMessageKey.NativeSwiftIntegrityApplyVotePenalty);
    penalty.Clicked += (_, _) =>
    {
      confirmingPenaltyIds.Add(flagId);
      Render();
    };
    actions.Children.Add(penalty);
    if (confirmingPenaltyIds.Contains(flagId))
    {
      var confirm = UiCopy.Bind(new Button
      {
        AutomationId = $"vote-integrity-penalty-confirm-{flagId}",
      }, Button.TextProperty, UiMessageKey.NativeSwiftIntegrityConfirmAction);
      confirm.Clicked += async (_, _) =>
      {
        confirmingPenaltyIds.Remove(flagId);
        await RunActionAsync(token => viewModel.ApplyVoteRingPenaltyAsync(flagId, token))
            .ConfigureAwait(true);
      };
      var cancel = UiCopy.Bind(new Button
      {
        AutomationId = $"vote-integrity-penalty-cancel-{flagId}",
      }, Button.TextProperty, UiMessageKey.NativeSwiftIntegrityCancel);
      cancel.Clicked += (_, _) =>
      {
        confirmingPenaltyIds.Remove(flagId);
        Render();
      };
      actions.Children.Add(confirm);
      actions.Children.Add(cancel);
    }
    content.Children.Add(actions);
  }

  private Button ResolutionButton(
      string flagId,
      UiMessageKey text,
      VoteIntegrityResolution resolution)
  {
    var button = UiCopy.Bind(new Button
    {
      AutomationId = $"vote-integrity-{resolution.ToString().ToLowerInvariant()}-{flagId}",
      IsEnabled = viewModel.CanResolve(flagId),
    }, Button.TextProperty, text);
    button.Clicked += async (_, _) =>
        await RunActionAsync(token => viewModel.ResolveAsync(flagId, resolution, token))
            .ConfigureAwait(true);
    return button;
  }
}
