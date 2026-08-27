using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.LandingPages;
using Microsoft.Maui.Controls;

namespace Voucha.Client.App.Pages;

public partial class LandingPagesPage : ContentPage, IQueryAttributable
{
  private readonly LandingPagesViewModel viewModel;

  public LandingPagesPage(LandingPagesViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI lifecycle handlers surface failures through view model state.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try
    {
      await LoadForAppearanceAsync().ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }

  public async Task LoadForAppearanceAsync(CancellationToken cancellationToken = default)
  {
    await viewModel.LoadAsync(cancellationToken).ConfigureAwait(true);
    await viewModel.LoadSelectedAnalyticsAsync(cancellationToken).ConfigureAwait(true);
  }

  public void ApplyQueryAttributes(IDictionary<string, object> query)
  {
    viewModel.SetInitialSlug(query.TryGetValue("slug", out var slug) ? slug as string : null);
  }

  private async void OnPageSelectionChanged(object? sender, SelectionChangedEventArgs e)
  {
    if (viewModel.IsLoading)
    {
      return;
    }

    if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is LandingPageRow row)
    {
      await viewModel.SelectPageAsync(row.Id).ConfigureAwait(true);
      await viewModel.LoadSelectedAnalyticsAsync().ConfigureAwait(true);
    }
  }

  private async void OnCreateClicked(object? sender, EventArgs e)
  {
    if (await viewModel
            .CreateAsync(NewTitleEntry.Text ?? "", NewSubtitleEntry.Text, NewSlugEntry.Text)
            .ConfigureAwait(true))
    {
      NewTitleEntry.Text = string.Empty;
      NewSubtitleEntry.Text = string.Empty;
      NewSlugEntry.Text = string.Empty;
    }
  }

  private async void OnSaveDetailsClicked(object? sender, EventArgs e) =>
      await viewModel.SaveDetailsAsync().ConfigureAwait(true);

  private async void OnMakeDefaultClicked(object? sender, EventArgs e) =>
      await viewModel.SetDefaultAsync().ConfigureAwait(true);

  private async void OnDeleteClicked(object? sender, EventArgs e) =>
      await viewModel.DeleteSelectedAsync().ConfigureAwait(true);

  private void OnAddItemClicked(object? sender, EventArgs e) => viewModel.AddSelectedItem();

  private void OnGroupMemberCheckedChanged(object? sender, CheckedChangedEventArgs e)
  {
    if (sender is CheckBox { BindingContext: LandingPageGroupMemberOption option })
    {
      viewModel.SetGroupMemberSelected(option, e.Value);
    }
  }

  private async void OnSaveContentClicked(object? sender, EventArgs e) =>
      await viewModel.SaveContentAsync().ConfigureAwait(true);

  private void OnMoveItemUpClicked(object? sender, EventArgs e) => MoveItem(sender, -1);

  private void OnMoveItemDownClicked(object? sender, EventArgs e) => MoveItem(sender, 1);

  private void MoveItem(object? sender, int direction)
  {
    if (sender is Button { BindingContext: LandingPageItem item })
    {
      viewModel.MoveItem(item, direction);
    }
  }

  private void OnRemoveItemClicked(object? sender, EventArgs e)
  {
    if (sender is Button { BindingContext: LandingPageItem item })
    {
      viewModel.RemoveItem(item);
    }
  }
}
