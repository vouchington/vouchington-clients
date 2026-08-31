using System.Text;
using Voucha.Client.Core.ImportExport;
using Xunit;

namespace Voucha.Client.Core.Tests.ImportExport;

public sealed class ImportExportFileAdapterTests
{
  [Theory]
  [InlineData("feeds.csv", SourceImportFormat.Csv, "url\nhttps://example.test/feed")]
  [InlineData("feeds.opml", SourceImportFormat.Opml, "<opml><outline xmlUrl=\"https://example.test/feed\"/></opml>")]
  public async Task PickReadsUtf8AndDerivesNativeFormat(string fileName, SourceImportFormat format, string content)
  {
    var picker = new RecordingPicker(fileName, content);
    var sharer = new RecordingSharer();
    var adapter = new ImportExportFileAdapter(picker, new StrictUtf8ImportExportFileReader(), sharer);

    var result = await adapter.PickSourceFileAsync(TestContext.Current.CancellationToken);

    Assert.Equal(format, result?.Format);
    Assert.Equal(content, result?.Text);
  }

  [Fact]
  public async Task PickRejectsExtensionsOutsideCsvAndOpml()
  {
    var adapter = new ImportExportFileAdapter(
        new RecordingPicker("feeds.txt", "https://example.test/feed"),
        new ThrowingReader(new InvalidOperationException("reader must not run")),
        new RecordingSharer());

    var error = await Assert.ThrowsAsync<InvalidDataException>(
        () => adapter.PickSourceFileAsync(TestContext.Current.CancellationToken));

    Assert.Equal("Choose a CSV or OPML file.", error.Message);
  }

  [Fact]
  public async Task ShareDelegatesExactPortableDocument()
  {
    var sharer = new RecordingSharer();
    var adapter = new ImportExportFileAdapter(new RecordingPicker(null, null), new StrictUtf8ImportExportFileReader(), sharer);
    using var document = ImportExportTestDocuments.Create();

    await adapter.ShareAsync(document, TestContext.Current.CancellationToken);

    Assert.Same(document, sharer.Document);
    Assert.Equal(TestContext.Current.CancellationToken, sharer.Token);
  }

  [Fact]
  public async Task PickerCancellationPropagatesToPageBoundary()
  {
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    var adapter = new ImportExportFileAdapter(
        new ThrowingPicker(new OperationCanceledException(cancellation.Token)),
        new StrictUtf8ImportExportFileReader(),
        new RecordingSharer());

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.PickSourceFileAsync(cancellation.Token));
  }

  [Fact]
  public async Task ReaderFailurePropagatesToPageBoundary()
  {
    var adapter = new ImportExportFileAdapter(
        new RecordingPicker("feeds.csv", "ignored"),
        new ThrowingReader(new DecoderFallbackException("invalid UTF-8")),
        new RecordingSharer());

    var error = await Assert.ThrowsAsync<DecoderFallbackException>(
        () => adapter.PickSourceFileAsync(TestContext.Current.CancellationToken));

    Assert.Equal("invalid UTF-8", error.Message);
  }

  [Fact]
  public async Task ReaderRejectsKnownOversizedStreamBeforeReading()
  {
    var stream = new TrackingMemoryStream(new byte[ImportExportValidation.MaximumBodyBytes + 1]);
    var file = new PickedImportFile("feeds.csv", _ => Task.FromResult<Stream>(stream));

    var error = await Assert.ThrowsAsync<InvalidDataException>(
        () => new StrictUtf8ImportExportFileReader().ReadUtf8Async(file, TestContext.Current.CancellationToken));

    Assert.Equal(0, stream.ReadCalls);
    Assert.Equal("Import files must be 2 MiB or smaller.", error.Message);
  }

  [Fact]
  public async Task ReaderBoundsNonSeekableStreamBeforeDecoding()
  {
    await using var stream = new NonSeekableReadStream(ImportExportValidation.MaximumBodyBytes + 1);
    var file = new PickedImportFile("feeds.csv", _ => Task.FromResult<Stream>(stream));

    await Assert.ThrowsAsync<InvalidDataException>(
        () => new StrictUtf8ImportExportFileReader().ReadUtf8Async(file, TestContext.Current.CancellationToken));

    Assert.Equal(ImportExportValidation.MaximumBodyBytes + 1, stream.BytesRead);
  }

  [Fact]
  public async Task ShareFailurePropagatesToPageBoundary()
  {
    var adapter = new ImportExportFileAdapter(
        new RecordingPicker(null, null),
        new StrictUtf8ImportExportFileReader(),
        new ThrowingSharer(new IOException("share failed")));

    var error = await Assert.ThrowsAsync<IOException>(() => adapter.ShareAsync(
        ImportExportTestDocuments.Create(),
        TestContext.Current.CancellationToken));

    Assert.Equal("share failed", error.Message);
  }

  private sealed class RecordingPicker(string? fileName, string? content) : IImportExportFilePicker
  {
    public Task<PickedImportFile?> PickAsync(CancellationToken token) => Task.FromResult(
        fileName is null ? null : new PickedImportFile(
            fileName,
            _ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(content!)))));
  }

  private sealed class RecordingSharer : IImportExportDocumentSharer
  {
    public ExportDocument? Document { get; private set; }
    public CancellationToken Token { get; private set; }
    public Task ShareAsync(ExportDocument document, CancellationToken token)
    {
      Document = document;
      Token = token;
      return Task.CompletedTask;
    }
  }

  private sealed class ThrowingPicker(Exception exception) : IImportExportFilePicker
  {
    public Task<PickedImportFile?> PickAsync(CancellationToken token) => Task.FromException<PickedImportFile?>(exception);
  }

  private sealed class ThrowingReader(Exception exception) : IImportExportFileReader
  {
    public Task<string> ReadUtf8Async(PickedImportFile file, CancellationToken token) => Task.FromException<string>(exception);
  }

  private sealed class ThrowingSharer(Exception exception) : IImportExportDocumentSharer
  {
    public Task ShareAsync(ExportDocument document, CancellationToken token) => Task.FromException(exception);
  }

  private sealed class TrackingMemoryStream(byte[] buffer) : MemoryStream(buffer)
  {
    public int ReadCalls { get; private set; }

    public override int Read(byte[] buffer, int offset, int count)
    {
      ReadCalls++;
      return base.Read(buffer, offset, count);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
      ReadCalls++;
      return base.ReadAsync(buffer, cancellationToken);
    }
  }

  private sealed class NonSeekableReadStream(int length) : Stream
  {
    private int remaining = length;
    public int BytesRead { get; private set; }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
      var count = Math.Min(remaining, buffer.Length);
      buffer.Span[..count].Fill((byte)'a');
      remaining -= count;
      BytesRead += count;
      return ValueTask.FromResult(count);
    }
  }
}
