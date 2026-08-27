using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.PointValuations;
using Xunit;

namespace Voucha.Client.Core.Tests.PointValuations;

public sealed class PointValuationApiAndDraftTests
{
  [Fact]
  public void ReadsScaleSixPointValue()
  {
    const string json = """{"results":[{"id":"v","rewards_program_id":"r","value_per_point":{"amount":35000,"currency":"usd","scale":6},"note":null,"rewards_program":{"id":"r","name":"Rewards","slug":"rewards"}}]}""";
    var response = JsonSerializer.Deserialize<PointValuationsResponse>(json, VouchaApiJson.Options)!;
    Assert.Equal(35_000, response.Results.Single().ValuePerPoint.Amount);
    Assert.Null(response.PageInfo);
  }

  [Fact]
  public void EndpointsForwardCursorFilterAndNumericBodies()
  {
    var list = VouchaApiEndpoints.PointValuations("next value", 2);
    Assert.Equal("next value", list.Query["after"]);
    Assert.Equal("2", list.Query["limit"]);
    var search = VouchaApiEndpoints.RewardsProgramTopics("travel");
    Assert.Equal("rewards_program", search.Query["topic_types"]);
    Assert.Equal("10", search.Query["limit"]);
    var patch = VouchaApiEndpoints.UpdatePointValuation(
        "value/id", new UpdatePointValuationBody(new ScaledMoney(35_000, "usd"), JsonNullableString.Null));
    var body = JsonSerializer.SerializeToNode(patch.Body, VouchaApiJson.Options)!.AsObject();
    Assert.Equal(35_000, body["value_per_point"]!["amount"]!.GetValue<long>());
    Assert.Null(body["note"]);
    Assert.Equal("/api/v1/my/rewards-program-point-valuations/value%2Fid", patch.Path);
  }

  [Theory]
  [InlineData("en-US", "1.25000", 1.25)]
  [InlineData("fr-FR", "1,25000", 1.25)]
  [InlineData("en-US", "0", 0)]
  public void DraftParsesLocalizedWholeDecimalAndDoesNotForceTrailingZeros(
      string cultureName, string text, decimal expected)
  {
    var draft = new PointValuationDraft(CultureInfo.GetCultureInfo(cultureName)) { ValuePerPoint = text };
    Assert.True(draft.TryBuildCreate("program", out var body));
    Assert.Equal(expected, body!.ValuePerPoint.ToMajorUnits());
    var existing = Valuation(expected);
    Assert.Equal(
        expected.ToString("0.############################", CultureInfo.InvariantCulture),
        new PointValuationDraft(existing, CultureInfo.InvariantCulture).ValuePerPoint);
  }

  [Theory]
  [InlineData("en-US", "1.000000", "1.0000000")]
  [InlineData("fr-FR", "1,000000", "1,0000000")]
  public void DraftEnforcesSourceTextScaleUsingLocalizedDecimalSeparator(
      string cultureName, string accepted, string rejected)
  {
    var draft = new PointValuationDraft(CultureInfo.GetCultureInfo(cultureName))
    {
      ValuePerPoint = accepted,
    };
    Assert.True(draft.TryBuildCreate("program", out _));

    draft.ValuePerPoint = rejected;
    Assert.False(draft.TryBuildCreate("program", out _));
  }

  [Fact]
  public void DraftRejectsNegativeAndBuildsNoOpOrExplicitNoteClear()
  {
    var original = Valuation(1.5m) with { Note = "memo" };
    var draft = new PointValuationDraft(original, CultureInfo.InvariantCulture);
    Assert.True(draft.TryBuildUpdate(out var unchanged));
    Assert.Null(unchanged);
    draft.Note = string.Empty;
    Assert.True(draft.TryBuildUpdate(out var clear));
    Assert.Null(clear!.Note!.Value.Value);
    draft.ValuePerPoint = "-1";
    Assert.False(draft.TryBuildUpdate(out _));
  }

  [Fact]
  public void DraftEncodesMicrocentUsdValueAndCanChangeToJpy()
  {
    var create = new PointValuationDraft(CultureInfo.InvariantCulture) { ValuePerPoint = "0.035" };
    Assert.True(create.TryBuildCreate("program", out var body));
    Assert.Equal(new ScaledMoney(35_000, "usd"), body!.ValuePerPoint);

    var original = Valuation(1m);
    var edit = new PointValuationDraft(original, CultureInfo.InvariantCulture) { Currency = "jpy" };
    Assert.True(edit.TryBuildUpdate(out var patch));
    Assert.Equal(new ScaledMoney(1_000_000, "jpy"), patch!.ValuePerPoint);
  }

  [Fact]
  public void DraftEnforcesPointValuationBusinessMaximum()
  {
    var draft = new PointValuationDraft(CultureInfo.InvariantCulture)
    {
      ValuePerPoint = "9999.999999",
    };
    Assert.True(draft.TryBuildCreate("program", out var maximum));
    Assert.Equal(PointValuation.MaximumValuePerPointAmount, maximum!.ValuePerPoint.Amount);

    draft.ValuePerPoint = "10000";
    Assert.False(draft.TryBuildCreate("program", out _));
  }

  [Fact]
  public void DraftRejectsHugeValuesWithoutOverflowing()
  {
    var hugeValue = decimal.MaxValue.ToString(CultureInfo.InvariantCulture);
    var create = new PointValuationDraft(CultureInfo.InvariantCulture)
    {
      ValuePerPoint = hugeValue,
    };
    Assert.False(create.TryBuildCreate("program", out var createBody));
    Assert.Null(createBody);

    var update = new PointValuationDraft(Valuation(1m), CultureInfo.InvariantCulture)
    {
      ValuePerPoint = hugeValue,
    };
    Assert.False(update.TryBuildUpdate(out var updateBody));
    Assert.Null(updateBody);
  }

  private static PointValuation Valuation(decimal value) =>
      new(
          "v",
          "r",
          new ScaledMoney(decimal.ToInt64(value * 1_000_000), "usd"),
          null,
          new RewardsProgramSummary("r", "Rewards", "rewards"));
}
