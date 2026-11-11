using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Topics;

namespace Voucha.Client.App.Pages;

public partial class TopicManagementPage
{
  private TopicMergeDetails? mergeOperation;
  private bool isCheckingMerge;

  private async void OnMergeClicked(object? sender, EventArgs e) => await MergeAsync().ConfigureAwait(true);

  private async void OnCheckMergeStatusClicked(object? sender, EventArgs e) =>
      await CheckMergeStatusAsync().ConfigureAwait(true);

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI merge failures are displayed inline.")]
  internal async Task MergeAsync()
  {
    if (mergeOperation is not null || !TryTopicId(out var topicId) ||
        string.IsNullOrWhiteSpace(MergeDestinationEntry.Text)) return;
    try
    {
      var response = await topicsService.MergeTopicAliasesAsync(topicId, MergeDestinationEntry.Text.Trim()).ConfigureAwait(true);
      // The producer queues merges (202): keep the operation, never claim the merge is done yet.
      ApplyMergeOperation(response.TopicMerge);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      StatusLabel.Text = ex.Message;
    }
  }

  /// <summary>One status read per call; there is no background polling.</summary>
  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI status failures are displayed inline.")]
  internal async Task CheckMergeStatusAsync()
  {
    if (mergeOperation is not { } operation || isCheckingMerge) return;
    isCheckingMerge = true;
    CheckMergeStatusButton.IsEnabled = false;
    try
    {
      var response = await topicsService
          .FetchTopicMergeStatusAsync(operation.SourceTopicId, operation.Id)
          .ConfigureAwait(true);
      ApplyMergeOperation(response.TopicMerge);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      StatusLabel.Text = UiCopy.Localize(UiMessageKey.SettingsTopicsMergeStatusFailed);
    }
    finally
    {
      isCheckingMerge = false;
      CheckMergeStatusButton.IsEnabled = true;
    }
  }

  private void ApplyMergeOperation(TopicMergeDetails operation)
  {
    mergeOperation = operation;
    StatusLabel.Text = UiCopy.Localize(TopicMergeProgress.MessageFor(operation));
    MergeButton.IsEnabled = false;
    CheckMergeStatusButton.IsVisible = TopicMergeProgress.CanCheckStatus(operation);
  }
}
