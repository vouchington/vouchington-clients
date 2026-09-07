using Voucha.Client.Core.Content;
using Xunit;

namespace Voucha.Client.Core.Tests.Content;

public sealed class NativeHtmlRendererTests
{
  [Fact]
  public void ParseDropsUnsafeTagsAndPreservesSafeText()
  {
    var document = NativeHtmlRenderer.Parse("<p>Hello <strong>world</strong><script>alert(1)</script></p>");

    var block = Assert.Single(document.Blocks);
    Assert.Equal(NativeHtmlBlockKind.Paragraph, block.Kind);
    Assert.Equal("Hello world", document.PlainText);
    Assert.Contains(block.Inlines, inline => inline.Text == "world" && inline.Strong);
    Assert.DoesNotContain("alert", document.PlainText, StringComparison.Ordinal);
  }

  [Fact]
  public void ParsePreservesLinksAndLists()
  {
    var document = NativeHtmlRenderer.Parse("<ol><li><a href=\"/posts/abc\">First</a></li><li>Second</li></ol>");

    Assert.Equal(2, document.Blocks.Count);
    Assert.Equal("1. First\n2. Second", document.PlainText);
    var link = Assert.Single(document.Blocks[0].Inlines, inline => inline.Kind == NativeHtmlInlineKind.Link);
    Assert.Equal("/posts/abc", link.Href);
  }

  [Fact]
  public void ParseCoversNativeBlockKinds()
  {
    var document = NativeHtmlRenderer.Parse(
        """
        <h3>Heading</h3>
        <blockquote>Quoted <em>line</em></blockquote>
        <pre>let x = 1</pre>
        <hr>
        <img src="/images/a.png" alt="Alt text">
        <section><span>Nested text</span></section>
        """);

    Assert.Equal(NativeHtmlBlockKind.Heading, document.Blocks[0].Kind);
    Assert.Equal(3, document.Blocks[0].Level);
    Assert.Equal(NativeHtmlBlockKind.Quote, document.Blocks[1].Kind);
    Assert.Contains(document.Blocks[1].Inlines, inline => inline.Text == "line" && inline.Emphasis);
    Assert.Equal(NativeHtmlBlockKind.Code, document.Blocks[2].Kind);
    Assert.Equal("let x = 1", document.Blocks[2].Inlines[0].Text);
    Assert.Equal(NativeHtmlBlockKind.Rule, document.Blocks[3].Kind);
    Assert.Equal(NativeHtmlBlockKind.Image, document.Blocks[4].Kind);
    Assert.Equal("/images/a.png", document.Blocks[4].ImageSource);
    Assert.Equal("Alt text", document.Blocks[4].ImageAlt);
    Assert.True(document.Blocks[4].HasAuthoredImageAlt);
    Assert.Equal(NativeHtmlBlockKind.Paragraph, document.Blocks[5].Kind);
    Assert.Equal("Nested text", document.Blocks[5].Inlines[0].Text);
  }

  [Fact]
  public void ParseDistinguishesLocalizedImageFallbackFromAuthoredAltText()
  {
    var block = Assert.Single(NativeHtmlRenderer.Parse("<img src=\"image.png\">", imageLabel: "Image").Blocks);

    Assert.Equal("Image", block.ImageAlt);
    Assert.False(block.HasAuthoredImageAlt);
  }

  [Fact]
  public void ParseCoversInlineVariants()
  {
    var document = NativeHtmlRenderer.Parse(
        """
        <p>
          <b>Bold</b><br>
          <i>Soft</i>
          <code>Code</code>
          <img alt="Logo">
          <span>Nested <strong>strong</strong></span>
          <button>Drop</button>
        </p>
        """);

    var block = Assert.Single(document.Blocks);
    Assert.Contains(block.Inlines, inline => inline.Text == "Bold" && inline.Strong);
    Assert.Contains(block.Inlines, inline => inline.Text == "\n");
    Assert.Contains(block.Inlines, inline => inline.Text == "Soft" && inline.Emphasis);
    Assert.Contains(block.Inlines, inline => inline.Kind == NativeHtmlInlineKind.Code && inline.Text == "Code");
    Assert.Contains(block.Inlines, inline => inline.Text.Contains("[Logo]", StringComparison.Ordinal));
    Assert.Contains(block.Inlines, inline => inline.Text.Contains("Nested", StringComparison.Ordinal));
    Assert.Contains(block.Inlines, inline => inline.Text == "strong" && inline.Strong);
    Assert.DoesNotContain("Drop", document.PlainText, StringComparison.Ordinal);
  }

