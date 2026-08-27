using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class StaffSupportEndpointTests
{
  [Fact]
  public void ThreadSearchCarriesQueryStatusAndCursor()
  {
    var request = VouchaApiEndpoints.StaffSupportThreads("account", StaffSupportThreadStatusFilter.Assigned, "cursor-1", 25);

    Assert.Equal(HttpMethod.Get, request.Method);
    Assert.Equal("/api/v1/support/threads", request.Path);
    Assert.Equal("account", request.Query["q"]);
    Assert.Equal("assigned", request.Query["status"]);
    Assert.Equal("cursor-1", request.Query["after"]);
    Assert.Equal("25", request.Query["limit"]);
  }

  [Fact]
  public void AssignmentCarriesAdministratorUuidWithoutAlias()
  {
    var request = VouchaApiEndpoints.AssignStaffSupportThread("thread/1", "00000000-0000-7000-8000-000000000001");
    var body = Assert.IsType<AssignSupportThreadBody>(request.Body);

    Assert.Equal("/api/v1/support/threads/thread%2F1", request.Path);
    Assert.Equal("00000000-0000-7000-8000-000000000001", body.AssignedToId);
  }

  [Fact]
  public void UnknownThreadStatusFailsStrictDecoding()
  {
    const string json = "{\"id\":\"thread-1\",\"support_contact_id\":\"contact-1\",\"subject\":\"Help\",\"conversation_id\":null,\"created_at\":\"2026-07-01T12:00:00Z\",\"updated_at\":\"2026-07-01T12:00:00Z\",\"assigned_at\":null,\"assigned_to_id\":null,\"resolved_at\":null,\"resolved_by_id\":null,\"status\":\"unknown\"}";

    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SupportThread>(json, VouchaApiJson.Options));
  }
}
