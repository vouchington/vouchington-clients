namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest PointValuations(string? after = null, int limit = 25) =>
      Get("/api/v1/my/rewards-program-point-valuations", Query(("after", after), ("limit", limit)));

  public static ApiRequest CreatePointValuation(CreatePointValuationBody body) =>
      new(HttpMethod.Post, "/api/v1/my/rewards-program-point-valuations")
      { Body = body ?? throw new ArgumentNullException(nameof(body)) };

  public static ApiRequest UpdatePointValuation(string id, UpdatePointValuationBody body) =>
      new(HttpMethod.Patch, $"/api/v1/my/rewards-program-point-valuations/{Path(id)}")
      { Body = body ?? throw new ArgumentNullException(nameof(body)) };

  public static ApiRequest DeletePointValuation(string id) =>
      new(HttpMethod.Delete, $"/api/v1/my/rewards-program-point-valuations/{Path(id)}");

  public static ApiRequest RewardsProgramTopics(string query, int limit = 10) =>
      SearchTopics(query, "rewards_program", limit);
}
