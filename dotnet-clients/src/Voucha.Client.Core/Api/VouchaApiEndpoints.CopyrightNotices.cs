using System.Globalization;

namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest CopyrightNotices(string? after = null, int? limit = null) =>
      Get("/api/v1/copyright-notices", CopyrightCaseQuery(after, limit));

  public static ApiRequest CopyrightEuDisputeSettlements(string id, string? after = null, int? limit = null) =>
      Get($"/api/v1/copyright-notices/{Path(id)}/eu-dispute-settlements", CopyrightCaseQuery(after, limit));

  private static Dictionary<string, string> CopyrightCaseQuery(string? after, int? limit)
  {
    var query = new Dictionary<string, string>();
    if (after is not null)
      query["after"] = after;
    if (limit is not null)
      query["limit"] = limit.Value.ToString(CultureInfo.InvariantCulture);
    return query;
  }

  public static ApiRequest CopyrightNotice(string id) =>
      Get($"/api/v1/copyright-notices/{Path(id)}");

  public static ApiRequest CopyrightParticipantNotice(string id) =>
      Get($"/api/v1/copyright-notices/{Path(id)}/participant");
}
