using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class ApiFixtureEndpointCoverageTests
{
  private static IReadOnlyDictionary<string, ApiRequest> WithRewardsProgramStatusEndpoints(
      Dictionary<string, ApiRequest> registry)
  {
    AddRewardsProgramStatusEndpoints(registry);
    return registry;
  }

  private static void AddRewardsProgramStatusEndpoints(IDictionary<string, ApiRequest> registry)
  {
    registry["native.rewards-statuses.empty"] = VouchaApiEndpoints.RewardsProgramStatuses();
    registry["native.rewards-statuses.page-1"] = VouchaApiEndpoints.RewardsProgramStatuses(limit: 2);
    registry["native.rewards-statuses.page-2"] = VouchaApiEndpoints.RewardsProgramStatuses(
        "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDc0MiIsInNjb3BlIjoibXktcmV3YXJkcy1wcm9ncmFtLXN0YXR1c2VzOjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDc0MDppZC1hc2MifQ", 2);
    registry["native.rewards-statuses.create.default"] = VouchaApiEndpoints.CreateRewardsProgramStatus(
        new CreateRewardsProgramStatusBody("00000000-0000-7000-8000-000000000751"));
    registry["native.rewards-program-status-topics.search.default"] = VouchaApiEndpoints.RewardsProgramStatusTopics("Gold");
    registry["native.rewards-statuses.update.full"] = VouchaApiEndpoints.UpdateRewardsProgramStatus(
        "00000000-0000-7000-8000-000000000741", new UpdateRewardsProgramStatusBody(
            JsonNullableDate.FromDate(new DateOnly(2025, 2, 1)), JsonNullableDate.FromDate(new DateOnly(2026, 12, 31))));
    registry["native.rewards-statuses.update.clear-dates"] = VouchaApiEndpoints.UpdateRewardsProgramStatus(
        "00000000-0000-7000-8000-000000000741", new UpdateRewardsProgramStatusBody(JsonNullableDate.Null, JsonNullableDate.Null));
    registry["native.rewards-statuses.delete.default"] = VouchaApiEndpoints.DeleteRewardsProgramStatus(
        "00000000-0000-7000-8000-000000000741");
  }
}
