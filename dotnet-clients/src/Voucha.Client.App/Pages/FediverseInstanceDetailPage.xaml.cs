using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Fediverse;
using Voucha.Client.Core.Api;
using Voucha.Client.App.Controls;

namespace Voucha.Client.App.Pages;

public partial class FediverseInstanceDetailPage : ContentPage
{
  private readonly FediverseInstanceDetailViewModel viewModel;
  private readonly ISessionStore sessionStore;
  private readonly string idOrSlug;

  public FediverseInstanceDetailPage(
      FediverseInstanceDetailViewModel viewModel,
      ISessionStore sessionStore,
      string idOrSlug)
  {
    InitializeComponent();
    this.viewModel = viewModel;
    this.sessionStore = sessionStore;
    this.idOrSlug = idOrSlug;
    BindingContext = viewModel;
    Unloaded += OnUnloaded;
  }

  private void OnUnloaded(object? sender, EventArgs e)
  {
    Unloaded -= OnUnloaded;
    viewModel.Dispose();
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await viewModel.LoadAsync(idOrSlug);
  }

  private async void OnChooseVote(object? sender, EventArgs e)
  {
    if (!sessionStore.Current.CanCastPublicVotes()) return;
    var choice = await SemanticVoteActionSheet.ChooseSentimentAsync(this, viewModel.Actions.CurrentVoteChoice);
    if (choice is not null) await viewModel.Actions.VoteTopicAsync(choice);
  }

  private async void OnClearVote(object? sender, EventArgs e)
  {
    if (sessionStore.Current.CanClearPublicVote(viewModel.Actions.CurrentVoteChoice)) await viewModel.Actions.VoteTopicAsync(null);
  }

  private async void OnFollow(object? sender, EventArgs e)
  {
    if (sessionStore.Current.IsAuthenticated) await viewModel.Actions.ToggleTopicFollowAsync();
  }

  private async void OnMute(object? sender, EventArgs e)
  {
    if (sessionStore.Current.IsAuthenticated) await viewModel.Actions.ToggleTopicMuteAsync();
  }
}
