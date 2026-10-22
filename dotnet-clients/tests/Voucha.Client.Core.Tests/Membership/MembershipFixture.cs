using System.Text.Json;
using System.Text.Json.Serialization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.NativeBilling;

internal static class MembershipFixture
{
  internal static readonly JsonSerializerOptions ReEncode = new(VouchaApiJson.Options)
  {
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
  };

  internal static string Json(string name) =>
      File.ReadAllText(FilamentsContractPaths.ApiFixture($"responses/{name}.json"));

  internal static T Decode<T>(string name) =>
      JsonSerializer.Deserialize<T>(Json(name), VouchaApiJson.Options)
      ?? throw new InvalidOperationException($"Fixture {name} did not decode.");

  internal static void AssertRoundTrip<T>(string name)
  {
    var json = Json(name);
    var dto = JsonSerializer.Deserialize<T>(json, VouchaApiJson.Options);
    var encoded = JsonSerializer.Serialize(dto, ReEncode);
    var missing = JsonKeyPaths.Extract(json);
    missing.ExceptWith(JsonKeyPaths.Extract(encoded));
    Assert.True(missing.Count == 0, $"{name} dropped {string.Join(", ", missing.OrderBy(path => path))}.");
  }
}
