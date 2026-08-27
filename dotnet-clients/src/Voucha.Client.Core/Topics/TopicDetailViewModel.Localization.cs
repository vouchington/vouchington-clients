using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Topics;

public sealed partial class TopicDetailViewModel
{
  private readonly IDisposable? localeSubscription;

  public string TopicFollowActionLabel => localization.Localize(IsFollowingTopic
      ? UiMessageKey.NativeDotnetDynamicUnfollowTopic
      : UiMessageKey.NativeDotnetDynamicFollowTopic);

  public string SourceFollowActionLabel => localization.Localize(IsFollowingSource
      ? UiMessageKey.NativeDotnetDynamicUnfollowSource
      : UiMessageKey.NativeDotnetDynamicFollowSource);

  public string TopicMuteActionLabel => localization.Localize(IsMutedTopic
      ? UiMessageKey.NativeDotnetDynamicUnmuteTopic
      : UiMessageKey.NativeDotnetDynamicMuteTopic);

  public string SourceMuteActionLabel => localization.Localize(IsMutedSource
      ? UiMessageKey.NativeDotnetDynamicUnmuteSource
      : UiMessageKey.NativeDotnetDynamicMuteSource);

  public void Dispose() => localeSubscription?.Dispose();

  public void OnUiLocaleChanged()
  {
    OnPropertyChanged(nameof(TopicFollowActionLabel));
    OnPropertyChanged(nameof(SourceFollowActionLabel));
    OnPropertyChanged(nameof(TopicMuteActionLabel));
    OnPropertyChanged(nameof(SourceMuteActionLabel));
    OnPropertyChanged(nameof(SourceCrawlsContinuationErrorMessage));
    OnPropertyChanged(nameof(SourceCrawlHistoryAccessErrorMessage));
    OnPropertyChanged(nameof(SourceCrawlHistoryRequestErrorMessage));
    OnPropertyChanged(nameof(LocalizedTopicType));
  }
}
