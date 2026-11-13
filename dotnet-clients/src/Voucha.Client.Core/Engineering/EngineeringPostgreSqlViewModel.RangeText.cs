using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Engineering;

public sealed partial class EngineeringPostgreSqlViewModel
{
  public string? ClassifierRangeErrorMessage => RangeErrorMessage(requestFamily: false);
  public string? RequestRangeErrorMessage => RangeErrorMessage(requestFamily: true);

  /// <summary>Localized create/retire title for the exact family and action in <paramref name="body"/>.</summary>
  public string RangeConfirmationTitle(PsqlJobBody body)
  {
    ArgumentNullException.ThrowIfNull(body);
    var retire = IsRetire(body);
    return localization.Localize(body.RequestRange is not null
        ? retire ? UiMessageKey.EngineeringRequestRangeRetire : UiMessageKey.EngineeringRequestRangeCreate
        : retire ? UiMessageKey.EngineeringClassifierRangeRetire : UiMessageKey.EngineeringClassifierRangeCreate);
  }

  /// <summary>Distinct request-history or classifier confirmation text with the frozen UTC bounds.</summary>
  public string RangeConfirmationMessage(PsqlJobBody body)
  {
    ArgumentNullException.ThrowIfNull(body);
    var retire = IsRetire(body);
    var range = body.RequestRange ?? body.ClassifierRange
        ?? throw new ArgumentException("A partition range is required.", nameof(body));
    var key = body.RequestRange is not null
        ? retire ? UiMessageKey.EngineeringRequestRangeConfirmRetire : UiMessageKey.EngineeringRequestRangeConfirmCreate
        : retire ? UiMessageKey.EngineeringClassifierRangeConfirmRetire : UiMessageKey.EngineeringClassifierRangeConfirmCreate;
    return localization.Format(key, ("fromDate", range.FromDate), ("toDate", range.ToDate));
  }

  private static bool IsRetire(PsqlJobBody body) => body.Type == "cleanupPartitions";

  private string? RangeErrorMessage(bool requestFamily)
  {
    if (RangeError == PsqlPartitionRangeError.None || rangeErrorIsRequest != requestFamily) return null;
    return localization.Localize(requestFamily
        ? UiMessageKey.EngineeringRequestRangeInvalidRange
        : UiMessageKey.EngineeringClassifierRangeInvalidRange);
  }

  private void NotifyRangeErrorMessages()
  {
    OnPropertyChanged(nameof(ClassifierRangeErrorMessage));
    OnPropertyChanged(nameof(RequestRangeErrorMessage));
  }
}
