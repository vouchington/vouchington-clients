using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App.Pages;

public sealed partial class CopyrightNoticesPage
{
  private void RenderList()
  {
    body.Children.Add(Message(UiMessageKey.NativeCopyrightNoticesPrivacyNote));
    foreach (var notice in viewModel.Notices)
    {
      body.Children.Add(Link(UiCopy.Format(UiMessageKey.NativeCopyrightNoticesCase,
          ("id", UiText.Verbatim(notice.Id))),
          NativeRoutePath.Segments("copyright", "notices", notice.Id), $"copyright-case-{notice.Id}"));
      body.Children.Add(Format(UiMessageKey.NativeCopyrightNoticesAcceptedDate,
          ("date", UiCopy.FormatDateTime(notice.AcceptedAt))));
      body.Children.Add(Format(UiMessageKey.NativeCopyrightNoticesAffectedPlacementCount, ("count", notice.TargetCount)));
      AddClaimant(notice);
    }
    if (!viewModel.HasMore) return;
    var more = UiCopy.Bind(new Button
    {
      AutomationId = "copyright-load-more", IsEnabled = !viewModel.IsLoading,
    }, Button.TextProperty, UiMessageKey.NativeCopyrightNoticesLoadMore);
    more.Clicked += (_, _) => _ = LoadMoreAsync();
    body.Children.Add(more);
  }

  private void AddClaimant(CopyrightNoticeBase notice)
  {
    if (notice.Claimant is not { } claimant) return;
    body.Children.Add(Message(UiMessageKey.NativeCopyrightNoticesClaimant));
    body.Children.Add(Link(UiCopy.Resolve(UiText.Verbatim(claimant.DisplayName)),
        NativeRoutePath.Entity("user", claimant.UserId), $"copyright-claimant-{notice.Id}"));
  }
}
