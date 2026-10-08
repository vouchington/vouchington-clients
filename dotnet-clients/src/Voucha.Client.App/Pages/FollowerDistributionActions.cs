using Microsoft.Extensions.DependencyInjection;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.FollowerDistributions;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

internal static class FollowerDistributionActions
{
  private static string T(UiMessageKey key) => UiCopy.Localize(key);

  public static bool CanSendPost(ISessionStore sessionStore, string? creatorId, string? postType, string? parentId, string? privacy, string? broadcast) =>
      sessionStore.Current.IsAuthenticated && sessionStore.Current.Identity?.Id is { } userId &&
      !string.Equals(userId, creatorId, StringComparison.Ordinal) &&
      FollowerDistributionEligibility.IsPublicTopLevelPost(postType, parentId, privacy, broadcast);

  public static bool CanSendRssItem(ISessionStore sessionStore) => sessionStore.Current.IsAuthenticated;
  public static async Task ShowAsync(Page page, IServiceProvider services, ISessionStore sessionStore, FollowerDistributionTargetKind kind, string id)
  {
    var userId = sessionStore.Current.Identity?.Id;
    if (!sessionStore.Current.IsAuthenticated || string.IsNullOrWhiteSpace(userId)) return;
    var state = new FollowerDistributionViewModel(services.GetRequiredService<IFriendsService>(), services.GetRequiredService<VouchaApiClient>(), kind, id);
    state.ReplaceContext(userId, kind, id);
    var share = T(UiMessageKey.NativeSwiftFollowerDistributionShareWithFollowers);
    var send = T(UiMessageKey.NativeSwiftFollowerDistributionSendToFollowers);
    var choice = await page.DisplayActionSheetAsync(
        T(UiMessageKey.NativeSwiftFollowerDistributionActions),
        UiCopy.Localize(UiMessageKey.CommonCancel),
        null,
        share,
        send);
    if (choice == share && sessionStore.Current.IsAuthenticated && sessionStore.Current.Identity?.Id == userId)
    {
      try
      {
        if (!await state.ShareAsync().ConfigureAwait(true))
          await page.DisplayAlertAsync(
              T(UiMessageKey.NativeSwiftFollowerDistributionActions),
              T(UiMessageKey.NativeSwiftFollowerDistributionUnableToShare),
              UiCopy.Localize(UiMessageKey.NativeDotnetCsharpOk));
      }
      finally { state.Dispose(); }
      return;
    }
    if (choice == send && sessionStore.Current.IsAuthenticated && sessionStore.Current.Identity?.Id == userId)
      await page.Navigation.PushModalAsync(new FollowerDistributionSendPage(state, userId, sessionStore)).ConfigureAwait(true);
    else state.Dispose();
  }
}
