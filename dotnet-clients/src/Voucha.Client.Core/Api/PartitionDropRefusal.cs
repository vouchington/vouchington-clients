using System.Text.Json.Serialization;

namespace Voucha.Client.Core.Api;

public enum PartitionDropRefusal
{
  [JsonStringEnumMemberName("check-pending")]
  CheckPending,
  [JsonStringEnumMemberName("in-flight")]
  InFlight,
  [JsonStringEnumMemberName("inside-retention")]
  InsideRetention,
  [JsonStringEnumMemberName("legal-hold")]
  LegalHold,
  [JsonStringEnumMemberName("no-declared-retention")]
  NoDeclaredRetention,
}
