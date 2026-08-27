using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed partial class VouchaApiEndpointParityTests
{
  [Fact]
  public void NotificationsResponseAllowsProductionShapeWithoutSidecars()
  {
    const string Json = """{"results":[],"page_info":{"has_more":false},"notifications":{}}""";
    var response = JsonSerializer.Deserialize<NotificationsResponse>(Json, VouchaApiJson.Options);
    Assert.NotNull(response);
    Assert.Null(response.Communities);
  }

  [Fact]
  public void NotificationLifecycleAndDigestTargetsDecodeFromFixtureShape()
  {
    const string Json = """
      {"results":[],"page_info":{"has_next_page":false},"notifications":{
        "lifecycle":{"id":"lifecycle","entity_type":"community_role_change","target_entity":{"__entity_type":"community","id":"c1"}},
        "digest":{"id":"digest","entity_type":"community_activity_digest","target_intent":"notifications_inbox"}}}
      """;
    var response = JsonSerializer.Deserialize<NotificationsResponse>(Json, VouchaApiJson.Options)!;
    Assert.Equal("community", response.Notifications["lifecycle"].TargetEntity!.EntityType);
    Assert.Equal("community_activity_digest", response.Notifications["digest"].EntityType);
    Assert.Equal("notifications_inbox", response.Notifications["digest"].TargetIntent);
  }
}
