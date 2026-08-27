using Voucha.Client.Core.Api;
using Voucha.Client.Core.PaymentCards;
using Xunit;

namespace Voucha.Client.Core.Tests.PaymentCards;

public sealed class PaymentCardsReloadReconciliationTests
{
  [Fact]
  public async Task SaveCompletingDuringReloadWinsOverTheStaleResponse()
  {
    var reloadPage = PendingPage();
    var service = new ReconciliationService();
    service.Pages.Enqueue(() => Task.FromResult(Page(Card("existing", "old"))));
    service.Pages.Enqueue(() => reloadPage.Task);
    service.UpdateResults.Enqueue(Card("existing", "saved"));
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var reload = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    viewModel.BeginEdit(viewModel.Rows.Single());
    viewModel.Draft!.Note = "saved";
    await viewModel.SaveAsync(TestContext.Current.CancellationToken);
    reloadPage.SetResult(Page(Card("existing", "old")));
    await reload;

    Assert.Equal("saved", viewModel.Cards.Single().Note);
  }

  [Fact]
  public async Task CreateAndDeleteDuringReloadAreReconciledWithTheResponse()
  {
    var reloadPage = PendingPage();
    var service = new ReconciliationService();
    service.Pages.Enqueue(() => Task.FromResult(Page(Card("deleted"))));
    service.Pages.Enqueue(() => reloadPage.Task);
    service.CreateResults.Enqueue(Card("created"));
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var reload = viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.CreateAsync(Topic("Created"), TestContext.Current.CancellationToken);
    await viewModel.DeleteAsync(
        viewModel.Rows.Single(row => row.Id == "deleted"),
        TestContext.Current.CancellationToken);
    reloadPage.SetResult(Page(Card("deleted"), Card("server")));
    await reload;

    Assert.Equal(["created", "server"], viewModel.Cards.Select(card => card.Id));
  }

  [Fact]
  public async Task CreateAfterInitialLoadFailurePreservesRetryAndReconcilesTheCreatedCard()
  {
    var service = new ReconciliationService();
    service.Pages.Enqueue(() => Task.FromException<PaymentCardPage>(new InvalidOperationException("offline")));
    service.Pages.Enqueue(() => Task.FromResult(Page(Card("server"))));
    service.CreateResults.Enqueue(Card("created"));
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.CreateAsync(Topic("Created"), TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasError);
    Assert.Equal(["created"], viewModel.Cards.Select(card => card.Id));
    await viewModel.RetryAsync(TestContext.Current.CancellationToken);
    Assert.Equal(["created", "server"], viewModel.Cards.Select(card => card.Id));
    Assert.False(viewModel.HasError);
  }

  [Fact]
  public async Task SaveAfterInitialLoadFailureSucceedsWithoutClearingRetry()
  {
    var service = new ReconciliationService();
    service.Pages.Enqueue(() => Task.FromException<PaymentCardPage>(new InvalidOperationException("offline")));
    service.CreateResults.Enqueue(Card("created"));
    service.UpdateResults.Enqueue(Card("created", "saved"));
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.CreateAsync(Topic("Created"), TestContext.Current.CancellationToken);

    viewModel.BeginEdit(viewModel.Rows.Single());
    viewModel.Draft!.Note = "saved";
    await viewModel.SaveAsync(TestContext.Current.CancellationToken);

    Assert.Equal("saved", viewModel.Cards.Single().Note);
    Assert.False(viewModel.HasDraft);
    Assert.False(viewModel.IsMutating);
    Assert.True(viewModel.HasError);
  }

  [Fact]
  public async Task NoOpSaveAfterInitialLoadFailureClosesDraftAndPreservesRetry()
  {
    var service = new ReconciliationService();
    service.Pages.Enqueue(() => Task.FromException<PaymentCardPage>(new InvalidOperationException("offline")));
    service.CreateResults.Enqueue(Card("created"));
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.CreateAsync(Topic("Created"), TestContext.Current.CancellationToken);
    viewModel.BeginEdit(viewModel.Rows.Single());

    await viewModel.SaveAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasDraft);
    Assert.False(viewModel.IsMutating);
    Assert.True(viewModel.HasError);
  }

  [Fact]
  public async Task ReloadAcknowledgesALocallyCreatedCardWithTheServerValue()
  {
    var service = new ReconciliationService();
    service.Pages.Enqueue(() => Task.FromException<PaymentCardPage>(new InvalidOperationException("offline")));
    service.Pages.Enqueue(() => Task.FromResult(Page(Card("created", "server"))));
    service.CreateResults.Enqueue(Card("created", "local"));
    var viewModel = new PaymentCardsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    await viewModel.CreateAsync(Topic("Created"), TestContext.Current.CancellationToken);

    await viewModel.RetryAsync(TestContext.Current.CancellationToken);

    Assert.Equal("server", viewModel.Cards.Single().Note);
    Assert.False(viewModel.HasError);
  }

  private static TaskCompletionSource<PaymentCardPage> PendingPage() =>
      new(TaskCreationOptions.RunContinuationsAsynchronously);

  private static PaymentCardPage Page(params PaymentCard[] cards) =>
      new(cards, new PageInfo(null, false, cards.FirstOrDefault()?.Id));

  private static PaymentCard Card(string id, string? note = null) =>
      new(id, $"topic-{id}", null, null, null, null, false, null, note, Topic($"Card {id}"), null);

  private static PaymentCardTopic Topic(string name) =>
      new($"topic-{name}", name, name.ToLowerInvariant());

  private sealed class ReconciliationService : IPaymentCardsService
  {
    public Queue<Func<Task<PaymentCardPage>>> Pages { get; } = new();
    public Queue<PaymentCard> CreateResults { get; } = new();
    public Queue<PaymentCard> UpdateResults { get; } = new();

    public Task<PaymentCardPage> FetchAsync(
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default) => Pages.Dequeue()();

    public Task<PaymentCard> CreateAsync(
        string topicId,
        CancellationToken cancellationToken = default) => Task.FromResult(CreateResults.Dequeue());

    public Task<PaymentCard> UpdateAsync(
        string id,
        UpdatePaymentCardBody body,
        CancellationToken cancellationToken = default) => Task.FromResult(UpdateResults.Dequeue());

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<PaymentCardTopic>> SearchTopicsAsync(
        string query,
        CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PaymentCardTopic>>([]);
  }
}
