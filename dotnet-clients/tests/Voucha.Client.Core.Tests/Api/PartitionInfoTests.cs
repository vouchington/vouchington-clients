using System.Text.Json;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Api;

public sealed class PartitionInfoTests
{
  [Theory]
  [InlineData("check-pending", PartitionDropRefusal.CheckPending)]
  [InlineData("in-flight", PartitionDropRefusal.InFlight)]
  [InlineData("inside-retention", PartitionDropRefusal.InsideRetention)]
  [InlineData("legal-hold", PartitionDropRefusal.LegalHold)]
  [InlineData("no-declared-retention", PartitionDropRefusal.NoDeclaredRetention)]
  public void DropRefusalWireValuesRoundTrip(string wire, PartitionDropRefusal expected)
  {
    var json = $$"""{"name":"posts_1","size_bytes":256,"drop_refusal":"{{wire}}"}""";
    var partition = JsonSerializer.Deserialize<PartitionInfo>(json, VouchaApiJson.Options)!;
    Assert.Equal("posts_1", partition.Name);
    Assert.Equal(256, partition.SizeBytes);
    Assert.Equal(expected, partition.DropRefusal);
    using var encoded = JsonDocument.Parse(JsonSerializer.Serialize(partition, VouchaApiJson.Options));
    Assert.Equal(wire, encoded.RootElement.GetProperty("drop_refusal").GetString());
  }

  [Fact]
  public void ExplicitNullIsRequiredAndRetainedWhenWriting()
  {
    const string json = """{"name":"posts_1","size_bytes":256,"drop_refusal":null}""";
    var partition = JsonSerializer.Deserialize<PartitionInfo>(json, VouchaApiJson.Options)!;
    Assert.Null(partition.DropRefusal);
    using var encoded = JsonDocument.Parse(JsonSerializer.Serialize(partition, VouchaApiJson.Options));
    Assert.Equal(JsonValueKind.Null, encoded.RootElement.GetProperty("drop_refusal").ValueKind);
  }

  [Theory]
  [InlineData("""{"name":"posts_1","size_bytes":256}""")]
  [InlineData("""{"name":"posts_1","size_bytes":256,"drop_refusal":"unknown"}""")]
  [InlineData("""{"name":"posts_1","size_bytes":256,"drop_refusal":3}""")]
  [InlineData("""{"name":"posts_1","size_bytes":256,"drop_refusal":"CHECK-PENDING"}""")]
  public void MissingOrInvalidDropRefusalIsRejected(string json)
  {
    Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PartitionInfo>(json, VouchaApiJson.Options));
  }
}
