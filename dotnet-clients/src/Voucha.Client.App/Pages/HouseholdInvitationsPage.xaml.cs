using Voucha.Client.Core.Api;
using Voucha.Client.Core.Households;
using Voucha.Client.Core.Support;

namespace Voucha.Client.App.Pages;

public partial class HouseholdInvitationsPage : ContentPage
{
  private readonly HouseholdInvitationsViewModel viewModel;

  public HouseholdInvitationsPage(HouseholdInvitationsViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
  }

  public void ConfigureSent(string householdId) => viewModel.ConfigureSent(householdId);

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await PageLoadGuard.RunAsync(() => viewModel.LoadAsync());
  }

  private async void OnLoadMoreClicked(object? sender, EventArgs e) => await viewModel.LoadMoreAsync();

  private async void OnSendClicked(object? sender, EventArgs e)
  {
    if (await viewModel.SendAsync(UsernameEntry.Text ?? string.Empty, RelationshipEntry.Text))
    {
      UsernameEntry.Text = string.Empty;
      RelationshipEntry.Text = string.Empty;
    }
  }

  private async void OnRevokeClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: HouseholdInvitation invitation }) await viewModel.RevokeAsync(invitation);
  }

  private async void OnAcceptClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: HouseholdInvitation invitation }) await viewModel.AcceptAsync(invitation);
  }

  private async void OnDeclineClicked(object? sender, EventArgs e)
  {
    if (sender is Button { CommandParameter: HouseholdInvitation invitation }) await viewModel.DeclineAsync(invitation);
  }
}
