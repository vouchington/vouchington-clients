using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Topics;

namespace Voucha.Client.App.Pages;

public partial class TopicManagementPage
{
  private TopicMergeViewModel mergeViewModel = null!;
  private ISessionStore? mergeSessionStore;

  private void InitializeMerge(ISessionStore? sessionStore)
  {
    mergeSessionStore = sessionStore;
    mergeViewModel = new TopicMergeViewModel(topicsService,
        sessionStore is null ? null : () => sessionStore.Current.Identity?.Id);
    mergeViewModel.SetAccount(sessionStore?.Current.Identity?.Id);
    MergeCompletedLabel.BindingContext = mergeViewModel;
    mergeViewModel.PropertyChanged += (_, _) => Dispatcher.Dispatch(RenderMerge);
  }

  private void OnMergeSessionChanged(object? sender, SessionChangedEventArgs args) =>
      mergeViewModel.SetAccount(args.Snapshot.Identity?.Id);

  private async void OnMergeClicked(object? sender, EventArgs e) => await MergeAsync().ConfigureAwait(true);

  private async void OnCheckMergeStatusClicked(object? sender, EventArgs e) =>
      await CheckMergeStatusAsync().ConfigureAwait(true);

  internal async Task MergeAsync()
  {
    if (!TryTopicId(out var topicId) || string.IsNullOrWhiteSpace(MergeDestinationEntry.Text)) return;
    await mergeViewModel.SubmitAsync(topicId, MergeDestinationEntry.Text.Trim()).ConfigureAwait(true);
  }

  internal Task CheckMergeStatusAsync() => mergeViewModel.CheckAsync();

  private void RenderMerge()
  {
    StatusLabel.Text = mergeViewModel.Message is { } message ? UiCopy.Resolve(message) : string.Empty;
    MergeButton.IsEnabled = mergeViewModel.CanSubmit;
    CheckMergeStatusButton.IsEnabled = mergeViewModel.CanCheck;
    CheckMergeStatusButton.IsVisible = mergeViewModel.Operation is { } operation && TopicMergeProgress.CanCheckStatus(operation);
    MergeStatusLabel.Text = UiCopy.Resolve(UiText.ProtocolValue(mergeViewModel.Operation?.Status));
    MergeStageLabel.Text = UiCopy.Resolve(UiText.ProtocolValue(mergeViewModel.Operation?.Stage));
    MergeErrorLabel.Text = UiCopy.Resolve(UiText.ProtocolValue(mergeViewModel.Operation?.ErrorCode));
  }
}
