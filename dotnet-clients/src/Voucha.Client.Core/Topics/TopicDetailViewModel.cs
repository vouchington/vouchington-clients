using Voucha.Client.Core.Api;
using Voucha.Client.Core.Bookmarks;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Voting;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Topics;

public sealed partial class TopicDetailViewModel : ObservableObject, IDisposable, IUiLocaleChangeListener
{
  private readonly ITopicsService topicsService;
  private readonly IBookmarkService bookmarkService;
  private Topic? topic;
  private TopicElection? topicElection;
  private ElectionVote? electionVote;
  private LoadState state = LoadState.Idle;
  private string? errorMessage;
  private string? sourceRssFeedId;
  private bool isFollowingTopic;
  private bool isFollowingSource;
  private bool isMutedTopic;
  private bool isMutedSource;
  private ElectionVoteChoice? currentVoteChoice;
  private double? voteScoreNet;
  private int? voteCountUp;
  private int? voteCountDown;
  private readonly IUiLocalization localization;
  private readonly IUiLocaleController? localeController;

  public EmailVerificationGatedMutation EmailVerificationGate { get; } = new();

  public TopicDetailViewModel(
      ITopicsService topicsService,
      IBookmarkService? bookmarkService = null,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null,
      VouchaApiClient? apiClient = null,
      bool canViewSourceCrawlHistory = false,
      bool canManageSourceCrawls = false,
      bool requiresAuthoritativeSourceCrawlMembership = false)
  {
    this.topicsService = topicsService ?? throw new ArgumentNullException(nameof(topicsService));
    this.bookmarkService = bookmarkService ?? NullBookmarkService.Instance;
    this.localization = localization ?? UiLocalization.English;
    this.localeController = localeController;
    ApiClient = apiClient;
    CanViewPaidSourceCrawlHistory = canViewSourceCrawlHistory;
    CanManageSourceCrawls = canManageSourceCrawls;
    RequiresAuthoritativeSourceCrawlMembership = requiresAuthoritativeSourceCrawlMembership;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

}
