using System.Net;
using System.Text;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.Api;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class NativeMarkdownEditorViewAutocompleteTests
{
  [Fact]
  public async Task UserAutocompleteFollowsCursorToFillPage()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var handler = new QueuedResponseHandler();
    handler.Enqueue(SearchResponseJson(["al0", "al1", "al2", "al3", "al4"], "cursor-1", hasNextPage: true));
    handler.Enqueue(SearchResponseJson(["al5", "al6", "al7", "al8"], null, hasNextPage: false));
    var view = new NativeMarkdownEditorView
    {
      ApiClient = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
    };

    Descendants<Editor>(view).First().Text = "@al";
    await WaitForSuggestionsAsync(view, 9);

    Assert.Equal(2, handler.Queries.Count);
    Assert.DoesNotContain("after=", handler.Queries[0]);
    Assert.Contains("after=cursor-1", handler.Queries[1]);
    var labels = SuggestionButtons(view).Select(button => button.Text).ToArray();
    Assert.Equal(["@al0", "@al1", "@al2", "@al3", "@al4", "@al5", "@al6", "@al7", "@al8"], labels);
  }

  [Fact]
  public async Task UserAutocompleteStopsAtPageSafetyBoundWithoutFillingPage()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application();
    var handler = new QueuedResponseHandler();
    for (var page = 0; page < 5; page++)
    {
      handler.Enqueue(SearchResponseJson([$"al{page}"], $"cursor-{page}", hasNextPage: true));
    }

    var view = new NativeMarkdownEditorView
    {
      ApiClient = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") }),
    };

    Descendants<Editor>(view).First().Text = "@al";
    await WaitForSuggestionsAsync(view, 5);

    Assert.Equal(5, handler.Queries.Count);
    Assert.Equal(5, SuggestionButtons(view).Count());
  }

  private static async Task WaitForSuggestionsAsync(NativeMarkdownEditorView view, int count)
  {
    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    while (SuggestionButtons(view).Count() < count)
    {
      cancellation.Token.ThrowIfCancellationRequested();
      await Task.Delay(10, CancellationToken.None);
    }
  }

  private static IEnumerable<Button> SuggestionButtons(Element root) =>
      Descendants<Button>(root).Where(button => button.Text is { } text && text.StartsWith('@'));

  private static string SearchResponseJson(IReadOnlyList<string> usernames, string? endCursor, bool hasNextPage)
  {
    var results = string.Join(",", usernames.Select((username, index) =>
        $$"""{"id":"u{{index}}-{{username}}","username":"{{username}}"}"""));
    var cursor = endCursor is null ? "null" : $"\"{endCursor}\"";
    return $$$"""
        {"results":[{{{results}}}],"page_info":{"end_cursor":{{{cursor}}},"has_next_page":{{{(hasNextPage ? "true" : "false")}}},"start_cursor":null}}
        """;
  }

  private static IEnumerable<T> Descendants<T>(Element root) where T : Element =>
      root.GetVisualTreeDescendants().OfType<T>();

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => new ImmediateDispatcher();
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }

  private sealed class QueuedResponseHandler : HttpMessageHandler
  {
    private readonly Queue<string> responses = new();
    public List<string> Queries { get; } = [];

    public void Enqueue(string json) => responses.Enqueue(json);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      Queries.Add(request.RequestUri?.Query ?? "");
      if (responses.Count == 0)
      {
        throw new InvalidOperationException("No queued response for " + request.RequestUri);
      }

      return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
      {
        Content = new StringContent(responses.Dequeue(), Encoding.UTF8, "application/json"),
      });
    }
  }
}
