namespace Voucha.Client.Core.Localization;

internal readonly record struct LocalizedMonthYear(DateOnly Value) : IFormattable
{
  public string ToString(string? format, IFormatProvider? formatProvider) =>
      Value.ToString("Y", formatProvider);
}
