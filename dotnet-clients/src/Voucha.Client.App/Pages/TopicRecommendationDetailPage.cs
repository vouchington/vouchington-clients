using Voucha.Client.App.Controls;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.TopicRecommendations;

namespace Voucha.Client.App.Pages;

public sealed class TopicRecommendationDetailPage : ContentPage
{
  private readonly TopicRecommendationDetailViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly EmailVerificationRecoveryCoordinator emailRecovery;
  private readonly Button vote;
  private readonly Button clearVote;

  public TopicRecommendationDetailPage(
      ITopicRecommendationDetailService service,
      string recommendationId,
      IUiLocalization localization,
      IUiLocaleController localeController,
      ISessionStore sessionStore,
      EmailVerificationRecoveryCoordinator emailRecovery)
  {
    viewModel = new(service, recommendationId, localization, localeController);
    this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
    this.emailRecovery = emailRecovery ?? throw new ArgumentNullException(nameof(emailRecovery));
    BindingContext = viewModel;
    SetBinding(TitleProperty, new Binding(nameof(TopicRecommendationDetailViewModel.Title)));

    var title = new Label { FontSize = 24, FontAttributes = FontAttributes.Bold };
    title.SetBinding(Label.TextProperty, nameof(TopicRecommendationDetailViewModel.Title));
    var body = new NativeHtmlContentView();
    body.SetBinding(NativeHtmlContentView.HtmlProperty, nameof(TopicRecommendationDetailViewModel.Html));
    body.SetBinding(NativeHtmlContentView.FallbackProperty, nameof(TopicRecommendationDetailViewModel.Markdown));
    var error = new Label { TextColor = Colors.IndianRed };
    error.SetBinding(Label.TextProperty, nameof(TopicRecommendationDetailViewModel.ErrorMessage));
    var retry = UiCopy.Bind(
        new Button(),
        Button.TextProperty,
        UiMessageKey.NativeSwiftCommonTryAgain);
    retry.SetBinding(IsVisibleProperty, nameof(TopicRecommendationDetailViewModel.HasError));
    retry.Clicked += async (_, _) => await viewModel.LoadAsync().ConfigureAwait(true);
    vote = UiCopy.Bind(
        new Button { MinimumHeightRequest = 44 },
        Button.TextProperty,
        UiMessageKey.ExtractedVotesSemanticVoteVote);
    vote.AutomationId = "topic-recommendation-vote";
    vote.Clicked += OnChooseVoteClicked;
    var positiveCount = new Label { FontSize = 13 };
    positiveCount.SetBinding(Label.TextProperty, nameof(TopicRecommendationDetailViewModel.VoteCountUp));
    var negativeCount = new Label { FontSize = 13 };
    negativeCount.SetBinding(Label.TextProperty, nameof(TopicRecommendationDetailViewModel.VoteCountDown));
    clearVote = UiCopy.Bind(
        new Button { MinimumHeightRequest = 44 },
        Button.TextProperty,
        UiMessageKey.ExtractedVotesSemanticVoteClear);
    clearVote.AutomationId = "topic-recommendation-clear-vote";
    clearVote.Clicked += OnClearVoteClicked;
    viewModel.PropertyChanged += (_, args) =>
    {
      if (args.PropertyName is nameof(TopicRecommendationDetailViewModel.IsVoting) or
          nameof(TopicRecommendationDetailViewModel.CurrentVoteChoice)) UpdateVoteActionAvailability();
    };
    UpdateVoteActionAvailability();

    Content = new ScrollView
    {
      Content = new VerticalStackLayout
      {
        Padding = 20,
        Spacing = 12,
        Children = { title, body, vote, positiveCount, negativeCount, clearVote, error, retry },
      },
    };
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    if (viewModel.Detail is null) await viewModel.LoadAsync().ConfigureAwait(true);
  }

  private async void OnChooseVoteClicked(object? sender, EventArgs e)
  {
    if (!vote.IsEnabled) return;
    var choice = await SemanticVoteActionSheet.ChooseRecommendationAsync(this);
    if (choice is null) return;
    await viewModel.VoteAsync(choice);
    await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate);
  }

  private async void OnClearVoteClicked(object? sender, EventArgs e)
  {
    if (!clearVote.IsEnabled) return;
    await viewModel.VoteAsync(null);
    await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate);
  }

  private void UpdateVoteActionAvailability()
  {
    vote.IsEnabled = sessionStore.Current.CanCastPublicVotes() && viewModel.CanCastVote;
    clearVote.IsEnabled = sessionStore.Current.CanClearPublicVote(viewModel.CurrentVoteChoice) &&
        viewModel.CanClearVote;
  }
}
