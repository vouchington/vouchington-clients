using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App.Pages;

public static class HouseholdRoutePageFactory
{
  public static ContentPage? Create(IServiceProvider services, NativeDeepLinkResolution resolution)
  {
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(resolution);
    if (!resolution.CanNavigate || resolution.DestinationId != NativeRouteDestinationId.Household) return null;
    return resolution.PathAndQuery.Split('?')[0] == "/my/household-invitations"
        ? services.GetRequiredService<HouseholdInvitationsPage>()
        : services.GetRequiredService<HouseholdPage>();
  }
}
