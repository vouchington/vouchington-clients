using System.Net;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class VouchaApiClientCrmTests
{
  [Fact]
  public async Task FetchCrmContactsAsyncDecodesTheSharedFixture()
  {
    var (client, handler) = CreateClient(ApiFixtureLoader.LoadResponse("native.crm.contacts.default"));

    var response = await client.FetchCrmContactsAsync(new CrmContactsRequest("alice", "new", "credit_cards", null, null, 25), TestContext.Current.CancellationToken);

    Assert.Equal("00000000-0000-7000-8000-000000000584", response.Results[0].Id);
    Assert.Equal("Alice Creator", response.Results[0].Name);
    Assert.Equal("alice@example.test", response.Results[0].Email);
    Assert.Equal(CrmContactVertical.CreditCards, response.Results[0].Vertical);
    Assert.Equal("crm_contact", response.Results[0].EntityType);
    Assert.Equal("/api/v1/crm/contacts?limit=25&q=alice&status=new&vertical=credit_cards", handler.Requests.Single().PathAndQuery);
  }

  [Fact]
  public async Task ImportCrmContactsAsyncReturnsValidationInsteadOfThrowingOn422()
  {
    var (client, handler) = CreateClient(
        ApiFixtureLoader.LoadResponse("native.crm.import.validation.default"),
        HttpStatusCode.UnprocessableEntity);

    var response = await client.ImportCrmContactsAsync("name,email,follower_count\nAlice Creator,alice@example.test,-1", TestContext.Current.CancellationToken);

    Assert.False(response.Valid);
    Assert.Equal("follower_count must be a non-negative integer", response.Validation!.Rows[0].Errors[0]);
    Assert.Equal("/api/v1/imports/crm-contacts", handler.Requests.Single().PathAndQuery);
  }

  private static (VouchaApiClient Client, RecordingHandler Handler) CreateClient(string body, HttpStatusCode statusCode = HttpStatusCode.OK)
  {
    var handler = new RecordingHandler(body, statusCode);
    return (new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }), handler);
  }
}
