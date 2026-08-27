using Voucha.Client.Core.ImportExport;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.ImportExport;

public sealed class ImportExportValidationTests
{
  [Fact]
  public void CsvValidationCountsQuotedMultilineFieldsAsOneRecord()
  {
    var rows = Enumerable.Range(1, 500)
        .Select(i => $"https://example.test/{i},\"Feed {i}\ncontinued\"");
    var fiveHundred = "\"url\",title\n" + string.Join('\n', rows);

    Assert.Null(ImportExportValidation.Validate(new(SourceImportFormat.Csv, Text: fiveHundred)));
    Assert.Equal(
        "Import up to 500 sources at a time.",
        Resolve(ImportExportValidation.Validate(new(
            SourceImportFormat.Csv,
            Text: fiveHundred + "\nhttps://example.test/501,Feed 501"))));
  }

  [Fact]
  public void CsvValidationHandlesEscapedQuotesAndCommasInHeaderTokens()
  {
    const string csv = "\"url\",\"title, display\"\nhttps://example.test/feed,\"A \"\"quoted\"\" title\"";

    Assert.Null(ImportExportValidation.Validate(new(SourceImportFormat.Csv, Text: csv)));
  }

  [Fact]
  public void CsvValidationIgnoresBlankLinesAndTrailingNewline()
  {
    var rows = string.Join('\n', Enumerable.Range(1, 500).Select(i => $"https://example.test/{i}"));
    var csv = $"url\n\n{rows}\n\n";

    Assert.Null(ImportExportValidation.Validate(new(SourceImportFormat.Csv, Text: csv)));
  }

  [Fact]
  public void CsvValidationIgnoresWhitespaceOnlyLinesBeforeHeader()
  {
    var rows = string.Join('\n', Enumerable.Range(1, 500).Select(i => $"https://example.test/{i}"));
    var csv = $"  \t  \nurl\n{rows}";

    Assert.Null(ImportExportValidation.Validate(new(SourceImportFormat.Csv, Text: csv)));
  }

  [Fact]
  public void CsvValidationCountsRowsWithEmptyFirstFieldAndDelimiter()
  {
    var rows = string.Join('\n', Enumerable.Repeat(",title", 501));

    Assert.Equal(
        "Add at least one source.",
        Resolve(ImportExportValidation.Validate(new(SourceImportFormat.Csv, Text: $"url,title\n{rows}"))));
  }

  [Fact]
  public void CsvValidationCountsParsedUrlsAcrossRecognizedHeadersAndFallbacks()
  {
    Assert.Null(ValidateCsv("\uFEFFxmlUrl,title\nhttps://example.test/feed,Feed"));
    Assert.Null(ValidateCsv("rss_feed_url,name\nhttps://example.test/rss,Feed"));
    Assert.Null(ValidateCsv("https://example.test/one\n https://example.test/two "));
    Assert.Null(ValidateCsv("title\turl\nOne\thttps://example.test/one\nEmpty\t"));
    Assert.Equal("Add at least one source.", ValidateCsv("title\tname\nOne\tFeed"));
  }

  [Fact]
  public void CsvValidationRejectsMalformedQuotedAndMismatchedRows()
  {
    Assert.Equal("Invalid CSV format.", ValidateCsv("url\n\"unclosed"));
    Assert.Equal("Invalid CSV format.", ValidateCsv("url,title\nhttps://example.test/feed"));
    Assert.Equal("Invalid CSV format.", ValidateCsv("url\nbad\"quote"));
  }

  [Fact]
  public void CsvParserMatchesBackendBomAndRecordDelimiterSemantics()
  {
    Assert.Equal(
        ["https://example.test/one", "https://example.test/two"],
        SourceImportParser.CsvUrls("url\nhttps://example.test/one\r\nhttps://example.test/two"));
    Assert.Equal(
        ["https://example.test/one\nhttps://example.test/two"],
        SourceImportParser.CsvUrls("url\rhttps://example.test/one\nhttps://example.test/two"));
    Assert.Equal(
        ["\uFEFF\uFEFFurl", "https://example.test/feed"],
        SourceImportParser.CsvUrls("\uFEFF\uFEFFurl\nhttps://example.test/feed"));
    Assert.Equal(
        ["   ", "https://example.test/feed"],
        SourceImportParser.CsvUrls("title\turl\ttype\nEmpty\t   \tArticle\nFeed\thttps://example.test/feed\tArticle"));
  }

  [Fact]
  public void CsvValidationAppliesLimitToNonemptyParsedUrlValues()
  {
    var urls = Enumerable.Repeat("https://example.test/feed,Feed", 500);
    var emptyUrls = Enumerable.Repeat(",Empty", 501);
    var withinLimit = "url,title\n" + string.Join('\n', urls.Concat(emptyUrls));

    Assert.Null(ValidateCsv(withinLimit));
    Assert.Equal(
        "Import up to 500 sources at a time.",
        ValidateCsv(withinLimit + "\nhttps://example.test/overflow,Overflow"));
  }

  [Fact]
  public void OpmlValidationMatchesBackendOutlineExtraction()
  {
    const string opml = """
        <!-- <outline xmlUrl="https://example.test/comment" /> -->
        <outline xmlUrl='https://example.test/single-quoted' />
        <outline xmlUrl="" />
        <outline XMLURL="https://example.test/malformed-tail" not-an-attribute>
        <not-outline xmlUrl="https://example.test/not-outline" />
        """;

    Assert.Equal(
        ["https://example.test/comment", "https://example.test/malformed-tail"],
        SourceImportParser.OpmlUrls(opml));
    Assert.Null(ImportExportValidation.Validate(new(SourceImportFormat.Opml, Text: opml)));
    Assert.Equal(
        "Add at least one source.",
        Resolve(ImportExportValidation.Validate(new(SourceImportFormat.Opml, Text: "<outline xmlUrl='' />"))));
  }

  private static string? ValidateCsv(string csv) =>
      Resolve(ImportExportValidation.Validate(new(SourceImportFormat.Csv, Text: csv)));

  private static string? Resolve(UiText? text) =>
      text is { } value ? UiLocalization.English.Resolve(value) : null;
}
