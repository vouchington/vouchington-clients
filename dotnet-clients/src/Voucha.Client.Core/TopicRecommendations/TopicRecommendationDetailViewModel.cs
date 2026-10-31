using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.TopicRecommendations;

public sealed partial class TopicRecommendationDetailViewModel :
    ObservableObject,
    IDisposable,
    IUiLocaleChangeListener
{
  private readonly ITopicRecommendationDetailService service;
  private readonly string recommendationId;
  private readonly IUiLocalization localization;
  private readonly IDisposable? localeSubscription;
  private PostResponse? detail;
  private string? errorMessage;
  private bool isLoading;
  private bool isVoting;
  private PostElection? election;
  private ElectionVoteChoice? currentVoteChoice;

  public EmailVerificationGatedMutation EmailVerificationGate { get; } = new();

  public TopicRecommendationDetailViewModel(
      ITopicRecommendationDetailService service,
      string recommendationId,
      IUiLocalization? localization = null,
      IUiLocaleController? localeController = null)
  {
    this.service = service ?? throw new ArgumentNullException(nameof(service));
    this.recommendationId = string.IsNullOrWhiteSpace(recommendationId)
        ? throw new ArgumentException("A recommendation ID is required.", nameof(recommendationId))
        : recommendationId;
    this.localization = localization ?? UiLocalization.English;
    localeSubscription = localeController?.SubscribeLocaleChanges(this);
  }

  public PostResponse? Detail
  {
    get => detail;
    private set
    {
      detail = value;
      Election = value?.PostElection;
      CurrentVoteChoice = value?.ElectionVote?.Choice;
      OnPropertyChanged(); OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(Html)); OnPropertyChanged(nameof(Markdown));
    }
  }
  public string Title => Detail?.Post.Title ??
      localization.Localize(UiMessageKey.NativeTaxonomyPostsTopicRecommendation);
  public string? Html => Detail?.Html ?? Detail?.Post.Html;
  public string? Markdown => Detail?.Post.Markdown;
  public string? ErrorMessage { get => errorMessage; private set { errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); } }
  public bool HasError => ErrorMessage is not null;
  public bool IsLoading { get => isLoading; private set { isLoading = value; OnPropertyChanged(); } }
  public bool IsVoting
  {
    get => isVoting;
    private set
    {
      isVoting = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanCastVote));
      OnPropertyChanged(nameof(CanClearVote));
    }
  }
  public PostElection? Election { get => election; private set { election = value; OnPropertyChanged(); OnPropertyChanged(nameof(VoteCountUp)); OnPropertyChanged(nameof(VoteCountDown)); } }
  public ElectionVoteChoice? CurrentVoteChoice
  {
    get => currentVoteChoice;
    private set
    {
      currentVoteChoice = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanClearVote));
    }
  }
  public bool CanCastVote => !IsVoting;
  public bool CanClearVote => CurrentVoteChoice is not null && !IsVoting;
  public int? VoteCountUp => Election?.VotesCountUp;
  public int? VoteCountDown => Election?.VotesCountDown;

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Detail load errors are presented with retry.")]
  public async Task LoadAsync(CancellationToken cancellationToken = default)
  {
    if (IsLoading) return;
    IsLoading = true;
    ErrorMessage = null;
    try
    {
      Detail = await service.FetchAsync(recommendationId, cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
    finally
    {
      IsLoading = false;
    }
  }

  public void OnUiLocaleChanged() => OnPropertyChanged(nameof(Title));

  public void Dispose() => localeSubscription?.Dispose();
}
