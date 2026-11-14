using System.Globalization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.RewardsProgramStatuses;

public sealed class RewardsProgramStatusDraft : ObservableObject
{
  private string since;
  private string until;
  private bool sinceEnabled;
  private bool untilEnabled;
  public RewardsProgramStatusDraft(RewardsProgramStatus? original = null)
  {
    Original = original;
    since = Format(original?.Since); until = Format(original?.Until);
    sinceEnabled = original?.Since is not null; untilEnabled = original?.Until is not null;
  }
  public RewardsProgramStatus? Original { get; }
  public string Since { get => since; set => SetProperty(ref since, value ?? string.Empty); }
  public string Until { get => until; set => SetProperty(ref until, value ?? string.Empty); }
  public bool SinceEnabled
  {
    get => sinceEnabled;
    set
    {
      if (value && !sinceEnabled && string.IsNullOrWhiteSpace(Since)) Since = Format(DateOnly.FromDateTime(DateTime.Today));
      SetProperty(ref sinceEnabled, value);
    }
  }
  public bool UntilEnabled
  {
    get => untilEnabled;
    set
    {
      if (value && !untilEnabled && string.IsNullOrWhiteSpace(Until)) Until = Format(DateOnly.FromDateTime(DateTime.Today));
      SetProperty(ref untilEnabled, value);
    }
  }
  public DateTime SinceDate { get => ParseOrToday(Since); set => Since = value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
  public DateTime UntilDate { get => ParseOrToday(Until); set => Until = value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }

  public bool TryBuildUpdate(out UpdateRewardsProgramStatusBody? body)
  {
    body = null;
    if (Original is null || !TryDate(SinceEnabled ? Since : string.Empty, out var newSince) || !TryDate(UntilEnabled ? Until : string.Empty, out var newUntil) || newSince is { } rangeSince && newUntil is { } rangeUntil && rangeSince > rangeUntil) return false;
    JsonNullableDate? sinceChange = newSince == Original.Since ? null : newSince is { } sinceValue ? JsonNullableDate.FromDate(sinceValue) : JsonNullableDate.Null;
    JsonNullableDate? untilChange = newUntil == Original.Until ? null : newUntil is { } untilValue ? JsonNullableDate.FromDate(untilValue) : JsonNullableDate.Null;
    if (sinceChange is not null || untilChange is not null) body = new(sinceChange, untilChange);
    return true;
  }
  private static string Format(DateOnly? value) => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
  private static DateTime ParseOrToday(string value) => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
      ? date.ToDateTime(TimeOnly.MinValue) : DateTime.Today;
  private static bool TryDate(string value, out DateOnly? date)
  {
    if (string.IsNullOrWhiteSpace(value)) { date = null; return true; }
    if (DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
    {
      date = parsed;
      return true;
    }
    date = null;
    return false;
  }
}
