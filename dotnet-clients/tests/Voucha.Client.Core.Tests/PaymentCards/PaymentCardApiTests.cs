using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.PaymentCards;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.PaymentCards;

public sealed class PaymentCardApiTests
{
  [Fact]
  public async Task FixturesDecodeStrictDatesAndIntegerMoney()
  {
    var page = Decode<PaymentCardsResponse>("native.cards.page-1");
    Assert.Equal(new DateOnly(2024, 1, 20), page.Results[0].OpenedOn);
    Assert.Equal(new Money(0, "usd"), page.Results[0].CreditLimit);
    Assert.Equal("Sapphire Reserve", page.Results[0].AuthorizedUserOfCard?.Card.Name);

    var handler = new RecordingHandler([new RecordedResponse(ApiFixtureLoader.LoadResponse("native.cards.page-1"))]);
    var service = new ApiPaymentCardsService(new VouchaApiClient(
        new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }));
    var domain = await service.FetchAsync(limit: 2, cancellationToken: TestContext.Current.CancellationToken);
    Assert.Equal(new Money(0, "usd"), domain.Results[0].CreditLimit);
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PaymentCardsResponse>(
        ApiFixtureLoader.LoadResponse("native.cards.page-1").Replace("2024-01-20", "01/20/2024", StringComparison.Ordinal),
        VouchaApiJson.Options));
  }

  [Fact]
  public void EndpointsForwardCursorAndSerializePatchNullsAndNumbers()
  {
    var list = VouchaApiEndpoints.PaymentCards("cursor value", 2);
    Assert.Equal("cursor value", list.Query["after"]);
    Assert.Equal("2", list.Query["limit"]);
    var update = VouchaApiEndpoints.UpdatePaymentCard(
        "card/id",
        new UpdatePaymentCardBody(
            JsonNullableDate.FromDate(new DateOnly(2024, 1, 20)),
            JsonNullableDate.Null,
            CreditLimit: JsonNullableMoney.FromMoney(new Money(0, "usd")),
            AuthorizedUserOfId: JsonNullableString.Null));
    var json = JsonSerializer.SerializeToNode(update.Body, VouchaApiJson.Options)!.AsObject();
    Assert.Equal("2024-01-20", json["opened_on"]!.GetValue<string>());
    Assert.Null(json["closed_on"]);
    Assert.Equal(0, json["credit_limit"]!["amount"]!.GetValue<long>());
    Assert.Equal("usd", json["credit_limit"]!["currency"]!.GetValue<string>());
    Assert.Null(json["authorized_user_of_id"]);
    Assert.False(json.ContainsKey("note"));
    Assert.Equal("/api/v1/my/cards/card%2Fid", update.Path);
  }

  [Fact]
  public void CardTopicSearchUsesTheCardFilter()
  {
    var request = VouchaApiEndpoints.PaymentCardTopics("Freedom");
    Assert.Equal("Freedom", request.Query["q"]);
    Assert.Equal("card", request.Query["topic_types"]);
    Assert.Equal("10", request.Query["limit"]);
    Assert.Single(Decode<TopicSearchResponse>("native.card-topics.search.default").Results);
  }

  private static T Decode<T>(string id) => JsonSerializer.Deserialize<T>(
      ApiFixtureLoader.LoadResponse(id), VouchaApiJson.Options)!;
}
