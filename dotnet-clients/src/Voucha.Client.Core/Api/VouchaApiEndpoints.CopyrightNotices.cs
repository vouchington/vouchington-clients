namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest CopyrightNotices(string? after = null, int? limit = null) =>
      Get("/api/v1/copyright-notices", Query(("after", after), ("limit", limit)));

  public static ApiRequest CopyrightEuDisputeSettlements(string id, string? after = null, int? limit = null) =>
      Get($"/api/v1/copyright-notices/{Path(id)}/eu-dispute-settlements", Query(("after", after), ("limit", limit)));

  public static ApiRequest CopyrightNotice(string id) =>
      Get($"/api/v1/copyright-notices/{Path(id)}");

  public static ApiRequest CopyrightParticipantNotice(string id) =>
      Get($"/api/v1/copyright-notices/{Path(id)}/participant");
}
