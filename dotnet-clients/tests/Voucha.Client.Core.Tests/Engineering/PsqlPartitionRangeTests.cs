using System.Text.Json;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Engineering;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Engineering;

public sealed class PsqlPartitionRangeTests
{
  [Theory]
  [InlineData("1969-12-31", "2026-03-01", null, PsqlPartitionRangeError.InvalidDate)]
  [InlineData("2026-02-30", "2026-03-01", null, PsqlPartitionRangeError.InvalidDate)]
  [InlineData("2026-2-01", "2026-03-01", null, PsqlPartitionRangeError.InvalidDate)]
  [InlineData("2026-02-01T00:00:00Z", "2026-03-01", null, PsqlPartitionRangeError.InvalidDate)]
  [InlineData("2026-03-01", "2026-03-01", null, PsqlPartitionRangeError.InvalidOrder)]
  [InlineData("2026-03-02", "2026-03-01", null, PsqlPartitionRangeError.InvalidOrder)]
  [InlineData("2024-02-29", "2024-03-01", 0, PsqlPartitionRangeError.InvalidRowLimit)]
  [InlineData("2024-02-29", "2024-03-01", 5001, PsqlPartitionRangeError.InvalidRowLimit)]
  [InlineData("1970-01-01", "2024-03-01", null, PsqlPartitionRangeError.None)]
  [InlineData("2024-02-29", "2024-03-01", 1, PsqlPartitionRangeError.None)]
  [InlineData("2024-02-29", "2024-03-01", 5000, PsqlPartitionRangeError.None)]
  public void ValidatesBothRangeFamilies(string from, string to, int? rows, PsqlPartitionRangeError expected)
  {
    var range = new PsqlClassifierRange(from, to, rows);
    Assert.Equal(expected, PsqlPartitionRange.Validate(new("createPartitions", ClassifierRange: range)));
    Assert.Equal(expected, PsqlPartitionRange.Validate(new("cleanupPartitions", RequestRange: range)));
  }

  [Fact]
  public void RejectsMixedFamiliesAndUnrelatedJobsButPreservesMonthlyJobs()
  {
    var range = new PsqlClassifierRange("2020-02-01", "2020-02-02");
    Assert.Equal(PsqlPartitionRangeError.MultipleFamilies,
        PsqlPartitionRange.Validate(new("createPartitions", range, range)));
    Assert.Equal(PsqlPartitionRangeError.InvalidJobType,
        PsqlPartitionRange.Validate(new("runMigrations", range)));
    Assert.Equal(PsqlPartitionRangeError.None, PsqlPartitionRange.Validate(new("cleanupPartitions")));
  }

  [Fact]
  public void SerializesExactRangePropertiesAndOmitsOptionalLimitsAndFamilies()
  {
    var range = new PsqlClassifierRange("2020-02-01", "2020-02-02", 100);
    Assert.Equal("{\"type\":\"createPartitions\",\"classifierRange\":{\"fromDate\":\"2020-02-01\",\"toDate\":\"2020-02-02\",\"maxDefaultRows\":100}}",
        JsonSerializer.Serialize(new PsqlJobBody("createPartitions", range), VouchaApiJson.Options));
    Assert.Equal("{\"type\":\"cleanupPartitions\",\"requestRange\":{\"fromDate\":\"2020-02-01\",\"toDate\":\"2020-02-02\"}}",
        JsonSerializer.Serialize(new PsqlJobBody("cleanupPartitions", RequestRange: range with { MaxDefaultRows = null }), VouchaApiJson.Options));
    Assert.Equal("{\"type\":\"createPartitions\"}",
        JsonSerializer.Serialize(new PsqlJobBody("createPartitions"), VouchaApiJson.Options));
  }

  [Theory]
  [InlineData("createPartitions")]
  [InlineData("cleanupPartitions")]
  public void RequestRangeSerializationMatchesTheCanonicalRequestSchema(string type)
  {
    using var contracts = JsonDocument.Parse(File.ReadAllText(FilamentsContractPaths.ApiContract("request-contracts.json")));
    var job = contracts.RootElement.GetProperty("components").GetProperty("PsqlJobRequest");
    var properties = job.GetProperty("properties");
    var requestRange = properties.GetProperty("requestRange");
    Assert.True(JsonElement.DeepEquals(properties.GetProperty("classifierRange"), requestRange));
    Assert.Contains(type, properties.GetProperty("type").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
    Assert.DoesNotContain("requestRange", job.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
    var required = requestRange.GetProperty("required").EnumerateArray().Select(value => Assert.IsType<string>(value.GetString())).ToArray();
    Assert.Equal(["fromDate", "toDate"], required);
    var fields = requestRange.GetProperty("properties");
    var budget = fields.GetProperty("maxDefaultRows");
    Assert.Equal("integer", budget.GetProperty("type").GetString());

    var minimum = budget.GetProperty("minimum").GetInt32();
    var maximum = budget.GetProperty("maximum").GetInt32();
    Assert.Equal(1, minimum);
    Assert.Equal(5000, maximum);
    foreach (var rows in new int?[] { null, minimum, maximum })
    {
      var body = new PsqlJobBody(type, RequestRange: new("2020-02-01", "2020-02-02", rows));
      Assert.Equal(PsqlPartitionRangeError.None, PsqlPartitionRange.Validate(body));
      using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(body, VouchaApiJson.Options));
      Assert.Equal(type, serialized.RootElement.GetProperty("type").GetString());
      Assert.False(serialized.RootElement.TryGetProperty("classifierRange", out _));
      var serializedRange = serialized.RootElement.GetProperty("requestRange");
      Assert.All(required, name => Assert.True(serializedRange.TryGetProperty(name, out _)));
      Assert.All(serializedRange.EnumerateObject(), property => Assert.True(fields.TryGetProperty(property.Name, out _)));
      Assert.Equal(rows.HasValue, serializedRange.TryGetProperty("maxDefaultRows", out var serializedBudget));
      if (rows.HasValue) Assert.Equal(rows.Value, serializedBudget.GetInt32());
    }
    Assert.Equal(PsqlPartitionRangeError.InvalidRowLimit,
        PsqlPartitionRange.Validate(new(type, RequestRange: new("2020-02-01", "2020-02-02", minimum - 1))));
    Assert.Equal(PsqlPartitionRangeError.InvalidRowLimit,
        PsqlPartitionRange.Validate(new(type, RequestRange: new("2020-02-01", "2020-02-02", maximum + 1))));
  }

}
