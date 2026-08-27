using Voucha.Client.Core.Api;
using Voucha.Client.Core.PaymentCards;
using Xunit;

namespace Voucha.Client.Core.Tests.PaymentCards;

public sealed class PaymentCardsSearchTests
{
  [Fact]
  public async Task NewerSearchResultWinsWhenTheOlderSearchCompletesLast()
  {
    var older = PendingSearch();
    var newer = PendingSearch();
    var service = new SearchService(older, newer);
    var viewModel = new PaymentCardsViewModel(service);

    var olderSearch = viewModel.SearchTopicsAsync("older", TestContext.Current.CancellationToken);
    var newerSearch = viewModel.SearchTopicsAsync("newer", TestContext.Current.CancellationToken);
    newer.SetResult([Topic("newer")]);
    await newerSearch;
    older.SetResult([Topic("older")]);
    await olderSearch;

    Assert.Equal(["newer"], viewModel.TopicResults.Select(topic => topic.Name));
    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task BlankSearchInvalidatesAnOlderFailureAndClearsSearchState()
  {
    var seeded = PendingSearch();
    var pending = PendingSearch();
    var viewModel = new PaymentCardsViewModel(new SearchService(seeded, pending));
    var seedSearch = viewModel.SearchTopicsAsync("seed", TestContext.Current.CancellationToken);
    seeded.SetResult([Topic("seed")]);
    await seedSearch;
    Assert.Equal(["seed"], viewModel.TopicResults.Select(topic => topic.Name));
    var olderSearch = viewModel.SearchTopicsAsync("older", TestContext.Current.CancellationToken);

    await viewModel.SearchTopicsAsync("   ", TestContext.Current.CancellationToken);
    pending.SetException(new InvalidOperationException("stale failure"));
    await olderSearch;

    Assert.Empty(viewModel.TopicResults);
    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task EditingVisibleQueryClearsResultsAndInvalidatesPendingSubmission()
  {
    var seeded = PendingSearch();
    var pending = PendingSearch();
    var viewModel = new PaymentCardsViewModel(new SearchService(seeded, pending));
    var seedSearch = viewModel.SearchTopicsAsync("seed", TestContext.Current.CancellationToken);
    seeded.SetResult([Topic("seed")]);
    await seedSearch;
    Assert.Equal(["seed"], viewModel.TopicResults.Select(topic => topic.Name));

    viewModel.TopicSearchQuery = "edited";
    Assert.Empty(viewModel.TopicResults);
    var submitted = viewModel.SearchTopicsAsync("submitted", TestContext.Current.CancellationToken);
    viewModel.TopicSearchQuery = "visible text changed";
    pending.SetResult([Topic("submitted")]);
    await submitted;

    Assert.Equal("visible text changed", viewModel.TopicSearchQuery);
    Assert.Empty(viewModel.TopicResults);
    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task EditingQueryPreservesContinuationErrorAndRetryOwnership()
  {
    var service = new SearchService();
    service.Fetches.Enqueue(() => Task.FromResult(Page(true, "next")));
    service.Fetches.Enqueue(() => Task.FromException<PaymentCardPage>(new InvalidOperationException("offline")));
    service.Fetches.Enqueue(() => Task.FromResult(Page(false, null)));
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    var continuationError = viewModel.ErrorText;

    viewModel.TopicSearchQuery = "edited";

    Assert.True(viewModel.HasContinuationError);
    Assert.Equal(continuationError, viewModel.ErrorText);
    await viewModel.RetryAsync(TestContext.Current.CancellationToken);
    Assert.Equal([null, "next", "next"], service.FetchCursors);
  }

  [Fact]
  public async Task EditingQueryClearsCompletedSearchOwnedError()
  {
    var failed = PendingSearch();
    var viewModel = new PaymentCardsViewModel(new SearchService(failed));
    var search = viewModel.SearchTopicsAsync("submitted", TestContext.Current.CancellationToken);
    failed.SetException(new InvalidOperationException("search failed"));
    await search;
    Assert.True(viewModel.HasError);

    viewModel.TopicSearchQuery = "edited";

    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task RetryAfterSearchFailureResubmitsTheVisibleTrimmedQuery()
  {
    var failed = PendingSearch();
    var retried = PendingSearch();
    var service = new SearchService(failed, retried);
    var viewModel = new PaymentCardsViewModel(service);
    var search = viewModel.SearchTopicsAsync("  current query  ", TestContext.Current.CancellationToken);
    failed.SetException(new InvalidOperationException("search failed"));
    await search;

    var retry = viewModel.RetryAsync(TestContext.Current.CancellationToken);
    retried.SetResult([Topic("result")]);
    await retry;

    Assert.Equal("  current query  ", viewModel.TopicSearchQuery);
    Assert.Equal(["current query", "current query"], service.SearchQueries);
    Assert.Equal(["result"], viewModel.TopicResults.Select(topic => topic.Name));
    Assert.False(viewModel.HasError);
    Assert.Empty(service.FetchCursors);
  }

  [Fact]
  public async Task RetryAfterGeneralFailureReloadsInsteadOfSearching()
  {
    var service = new SearchService();
    service.Fetches.Enqueue(() => Task.FromException<PaymentCardPage>(new InvalidOperationException("offline")));
    service.Fetches.Enqueue(() => Task.FromResult(Page(false, null)));
    var viewModel = new PaymentCardsViewModel(service) { TopicSearchQuery = "visible query" };
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.RetryAsync(TestContext.Current.CancellationToken);

    Assert.Equal([null, null], service.FetchCursors);
    Assert.Empty(service.SearchQueries);
    Assert.False(viewModel.HasError);
  }

  private static TaskCompletionSource<IReadOnlyList<PaymentCardTopic>> PendingSearch() =>
      new(TaskCreationOptions.RunContinuationsAsynchronously);

  private static PaymentCardTopic Topic(string name) => new($"topic-{name}", name, name);

  private static PaymentCardPage Page(bool more, string? cursor) =>
      new([], new PageInfo(cursor, more, null));

  private sealed class SearchService(params TaskCompletionSource<IReadOnlyList<PaymentCardTopic>>[] searches)
      : IPaymentCardsService
  {
    private readonly Queue<TaskCompletionSource<IReadOnlyList<PaymentCardTopic>>> pending = new(searches);
    public Queue<Func<Task<PaymentCardPage>>> Fetches { get; } = new();
    public List<string?> FetchCursors { get; } = [];
    public List<string> SearchQueries { get; } = [];
    public Task<IReadOnlyList<PaymentCardTopic>> SearchTopicsAsync(string query, CancellationToken cancellationToken = default)
    {
      SearchQueries.Add(query);
      return pending.Dequeue().Task;
    }
    public Task<PaymentCardPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default)
    {
      FetchCursors.Add(after);
      return Fetches.Dequeue()();
    }
    public Task<PaymentCard> CreateAsync(string topicId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task<PaymentCard> UpdateAsync(string id, UpdatePaymentCardBody body, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
  }
}
