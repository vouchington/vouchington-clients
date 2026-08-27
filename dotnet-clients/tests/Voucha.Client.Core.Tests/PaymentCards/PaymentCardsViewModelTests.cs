using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.PaymentCards;
using Xunit;

namespace Voucha.Client.Core.Tests.PaymentCards;

public sealed class PaymentCardsViewModelTests
{
  [Fact]
  public async Task NoneParentOptionClearsTheLiveParentWithoutDisablingAuthorizedUser()
  {
    var child = Card("child") with { IsAuthorizedUser = true, AuthorizedUserOfId = "parent" };
    var service = LoadedService(child, Card("parent"));
    service.UpdateResults.Enqueue(() => Task.FromResult(child with { AuthorizedUserOfId = null }));
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.BeginEdit(viewModel.Rows.Single(row => row.Id == "child"));
    var none = viewModel.ParentCandidates.First();
    Assert.Null(none.Id); Assert.Equal("None", none.LocalizedName);
    viewModel.Draft!.AuthorizedUserOfId = none.Id;
    Assert.True(viewModel.Draft.IsAuthorizedUser);
    await viewModel.SaveAsync(TestContext.Current.CancellationToken);
    Assert.Equal(JsonNullableString.Null, service.LastUpdateBody!.AuthorizedUserOfId);
  }

  [Fact]
  public async Task DeletingAParentClearsAnActiveChildDraftAndPreventsRehydration()
  {
    var parent = Card("parent");
    var child = Card("child") with
    {
      IsAuthorizedUser = true,
      AuthorizedUserOfId = parent.Id,
      AuthorizedUserOfCard = new PaymentCardParentSummary(parent.Id, null, null, parent.Card),
    };
    var service = LoadedService(parent, child);
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.BeginEdit(viewModel.Rows.Single(row => row.Id == child.Id));
    await viewModel.DeleteAsync(viewModel.Rows.Single(row => row.Id == parent.Id), TestContext.Current.CancellationToken);
    Assert.NotNull(viewModel.Draft); Assert.Null(viewModel.Draft.AuthorizedUserOfId);
    Assert.True(viewModel.Draft.IsAuthorizedUser);
    Assert.DoesNotContain(viewModel.ParentCandidates, option => option.Id == parent.Id);
    Assert.Null(viewModel.Cards.Single().AuthorizedUserOfCard);
  }

  [Fact]
  public async Task ClearingAClosedCurrentParentRemovesItsHydratedCandidate()
  {
    var child = Card("child") with
    {
      IsAuthorizedUser = true,
      AuthorizedUserOfId = "closed-parent",
      AuthorizedUserOfCard = new PaymentCardParentSummary(
          "closed-parent", null, new DateOnly(2025, 1, 1), Topic("Closed parent")),
    };
    var viewModel = new PaymentCardsViewModel(LoadedService(child));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.BeginEdit(viewModel.Rows.Single());
    var notifications = new List<string?>();
    viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
    Assert.Contains(viewModel.ParentCandidates, option => option.Id == "closed-parent");
    viewModel.Draft!.AuthorizedUserOfId = null;
    Assert.DoesNotContain(viewModel.ParentCandidates, option => option.Id == "closed-parent");
    Assert.Contains(nameof(PaymentCardsViewModel.ParentCandidates), notifications);
  }

  [Fact]
  public async Task WhitespaceNotesRoundTripVerbatim()
  {
    var service = LoadedService(Card("a"));
    service.UpdateResults.Enqueue(() => Task.FromResult(Card("a", "   ")));
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.BeginEdit(viewModel.Rows.Single()); viewModel.Draft!.Note = "   ";
    await viewModel.SaveAsync(TestContext.Current.CancellationToken);
    Assert.Equal("   ", service.LastUpdateBody!.Note!.Value.Value);
    Assert.Equal("   ", viewModel.Cards.Single().Note);
  }

