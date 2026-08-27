using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App.Pages;

public partial class NavigationIntentPage : ContentPage
{
  public NavigationIntentPage(NavigationIntentViewModel viewModel)
  {
    InitializeComponent();
    BindingContext = viewModel;
  }
}
