using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private View BuildBanRowActions()
  {
    var picker = new Picker { ItemDisplayBinding = new Binding(nameof(CommunityBanRow.Id)) };
    picker.SetBinding(Picker.ItemsSourceProperty, nameof(CommunityDetailViewModel.BanRows));
    var reason = new Label();
    reason.SetBinding(Label.TextProperty, "SelectedBanDetail.Reason");
    return new VerticalStackLayout
    {
      Spacing = 8,
      Children =
      {
        ActionButton(UiMessageKey.NativeDotnetProfileHistory).Also(button => button.Clicked += async (_, _) =>
            await InvokeAndRenderAsync(viewModel.LoadBanHistoryAsync).ConfigureAwait(true)),
        picker,
        ActionButton(UiMessageKey.ModerationCommunityBansActive).Also(button => button.Clicked += async (_, _) =>
        {
          if (picker.SelectedItem is CommunityBanRow row)
            await InvokeAndRenderAsync(ct => viewModel.LoadActiveBansAsync(row.UserId, ct)).ConfigureAwait(true);
        }),
        reason,
        ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesReason).Also(button => button.Clicked += async (_, _) =>
        {
          if (picker.SelectedItem is CommunityBanRow row)
            await InvokeAndRenderAsync(ct => viewModel.LoadBanDetailAsync(row.Id, ct)).ConfigureAwait(true);
        }),
        ActionButton(UiMessageKey.NativeDotnetCsharpCommunitiesLift).Also(button => button.Clicked += async (_, _) =>
        {
          if (picker.SelectedItem is CommunityBanRow row)
            await InvokeAndRenderAsync(ct => viewModel.LiftBanAsync(row.Id, ct)).ConfigureAwait(true);
        }),
        ActionButton(UiMessageKey.ModerationCommunityBansLiftAll).Also(button => button.Clicked += async (_, _) =>
        {
          if (picker.SelectedItem is not CommunityBanRow row) return;
          viewModel.RequestLiftAllBans(row.Id);
          var confirmed = await DisplayAlertAsync(
              UiCopy.Localize(UiMessageKey.ModerationCommunityBansConfirmLiftAll),
              UiCopy.Format(UiMessageKey.ModerationCommunityBansConfirmLiftAllDescription, ("userLabel", row.UserId)),
              UiCopy.Localize(UiMessageKey.ModerationCommunityBansLiftAll),
              UiCopy.Localize(UiMessageKey.CommonCancel)).ConfigureAwait(true);
          await InvokeAndRenderAsync(ct => viewModel.ConfirmLiftAllBansAsync(confirmed, ct)).ConfigureAwait(true);
        }),
      },
    };
  }
}
