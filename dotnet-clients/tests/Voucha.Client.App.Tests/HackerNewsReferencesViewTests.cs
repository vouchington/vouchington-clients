using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Content;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class HackerNewsReferencesViewTests
{
  [Fact]
  public async Task CompactAndDetailControlsRenderStoredReferencesAndOpenCanonicalLinks()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var empty = new HackerNewsReferencesView();
    Assert.False(empty.IsVisible);
    Assert.Null(empty.Presentation);
    var launcher = new RecordingLauncher();
    var presentation = new HackerNewsPresentation(new(1, 1,
        [new(303, 301, DateTimeOffset.UnixEpoch)], [new(301, 42, DateTimeOffset.UnixEpoch, 7)]));
    var compact = new HackerNewsReferencesView { Presentation = presentation };
    Assert.True(compact.IsVisible);
    Assert.Equal("Seen on Hacker News · 1 · Mentioned · 1", Assert.IsType<Label>(Assert.Single(compact.Children)).Text);

    var detail = new HackerNewsReferencesView(launcher) { Presentation = presentation };
    detail.ShowDetails = true;
    Assert.True(detail.ShowDetails);
    Assert.Equal(["Seen on Hacker News · 1 · Mentioned · 1", "Seen on Hacker News", "Mentioned on Hacker News"],
        detail.Children.OfType<Label>().Select(label => label.Text));
    var links = detail.Children.OfType<Button>().ToArray();
    Assert.Equal("42 points · 7 comments", links[0].Text);
    Assert.StartsWith("Comment on ", links[1].Text, StringComparison.Ordinal);
    links[0].SendClicked();
    links[1].SendClicked();
    await launcher.Opened.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
    Assert.Equal(["https://news.ycombinator.com/item?id=301", "https://news.ycombinator.com/item?id=303"],
        launcher.Urls.Select(url => url.AbsoluteUri));

    detail.Presentation = new(new(1, 0));
    Assert.Single(detail.Children);
    detail.Presentation = new(new(0, 0));
    Assert.False(detail.IsVisible);
    Assert.Empty(detail.Children);
    compact.Presentation = null;
    Assert.False(compact.IsVisible);
  }

  private sealed class RecordingLauncher : ILauncher
  {
    public List<Uri> Urls { get; } = [];
    public TaskCompletionSource Opened { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<bool> CanOpenAsync(Uri uri) => Task.FromResult(true);
    public Task<bool> OpenAsync(Uri uri)
    {
      Urls.Add(uri);
      if (Urls.Count == 2) Opened.TrySetResult();
      return Task.FromResult(true);
    }
    public Task<bool> TryOpenAsync(Uri uri) => OpenAsync(uri);
    public Task<bool> OpenAsync(OpenFileRequest request) => Task.FromResult(false);
  }
}
