using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Tags;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.Core.Tests.Tags;

public sealed partial class TagManagementViewModelTests
{
  [Fact]
  public async Task AddTagAsyncSetsTagLimitReachedWithoutErrorMessageOn403()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(TagLimitReachedErrorJson(), HttpStatusCode.Forbidden),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new TagManagementViewModel(client);
    viewModel.SetContext(new TagManagementRouteContext("post", "post-1", "topic"));

    await viewModel.AddTagAsync("topic-2", TestContext.Current.CancellationToken);

    Assert.True(viewModel.TagLimitReached);
    Assert.Null(viewModel.ErrorMessage);
    Assert.False(viewModel.IsLoading);
  }

  [Fact]
  public async Task AddTagAsyncClearsTagLimitReachedOnSuccessfulRetry()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(TagLimitReachedErrorJson(), HttpStatusCode.Forbidden),
        new RecordedResponse(CreateRelationJson()),
        new RecordedResponse(EntityRelationsJson()),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new TagManagementViewModel(client);
    viewModel.SetContext(new TagManagementRouteContext("post", "post-1", "topic"));

    await viewModel.AddTagAsync("topic-2", TestContext.Current.CancellationToken);
    Assert.True(viewModel.TagLimitReached);

    await viewModel.AddTagAsync("topic-3", TestContext.Current.CancellationToken);

    Assert.False(viewModel.TagLimitReached);
    Assert.Null(viewModel.ErrorMessage);
  }

  [Fact]
  public async Task SelectTabAsyncResetsTagLimitReached()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(PostResponseJson()),
        new RecordedResponse(EntityRelationsJson()),
        new RecordedResponse(TagLimitReachedErrorJson(), HttpStatusCode.Forbidden),
        new RecordedResponse(EntityRelationsJson()),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new TagManagementViewModel(client);
    viewModel.SetContext(new TagManagementRouteContext("post", "post-1", "topic"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.AddTagAsync("topic-2", TestContext.Current.CancellationToken);
    Assert.True(viewModel.TagLimitReached);

    // Each relation tab has its own tag-limit budget, so a cap reached on one tab must not keep
    // hiding the add-tag form on another.
    await viewModel.SelectTabAsync("post", TestContext.Current.CancellationToken);

    Assert.False(viewModel.TagLimitReached);
  }

  [Fact]
  public async Task LoadAsyncResetsTagLimitReached()
  {
    var handler = new RecordingHandler([
        new RecordedResponse(PostResponseJson()),
        new RecordedResponse(EntityRelationsJson()),
        new RecordedResponse(TagLimitReachedErrorJson(), HttpStatusCode.Forbidden),
        new RecordedResponse(PostResponseJson()),
        new RecordedResponse(EntityRelationsJson()),
    ]);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new TagManagementViewModel(client);
    viewModel.SetContext(new TagManagementRouteContext("post", "post-1", "topic"));
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.AddTagAsync("topic-2", TestContext.Current.CancellationToken);
    Assert.True(viewModel.TagLimitReached);

    // A full reload (e.g. returning to this page after upgrading) must re-derive the flag from a
    // fresh mutation, not keep hiding the add-tag form behind a cap reached on a prior visit.
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.TagLimitReached);
  }

  [Fact]
  public async Task AddTagAsyncOtherErrorsSetErrorMessageAndLeaveTagLimitReachedFalse()
  {
    var handler = new RecordingHandler("not-json", HttpStatusCode.InternalServerError);
    var client = new VouchaApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
    var viewModel = new TagManagementViewModel(client);
    viewModel.SetContext(new TagManagementRouteContext("post", "post-1", "topic"));

    await viewModel.AddTagAsync("topic-2", TestContext.Current.CancellationToken);

    Assert.False(viewModel.TagLimitReached);
    Assert.NotNull(viewModel.ErrorMessage);
  }

  private static string TagLimitReachedErrorJson() =>
      """
      {
        "code": "TAG_LIMIT_REACHED",
        "message": "You have reached your tag limit."
      }
      """;
}
