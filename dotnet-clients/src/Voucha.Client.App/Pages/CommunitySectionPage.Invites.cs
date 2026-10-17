using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private Task OpenInviteCreationAsync()
  {
    var provider = serviceProvider ?? throw new InvalidOperationException("Community invite navigation is unavailable.");
    var model = new CommunityActionViewModel(
        provider.GetRequiredService<ICommunitiesService>(), CommunityActionKind.Invite,
        provider.GetRequiredService<AppConfig>(), provider.GetRequiredService<IUiLocalization>(),
        provider.GetRequiredService<IUiLocaleController>());
    var page = new CommunityActionPage(model, provider.GetRequiredService<ITurnstileTokenProvider>(),
        provider.GetRequiredService<VouchaApiClient>());
    page.SetContext(Slug);
    return Navigation.PushAsync(page);
  }
}
