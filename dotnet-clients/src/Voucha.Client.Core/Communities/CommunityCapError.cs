using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Communities;

internal static class CommunityCapError
{
  internal static UiMessageKey? MessageKey(Exception error) => error is VouchaApiException { StatusCode: HttpStatusCode.Conflict } api
      ? api.ErrorCode switch
      {
        "COMMUNITY_MEMBERSHIP_LIMIT_REACHED" => UiMessageKey.NativeCommunitiesMembershipLimitReached,
        "COMMUNITY_MODERATOR_LIMIT_REACHED" => UiMessageKey.NativeCommunitiesModeratorLimitReached,
        _ => null,
      }
      : null;
}
