using System.Globalization;
using Microsoft.Maui.Controls;

namespace Voucha.Client.App.Controls;

public partial class DirectMessageParticipantView : ContentView
{
  public DirectMessageParticipantView() => InitializeComponent();
}

public sealed class DirectMessageParticipantAvatarColumnWidthConverter : IValueConverter
{
  public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      value is true ? new GridLength(44) : new GridLength(0);

  public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
      throw new NotSupportedException();
}
