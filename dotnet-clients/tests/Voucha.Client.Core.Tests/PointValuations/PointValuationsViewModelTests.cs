using System.Globalization;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.PointValuations;
using Xunit;

namespace Voucha.Client.Core.Tests.PointValuations;

public sealed class PointValuationsViewModelTests
{
  [Theory]
  [InlineData(35_000, "0.035")]
  [InlineData(1, "0.000001")]
  public void RowsPreserveScaleSixSubCentPrecision(long amount, string expected)
  {
    var row = new PointValuationRow(
        new PointValuation(
            "valuation",
            "program",
            new ScaledMoney(amount, "usd"),
            null,
            new RewardsProgramSummary("program", "Rewards", "rewards")),
        UiLocalization.English);

    Assert.Contains(expected, row.LocalizedValue, StringComparison.Ordinal);
  }

  [Fact]
  public async Task PaginatesDeduplicatesAndFiltersExistingSearchResults()
  {
    var service = new Service(
        [Page(true, "next", Valuation("a", "ra"))],
        [new("ra", "Existing", "existing"), new("rb", "New", "new")]);
    var model = new PointValuationsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    service.Pages.Enqueue(Page(false, null, Valuation("b", "rb"), Valuation("a", "ra", 2m)));
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["a", "b"], model.Valuations.Select(value => value.Id));
    Assert.Equal(2m, model.Valuations[0].ValuePerPoint.ToMajorUnits());
    model.SearchQuery = "reward";
    await model.SearchAsync(TestContext.Current.CancellationToken);
    Assert.Empty(model.SearchRows);
  }

  [Fact]
  public async Task EditNoOpSkipsPatchAndFailurePreservesDraft()
  {
    var service = new Service([Page(false, null, Valuation("a", "ra"))]);
    var model = new PointValuationsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    model.BeginEdit(model.Rows.Single());
    await model.SaveAsync(TestContext.Current.CancellationToken);
    Assert.Equal(0, service.Updates);
    Assert.Null(model.EditDraft);

    model.BeginEdit(model.Rows.Single());
    model.EditDraft!.ValuePerPoint = "3";
    service.UpdateError = new InvalidOperationException("offline");
    await model.SaveAsync(TestContext.Current.CancellationToken);
    Assert.NotNull(model.EditDraft);
    Assert.Equal("3", model.EditDraft.ValuePerPoint);
  }

  [Fact]
  public async Task DeleteIsOptimisticAndRollsBackAtOriginalRank()
  {
    var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new Service([Page(false, null, Valuation("a", "ra"), Valuation("b", "rb"))])
    { DeletePending = pending.Task };
    var model = new PointValuationsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var deletion = model.DeleteAsync(model.Rows[0], TestContext.Current.CancellationToken);
    Assert.Equal(["b"], model.Valuations.Select(value => value.Id));
    pending.SetException(new InvalidOperationException("offline"));
    await deletion;
    Assert.Equal(["a", "b"], model.Valuations.Select(value => value.Id));
  }

  [Fact]
  public async Task DeleteSuppressesCapturedStaleRefreshThenReleasesTombstone()
  {
    var service = new Service([Page(false, null, Valuation("a", "ra"))]);
    var model = new PointValuationsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var staleRefresh = new TaskCompletionSource<PointValuationPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    var pendingDelete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    service.FetchPending = staleRefresh.Task;
    service.DeletePending = pendingDelete.Task;
    var refresh = model.LoadAsync(TestContext.Current.CancellationToken);
    var deletion = model.DeleteAsync(model.Rows.Single(), TestContext.Current.CancellationToken);

    staleRefresh.SetResult(Page(false, null, Valuation("a", "ra")));
    await refresh;
    Assert.Empty(model.Valuations);
    pendingDelete.SetResult();
    await deletion;

    service.Pages.Enqueue(Page(false, null, Valuation("a", "ra", 3m)));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(3m, model.Valuations.Single().ValuePerPoint.ToMajorUnits());
  }

  [Fact]
  public async Task StaleSearchCannotReplaceEditedQuery()
  {
    var pending = new TaskCompletionSource<IReadOnlyList<RewardsProgramOption>>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new Service([Page(false, null)]) { SearchPending = pending.Task };
    var model = new PointValuationsViewModel(service) { SearchQuery = "old" };
    var search = model.SearchAsync(TestContext.Current.CancellationToken);
    model.SearchQuery = "new";
    pending.SetResult([new("r", "Rewards", "rewards")]);
    await search;
    Assert.Empty(model.SearchRows);
  }

  [Fact]
  public async Task ContinuationFailureNotifiesOnFailureAndRetryClear()
  {
    var service = new Service([Page(true, "next", Valuation("a", "ra"))]);
    var model = new PointValuationsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    service.FetchErrors.Enqueue(new InvalidOperationException("offline"));
    var notifications = new List<string?>();
    model.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
    await model.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.True(model.HasContinuationError);
    service.Pages.Enqueue(Page(false, null, Valuation("b", "rb")));
    await model.RetryAsync(TestContext.Current.CancellationToken);
    Assert.False(model.HasContinuationError);
    Assert.Equal(2, notifications.Count(name => name == nameof(model.HasContinuationError)));
    Assert.Equal(["a", "b"], model.Valuations.Select(value => value.Id));
  }

  [Fact]
  public async Task InFlightRefreshPreservesLocalUpdateThenReleasesOverlay()
  {
    var service = new Service([Page(false, null, Valuation("a", "ra"))]);
    var model = new PointValuationsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var staleRefresh = new TaskCompletionSource<PointValuationPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.FetchPending = staleRefresh.Task;
    var refresh = model.LoadAsync(TestContext.Current.CancellationToken);
    model.BeginEdit(model.Rows.Single());
    model.EditDraft!.ValuePerPoint = "2";
    await model.SaveAsync(TestContext.Current.CancellationToken);

    staleRefresh.SetResult(Page(false, null, Valuation("a", "ra")));
    await refresh;
    Assert.Equal(2m, model.Valuations.Single().ValuePerPoint.ToMajorUnits());

    service.Pages.Enqueue(Page(false, null, Valuation("a", "ra", 3m)));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(3m, model.Valuations.Single().ValuePerPoint.ToMajorUnits());
  }

  [Fact]
  public async Task CreatedRowOverlayExpiresWhenCapturedRefreshSettlesWithoutIt()
  {
    var service = new Service([Page(false, null)]);
    var model = new PointValuationsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    var staleRefresh = new TaskCompletionSource<PointValuationPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.FetchPending = staleRefresh.Task;
    var refresh = model.LoadAsync(TestContext.Current.CancellationToken);
    service.CreateResult = Valuation("created", "ra", 2m);
    model.CreateDraft.ValuePerPoint = "2";

    await model.CreateAsync(
        new RewardsProgramRow(new RewardsProgramOption("ra", "Rewards", "rewards"), UiLocalization.English),
        TestContext.Current.CancellationToken);
    staleRefresh.SetResult(Page(false, null));
    await refresh;
    Assert.Equal(["created"], model.Valuations.Select(value => value.Id));

    service.Pages.Enqueue(Page(false, null));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Empty(model.Valuations);
  }

  [Fact]
  public async Task SearchFailureRetryRepeatsFailedSearch()
  {
    var service = new Service([Page(false, null)], [new("ra", "Rewards", "rewards")]);
    service.SearchErrors.Enqueue(new InvalidOperationException("offline"));
    var model = new PointValuationsViewModel(service) { SearchQuery = "reward" };
    await model.SearchAsync(TestContext.Current.CancellationToken);
    Assert.True(model.HasSearchError);
    await model.RetrySearchAsync(TestContext.Current.CancellationToken);
    Assert.False(model.HasSearchError);
    Assert.Single(model.SearchRows);
    Assert.Equal(2, service.Searches);
  }

  [Fact]
  public async Task RequestedDeleteCancellationRollsBackWithoutEscaping()
  {
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    var service = new Service([Page(false, null, Valuation("a", "ra"), Valuation("b", "rb"))])
    { DeletePending = Task.FromCanceled(cancellation.Token) };
    var model = new PointValuationsViewModel(service);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.DeleteAsync(model.Rows[0], cancellation.Token);
    Assert.Equal(["a", "b"], model.Valuations.Select(value => value.Id));
    Assert.False(model.HasError);
  }

  [Fact]
  public async Task LocaleChangesRebuildVisiblePresentationAndLocalizedErrors()
  {
    var controller = new UiLocaleController(new Languages("en"));
    var service = new Service(
        [Page(false, null, Valuation("a", "ra", 1.5m))],
        [new("rb", "New", "new")]);
    var model = new PointValuationsViewModel(service, new UiLocalization(controller), controller);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    model.SearchQuery = "new";
    await model.SearchAsync(TestContext.Current.CancellationToken);
    var rows = model.Rows;
    var searchRows = model.SearchRows;

    service.FetchErrors.Enqueue(new InvalidOperationException(string.Empty));
    service.SearchErrors.Enqueue(new InvalidOperationException(string.Empty));
    await model.LoadAsync(TestContext.Current.CancellationToken);
    await model.SearchAsync(TestContext.Current.CancellationToken);
    Assert.Equal("Try Again", model.LocalizedErrorMessage);
    Assert.Equal("Try Again", model.LocalizedSearchErrorMessage);

    controller.ApplySavedLocale("fr");

    Assert.NotSame(rows, model.Rows);
    Assert.NotSame(searchRows, model.SearchRows);
    Assert.Contains("1,5", model.Rows.Single().LocalizedValue, StringComparison.Ordinal);
    Assert.Equal("Réessayer", model.LocalizedErrorMessage);
    Assert.Equal("Réessayer", model.LocalizedSearchErrorMessage);
  }

  [Fact]
  public async Task LocaleChangesReformatUntouchedDraftsAndPreserveTypedText()
  {
    var controller = new UiLocaleController(new Languages("en"));
    var model = new PointValuationsViewModel(
        new Service([Page(false, null, Valuation("a", "ra", 1.5m))]),
        new UiLocalization(controller),
        controller);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    model.BeginEdit(model.Rows.Single());
    model.CreateDraft.ValuePerPoint = "2.75";

    controller.ApplySavedLocale("fr");

    Assert.Equal("1,5", model.EditDraft!.ValuePerPoint);
    Assert.Equal("2.75", model.CreateDraft.ValuePerPoint);
    Assert.True(model.CreateDraft.TryBuildCreate("new", out var create));
    Assert.Equal(2.75m, create!.ValuePerPoint.ToMajorUnits());

    model.EditDraft.ValuePerPoint = "1,75";
    controller.ApplySavedLocale("en");
    Assert.Equal("1,75", model.EditDraft.ValuePerPoint);
    Assert.True(model.EditDraft.TryBuildUpdate(out var update));
    Assert.Equal(1.75m, update!.ValuePerPoint!.ToMajorUnits());
  }

  private static PointValuation Valuation(string id, string program, decimal value = 1m) =>
      new(
          id,
          program,
          new ScaledMoney(decimal.ToInt64(value * 1_000_000), "usd"),
          null,
          new RewardsProgramSummary(program, "Rewards " + program, program));
  private static PointValuationPage Page(bool more, string? cursor, params PointValuation[] values) =>
      new(values, new PageInfo(cursor, more, null));

  private sealed class Languages(string language) : IDeviceLanguageProvider
  { public IReadOnlyList<string> PreferredLanguages { get; } = [language]; }

  private sealed class Service(IEnumerable<PointValuationPage> pages, IReadOnlyList<RewardsProgramOption>? search = null)
      : IPointValuationsService
  {
    public Queue<PointValuationPage> Pages { get; } = new(pages);
    public Exception? UpdateError { get; set; }
    public Task? DeletePending { get; set; }
    public Task<IReadOnlyList<RewardsProgramOption>>? SearchPending { get; set; }
    public Task<PointValuationPage>? FetchPending { get; set; }
    public PointValuation? CreateResult { get; set; }
    public Queue<Exception> FetchErrors { get; } = [];
    public Queue<Exception> SearchErrors { get; } = [];
    public int Updates { get; private set; }
    public int Searches { get; private set; }
    public Task<PointValuationPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default)
    {
      if (FetchErrors.TryDequeue(out var error)) return Task.FromException<PointValuationPage>(error);
      if (FetchPending is not { } pending) return Task.FromResult(Pages.Dequeue());
      FetchPending = null;
      return pending;
    }
    public Task<PointValuation> CreateAsync(CreatePointValuationBody body, CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateResult ?? throw new NotSupportedException());
    public Task<PointValuation> UpdateAsync(string id, UpdatePointValuationBody body, CancellationToken cancellationToken = default)
    {
      Updates++;
      return UpdateError is null
          ? Task.FromResult(Valuation(id, "ra", body.ValuePerPoint?.ToMajorUnits() ?? 1m))
          : Task.FromException<PointValuation>(UpdateError);
    }
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => DeletePending ?? Task.CompletedTask;
    public Task<IReadOnlyList<RewardsProgramOption>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
      Searches++;
      return SearchErrors.TryDequeue(out var error) ? Task.FromException<IReadOnlyList<RewardsProgramOption>>(error)
          : SearchPending ?? Task.FromResult(search ?? []);
    }
  }
}
