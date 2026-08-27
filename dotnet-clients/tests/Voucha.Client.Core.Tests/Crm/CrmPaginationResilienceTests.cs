using Voucha.Client.Core.Api;
using Voucha.Client.Core.Crm;
using Xunit;

namespace Voucha.Client.Core.Tests.Crm;

public sealed partial class CrmViewModelTests
{
  [Fact]
  public async Task ContactContinuationFailureRetainsRowsAndRetriesCursorWithoutDuplicates()
  {
    var service = new RecordingCrmService();
    service.ContactPageResults.Enqueue(() => Task.FromResult(new CrmContactListResponse(
        [Contact("contact-1")], new PageInfo("contacts-next", true, null))));
    service.ContactPageResults.Enqueue(() => Task.FromException<CrmContactListResponse>(
        new InvalidOperationException("contact page failed")));
    service.ContactPageResults.Enqueue(() => Task.FromResult(new CrmContactListResponse(
        [Contact("contact-1"), Contact("contact-2", "Bob", "bob@example.test")],
        new PageInfo(null, false, null))));
    var viewModel = new CrmContactsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    await viewModel.LoadMoreContactsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["contact-1"], viewModel.Contacts.Select(row => row.Id));
    Assert.True(viewModel.HasContactPaginationError);
    Assert.Equal("contact page failed", viewModel.ContactPaginationErrorMessage);
    Assert.True(viewModel.HasMoreContacts);

    await viewModel.LoadMoreContactsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["contact-1", "contact-2"], viewModel.Contacts.Select(row => row.Id));
    Assert.False(viewModel.HasContactPaginationError);
    Assert.Null(viewModel.ContactPaginationErrorMessage);
    Assert.False(viewModel.HasMoreContacts);
    Assert.Equal(new string?[] { null, "contacts-next", "contacts-next" }, service.ContactCursors);
  }

  [Fact]
  public async Task RefreshRejectsAStaleContactContinuationCompletion()
  {
    var stalePage = new TaskCompletionSource<CrmContactListResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingCrmService();
    service.ContactPageResults.Enqueue(() => Task.FromResult(new CrmContactListResponse(
        [Contact("contact-1")], new PageInfo("contacts-next", true, null))));
    service.ContactPageResults.Enqueue(() => stalePage.Task);
    service.ContactPageResults.Enqueue(() => Task.FromResult(new CrmContactListResponse(
        [Contact("contact-3", "Carol", "carol@example.test")],
        new PageInfo(null, false, null))));
    var viewModel = new CrmContactsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var continuation = viewModel.LoadMoreContactsAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    stalePage.SetResult(new CrmContactListResponse(
        [Contact("contact-2", "Bob", "bob@example.test")],
        new PageInfo(null, false, null)));
    await continuation;

    Assert.Equal(["contact-3"], viewModel.Contacts.Select(row => row.Id));
    Assert.False(viewModel.HasContactPaginationError);
    Assert.False(viewModel.HasMoreContacts);
  }

  [Fact]
  public async Task RefreshRejectsAStaleContactContinuationFailure()
  {
    var stalePage = new TaskCompletionSource<CrmContactListResponse>(
        TaskCreationOptions.RunContinuationsAsynchronously);
    var service = new RecordingCrmService();
    service.ContactPageResults.Enqueue(() => Task.FromResult(new CrmContactListResponse(
        [Contact("contact-1")], new PageInfo("contacts-next", true, null))));
    service.ContactPageResults.Enqueue(() => stalePage.Task);
    service.ContactPageResults.Enqueue(() => Task.FromResult(new CrmContactListResponse(
        [Contact("contact-3", "Carol", "carol@example.test")],
        new PageInfo(null, false, null))));
    var viewModel = new CrmContactsViewModel(service);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    var continuation = viewModel.LoadMoreContactsAsync(TestContext.Current.CancellationToken);
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    stalePage.SetException(new InvalidOperationException("stale failure"));
    await continuation;

    Assert.Equal(["contact-3"], viewModel.Contacts.Select(row => row.Id));
    Assert.Null(viewModel.ErrorMessage);
    Assert.False(viewModel.HasContactPaginationError);
  }

  [Fact]
  public async Task CrmHasMoreRequiresAnInitialPage()
  {
    var contacts = new CrmContactsViewModel(new RecordingCrmService());
    var service = new RecordingCrmService();
    service.EmailPageResults.Enqueue(() => Task.FromException<CrmEmailListResponse>(
        new InvalidOperationException("initial email failed")));
    var detail = new CrmContactDetailViewModel(service);

    Assert.False(contacts.HasMoreContacts);
    await detail.LoadAsync("contact-1", TestContext.Current.CancellationToken);

    Assert.False(detail.HasMoreEmails);
    Assert.False(detail.HasMoreNotes);
  }

  [Fact]
  public async Task UnexpectedInitialContactCancellationSurfacesThroughGlobalErrorAndRetries()
  {
    var service = new RecordingCrmService();
    service.ContactPageResults.Enqueue(() => Task.FromCanceled<CrmContactListResponse>(
        new CancellationToken(true)));
    service.ContactPageResults.Enqueue(() => Task.FromResult(new CrmContactListResponse(
        [Contact("contact-1")], new PageInfo(null, false, null))));
    var viewModel = new CrmContactsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.True(viewModel.HasError);
    Assert.True(viewModel.HasContactPaginationError);
    Assert.False(viewModel.HasMoreContacts);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.False(viewModel.HasError);
    Assert.False(viewModel.HasContactPaginationError);
    Assert.Equal(["contact-1"], viewModel.Contacts.Select(row => row.Id));
    Assert.Equal(new string?[] { null, null }, service.ContactCursors);
  }

  [Fact]
  public async Task EmailContinuationFailureIsIsolatedAndRetriesCursorWithoutDuplicates()
  {
    var firstMessage = Message("message-1", "First", CrmMessageDirection.Inbound);
    var service = new RecordingCrmService();
    service.EmailPageResults.Enqueue(() => Task.FromResult(new CrmEmailListResponse(
        [firstMessage], new PageInfo("emails-next", true, null))));
    service.EmailPageResults.Enqueue(() => Task.FromException<CrmEmailListResponse>(
        new InvalidOperationException("email page failed")));
    service.EmailPageResults.Enqueue(() => Task.FromResult(new CrmEmailListResponse(
        [firstMessage, Message("message-2", "Second", CrmMessageDirection.Inbound)],
        new PageInfo(null, false, null))));
    var viewModel = new CrmContactDetailViewModel(service);
    await viewModel.LoadAsync("contact-1", TestContext.Current.CancellationToken);

    await viewModel.LoadMoreEmailsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["message-1"], viewModel.Emails.Select(row => row.Id));
    Assert.True(viewModel.HasEmailPaginationError);
    Assert.False(viewModel.HasNotePaginationError);
    Assert.True(viewModel.HasMoreEmails);

    await viewModel.LoadMoreEmailsAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["message-1", "message-2"], viewModel.Emails.Select(row => row.Id));
    Assert.False(viewModel.HasEmailPaginationError);
    Assert.False(viewModel.HasMoreEmails);
    Assert.Equal(new string?[] { null, "emails-next", "emails-next" }, service.EmailCursors);
  }
}
