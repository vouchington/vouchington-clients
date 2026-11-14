using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Households;
using Xunit;

namespace Voucha.Client.Core.Tests.Households;

public sealed class HouseholdInvitationApiTests
{
  [Theory]
  [InlineData("sent")]
  [InlineData("received")]
  public async Task ListsDecodeEmbeddedIdentityAndForwardOpaquePagination(string direction)
  {
    using var handler = new InvitationHandler(direction, HttpStatusCode.OK);
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = new ApiHouseholdInvitationService(new VouchaApiClient(http));
    var token = TestContext.Current.CancellationToken;
    var response = direction == "sent"
        ? await service.FetchSentAsync("home/one", "opaque+/=", 17, token)
        : await service.FetchReceivedAsync("opaque+/=", 17, token);

    var invitation = Assert.Single(response.Results);
    Assert.Equal("household-invitee", invitation.InvitedUser.Username);
    Assert.Equal(invitation.InvitedUserId, invitation.InvitedUser.Id);
    Assert.Equal("household-owner", invitation.InvitedBy.Username);
    Assert.Equal(invitation.InvitedById, invitation.InvitedBy.Id);
    Assert.Equal("household-owner", invitation.HouseholdOwner.Username);
    Assert.Equal("partner", invitation.Relationship);
    Assert.Null(invitation.AcceptedAt);
    Assert.Null(invitation.DeclinedAt);
    Assert.Null(invitation.RevokedAt);
    Assert.False(response.PageInfo.HasNextPage);
    Assert.NotNull(response.PageInfo.StartCursor);
    Assert.Equal(HttpMethod.Get, handler.Method);
    Assert.Equal(direction == "sent"
        ? "/api/v1/households/home%2Fone/invitations?after=opaque%2B%2F%3D&limit=17"
        : "/api/v1/my/household-invitations?after=opaque%2B%2F%3D&limit=17", handler.Path);
  }

  [Theory]
  [InlineData("partner")]
  [InlineData(null)]
  public async Task CreateSendsUsernameAndOptionalRelationship(string? relationship)
  {
    using var handler = new InvitationHandler("create", HttpStatusCode.Created);
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = new ApiHouseholdInvitationService(new VouchaApiClient(http));

    var invitation = await service.CreateAsync(
        "home/one", "household-invitee", relationship, TestContext.Current.CancellationToken);

    Assert.Equal("00000000-0000-7000-8000-000000000401", invitation.Id);
    Assert.Equal(HttpMethod.Post, handler.Method);
    Assert.Equal("/api/v1/households/home%2Fone/invitations", handler.Path);
    var body = JsonNode.Parse(handler.Body!)!.AsObject();
    Assert.Equal("household-invitee", body["user"]!.GetValue<string>());
    Assert.Equal(relationship is null ? 1 : 2, body.Count);
    Assert.Equal(relationship, body["relationship"]?.GetValue<string>());
  }

  [Fact]
  public async Task AcceptanceReturnsMembershipAndUsesEmptyObjectBody()
  {
    using var handler = new InvitationHandler("accept", HttpStatusCode.OK);
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = new ApiHouseholdInvitationService(new VouchaApiClient(http));

    var membership = await service.AcceptAsync("invite/one", TestContext.Current.CancellationToken);

    Assert.Equal("household-invitee", membership.Individual.Username);
    Assert.Equal("partner", membership.Relationship);
    Assert.Equal("00000000-0000-7000-8000-000000000101", membership.HouseholdId);
    Assert.Equal(HttpMethod.Post, handler.Method);
    Assert.Equal("/api/v1/my/household-invitations/invite%2Fone/acceptance", handler.Path);
    Assert.Equal("{}", handler.Body);
  }

  [Theory]
  [InlineData("revoke")]
  [InlineData("decline")]
  public async Task NoContentActionsUseTheirRespectiveRoutes(string action)
  {
    using var handler = new InvitationHandler(action, HttpStatusCode.NoContent);
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = new ApiHouseholdInvitationService(new VouchaApiClient(http));
    if (action == "revoke")
      await service.RevokeAsync("home/one", "invite/two", TestContext.Current.CancellationToken);
    else
      await service.DeclineAsync("invite/two", TestContext.Current.CancellationToken);

    Assert.Equal(action == "revoke" ? HttpMethod.Delete : HttpMethod.Post, handler.Method);
    Assert.Equal(action == "revoke"
        ? "/api/v1/households/home%2Fone/invitations/invite%2Ftwo"
        : "/api/v1/my/household-invitations/invite%2Ftwo/decline", handler.Path);
    Assert.Equal(action == "revoke" ? null : "{}", handler.Body);
  }

