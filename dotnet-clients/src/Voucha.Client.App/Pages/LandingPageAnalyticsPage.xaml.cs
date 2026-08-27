using Microsoft.Maui.Controls;
using Voucha.Client.Core.LandingPages;

namespace Voucha.Client.App.Pages;

public partial class LandingPageAnalyticsPage : ContentPage
{
  private readonly LandingPageAnalyticsViewModel viewModel;
  private readonly string pageId;

  public LandingPageAnalyticsPage(LandingPageAnalyticsViewModel viewModel, string pageId)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.pageId = pageId ?? throw new ArgumentNullException(nameof(pageId));
    BindingContext = viewModel;
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try
    {
      await viewModel.LoadAsync(pageId).ConfigureAwait(true);
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
    }
  }
}
