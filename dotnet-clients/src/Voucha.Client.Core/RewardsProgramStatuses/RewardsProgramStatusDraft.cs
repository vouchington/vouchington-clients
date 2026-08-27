using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.RewardsProgramStatuses;

public sealed class RewardsProgramStatusDraft : INotifyPropertyChanged
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
  public event PropertyChangedEventHandler? PropertyChanged;
  public RewardsProgramStatus? Original { get; }
  public string Since { get => since; set => Set(ref since, value ?? string.Empty); }
  public string Until { get => until; set => Set(ref until, value ?? string.Empty); }
  public bool SinceEnabled
  {
    get => sinceEnabled;
    set
    {
      if (value && !sinceEnabled && string.IsNullOrWhiteSpace(Since)) Since = Format(DateOnly.FromDateTime(DateTime.Today));
      Set(ref sinceEnabled, value);
    }
  }
  public bool UntilEnabled
  {
    get => untilEnabled;
    set
    {
      if (value && !untilEnabled && string.IsNullOrWhiteSpace(Until)) Until = Format(DateOnly.FromDateTime(DateTime.Today));
      Set(ref untilEnabled, value);
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
  private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
  {
    if (EqualityComparer<T>.Default.Equals(field, value)) return;
    field = value;
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
  }
}
