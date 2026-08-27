using Voucha.Client.Core.Content;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Controls;

public sealed partial class NativeHtmlContentView :
    ContentView,
    IUiLocaleChangeListener
{
  public static readonly BindableProperty HtmlProperty = BindableProperty.Create(
      nameof(Html),
      typeof(string),
      typeof(NativeHtmlContentView),
      propertyChanged: OnContentChanged);

  public static readonly BindableProperty FallbackProperty = BindableProperty.Create(
      nameof(Fallback),
      typeof(string),
      typeof(NativeHtmlContentView),
      propertyChanged: OnContentChanged);

  public static readonly BindableProperty MaxLinesProperty = BindableProperty.Create(
      nameof(MaxLines),
      typeof(int),
      typeof(NativeHtmlContentView),
      -1,
      propertyChanged: OnContentChanged);

  public string? Html
  {
    get => (string?)GetValue(HtmlProperty);
    set => SetValue(HtmlProperty, value);
  }

  public string? Fallback
  {
    get => (string?)GetValue(FallbackProperty);
    set => SetValue(FallbackProperty, value);
  }

  public int MaxLines
  {
    get => (int)GetValue(MaxLinesProperty);
    set => SetValue(MaxLinesProperty, value);
  }

  private static void OnContentChanged(BindableObject bindable, object oldValue, object newValue)
  {
    if (bindable is NativeHtmlContentView view) view.Render();
  }

  private void Render()
  {
    var document = NativeHtmlRenderer.Parse(
        Html,
        Fallback,
        UiCopy.Localize(UiMessageKey.NativeDotnetResidualImage));
    if (MaxLines > 0)
    {
      var label = LabelFor(document);
      Content = string.IsNullOrWhiteSpace(label.Text) && label.FormattedText.Spans.Count == 0 ? null : label;
      return;
    }

    var stack = new VerticalStackLayout { Spacing = 8 };
    foreach (var block in document.Blocks)
    {
      stack.Children.Add(ViewFor(block));
    }

    Content = stack.Children.Count == 0 ? null : stack;
  }

  private Label LabelFor(NativeHtmlDocument document)
  {
    var label = new Label
    {
      FormattedText = Formatted(document.Blocks),
      LineBreakMode = LineBreakMode.TailTruncation,
      MaxLines = MaxLines,
    };
    return label;
  }

  private View ViewFor(NativeHtmlBlock block) =>
      block.Kind switch
      {
        NativeHtmlBlockKind.Rule => new BoxView { HeightRequest = 1, Color = Colors.LightGray },
        NativeHtmlBlockKind.Image => ImagePlaceholder(block),
        _ => LabelFor(block),
      };

  private Label LabelFor(NativeHtmlBlock block)
  {
    var label = new Label
    {
      FormattedText = Formatted(block.Inlines),
      LineBreakMode = LineBreakMode.WordWrap,
    };
    if (MaxLines > 0) label.MaxLines = MaxLines;
    switch (block.Kind)
    {
      case NativeHtmlBlockKind.Heading:
        label.FontAttributes = FontAttributes.Bold;
        label.FontSize = block.Level <= 2 ? 20 : 16;
        break;
      case NativeHtmlBlockKind.Quote:
        label.TextColor = Colors.DimGray;
        break;
      case NativeHtmlBlockKind.Code:
        label.FontFamily = "Courier";
        break;
    }

    return label;
  }

  private static FormattedString Formatted(IReadOnlyList<NativeHtmlBlock> blocks)
  {
    var formatted = new FormattedString();
    foreach (var block in blocks)
    {
      if (formatted.Spans.Count > 0) formatted.Spans.Add(new Span { Text = "\n" });
      foreach (var span in Formatted(block.Inlines).Spans)
      {
        formatted.Spans.Add(Clone(span));
      }
    }

    return formatted;
  }

  private static Label ImagePlaceholder(NativeHtmlBlock block) =>
      new()
      {
        Text = string.IsNullOrWhiteSpace(block.ImageAlt)
            ? UiCopy.Localize(UiMessageKey.NativeDotnetResidualImage)
            : block.ImageAlt,
        FontAttributes = FontAttributes.Italic,
        TextColor = Colors.DimGray,
      };

  private static FormattedString Formatted(IReadOnlyList<NativeHtmlInline> inlines)
  {
    var formatted = new FormattedString();
    foreach (var inline in inlines)
    {
      Uri? linkUrl = null;
      if (inline.Kind == NativeHtmlInlineKind.Link &&
          Uri.TryCreate(inline.Href, UriKind.Absolute, out var parsedUrl) &&
          IsSafeLinkScheme(parsedUrl))
      {
        linkUrl = parsedUrl;
      }

      var hasAbsoluteLink = linkUrl is not null;
      var span = new Span
      {
        Text = inline.Text,
        FontAttributes = Attributes(inline),
        TextDecorations = hasAbsoluteLink ? TextDecorations.Underline : TextDecorations.None,
      };
      if (hasAbsoluteLink) span.TextColor = Colors.Blue;
      if (inline.Kind == NativeHtmlInlineKind.Code) span.FontFamily = "Courier";
      if (linkUrl is { } url)
      {
        span.GestureRecognizers.Add(new TapGestureRecognizer
        {
          Command = new Command(() => _ = OpenLinkAsync(url)),
        });
      }

      formatted.Spans.Add(span);
    }

    return formatted;
  }

  private static async Task OpenLinkAsync(Uri url)
  {
    try
    {
      await Launcher.OpenAsync(url).ConfigureAwait(true);
    }
    catch (Exception)
    {
    }
  }

  private static FontAttributes Attributes(NativeHtmlInline inline)
  {
    var attrs = FontAttributes.None;
    if (inline.Strong) attrs |= FontAttributes.Bold;
    if (inline.Emphasis) attrs |= FontAttributes.Italic;
    return attrs;
  }

  private static bool IsSafeLinkScheme(Uri url) =>
      url.Scheme is "http" or "https" or "mailto";
}
