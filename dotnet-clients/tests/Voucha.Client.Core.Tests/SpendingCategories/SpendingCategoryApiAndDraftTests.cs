using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.SpendingCategories;
using Xunit;

namespace Voucha.Client.Core.Tests.SpendingCategories;

public sealed class SpendingCategoryApiAndDraftTests
{
  [Fact]
  public void ReadsMoneyDefaultsCapabilityAndForwardsCursorSearchFlag()
  {
    const string json = """{"results":[{"id":"entry","spending_category_id":"topic","amount":{"amount":150,"currency":"usd"},"spending_frequency":"monthly","note":null,"owner_type":"individual","spending_category":{"id":"topic","name":"Food","slug":"food"}}]}""";
    var response = JsonSerializer.Deserialize<SpendingCategoriesResponse>(json, VouchaApiJson.Options)!;
    Assert.Equal(new Money(150, "usd"), response.Results.Single().Amount);
    Assert.True(response.Results.Single().CanManage);
    var list = VouchaApiEndpoints.SpendingCategories("cursor", 2);
    Assert.Equal("cursor", list.Query["after"]);
    Assert.Equal("true", VouchaApiEndpoints.SpendingCategoryTopics("food").Query["spending_category"]);
  }

  [Fact]
  public void DraftParsesLocalizedFrequencyAndClearsEmptyNoteWithoutNoOpMutation()
  {
    var original = new SpendingCategory(
        "entry", "topic", new Money(150, "eur"), "monthly", "note", "individual", true,
        new SpendingCategorySummary("topic", "Food", "food"));
    var draft = new SpendingCategoryDraft(original, CultureInfo.GetCultureInfo("fr-FR")) { Amount = "1,5" };
    Assert.Equal("eur", draft.Currency);
    Assert.True(draft.TryBuildUpdate(out var unchanged)); Assert.Null(unchanged);
    draft.Frequency = "annually"; draft.Note = string.Empty;
    Assert.True(draft.TryBuildUpdate(out var update));
    var body = JsonSerializer.SerializeToNode(update, VouchaApiJson.Options)!.AsObject();
    Assert.Equal("annually", body["spending_frequency"]!.GetValue<string>());
    Assert.Null(body["note"]);
    draft.Amount = "-1"; Assert.False(draft.TryBuildUpdate(out _));
    draft.Amount = "1,001"; Assert.False(draft.TryBuildUpdate(out _));
  }

  [Theory]
  [InlineData("usd", "90071992547409.91", 9007199254740991)]
  [InlineData("jpy", "9007199254740991", 9007199254740991)]
  public void DraftAcceptsExactMoneyMaximum(string currency, string input, long expectedAmount)
  {
    var draft = new SpendingCategoryDraft(CultureInfo.InvariantCulture)
    {
      Amount = input,
      Currency = currency,
    };

    Assert.True(draft.TryBuildCreate("topic", out var body));
    Assert.Equal(new Money(expectedAmount, currency), body!.Amount);
  }

  [Theory]
  [InlineData("usd", "90071992547409.92")]
  [InlineData("usd", "79228162514264337593543950335")]
  [InlineData("jpy", "9007199254740992")]
  [InlineData("jpy", "79228162514264337593543950335")]
  public void DraftRejectsAmountsAboveMoneyMaximumWithoutOverflow(string currency, string input)
  {
    var draft = new SpendingCategoryDraft(CultureInfo.InvariantCulture)
    {
      Amount = input,
      Currency = currency,
    };

    Assert.False(draft.TryBuildCreate("topic", out _));
  }
}
