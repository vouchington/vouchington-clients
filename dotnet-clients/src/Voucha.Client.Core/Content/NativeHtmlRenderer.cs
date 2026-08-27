using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Content;

public static partial class NativeHtmlRenderer
{
  private static readonly HtmlParser Parser = new();

  public static NativeHtmlDocument Parse(
      string? html,
      string? fallback = null,
      string? imageLabel = null)
  {
    if (string.IsNullOrWhiteSpace(html))
    {
      return string.IsNullOrWhiteSpace(fallback)
          ? new NativeHtmlDocument([])
          : new NativeHtmlDocument([Paragraph([Text(fallback)])]);
    }

    var document = Parser.ParseDocument(html);
    var effectiveImageLabel = imageLabel
        ?? UiLocalization.English.Localize(UiMessageKey.NativeDotnetResidualImage);
    foreach (var image in document.QuerySelectorAll("img"))
    {
      if (string.IsNullOrWhiteSpace(image.GetAttribute("alt")))
      {
        image.SetAttribute("alt", effectiveImageLabel);
      }
    }
    var blocks = new List<NativeHtmlBlock>();
    if (document.Body is { } body)
    {
      foreach (var child in body.ChildNodes)
      {
        AppendNode(child, blocks);
      }
    }

    return blocks.Count == 0 && !string.IsNullOrWhiteSpace(fallback)
        ? new NativeHtmlDocument([Paragraph([Text(fallback)])])
        : new NativeHtmlDocument(blocks);
  }

  private static void AddBlock(List<NativeHtmlBlock> blocks, NativeHtmlBlock block)
  {
    if (IsVisible(block)) blocks.Add(block);
  }

  private static bool IsVisible(NativeHtmlBlock block) =>
      block.Kind is NativeHtmlBlockKind.Rule or NativeHtmlBlockKind.Image ||
      block.Inlines.Any(inline => !string.IsNullOrWhiteSpace(inline.Text));

  private static void AppendNode(INode node, List<NativeHtmlBlock> blocks)
  {
    if (node is IText textNode)
    {
      var text = Collapse(textNode.TextContent);
      if (!string.IsNullOrWhiteSpace(text)) blocks.Add(Paragraph([Text(text)]));
      return;
    }

    if (node is not IElement element) return;

    if (IsUnsafeElement(element)) return;

    switch (element.TagName.ToUpperInvariant())
    {
      case "H1":
      case "H2":
      case "H3":
      case "H4":
      case "H5":
      case "H6":
        AddBlock(blocks, new NativeHtmlBlock(NativeHtmlBlockKind.Heading, InlineChildren(element), HeadingLevel(element)));
        return;
      case "P":
      case "DIV":
      case "SECTION":
      case "ARTICLE":
        AddBlock(blocks, Paragraph(InlineChildren(element)));
        return;
      case "BLOCKQUOTE":
        AddBlock(blocks, new NativeHtmlBlock(NativeHtmlBlockKind.Quote, QuoteChildren(element)));
        return;
      case "PRE":
        AddBlock(blocks, new NativeHtmlBlock(NativeHtmlBlockKind.Code, [Text(TrimRendererTrailingLineBreak(element.TextContent))]));
        return;
      case "UL":
      case "OL":
        AppendList(element, blocks, element.TagName.Equals("OL", StringComparison.OrdinalIgnoreCase));
        return;
      case "TABLE":
        AppendTable(element, blocks);
        return;
      case "HR":
        blocks.Add(new NativeHtmlBlock(NativeHtmlBlockKind.Rule, []));
        return;
      case "IMG":
        blocks.Add(new NativeHtmlBlock(
            NativeHtmlBlockKind.Image,
            [],
            ImageSource: element.GetAttribute("src"),
            ImageAlt: element.GetAttribute("alt")));
        return;
      default:
        var runs = InlineChildren(element);
        if (runs.Count > 0) AddBlock(blocks, Paragraph(runs));
        return;
    }
  }

  private static void AppendList(IElement element, List<NativeHtmlBlock> blocks, bool ordered)
  {
    var index = 1;
    foreach (var child in element.Children.Where(child => child.TagName.Equals("LI", StringComparison.OrdinalIgnoreCase)))
    {
      var prefix = ordered ? $"{index}. " : "- ";
      var childRuns = InlineChildren(child, skipNestedLists: true);
      if (childRuns.Any(inline => !string.IsNullOrWhiteSpace(inline.Text)))
      {
        var runs = new List<NativeHtmlInline> { Text(prefix) };
        runs.AddRange(childRuns);
        AddBlock(blocks, new NativeHtmlBlock(NativeHtmlBlockKind.ListItem, runs));
      }
      foreach (var nestedList in child.Children.Where(IsListElement))
      {
        AppendList(nestedList, blocks, nestedList.TagName.Equals("OL", StringComparison.OrdinalIgnoreCase));
      }
      index += 1;
    }
  }

  private static void AppendTable(IElement element, List<NativeHtmlBlock> blocks)
  {
    foreach (var row in element.QuerySelectorAll("tr").OfType<IElement>())
    {
      var runs = new List<NativeHtmlInline>();
      foreach (var cell in row.Children.Where(IsTableCell))
      {
        if (runs.Count > 0) runs.Add(Text(" | "));
        runs.AddRange(InlineChildren(cell));
      }
      AddBlock(blocks, Paragraph(runs));
    }
  }

  private static bool IsTableCell(IElement element) =>
      element.TagName.Equals("TH", StringComparison.OrdinalIgnoreCase) ||
      element.TagName.Equals("TD", StringComparison.OrdinalIgnoreCase);

  private static bool IsListElement(IElement element) =>
      element.TagName.Equals("UL", StringComparison.OrdinalIgnoreCase) ||
      element.TagName.Equals("OL", StringComparison.OrdinalIgnoreCase);

  private static NativeHtmlBlock Paragraph(IReadOnlyList<NativeHtmlInline> inlines) =>
      new(NativeHtmlBlockKind.Paragraph, inlines);

  private static NativeHtmlInline Text(string? text, bool strong = false, bool emphasis = false) =>
      new(NativeHtmlInlineKind.Text, text ?? string.Empty, Strong: strong, Emphasis: emphasis);

  private static int HeadingLevel(IElement element) =>
      int.TryParse(element.TagName[1..], out var level) ? level : 2;

  private static string TrimRendererTrailingLineBreak(string? value)
  {
    if (string.IsNullOrEmpty(value)) return string.Empty;
    if (value.EndsWith("\r\n", StringComparison.Ordinal)) return value[..^2];
    if (value.EndsWith('\n') || value.EndsWith('\r')) return value[..^1];
    return value;
  }

  private static string Collapse(string? value) =>
      CollapseWhitespace(value, trim: true);
}
