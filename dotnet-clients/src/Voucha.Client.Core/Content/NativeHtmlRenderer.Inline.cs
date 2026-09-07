using AngleSharp.Dom;
using System.Text.RegularExpressions;

namespace Voucha.Client.Core.Content;

public static partial class NativeHtmlRenderer
{
  private static List<NativeHtmlInline> InlineChildren(INode node)
  {
    return InlineChildren(node, skipNestedLists: false);
  }

  private static List<NativeHtmlInline> InlineChildren(INode node, bool skipNestedLists)
  {
    var output = new List<NativeHtmlInline>();
    AppendInlineChildren(node, output, strong: false, emphasis: false, skipNestedLists);
    return MergeAdjacentText(output);
  }

  private static void AppendInlineChildren(
      INode node,
      List<NativeHtmlInline> output,
      bool strong,
      bool emphasis,
      bool skipNestedLists)
  {
    foreach (var child in node.ChildNodes)
    {
      if (child is IText textNode)
      {
        var rawText = textNode.TextContent ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawText) && rawText.Contains('\n', StringComparison.Ordinal))
        {
          if (PreserveSoftBreakWhitespace(child, output)) output.Add(Text(" ", strong, emphasis));
          continue;
        }
        var text = NormalizeInlineText(rawText);
        if (text.Length > 0) output.Add(Text(text, strong, emphasis));
        continue;
      }

      if (child is not IElement element) continue;
      if (IsUnsafeElement(element)) continue;
      if (skipNestedLists && IsListElement(element)) continue;

      switch (element.TagName.ToUpperInvariant())
      {
        case "BR":
          output.Add(Text("\n", strong, emphasis));
          break;
        case "STRONG":
        case "B":
          AppendInlineChildren(element, output, strong: true, emphasis, skipNestedLists);
          break;
        case "EM":
        case "I":
          AppendInlineChildren(element, output, strong, emphasis: true, skipNestedLists);
          break;
        case "CODE":
          output.Add(new NativeHtmlInline(NativeHtmlInlineKind.Code, element.TextContent, Strong: strong, Emphasis: emphasis));
          break;
        case "A":
          AppendLinkChildren(element, output, strong, emphasis);
          break;
        case "IMG":
          output.Add(Text($"[{element.GetAttribute("alt") ?? string.Empty}]", strong, emphasis));
          break;
        default:
          AppendInlineChildren(element, output, strong, emphasis, skipNestedLists);
          break;
      }
    }
  }

  private static void AppendLinkChildren(IElement element, List<NativeHtmlInline> output, bool strong, bool emphasis)
  {
    var runs = InlineChildren(element);
    var href = element.GetAttribute("href");
    foreach (var run in runs)
    {
      if (string.IsNullOrWhiteSpace(run.Text)) continue;
      output.Add(new NativeHtmlInline(
          NativeHtmlInlineKind.Link,
          run.Text,
          href,
          strong || run.Strong,
          emphasis || run.Emphasis));
    }
  }

  private static List<NativeHtmlInline> QuoteChildren(
      IElement element,
      IReadOnlySet<IElement> authoredImageAlts)
  {
    if (!element.Children.Any(IsQuoteStructuredChild)) return InlineChildren(element);

    var blocks = new List<NativeHtmlBlock>();
    foreach (var child in element.ChildNodes)
    {
      AppendNode(child, blocks, authoredImageAlts);
    }
    if (blocks.Count == 0) return InlineChildren(element);

    var output = new List<NativeHtmlInline>();
    foreach (var block in blocks)
    {
      var runs = QuoteBlockInlines(block);
      if (runs.Count == 0) continue;
      if (output.Count > 0) output.Add(Text("\n"));
      output.AddRange(runs);
    }

    return MergeAdjacentText(output);
  }

  private static IReadOnlyList<NativeHtmlInline> QuoteBlockInlines(NativeHtmlBlock block) =>
      block.Kind switch
      {
        NativeHtmlBlockKind.Image => [Text($"[{block.ImageAlt ?? string.Empty}]")],
        NativeHtmlBlockKind.Rule => [Text("---")],
        _ => block.Inlines,
      };

  private static bool IsQuoteStructuredChild(IElement element) =>
      element.TagName.Equals("P", StringComparison.OrdinalIgnoreCase) ||
      element.TagName.Equals("DIV", StringComparison.OrdinalIgnoreCase) ||
      element.TagName.Equals("SECTION", StringComparison.OrdinalIgnoreCase) ||
      element.TagName.Equals("ARTICLE", StringComparison.OrdinalIgnoreCase) ||
      element.TagName.Equals("UL", StringComparison.OrdinalIgnoreCase) ||
      element.TagName.Equals("OL", StringComparison.OrdinalIgnoreCase) ||
      element.TagName.Equals("TABLE", StringComparison.OrdinalIgnoreCase);

  private static bool IsUnsafeElement(IElement element) =>
      element.TagName.ToUpperInvariant() is
          "SCRIPT" or
          "STYLE" or
          "IFRAME" or
          "FORM" or
          "INPUT" or
          "BUTTON" or
          "TEXTAREA" or
          "SELECT" or
          "SVG" or
          "OBJECT" or
          "EMBED" or
          "VIDEO" or
          "AUDIO" or
          "META" or
          "BASE" or
          "LINK" or
          "NOSCRIPT" or
          "TEMPLATE";

  private static string NormalizeInlineText(string? value) =>
      CollapseWhitespace(value, trim: false);

  private static bool PreserveSoftBreakWhitespace(INode node, List<NativeHtmlInline> output) =>
      output.Count > 0 &&
      !output[^1].Text.EndsWith('\n') &&
      HasFollowingInlineContent(node);

  private static bool HasFollowingInlineContent(INode node)
  {
    for (var sibling = node.NextSibling; sibling is not null; sibling = sibling.NextSibling)
    {
      if (sibling is IText text && !string.IsNullOrWhiteSpace(text.TextContent)) return true;
      if (sibling is IElement element && !IsUnsafeElement(element)) return true;
    }
    return false;
  }

  private static string CollapseWhitespace(string? value, bool trim)
  {
    var collapsed = Regex.Replace(value ?? string.Empty, @"\s+", " ");
    return trim ? collapsed.Trim() : collapsed;
  }

  private static List<NativeHtmlInline> MergeAdjacentText(List<NativeHtmlInline> runs)
  {
    var output = new List<NativeHtmlInline>();
    foreach (var run in runs)
    {
      if (output.LastOrDefault() is { } previous &&
          previous.Kind == run.Kind &&
          previous.Href == run.Href &&
          previous.Strong == run.Strong &&
          previous.Emphasis == run.Emphasis)
      {
        output[^1] = previous with { Text = previous.Text + run.Text };
      }
      else
      {
        output.Add(run);
      }
    }

    return output;
  }
}
