using System.Collections.ObjectModel;

namespace Voucha.Client.Core.Content;

public enum NativeHtmlInlineKind
{
  Text,
  Link,
  Code
}

public sealed record NativeHtmlInline(
    NativeHtmlInlineKind Kind,
    string Text,
    string? Href = null,
    bool Strong = false,
    bool Emphasis = false);

public enum NativeHtmlBlockKind
{
  Paragraph,
  Heading,
  Quote,
  Code,
  ListItem,
  Image,
  Rule
}

public sealed record NativeHtmlBlock(
    NativeHtmlBlockKind Kind,
    IReadOnlyList<NativeHtmlInline> Inlines,
    int Level = 0,
    string? ImageSource = null,
    string? ImageAlt = null,
    bool HasAuthoredImageAlt = false);

public sealed class NativeHtmlDocument
{
  public NativeHtmlDocument(IReadOnlyList<NativeHtmlBlock> blocks) =>
      Blocks = new ReadOnlyCollection<NativeHtmlBlock>(blocks.ToArray());

  public IReadOnlyList<NativeHtmlBlock> Blocks { get; }

  public string PlainText => string.Join(
      "\n",
      Blocks.Select(block => string.Concat(block.Inlines.Select(run => run.Text))).Where(text => !string.IsNullOrWhiteSpace(text)));
}
