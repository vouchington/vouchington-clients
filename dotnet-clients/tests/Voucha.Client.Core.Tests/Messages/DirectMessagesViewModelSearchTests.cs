using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Messages;
using Xunit;

namespace Voucha.Client.Core.Tests.Messages;

public sealed class DirectMessagesViewModelSearchTests
{
  [Fact]
  public async Task SearchUsersIgnoresStaleComposerResultsAfterClearAndLaterQuery()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var firstSearch = viewModel.SearchUsersAsync("al", TestContext.Current.CancellationToken);
    var clearedSearch = viewModel.SearchUsersAsync("   ", TestContext.Current.CancellationToken);
    var secondSearch = viewModel.SearchUsersAsync("ali", TestContext.Current.CancellationToken);

    await clearedSearch;
    Assert.Empty(viewModel.ComposerUserResults);

    service.CompleteSearch(
        "ali",
        new UsersSearchResponse([new UserSearchResult("u2", "alice")], new PageInfo(null, false, null)));
    await secondSearch;

    service.CompleteSearch(
        "al",
        new UsersSearchResponse([new UserSearchResult("u1", "al")], new PageInfo(null, false, null)));
    await firstSearch;

    Assert.Equal(["u2"], viewModel.ComposerUserResults.Select(row => row.Id));
    Assert.Equal("ali", service.SearchRequests[1].Query);
  }

  [Fact]
  public async Task ComposerAndParticipantSearchResultsStaySeparate()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var participantSearch = viewModel.SearchParticipantUsersAsync("bob", TestContext.Current.CancellationToken);
    service.CompleteSearch(
        "bob",
        new UsersSearchResponse([new UserSearchResult("u2", "bob")], new PageInfo(null, false, null)));
    await participantSearch;

    var composerSearch = viewModel.SearchUsersAsync("alice", TestContext.Current.CancellationToken);
    service.CompleteSearch(
        "alice",
        new UsersSearchResponse([new UserSearchResult("u1", "alice")], new PageInfo(null, false, null)));
    await composerSearch;

    Assert.Equal(["u1"], viewModel.ComposerUserResults.Select(row => row.Id));
    Assert.Equal(["u2"], viewModel.ParticipantUserResults.Select(row => row.Id));
  }

  [Fact]
  public async Task ParticipantSearchIgnoresStaleResultsAfterClearAndLaterQuery()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var firstSearch = viewModel.SearchParticipantUsersAsync("bo", TestContext.Current.CancellationToken);
    var clearedSearch = viewModel.SearchParticipantUsersAsync("   ", TestContext.Current.CancellationToken);
    var secondSearch = viewModel.SearchParticipantUsersAsync("bob", TestContext.Current.CancellationToken);

    await clearedSearch;
    Assert.Empty(viewModel.ParticipantUserResults);

    service.CompleteSearch(
        "bob",
        new UsersSearchResponse([new UserSearchResult("u2", "bob")], new PageInfo(null, false, null)));
    await secondSearch;

    service.CompleteSearch(
        "bo",
        new UsersSearchResponse([new UserSearchResult("u1", "bo")], new PageInfo(null, false, null)));
    await firstSearch;

    Assert.Equal(["u2"], viewModel.ParticipantUserResults.Select(row => row.Id));
    Assert.Empty(viewModel.ComposerUserResults);
  }

  [Fact]
  public async Task SearchUsersFollowsCursorToFillComposerPage()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var search = viewModel.SearchUsersAsync("al", TestContext.Current.CancellationToken);
    await service.WaitForPendingSearchRequestsAsync("al", 1, TestContext.Current.CancellationToken);
    service.CompleteSearch(
        "al",
        new UsersSearchResponse(
            [.. Enumerable.Range(0, 6).Select(index => new UserSearchResult($"u{index}", $"al{index}"))],
            new PageInfo("cursor-1", true, null)));

    await service.WaitForPendingSearchRequestsAsync("al", 1, TestContext.Current.CancellationToken);
    service.CompleteSearch(
        "al",
        new UsersSearchResponse(
            [.. Enumerable.Range(6, 4).Select(index => new UserSearchResult($"u{index}", $"al{index}"))],
            new PageInfo(null, false, null)));

    await search;

    Assert.Equal(10, viewModel.ComposerUserResults.Count);
    Assert.Equal(2, service.SearchRequests.Count);
    Assert.Null(service.SearchRequests[0].After);
    Assert.Equal("cursor-1", service.SearchRequests[1].After);
  }

  [Fact]
  public async Task SearchUsersStopsAtPageSafetyBoundWithoutFillingComposerPage()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var search = viewModel.SearchUsersAsync("al", TestContext.Current.CancellationToken);
    for (var page = 0; page < 5; page++)
    {
      await service.WaitForPendingSearchRequestsAsync("al", 1, TestContext.Current.CancellationToken);
      service.CompleteSearch(
          "al",
          new UsersSearchResponse(
              [new UserSearchResult($"u{page}", $"al{page}")],
              new PageInfo($"cursor-{page}", true, null)));
    }

    await search;

    Assert.Equal(5, viewModel.ComposerUserResults.Count);
    Assert.Equal(5, service.SearchRequests.Count);
  }

  [Fact]
  public async Task SearchParticipantUsersFollowsCursorToFillPage()
  {
    var service = new BlockingDirectMessagesService();
    var viewModel = new DirectMessagesViewModel(service, "me");

    var search = viewModel.SearchParticipantUsersAsync("bo", TestContext.Current.CancellationToken);
    await service.WaitForPendingSearchRequestsAsync("bo", 1, TestContext.Current.CancellationToken);
    service.CompleteSearch(
        "bo",
        new UsersSearchResponse(
            [.. Enumerable.Range(0, 6).Select(index => new UserSearchResult($"u{index}", $"bo{index}"))],
            new PageInfo("cursor-1", true, null)));

    await service.WaitForPendingSearchRequestsAsync("bo", 1, TestContext.Current.CancellationToken);
    service.CompleteSearch(
        "bo",
        new UsersSearchResponse(
            [.. Enumerable.Range(6, 4).Select(index => new UserSearchResult($"u{index}", $"bo{index}"))],
            new PageInfo(null, false, null)));

    await search;

    Assert.Equal(10, viewModel.ParticipantUserResults.Count);
    Assert.Empty(viewModel.ComposerUserResults);
    Assert.Equal(2, service.SearchRequests.Count);
    Assert.Null(service.SearchRequests[0].After);
    Assert.Equal("cursor-1", service.SearchRequests[1].After);
  }

  [Fact]
  public async Task UnknownRecipientLabelsRefreshAcrossAllSupportedLocales()
  {
    var service = new RecordingDirectMessagesService
    {
      UserSearchResponse = new UsersSearchResponse(
          [new UserSearchResult("u1", null)],
          new PageInfo(null, false, null)),
    };
    var controller = new UiLocaleController(new EnglishDeviceLanguageProvider());
    using var viewModel = new DirectMessagesViewModel(
        service,
        "me",
        new UiLocalization(controller),
        controller);
    var changedProperties = new List<string?>();
    viewModel.PropertyChanged += (_, eventArgs) =>
        changedProperties.Add(eventArgs.PropertyName);

    await viewModel.SearchUsersAsync("unknown", TestContext.Current.CancellationToken);

    Assert.Equal("Unknown recipient", Assert.Single(viewModel.ComposerUserResults).Username);
    controller.ApplySavedLocale("es");
    Assert.Equal("Destinatario desconocido", viewModel.ComposerUserResults[0].Username);
    controller.ApplySavedLocale("fr");
    Assert.Equal("Destinataire inconnu", viewModel.ComposerUserResults[0].Username);
    controller.ApplySavedLocale("pt");
    Assert.Equal("Destinatário desconhecido", viewModel.ComposerUserResults[0].Username);
    Assert.Contains(nameof(DirectMessagesViewModel.ComposerUserResults), changedProperties);
  }

  private sealed class EnglishDeviceLanguageProvider : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = ["en"];
  }
}
