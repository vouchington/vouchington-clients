using System.Globalization;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App;

public sealed class UiLocalizedValueConverter(IUiLocalization localization) : IMultiValueConverter
{
    private readonly UiLocalizedValueFormatter formatter = new(localization);

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var value = values.FirstOrDefault();
        return value is null || ReferenceEquals(value, BindableProperty.UnsetValue)
            ? string.Empty
            : formatter.Format(value, parameter as string);
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
