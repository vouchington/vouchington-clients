using System.Text.Json;
using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public static class VouchaApiJson
{
  public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
  {
    AllowOutOfOrderMetadataProperties = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
  };
}
