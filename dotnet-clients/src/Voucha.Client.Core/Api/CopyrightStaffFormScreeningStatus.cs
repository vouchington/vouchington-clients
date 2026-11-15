using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Api;

public static class CopyrightStaffFormScreeningStatus
{
  /// <summary>Copy for a staff form-screening attempt; unknown protocol states carry no copy.</summary>
  public static UiMessageKey? StatusKey(this CopyrightStaffFormScreening screening)
  {
    ArgumentNullException.ThrowIfNull(screening);
    return screening.State switch
    {
      "pending" => UiMessageKey.NativeSwiftAiRunStatusCopyrightScreeningPending,
      "failed" => UiMessageKey.NativeSwiftAiRunStatusCopyrightScreeningFailed,
      "completed" => UiMessageKey.NativeSwiftAiRunStatusCopyrightScreeningCompleted,
      _ => null,
    };
  }
}
