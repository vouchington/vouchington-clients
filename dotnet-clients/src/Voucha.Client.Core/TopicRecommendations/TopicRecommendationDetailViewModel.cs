using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.TopicRecommendations;

public sealed partial class TopicRecommendationDetailViewModel :
    INotifyPropertyChanged,
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

  public event PropertyChangedEventHandler? PropertyChanged;

  public PostResponse? Detail
  {
    get => detail;
    private set
    {
      detail = value;
      Election = value?.PostElection;
      CurrentVoteChoice = value?.ElectionVote?.Choice;
      Changed(); Changed(nameof(Title)); Changed(nameof(Html)); Changed(nameof(Markdown));
    }
  }
  public string Title => Detail?.Post.Title ??
      localization.Localize(UiMessageKey.NativeTaxonomyPostsTopicRecommendation);
  public string? Html => Detail?.Html ?? Detail?.Post.Html;
  public string? Markdown => Detail?.Post.Markdown;
  public string? ErrorMessage { get => errorMessage; private set { errorMessage = value; Changed(); Changed(nameof(HasError)); } }
  public bool HasError => ErrorMessage is not null;
  public bool IsLoading { get => isLoading; private set { isLoading = value; Changed(); } }
  public bool IsVoting
  {
    get => isVoting;
    private set
    {
      isVoting = value;
      Changed();
      Changed(nameof(CanCastVote));
      Changed(nameof(CanClearVote));
    }
  }
  public PostElection? Election { get => election; private set { election = value; Changed(); Changed(nameof(VoteCountUp)); Changed(nameof(VoteCountDown)); } }
  public ElectionVoteChoice? CurrentVoteChoice
  {
    get => currentVoteChoice;
    private set
    {
      currentVoteChoice = value;
      Changed();
      Changed(nameof(CanClearVote));
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

  private void Changed([CallerMemberName] string? name = null) =>
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

  public void OnUiLocaleChanged() => Changed(nameof(Title));

  public void Dispose() => localeSubscription?.Dispose();
}
