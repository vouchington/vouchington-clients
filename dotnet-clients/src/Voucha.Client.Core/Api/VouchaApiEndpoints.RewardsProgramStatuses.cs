namespace Voucha.Client.Core.Api;

public static partial class VouchaApiEndpoints
{
  public static ApiRequest RewardsProgramStatuses(string? after = null, int limit = 25) =>
      Get("/api/v1/my/rewards-program-statuses", Query(("after", after), ("limit", limit)));

  public static ApiRequest CreateRewardsProgramStatus(CreateRewardsProgramStatusBody body) =>
      new(HttpMethod.Post, "/api/v1/my/rewards-program-statuses")
      { Body = body ?? throw new ArgumentNullException(nameof(body)) };

  public static ApiRequest UpdateRewardsProgramStatus(string id, UpdateRewardsProgramStatusBody body) =>
      new(HttpMethod.Patch, $"/api/v1/my/rewards-program-statuses/{Path(id)}")
      { Body = body ?? throw new ArgumentNullException(nameof(body)) };

  public static ApiRequest DeleteRewardsProgramStatus(string id) =>
      new(HttpMethod.Delete, $"/api/v1/my/rewards-program-statuses/{Path(id)}");

  public static ApiRequest RewardsProgramStatusTopics(string query, int limit = 10) =>
      SearchTopics(query, "rewards_program_status", limit);
}
