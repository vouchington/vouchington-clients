using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

[JsonConverter(typeof(JsonStringEnumConverter<AccountType>))]
public enum AccountType
{
  [JsonStringEnumMemberName("official")]
  Official,
  [JsonStringEnumMemberName("system")]
  System,
  [JsonStringEnumMemberName("ai_agent")]
  AiAgent,
}
