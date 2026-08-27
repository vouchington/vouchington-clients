using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Posts;
using Voucha.Client.Core.FollowerDistributions;
using Voucha.Client.App.Controls;

namespace Voucha.Client.App.Pages;

public partial class PostsPage : ContentPage
{
  private readonly PostsListViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly IServiceProvider serviceProvider;
  private readonly EmailVerificationRecoveryCoordinator emailRecovery;
  private string mode;
  private string? postTypes;

  public PostsPage(
      PostsListViewModel viewModel,
      ISessionStore sessionStore,
      IServiceProvider serviceProvider,
      EmailVerificationRecoveryCoordinator emailRecovery)
  {
    InitializeComponent();
    this.viewModel = viewModel;
    this.sessionStore = sessionStore;
    this.serviceProvider = serviceProvider;
    this.emailRecovery = emailRecovery;
    mode = sessionStore.Current.IsAuthenticated ? "feed" : "browse";
    BindingContext = new PostsPageBinding(viewModel, sessionStore, LoadCurrentAsync);
    PaginationControl.LoadNextPageRequested += OnLoadNextPageRequested;
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async void lifecycle methods must not allow load failures to escape to the dispatcher.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    if (BindingContext is PostsPageBinding binding) binding.AttachSessionChanged();
    try
    {
      await LoadCurrentAsync();
    }
    catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  protected override void OnDisappearing()
  {
    if (BindingContext is PostsPageBinding binding) binding.DetachSessionChanged();
    base.OnDisappearing();
  }

  private void OnRemainingItemsThresholdReached(object? sender, EventArgs e) =>
      PaginationControl.TryLoadAutomatically();

  private async void OnLoadNextPageRequested(object? sender, EventArgs e) =>
      await viewModel.LoadMoreAsync().ConfigureAwait(true);

  private Task LoadCurrentAsync() =>
      mode == "browse" || !sessionStore.Current.IsAuthenticated
          ? viewModel.LoadBrowseAsync(postTypes)
          : viewModel.LoadFeedAsync("any", postTypes);

  private async void OnFeedClicked(object? sender, EventArgs e)
  {
    mode = "feed";
    postTypes = null;
    await LoadCurrentAsync();
  }

  private async void OnAllClicked(object? sender, EventArgs e)
  {
    mode = "browse";
    postTypes = null;
    await LoadCurrentAsync();
  }

  private async void OnReviewsClicked(object? sender, EventArgs e)
  {
    mode = "browse";
    postTypes = "review";
    await LoadCurrentAsync();
  }

  private async void OnDiscussionsClicked(object? sender, EventArgs e)
  {
    mode = "browse";
    postTypes = "discussion";
    await LoadCurrentAsync();
  }

  private async void OnComposeClicked(object? sender, EventArgs e)
  {
    await Navigation.PushAsync(serviceProvider.GetRequiredService<PostComposePage>());
  }

  private async void OnPostClicked(object? sender, TappedEventArgs e)
  {
    if (sender is Border { BindingContext: PostRow item })
    {
      await Navigation.PushAsync(ActivatorUtilities.CreateInstance<PostDetailPage>(serviceProvider, item.Id));
    }
  }

  private async void OnOpenClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: PostRow item })
    {
      await Navigation.PushAsync(new PostDetailPage(
          serviceProvider.GetRequiredService<ICommentThreadService>(),
          serviceProvider.GetRequiredService<IPostsService>(),
          sessionStore,
          serviceProvider,
          item.Id));
    }
  }

  private async void OnChooseVoteClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: PostRow item })
    {
      if (!sessionStore.Current.CanCastPublicVotes()) return;
      var choice = string.Equals(item.ProtocolPostType, "topic_recommendation", StringComparison.Ordinal)
          ? await SemanticVoteActionSheet.ChooseRecommendationAsync(this)
          : await SemanticVoteActionSheet.ChooseSentimentAsync(this, item.CurrentVoteChoice);
      if (choice is null) return;
      await viewModel.VotePostAsync(item, choice);
      await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate);
    }
  }

  private async void OnClearVoteClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: PostRow item })
    {
      if (!sessionStore.Current.CanClearPublicVote(item.CurrentVoteChoice)) return;
      await viewModel.VotePostAsync(item, null);
      await emailRecovery.PresentIfRequestedAsync(this, viewModel.EmailVerificationGate);
    }
  }

  private async void OnSaveClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: PostRow item })
    {
      await viewModel.ToggleSaveAsync(item);
    }
  }

  private async void OnFollowerSendClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: PostRow item })
      if (FollowerDistributionActions.CanSendPost(sessionStore, item.CreatedById, item.ProtocolPostType, item.ParentId, item.Privacy, item.Broadcast))
        await FollowerDistributionActions.ShowAsync(this, serviceProvider, sessionStore, FollowerDistributionTargetKind.Post, item.Id);
  }

  private async void OnHideClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: PostRow item })
    {
      await viewModel.ToggleHideAsync(item);
    }
  }
}
