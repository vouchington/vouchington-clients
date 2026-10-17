using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private Button? inviteCreateButton;
  private Entry? inviteRevokeId;
  private Button? inviteRevokeButton;

  private View BuildInvitesActions()
  {
    inviteCreateButton = ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesInviteMember);
    inviteCreateButton.AutomationId = "community-create-invite";
    inviteCreateButton.Clicked += async (_, _) => await InvokeAndRenderAsync(_ => OpenInviteCreationAsync()).ConfigureAwait(true);
    var inviteId = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesInviteId);
    inviteRevokeId = inviteId;
    inviteRevokeButton = ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesRevoke);
    inviteRevokeButton.AutomationId = "community-revoke-invite";
    inviteRevokeButton.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.RevokeInviteAsync(inviteId.Text ?? string.Empty, ct)).ConfigureAwait(true);
    return new HorizontalStackLayout
    {
      Spacing = 8,
      Children = { inviteCreateButton, inviteId, inviteRevokeButton },
    };
  }

  private void RenderInviteActions()
  {
    if (inviteCreateButton is not null) inviteCreateButton.IsVisible = viewModel.CanCreateInvite;
    if (inviteRevokeId is not null) inviteRevokeId.IsVisible = viewModel.CanRevokeInvite;
    if (inviteRevokeButton is not null) inviteRevokeButton.IsVisible = viewModel.CanRevokeInvite;
  }

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
