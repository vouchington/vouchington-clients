using Microsoft.Maui.Controls;
using Voucha.Client.Core.LandingPages;

namespace Voucha.Client.App.Pages;

public partial class LandingPageAnalyticsPage : ContentPage
{
  private readonly LandingPageAnalyticsViewModel viewModel;
  private readonly string username;
  private readonly string slug;

  public LandingPageAnalyticsPage(LandingPageAnalyticsViewModel viewModel, string username, string slug)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.username = username ?? throw new ArgumentNullException(nameof(username));
    this.slug = slug ?? throw new ArgumentNullException(nameof(slug));
    BindingContext = viewModel;
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try
    {
      await viewModel.LoadAsync(username, slug).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }
}
