using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.NewsFeeds;

public sealed record NewsFeedItem(
    string Id,
    string Title,
    string Source,
    string Summary,
    Uri? Link,
    DateTimeOffset PublishedAt,
    NewsFeedItemKind Kind = NewsFeedItemKind.Article,
    string? TopicId = null,
    bool IsFollowingSource = false,
    bool IsFollowingTopic = false,
    bool IsMutedSource = false,
    bool IsMutedTopic = false,
    bool IsSaved = false,
    bool IsHidden = false,
    bool IsRead = false,
    double? VoteScoreNet = null,
    int? VoteCountUp = null,
    int? VoteCountDown = null,
    Api.ElectionVoteChoice? CurrentVoteChoice = null,
    Uri? MediaUrl = null,
    Uri? ThumbnailUrl = null,
    string? ProtocolMediaType = null,
    string? VideoPlatform = null,
    string? VideoId = null,
    int? DurationSeconds = null,
    string? StoryId = null,
    int StoryPeerCount = 0,
    string? StoryPostId = null,
    bool IsStartingStoryDiscussion = false,
    IUiLocalization? Localization = null)
{
  public bool IsSource => Kind == NewsFeedItemKind.Source;

  public bool IsArticle => Kind == NewsFeedItemKind.Article;

  public bool IsMedia => Kind == NewsFeedItemKind.Media;

  public bool HasItemActions => IsArticle || IsMedia;

  public bool IsAudioMedia =>
      IsMedia &&
      IsProtocolMediaType(ProtocolMediaType, "audio");

  public bool IsVideoMedia =>
      IsMedia &&
      !IsAudioMedia &&
      (IsProtocolMediaType(ProtocolMediaType, "video") ||
       !string.IsNullOrWhiteSpace(VideoPlatform) ||
       !string.IsNullOrWhiteSpace(VideoId));

  public bool HasTopic => !string.IsNullOrWhiteSpace(TopicId);

  public bool HasVoteCounts => VoteCountUp is not null || VoteCountDown is not null;

  public bool HasDirectPlayback => MediaUrl is not null;

  public bool CanOpenExternally => Link is not null;

  public bool IsEmbedOnlyVideo => IsVideoMedia && !HasDirectPlayback;

  public bool HasExternalAudioFallback => IsAudioMedia && !HasDirectPlayback && CanOpenExternally;

  public bool HasMediaPlayback => IsMedia && (HasDirectPlayback || HasExternalAudioFallback);

  public bool CanStartStoryDiscussion =>
      IsArticle && StoryId is not null && StoryPeerCount > 0 && StoryPostId is null && !IsStartingStoryDiscussion;

  public bool CanOpenStoryDiscussion => IsArticle && !string.IsNullOrWhiteSpace(StoryPostId);

  internal static bool IsProtocolMediaType(string? mediaType, string mediaKind) =>
      mediaType?.Trim().StartsWith($"{mediaKind}/", StringComparison.OrdinalIgnoreCase) == true ||
      string.Equals(mediaType?.Trim(), mediaKind, StringComparison.OrdinalIgnoreCase);


  private IUiLocalization L => Localization ?? UiLocalization.English;

  public string LocalizedMediaType =>
      L.Resolve(UiTaxonomy.MediaType(ProtocolMediaType));

  public string SourceFollowActionLabel => L.Localize(IsFollowingSource ? UiMessageKey.NativeDotnetDynamicUnfollowSource : UiMessageKey.NativeDotnetDynamicFollowSource);

  public string TopicFollowActionLabel => L.Localize(IsFollowingTopic ? UiMessageKey.NativeDotnetDynamicUnfollowTopic : UiMessageKey.NativeDotnetDynamicFollowTopic);

  public string SourceMuteActionLabel => L.Localize(IsMutedSource ? UiMessageKey.NativeDotnetDynamicUnmuteSource : UiMessageKey.NativeDotnetDynamicMuteSource);

  public string TopicMuteActionLabel => L.Localize(IsMutedTopic ? UiMessageKey.NativeDotnetDynamicUnmuteTopic : UiMessageKey.NativeDotnetDynamicMuteTopic);

  public string SaveActionLabel => L.Localize(IsSaved ? UiMessageKey.NativeDotnetDynamicUnsave : UiMessageKey.NativeDotnetDynamicSave);

  public string HideActionLabel => L.Localize(IsHidden ? UiMessageKey.NativeDotnetDynamicUnhide : UiMessageKey.NativeDotnetDynamicHide);

  public string ReadActionLabel => L.Localize(IsRead ? UiMessageKey.NativeDotnetDynamicUnread : UiMessageKey.NativeDotnetDynamicRead);

  public string StoryDiscussionActionLabel => L.Localize(IsStartingStoryDiscussion ? UiMessageKey.NativeDotnetDynamicCreating : UiMessageKey.NativeDotnetDynamicDiscussStory);
}

public enum NewsFeedItemKind
{
  Article,
  Media,
  Source,
}
