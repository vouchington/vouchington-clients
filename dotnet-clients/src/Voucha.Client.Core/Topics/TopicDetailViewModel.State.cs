using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Topics;

public sealed partial class TopicDetailViewModel
{
  public Topic? Topic
  {
    get => topic;
    private set
    {
      if (!SetProperty(ref topic, value)) return;
      OnPropertyChanged(nameof(CanFollowSource));
      OnPropertyChanged(nameof(CanMuteSource));
      OnPropertyChanged(nameof(CanViewSourceCrawlHistory));
      OnPropertyChanged(nameof(CanMuteTopic));
      OnPropertyChanged(nameof(LocalizedTopicType));
      OnPropertyChanged(nameof(LocalizedProvenanceLabel));
      OnPropertyChanged(nameof(HasProvenance));
    }
  }

  public TopicElection? TopicElection
  {
    get => topicElection;
    private set => SetProperty(ref topicElection, value);
  }

  public ElectionVote? ElectionVote
  {
    get => electionVote;
    private set => SetProperty(ref electionVote, value);
  }

  public bool IsFollowingTopic
  {
    get => isFollowingTopic;
    private set
    {
      if (SetProperty(ref isFollowingTopic, value)) OnPropertyChanged(nameof(TopicFollowActionLabel));
    }
  }

  public bool IsFollowingSource
  {
    get => isFollowingSource;
    private set
    {
      if (SetProperty(ref isFollowingSource, value)) OnPropertyChanged(nameof(SourceFollowActionLabel));
    }
  }

  public bool IsMutedTopic
  {
    get => isMutedTopic;
    private set
    {
      if (SetProperty(ref isMutedTopic, value)) OnPropertyChanged(nameof(TopicMuteActionLabel));
    }
  }

  public bool IsMutedSource
  {
    get => isMutedSource;
    private set
    {
      if (SetProperty(ref isMutedSource, value)) OnPropertyChanged(nameof(SourceMuteActionLabel));
    }
  }

  public bool CanFollowSource => Topic?.TopicType == "rss_feed" && !string.IsNullOrWhiteSpace(SourceRssFeedId);

  public string LocalizedTopicType => localization.Resolve(UiTaxonomy.TopicType(Topic?.TopicType));

  public bool CanMuteSource => CanFollowSource;

  public bool CanMuteTopic => Topic is not null;

  public string? SourceRssFeedId
  {
    get => sourceRssFeedId;
    private set
    {
      if (!SetProperty(ref sourceRssFeedId, value)) return;
      OnPropertyChanged(nameof(CanFollowSource));
      OnPropertyChanged(nameof(CanMuteSource));
      OnPropertyChanged(nameof(CanViewSourceCrawlHistory));
    }
  }

  public LoadState State
  {
    get => state;
    private set
    {
      if (!SetProperty(ref state, value)) return;
      OnPropertyChanged(nameof(IsLoading));
      OnPropertyChanged(nameof(HasError));
    }
  }

  public bool IsLoading => State == LoadState.Loading;

  public string? ErrorMessage
  {
    get => errorMessage;
    private set
    {
      if (SetProperty(ref errorMessage, value)) OnPropertyChanged(nameof(HasError));
    }
  }

  public bool HasError => State == LoadState.Error || !string.IsNullOrWhiteSpace(ErrorMessage);

  public double? VoteScoreNet
  {
    get => voteScoreNet;
    private set => SetProperty(ref voteScoreNet, value);
  }

  public int? VoteCountUp
  {
    get => voteCountUp;
    private set => SetProperty(ref voteCountUp, value);
  }

  public int? VoteCountDown
  {
    get => voteCountDown;
    private set => SetProperty(ref voteCountDown, value);
  }

  public ElectionVoteChoice? CurrentVoteChoice
  {
    get => currentVoteChoice;
    private set => SetProperty(ref currentVoteChoice, value);
  }
}
