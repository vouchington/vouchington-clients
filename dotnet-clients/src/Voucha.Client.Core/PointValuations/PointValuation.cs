using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.PointValuations;

public sealed record PointValuation(
    string Id,
    string RewardsProgramId,
    ScaledMoney ValuePerPoint,
    string? Note,
    RewardsProgramSummary RewardsProgram)
{
  public const long MaximumValuePerPointAmount = 9_999_999_999;

  internal static PointValuation FromWire(PointValuationWire wire)
  {
    ArgumentNullException.ThrowIfNull(wire);
    return new(wire.Id, wire.RewardsProgramId, wire.ValuePerPoint, wire.Note, wire.RewardsProgram);
  }
}

public sealed record RewardsProgramOption(string Id, string Name, string Slug);
