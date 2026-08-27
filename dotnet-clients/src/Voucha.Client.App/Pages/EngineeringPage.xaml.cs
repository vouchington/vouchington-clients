using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.FeatureFlags;

namespace Voucha.Client.App.Pages;

public partial class EngineeringPage : ContentPage
{
  private readonly IServiceProvider serviceProvider;

  private readonly ISessionStore sessionStore;

  public EngineeringPage(IServiceProvider serviceProvider, ISessionStore sessionStore)
  {
    InitializeComponent();
    this.serviceProvider = serviceProvider;
    this.sessionStore = sessionStore;
    var roles = sessionStore.Current.Identity?.Roles;
    FeatureFlagOverridesCard.IsVisible = FeatureFlagOverridePolicy.CanManage(roles);
    OperationsCards.IsVisible = roles?.Contains("administrator", StringComparer.Ordinal) == true;
  }

  private async void OnFeatureFlagOverridesClicked(object? sender, EventArgs e)
  {
    if (FeatureFlagOverridesPageFactory.TryCreate(serviceProvider, sessionStore, out var page))
    {
      await Navigation.PushAsync(page);
    }
  }

  private async void OnQueuesClicked(object? sender, EventArgs e) =>
      await Navigation.PushAsync(serviceProvider.GetRequiredService<EngineeringQueuesPage>());

  private async void OnPostgreSqlClicked(object? sender, EventArgs e) =>
      await Navigation.PushAsync(serviceProvider.GetRequiredService<EngineeringPostgreSqlPage>());

  private async void OnValkeyClicked(object? sender, EventArgs e) =>
      await Navigation.PushAsync(serviceProvider.GetRequiredService<EngineeringValkeyPage>());

  private async void OnAiCostsClicked(object? sender, EventArgs e) =>
      await Navigation.PushAsync(serviceProvider.GetRequiredService<AiCostsPage>());

  private async void OnDynamicConfigClicked(object? sender, EventArgs e) =>
      await Navigation.PushAsync(serviceProvider.GetRequiredService<DynamicConfigPage>());
}
