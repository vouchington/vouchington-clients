using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Api;

public static class RelationMutationErrors
{
  public static string Message(Exception error, IUiLocalization localization)
  {
    ArgumentNullException.ThrowIfNull(error);
    ArgumentNullException.ThrowIfNull(localization);
    return error is VouchaApiException { IsRelationLimitReached: true }
        ? localization.Localize(UiMessageKey.NativeDotnetCsharpActionFailed)
        : error.Message;
  }
}
