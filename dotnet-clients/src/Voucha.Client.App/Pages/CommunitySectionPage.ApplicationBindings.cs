using System.Globalization;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public abstract partial class CommunitySectionPage
{
  private static MultiBinding ApplicationRowValueBinding(string path, string? format = null)
  {
    var resources = Application.Current?.Resources
        ?? throw new InvalidOperationException("Application resources are not available.");
    var version = resources["UiLocaleVersion"] as UiLocaleVersion
        ?? throw new InvalidOperationException("UiLocaleVersion is not registered.");
    var valueConverter = resources["UiLocalizedValue"] as IMultiValueConverter
        ?? throw new InvalidOperationException("UiLocalizedValue converter is not registered.");
    return new MultiBinding
    {
      Converter = format is null ? new ApplicationTextConverter(UiCopy.CurrentLocalization) : valueConverter,
      ConverterParameter = format,
      Bindings =
      {
        new Binding(path),
        new Binding(nameof(UiLocaleVersion.Version), source: version),
      },
    };
  }

  private sealed class ApplicationTextConverter(IUiLocalization localization) : IMultiValueConverter
  {
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values.FirstOrDefault() is UiText text ? localization.Resolve(text) : string.Empty;

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
  }
}
