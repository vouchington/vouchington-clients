namespace Voucha.Client.App.Controls;

public sealed partial class NativeHtmlContentView
{
  private static Span Clone(Span span)
  {
    var clone = new Span
    {
      Text = span.Text,
      FontAttributes = span.FontAttributes,
      TextDecorations = span.TextDecorations,
      FontFamily = span.FontFamily,
    };
    if (span.TextColor is not null) clone.TextColor = span.TextColor;
    foreach (var recognizer in span.GestureRecognizers.Select(Clone))
    {
      clone.GestureRecognizers.Add(recognizer);
    }
    return clone;
  }

  private static IGestureRecognizer Clone(IGestureRecognizer recognizer) =>
      recognizer switch
      {
        TapGestureRecognizer tap => new TapGestureRecognizer
        {
          Command = tap.Command,
          CommandParameter = tap.CommandParameter,
          NumberOfTapsRequired = tap.NumberOfTapsRequired,
          Buttons = tap.Buttons,
        },
        _ => recognizer,
      };
}
