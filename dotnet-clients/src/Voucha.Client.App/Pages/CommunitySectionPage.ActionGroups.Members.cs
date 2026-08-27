using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private View BuildMemberActions()
  {
    var userId = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesUserId);
    var role = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesRole, UiText.Verbatim("member"));
    var transferUserId = TextField(UiMessageKey.NativeDotnetCsharpCommunitiesTransferUserId);
    role.SetBinding(IsVisibleProperty, nameof(CommunityDetailViewModel.CanManageCommunity));
    transferUserId.SetBinding(IsVisibleProperty, nameof(CommunityDetailViewModel.CanManageCommunity));
    var updateRole = ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesUpdateRole);
    updateRole.SetBinding(IsVisibleProperty, nameof(CommunityDetailViewModel.CanManageCommunity));
    updateRole.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.UpdateMemberRoleAsync(userId.Text ?? string.Empty, role.Text ?? "member", ct)).ConfigureAwait(true);
    var transferOwnership = ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesTransferOwnership);
    transferOwnership.SetBinding(IsVisibleProperty, nameof(CommunityDetailViewModel.CanManageCommunity));
    transferOwnership.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.TransferOwnershipAsync(transferUserId.Text ?? string.Empty, ct)).ConfigureAwait(true);
    return new VerticalStackLayout
    {
      Spacing = 8,
      Children =
      {
        new HorizontalStackLayout { Spacing = 8, Children = { userId, role, ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesRemove).Also(button => button.Clicked += async (_, _) => await InvokeAndRenderAsync(ct => viewModel.RemoveMemberAsync(userId.Text ?? string.Empty, ct)).ConfigureAwait(true)), updateRole } },
        new HorizontalStackLayout { Spacing = 8, Children = { transferUserId, transferOwnership } },
      },
    };
  }
}