  [Fact]
  public async Task ContinuationRetryUsesTheFailedCursorAndPreservesRows()
  {
    var service = new FakeService();
    service.Pages.Enqueue(() => Task.FromResult(Page(true, "next", Card("a"))));
    service.Pages.Enqueue(() => Task.FromException<PaymentCardPage>(new InvalidOperationException("offline")));
    service.Pages.Enqueue(() => Task.FromResult(Page(false, null, Card("b"))));
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.HasContinuationError); Assert.Equal(["a"], viewModel.Cards.Select(card => card.Id));
    await viewModel.RetryAsync(TestContext.Current.CancellationToken);
    Assert.Equal([null, "next", "next"], service.Fetches.Select(call => call.After));
    Assert.Equal(["a", "b"], viewModel.Cards.Select(card => card.Id));
  }

  [Fact]
  public async Task ErrorsUseTypedLocalizedOrVerbatimPresentation()
  {
    var server = new FakeService();
    server.Pages.Enqueue(() => Task.FromException<PaymentCardPage>(new InvalidOperationException("server detail")));
    var serverViewModel = new PaymentCardsViewModel(server);
    await serverViewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Null(serverViewModel.ErrorText!.Value.Key);
    Assert.Equal("server detail", serverViewModel.ErrorText.Value.VerbatimValue);

    var fallback = new FakeService();
    fallback.Pages.Enqueue(() => Task.FromException<PaymentCardPage>(new InvalidOperationException(string.Empty)));
    var fallbackViewModel = new PaymentCardsViewModel(fallback);
    await fallbackViewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(UiMessageKey.NativeDotnetPaymentCardsOperationFailed, fallbackViewModel.ErrorText!.Value.Key);

    var validationViewModel = new PaymentCardsViewModel(LoadedService(Card("a")));
    await validationViewModel.LoadAsync(TestContext.Current.CancellationToken);
    validationViewModel.BeginEdit(validationViewModel.Rows.Single());
    validationViewModel.Draft!.CreditLimit = "invalid";
    await validationViewModel.SaveAsync(TestContext.Current.CancellationToken);
    Assert.Equal(UiMessageKey.NativeDotnetPaymentCardsCreditLimitValidation, validationViewModel.ErrorText!.Value.Key);
  }

  [Fact]
  public async Task LocaleChangesReformatUntouchedDecimalDraftsAndPreserveEditedText()
  {
    var controller = new UiLocaleController(new Languages("en"));
    var service = LoadedService(Card("a") with { CreditLimit = new Money(1250, "usd") });
    service.UpdateResults.Enqueue(
        () => Task.FromResult(Card("a") with { CreditLimit = new Money(1275, "usd") }));
    var viewModel = new PaymentCardsViewModel(service, new UiLocalization(controller), controller);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.BeginEdit(viewModel.Rows.Single()); Assert.Equal("12.5", viewModel.Draft!.CreditLimit);
    controller.ApplySavedLocale("fr"); Assert.Equal("12,5", viewModel.Draft.CreditLimit);
    viewModel.Draft.CreditLimit = "12,75";
    controller.ApplySavedLocale("en"); Assert.Equal("12,75", viewModel.Draft.CreditLimit);
    await viewModel.SaveAsync(TestContext.Current.CancellationToken);
    Assert.Equal(new Money(1275, "usd"), service.LastUpdateBody!.CreditLimit!.Value.Value);
  }

  [Fact]
  public async Task RowsRenderLocalizedLabelsIncludingZeroAndAuthorizedUserWithoutParent()
  {
    var card = Card("a") with
    {
      CreditLimit = new Money(0, "usd"),
      OpenedOn = new DateOnly(2024, 1, 2),
      ClosedOn = new DateOnly(2024, 2, 3),
      ReceivedSignUpBonusOn = new DateOnly(2024, 3, 4),
      IsAuthorizedUser = true,
      Note = "Keep this memo",
    };
    var viewModel = new PaymentCardsViewModel(LoadedService(card));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var row = viewModel.Rows.Single();
    Assert.Equal("Credit limit: USD\u00A00.00", row.LocalizedCreditLimitDetail);
    Assert.StartsWith("Opened: ", row.LocalizedOpenedOnDetail);
    Assert.StartsWith("Closed: ", row.LocalizedClosedOnDetail);
    Assert.StartsWith("Sign-up bonus received: ", row.LocalizedBonusOnDetail);
    Assert.Equal("Note: Keep this memo", row.LocalizedNoteDetail);
    Assert.Equal("Authorized user, no parent card", row.LocalizedAuthorizedUserDetail);
  }

  [Fact]
  public async Task PaginationAppendsDeduplicatesAndRejectsDuplicateLoads()
  {
    var service = new FakeService();
    service.Pages.Enqueue(() => Task.FromResult(Page(true, "next", Card("b"), Card("a"))));
    var continuation = new TaskCompletionSource<PaymentCardPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.Pages.Enqueue(() => continuation.Task);
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var first = viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    var duplicate = viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    continuation.SetResult(Page(false, null, Card("b"), Card("c")));
    await Task.WhenAll(first, duplicate);
    Assert.Equal(["a", "b", "c"], viewModel.Cards.Select(card => card.Id));
    Assert.Equal([null, "next"], service.Fetches.Select(call => call.After));
  }

  [Fact]
  public async Task FailedContinuationPreservesRowsAndCanRetry()
  {
    var service = new FakeService();
    service.Pages.Enqueue(() => Task.FromResult(Page(true, "next", Card("a"))));
    service.Pages.Enqueue(() => Task.FromException<PaymentCardPage>(new InvalidOperationException("offline")));
    service.Pages.Enqueue(() => Task.FromResult(Page(false, null, Card("b"))));
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["a"], viewModel.Cards.Select(card => card.Id));
    Assert.True(viewModel.HasNextPage);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["a", "b"], viewModel.Cards.Select(card => card.Id));
  }

  [Fact]
  public async Task DeletedParentTombstoneClearsAChildArrivingFromContinuation()
  {
    var parent = Card("parent");
    var child = Card("child") with
    {
      IsAuthorizedUser = true,
      AuthorizedUserOfId = parent.Id,
      AuthorizedUserOfCard = new PaymentCardParentSummary(parent.Id, null, null, parent.Card),
    };
    var continuation = new TaskCompletionSource<PaymentCardPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeService();
    service.Pages.Enqueue(() => Task.FromResult(Page(true, "next", parent))); service.Pages.Enqueue(() => continuation.Task);
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var loadMore = viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    await viewModel.DeleteAsync(viewModel.Rows.Single(), TestContext.Current.CancellationToken);
    continuation.SetResult(Page(false, null, child)); await loadMore;
    var loadedChild = Assert.Single(viewModel.Cards);
    Assert.Null(loadedChild.AuthorizedUserOfId); Assert.Null(loadedChild.AuthorizedUserOfCard); Assert.True(loadedChild.IsAuthorizedUser);
  }

  [Fact]
  public async Task RefreshRejectsAStaleContinuation()
  {
    var service = new FakeService();
    service.Pages.Enqueue(() => Task.FromResult(Page(true, "next", Card("a"))));
    var stale = new TaskCompletionSource<PaymentCardPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    service.Pages.Enqueue(() => stale.Task);
    service.Pages.Enqueue(() => Task.FromResult(Page(false, null, Card("fresh"))));
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var continuation = viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    stale.SetResult(Page(false, null, Card("stale")));
    await continuation;
    Assert.Equal(["fresh"], viewModel.Cards.Select(card => card.Id));
  }

  [Fact]
  public async Task RefreshAndStaleContinuationCannotClearTheCurrentContinuationState()
  {
    var stalePage = new TaskCompletionSource<PaymentCardPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    var currentPage = new TaskCompletionSource<PaymentCardPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeService();
    service.Pages.Enqueue(() => Task.FromResult(Page(true, "stale-next", Card("initial"))));
    service.Pages.Enqueue(() => stalePage.Task);
    service.Pages.Enqueue(() => Task.FromResult(Page(true, "current-next", Card("fresh"))));
    service.Pages.Enqueue(() => currentPage.Task);
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var staleContinuation = viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.False(viewModel.IsLoadingMore);
    var currentContinuation = viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);
    Assert.True(viewModel.IsLoadingMore);

    stalePage.SetResult(Page(false, null, Card("stale")));
    await staleContinuation;
    Assert.True(viewModel.IsLoadingMore);
    Assert.Equal(["fresh"], viewModel.Cards.Select(card => card.Id));

    currentPage.SetResult(Page(false, null, Card("current")));
    await currentContinuation;
    Assert.False(viewModel.IsLoadingMore);
    Assert.Equal(["current", "fresh"], viewModel.Cards.Select(card => card.Id));
    Assert.Equal([null, "stale-next", null, "current-next"], service.Fetches.Select(call => call.After));
  }

  [Fact]
  public async Task DateTogglesRestoreEachPriorValueAndDefaultOnlyWhenUnset()
  {
    var opened = new DateOnly(2024, 1, 2);
    var closed = new DateOnly(2024, 2, 3);
    var bonus = new DateOnly(2024, 3, 4);
    var viewModel = new PaymentCardsViewModel(LoadedService(Card("dated") with
    {
      OpenedOn = opened,
      ClosedOn = closed,
      ReceivedSignUpBonusOn = bonus,
    }));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.BeginEdit(viewModel.Rows.Single());
    var draft = viewModel.Draft!;

    draft.HasOpenedOn = false; draft.HasOpenedOn = true;
    draft.HasClosedOn = false; draft.HasClosedOn = true;
    draft.HasBonusOn = false; draft.HasBonusOn = true;
    Assert.Equal(opened, draft.OpenedOn);
    Assert.Equal(closed, draft.ClosedOn);
    Assert.Equal(bonus, draft.ReceivedSignUpBonusOn);

    var changedOpened = opened.AddDays(1);
    var changedClosed = closed.AddDays(1);
    var changedBonus = bonus.AddDays(1);
    draft.OpenedOn = changedOpened; draft.HasOpenedOn = false; draft.HasOpenedOn = true;
    draft.ClosedOn = changedClosed; draft.HasClosedOn = false; draft.HasClosedOn = true;
    draft.ReceivedSignUpBonusOn = changedBonus; draft.HasBonusOn = false; draft.HasBonusOn = true;
    Assert.Equal(changedOpened, draft.OpenedOn);
    Assert.Equal(changedClosed, draft.ClosedOn);
    Assert.Equal(changedBonus, draft.ReceivedSignUpBonusOn);

    var unsetViewModel = new PaymentCardsViewModel(LoadedService(Card("unset")));
    await unsetViewModel.LoadAsync(TestContext.Current.CancellationToken);
    unsetViewModel.BeginEdit(unsetViewModel.Rows.Single());
    var earliestDefault = DateOnly.FromDateTime(DateTime.Today);
    unsetViewModel.Draft!.HasOpenedOn = true;
    unsetViewModel.Draft.HasClosedOn = true;
    unsetViewModel.Draft.HasBonusOn = true;
    var latestDefault = DateOnly.FromDateTime(DateTime.Today);
    Assert.InRange(unsetViewModel.Draft.OpenedOn!.Value, earliestDefault, latestDefault);
    Assert.InRange(unsetViewModel.Draft.ClosedOn!.Value, earliestDefault, latestDefault);
    Assert.InRange(unsetViewModel.Draft.ReceivedSignUpBonusOn!.Value, earliestDefault, latestDefault);
  }

  [Fact]
  public async Task PresentationRowsAreCachedAndInvalidatedWithTheirInputs()
  {
    var service = LoadedService(Card("a"));
    service.SearchResults.Enqueue([Topic("First")]);
    service.SearchResults.Enqueue([Topic("Second")]);
    service.CreateResults.Enqueue(() => Task.FromResult(Card("created")));
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var initialRows = viewModel.Rows;
    Assert.Same(initialRows, viewModel.Rows);

    await viewModel.SearchTopicsAsync("first", TestContext.Current.CancellationToken);
    var initialTopicRows = viewModel.TopicRows;
    Assert.Same(initialTopicRows, viewModel.TopicRows);
    await viewModel.CreateAsync(Topic("Created"), TestContext.Current.CancellationToken);
    Assert.NotSame(initialRows, viewModel.Rows);
    await viewModel.SearchTopicsAsync("second", TestContext.Current.CancellationToken);
    Assert.NotSame(initialTopicRows, viewModel.TopicRows);

    var rowsBeforeLocaleChange = viewModel.Rows;
    var topicsBeforeLocaleChange = viewModel.TopicRows;
    viewModel.OnUiLocaleChanged();
    Assert.NotSame(rowsBeforeLocaleChange, viewModel.Rows);
    Assert.NotSame(topicsBeforeLocaleChange, viewModel.TopicRows);
  }

  [Fact]
  public async Task MutationsPreserveDraftsAndRowsUntilSuccess()
  {
    var service = LoadedService(Card("a", note: "before"));
    service.UpdateResults.Enqueue(() => Task.FromException<PaymentCard>(new InvalidOperationException("offline")));
    service.UpdateResults.Enqueue(() => Task.FromResult(Card("a", note: "after")));
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.BeginEdit(viewModel.Rows.Single()); viewModel.Draft!.Note = "after";
    await viewModel.SaveAsync(TestContext.Current.CancellationToken);
    Assert.Equal("before", viewModel.Cards.Single().Note); Assert.NotNull(viewModel.Draft);
    await viewModel.SaveAsync(TestContext.Current.CancellationToken);
    Assert.Equal("after", viewModel.Cards.Single().Note); Assert.Null(viewModel.Draft);
  }

  [Fact]
  public async Task NoOpEditSkipsPatchAndDeletingParentCleansChildSummary()
  {
    var parent = Card("parent");
    var child = Card("child") with
    {
      IsAuthorizedUser = true,
      AuthorizedUserOfId = "parent",
      AuthorizedUserOfCard = new PaymentCardParentSummary("parent", null, null, parent.Card),
    };
    var service = LoadedService(parent, child);
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.BeginEdit(viewModel.Rows.Single(row => row.Id == "child"));
    await viewModel.SaveAsync(TestContext.Current.CancellationToken);
    Assert.Equal(0, service.UpdateCalls);
    await viewModel.DeleteAsync(viewModel.Rows.Single(row => row.Id == "parent"), TestContext.Current.CancellationToken);
    Assert.Null(viewModel.Cards.Single().AuthorizedUserOfId);
    Assert.True(viewModel.Cards.Single().IsAuthorizedUser);
  }

  [Fact]
  public async Task ParentCandidatesExcludeSelfAndClosedExceptCurrent()
  {
    var current = Card("child") with
    {
      AuthorizedUserOfId = "closed-current",
      IsAuthorizedUser = true,
      AuthorizedUserOfCard = new PaymentCardParentSummary(
          "closed-current", null, new DateOnly(2025, 1, 1), Topic("Current parent")),
    };
    var service = LoadedService(current, Card("open"), Card("closed") with { ClosedOn = new DateOnly(2025, 1, 1) });
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.BeginEdit(viewModel.Rows.Single(row => row.Id == "child"));
    Assert.Equal([null, "open", "closed-current"], viewModel.ParentCandidates.Select(option => option.Id));
  }

  [Fact]
  public async Task DecimalDraftUsesLocaleAndPreservesInvalidText()
  {
    var controller = new UiLocaleController(new Languages("fr-FR"));
    var service = LoadedService(Card("a") with { CreditLimit = new Money(1250, "usd") });
    var viewModel = new PaymentCardsViewModel(service, new UiLocalization(controller), controller);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.BeginEdit(viewModel.Rows.Single());
    Assert.Equal("12,5", viewModel.Draft!.CreditLimit);
    viewModel.Draft.CreditLimit = "not a number";
    await viewModel.SaveAsync(TestContext.Current.CancellationToken);
    Assert.Equal("not a number", viewModel.Draft.CreditLimit);
    Assert.Equal(0, service.UpdateCalls);
  }

  [Fact]
  public async Task CreateAllowsRepeatedTopicsAndDisablingAuthorizedUserClearsParent()
  {
    var child = Card("child") with { IsAuthorizedUser = true, AuthorizedUserOfId = "parent" };
    var service = LoadedService(child);
    service.CreateResults.Enqueue(() => Task.FromResult(Card("created-1")));
    service.CreateResults.Enqueue(() => Task.FromResult(Card("created-2")));
    service.UpdateResults.Enqueue(() => Task.FromResult(child with
    {
      IsAuthorizedUser = false,
      AuthorizedUserOfId = null,
      AuthorizedUserOfCard = null,
    }));
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    var topic = Topic("Repeated");
    await viewModel.CreateAsync(topic, TestContext.Current.CancellationToken);
    await viewModel.CreateAsync(topic, TestContext.Current.CancellationToken);
    Assert.Equal([topic.Id, topic.Id], service.CreatedTopicIds);
    viewModel.BeginEdit(viewModel.Rows.Single(row => row.Id == "child"));
    viewModel.Draft!.IsAuthorizedUser = false;
    await viewModel.SaveAsync(TestContext.Current.CancellationToken);
    Assert.False(service.LastUpdateBody!.IsAuthorizedUser);
    Assert.Equal(JsonNullableString.Null, service.LastUpdateBody.AuthorizedUserOfId);
  }

  [Fact]
  public async Task InitialLoadCompletingAfterCreatePreservesTheCreatedCard()
  {
    var firstPage = new TaskCompletionSource<PaymentCardPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new FakeService();
    service.Pages.Enqueue(() => firstPage.Task);
    service.CreateResults.Enqueue(() => Task.FromResult(Card("created")));
    var viewModel = new PaymentCardsViewModel(service);

    var load = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.CreateAsync(Topic("Created"), TestContext.Current.CancellationToken);
    firstPage.SetResult(Page(false, null, Card("server")));
    await load;

    Assert.Equal(["created", "server"], viewModel.Cards.Select(card => card.Id));
  }

  [Fact]
  public async Task RefreshRacePreservesOnlyCardsCreatedWhileTheRequestWasInFlight()
  {
    var refreshPage = new TaskCompletionSource<PaymentCardPage>(TaskCreationOptions.RunContinuationsAsynchronously);
    var service = LoadedService(Card("stale"));
    service.Pages.Enqueue(() => refreshPage.Task);
    service.CreateResults.Enqueue(() => Task.FromResult(Card("created")));
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var refresh = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.CreateAsync(Topic("Created"), TestContext.Current.CancellationToken);
    refreshPage.SetResult(Page(false, null, Card("server")));
    await refresh;

    Assert.Equal(["created", "server"], viewModel.Cards.Select(card => card.Id));
  }

  private static FakeService LoadedService(params PaymentCard[] cards)
  {
    var service = new FakeService(); service.Pages.Enqueue(() => Task.FromResult(Page(false, null, cards))); return service;
  }
  private static PaymentCardPage Page(bool more, string? cursor, params PaymentCard[] cards) =>
      new(cards, new PageInfo(cursor, more, cards.FirstOrDefault()?.Id));
  private static PaymentCard Card(string id, string? note = null) =>
      new(id, $"topic-{id}", null, null, null, null, false, null, note, Topic($"Card {id}"), null);
  private static PaymentCardTopic Topic(string name) => new($"topic-{name}", name, name.ToLowerInvariant().Replace(' ', '-'));

  private sealed class Languages(string language) : IDeviceLanguageProvider
  { public IReadOnlyList<string> PreferredLanguages { get; } = [language]; }

  private sealed class FakeService : IPaymentCardsService
  {
    public Queue<Func<Task<PaymentCardPage>>> Pages { get; } = new();
    public Queue<Func<Task<PaymentCard>>> CreateResults { get; } = new();
    public Queue<Func<Task<PaymentCard>>> UpdateResults { get; } = new();
    public Queue<IReadOnlyList<PaymentCardTopic>> SearchResults { get; } = new();
    public List<(string? After, int Limit)> Fetches { get; } = [];
    public List<string> CreatedTopicIds { get; } = [];
    public int UpdateCalls { get; private set; }
    public UpdatePaymentCardBody? LastUpdateBody { get; private set; }
    public Task<PaymentCardPage> FetchAsync(string? after = null, int limit = 25, CancellationToken cancellationToken = default)
    { Fetches.Add((after, limit)); return Pages.Dequeue()(); }
    public Task<PaymentCard> CreateAsync(string topicId, CancellationToken cancellationToken = default)
    { CreatedTopicIds.Add(topicId); return CreateResults.Dequeue()(); }
    public Task<PaymentCard> UpdateAsync(string id, UpdatePaymentCardBody body, CancellationToken cancellationToken = default)
    { UpdateCalls++; LastUpdateBody = body; return UpdateResults.Dequeue()(); }
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<PaymentCardTopic>> SearchTopicsAsync(string query, CancellationToken cancellationToken = default) =>
        Task.FromResult(SearchResults.Count > 0 ? SearchResults.Dequeue() : []);
  }
}