  [Theory]
  [InlineData("sent", 403)]
  [InlineData("sent", 404)]
  [InlineData("received", 403)]
  [InlineData("received", 404)]
  [InlineData("create", 403)]
  [InlineData("create", 404)]
  [InlineData("revoke", 403)]
  [InlineData("revoke", 404)]
  [InlineData("accept", 403)]
  [InlineData("accept", 404)]
  [InlineData("decline", 403)]
  [InlineData("decline", 404)]
  public async Task OperationsPreserveProducerAuthorizationAndPrivacyStatuses(string action, int status)
  {
    using var handler = new InvitationHandler(action, (HttpStatusCode)status);
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = new ApiHouseholdInvitationService(new VouchaApiClient(http));
    var token = TestContext.Current.CancellationToken;

    var error = await Assert.ThrowsAsync<VouchaApiException>(() => action switch
    {
      "sent" => service.FetchSentAsync("household", null, 25, token),
      "received" => service.FetchReceivedAsync(null, 25, token),
      "create" => service.CreateAsync("household", "invitee", null, token),
      "revoke" => service.RevokeAsync("household", "invitation", token),
      "accept" => service.AcceptAsync("invitation", token),
      "decline" => service.DeclineAsync("invitation", token),
      _ => throw new ArgumentOutOfRangeException(nameof(action)),
    });

    Assert.Equal((HttpStatusCode)status, error.StatusCode);
  }

  [Theory]
  [InlineData("HOUSEHOLD_LIMIT_REACHED")]
  [InlineData("HOUSEHOLD_MEMBER_LIMIT_REACHED")]
  [InlineData("INDIVIDUAL_HOUSEHOLD_LIMIT_REACHED")]
  public async Task AcceptancePreservesProducerConflictCode(string code)
  {
    using var handler = new InvitationHandler("accept", HttpStatusCode.Conflict, "{\"code\":\"" + code + "\"}");
    using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test") };
    var service = new ApiHouseholdInvitationService(new VouchaApiClient(http));
    var error = await Assert.ThrowsAsync<VouchaApiException>(() => service.AcceptAsync("invite", TestContext.Current.CancellationToken));
    Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
    Assert.Equal(code, error.ErrorCode);
  }

  [Fact]
  public void ListsOmitUnspecifiedPaginationAndServiceRejectsMissingClient()
  {
    Assert.Empty(VouchaApiEndpoints.HouseholdInvitations("household").Query);
    Assert.Empty(VouchaApiEndpoints.ReceivedHouseholdInvitations().Query);
    Assert.Throws<ArgumentNullException>(() => new ApiHouseholdInvitationService(null!));
  }

  [Fact]
  public void NullableUsernamesRemainNullAndLifecycleDatesDecode()
  {
    var root = JsonNode.Parse(Fixture("sent"))!;
    var invitation = root["results"]![0]!;
    foreach (var field in new[] { "invited_user", "invited_by", "household_owner" })
      invitation[field]!["username"] = null;
    invitation["relationship"] = null;
    invitation["accepted_at"] = "2026-07-03T00:00:00Z";
    invitation["declined_at"] = "2026-07-04T00:00:00Z";
    invitation["revoked_at"] = "2026-07-05T00:00:00Z";
    root["page_info"]!["has_next_page"] = true;
    root["page_info"]!["end_cursor"] = "opaque+/=";

    var decoded = root.Deserialize<HouseholdInvitationsResponse>(VouchaApiJson.Options)!;
    var item = Assert.Single(decoded.Results);
    Assert.Null(item.InvitedUser.Username);
    Assert.Null(item.InvitedBy.Username);
    Assert.Null(item.HouseholdOwner.Username);
    Assert.Null(item.Relationship);
    Assert.Equal(3, item.AcceptedAt!.Value.Day);
    Assert.Equal(4, item.DeclinedAt!.Value.Day);
    Assert.Equal(5, item.RevokedAt!.Value.Day);
    Assert.True(decoded.PageInfo.HasNextPage);
    Assert.Equal("opaque+/=", decoded.PageInfo.EndCursor);
  }

  private static string Fixture(string action) => action switch
  {
    "sent" or "received" => $$$"""
        {"results":[{{{InvitationJson}}}],"page_info":{
          "end_cursor":null,"has_next_page":false,"start_cursor":"first"}}
        """,
    "create" => $$$"""{"invitation":{{{InvitationJson}}}}""",
    "accept" => """
        {"membership":{"id":"membership","household_id":"00000000-0000-7000-8000-000000000101",
          "individual":{"id":"individual","user_id":"invitee","username":"household-invitee",
            "updated_at":"2026-07-02T00:00:00Z"},
          "relationship":"partner","updated_at":"2026-07-02T00:00:00Z"}}
        """,
    _ => throw new ArgumentOutOfRangeException(nameof(action)),
  };

  private const string InvitationJson = """
      {"id":"00000000-0000-7000-8000-000000000401",
        "household_id":"00000000-0000-7000-8000-000000000101",
        "invited_user_id":"invitee","invited_by_id":"owner",
        "invited_user":{"id":"invitee","username":"household-invitee"},
        "invited_by":{"id":"owner","username":"household-owner"},
        "household_owner":{"id":"owner","username":"household-owner"},
        "relationship":"partner","accepted_at":null,"declined_at":null,"revoked_at":null,
        "created_at":"2026-07-02T00:00:00Z","updated_at":"2026-07-02T00:00:00Z"}
      """;

  private sealed class InvitationHandler(string action, HttpStatusCode status, string errorBody = "{}") : HttpMessageHandler
  {
    public HttpMethod? Method { get; private set; }
    public string? Path { get; private set; }
    public string? Body { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
      Method = request.Method;
      Path = request.RequestUri!.PathAndQuery;
      Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
      return new HttpResponseMessage(status)
      {
        Content = status == HttpStatusCode.NoContent ? null : new StringContent(
            (int)status >= 400 ? errorBody : Fixture(action), Encoding.UTF8, "application/json"),
      };
    }
  }
}
