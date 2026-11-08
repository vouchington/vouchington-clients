using System.Globalization;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Engineering;

public enum PsqlPartitionRangeError
{
  None,
  InvalidDate,
  InvalidOrder,
  InvalidRowLimit,
  InvalidJobType,
  MultipleFamilies,
}

public static class PsqlPartitionRange
{
  public static PsqlPartitionRangeError Validate(PsqlJobBody body)
  {
    ArgumentNullException.ThrowIfNull(body);
    if (body.ClassifierRange is not null && body.RequestRange is not null)
      return PsqlPartitionRangeError.MultipleFamilies;
    var range = body.ClassifierRange ?? body.RequestRange;
    if (range is null) return PsqlPartitionRangeError.None;
    if (body.Type is not ("createPartitions" or "cleanupPartitions"))
      return PsqlPartitionRangeError.InvalidJobType;
    if (!TryDate(range.FromDate, out var from) || !TryDate(range.ToDate, out var to))
      return PsqlPartitionRangeError.InvalidDate;
    if (from >= to) return PsqlPartitionRangeError.InvalidOrder;
    return range.MaxDefaultRows is < 1 or > 5000
        ? PsqlPartitionRangeError.InvalidRowLimit
        : PsqlPartitionRangeError.None;
  }

  private static bool TryDate(string value, out DateOnly date) =>
      DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
      && date.Year >= 1970;
}