  [Fact]
  public void ParsePreservesCollapsedWhitespaceBetweenInlineRuns()
  {
    var document = NativeHtmlRenderer.Parse("<p><strong>Bold</strong> <em>soft</em></p>");

    Assert.Equal("Bold soft", document.PlainText);
  }

  [Fact]
  public void ParsePreservesSoftBreakWhitespaceBetweenInlineRuns()
  {
    var document = NativeHtmlRenderer.Parse("<p><strong>Bold</strong>\n<em>soft</em></p>");

    Assert.Equal("Bold soft", document.PlainText);
  }

  [Fact]
  public void ParseFiltersUnsafeChildrenInsideLinks()
  {
    var document = NativeHtmlRenderer.Parse("<p><a href=\"https://example.test\"><script>alert(1)</script>safe</a></p>");

    var block = Assert.Single(document.Blocks);
    var link = Assert.Single(block.Inlines);
    Assert.Equal(NativeHtmlInlineKind.Link, link.Kind);
    Assert.Equal("safe", link.Text);
    Assert.Equal("https://example.test", link.Href);
  }

  [Fact]
  public void ParseDropsResourceLoadingUnsafeTags()
  {
    var document = NativeHtmlRenderer.Parse(
        "<p>safe</p><svg><text>drop</text></svg><object>drop</object><template>drop</template>");

    Assert.Equal("safe", document.PlainText);
  }

  [Fact]
  public void ParsePreservesBlockquoteParagraphBreaks()
  {
    var document = NativeHtmlRenderer.Parse("<blockquote><p>one</p><p>two</p></blockquote>");

    Assert.Equal("one\ntwo", document.PlainText);
  }

  [Fact]
  public void ParsePreservesBlockquoteListStructure()
  {
    var document = NativeHtmlRenderer.Parse("<blockquote><ul><li>one</li><li>two</li></ul></blockquote>");

    Assert.Equal("- one\n- two", document.PlainText);
  }

  [Fact]
  public void ParsePreservesNestedListItemBoundaries()
  {
    var document = NativeHtmlRenderer.Parse("<ul><li>parent<ul><li>child</li></ul></li></ul>");

    Assert.Equal("- parent\n- child", document.PlainText);
  }

  [Fact]
  public void ParsePreservesTableCellBoundaries()
  {
    var document = NativeHtmlRenderer.Parse("<table><tr><th>A</th><th>B</th></tr><tr><td>1</td><td>2</td></tr></table>");

    Assert.Equal("A | B\n1 | 2", document.PlainText);
  }

  [Fact]
  public void ParsePreservesCodeBlockIndentation()
  {
    var document = NativeHtmlRenderer.Parse("<pre><code>  first\n    second\n</code></pre>");

    var block = Assert.Single(document.Blocks);
    Assert.Equal("  first\n    second", block.Inlines[0].Text);
  }

  [Fact]
  public void ParseWrapsLooseTextAndUsesFallbackWhenAllHtmlIsUnsafe()
  {
    var loose = NativeHtmlRenderer.Parse("loose text");
    var unsafeOnly = NativeHtmlRenderer.Parse("<script>alert(1)</script>", "fallback text");
    var blank = NativeHtmlRenderer.Parse(" ");

    Assert.Equal("loose text", loose.PlainText);
    Assert.Equal("fallback text", unsafeOnly.PlainText);
    Assert.Empty(blank.Blocks);
  }

  [Fact]
  public void ParseUsesFallbackWhenHtmlIsBlank()
  {
    var document = NativeHtmlRenderer.Parse("", "raw markdown");

    var block = Assert.Single(document.Blocks);
    Assert.Equal(NativeHtmlBlockKind.Paragraph, block.Kind);
    Assert.Equal("raw markdown", document.PlainText);
  }

  [Fact]
  public void ParseUsesFallbackWhenSafeWrapperHasNoVisibleContent()
  {
    var document = NativeHtmlRenderer.Parse("<p><script>alert(1)</script></p>", "raw markdown");

    var block = Assert.Single(document.Blocks);
    Assert.Equal(NativeHtmlBlockKind.Paragraph, block.Kind);
    Assert.Equal("raw markdown", document.PlainText);
  }
}
