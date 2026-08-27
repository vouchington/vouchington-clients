using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.Core.Tests.Support;

public sealed class StaffSupportListViewModelTests
{
  [Fact]
  public void UnexpectedFailuresUseTheLocalizedErrorInsteadOfLeakingImplementationDetails()
  {
    var service = new StaffSupportTestService();
    var error = new InvalidOperationException("database details");
    var expected = UiLocalization.English.Localize(UiMessageKey.NativeDotnetCsharpError);
    var threads = new StaffSupportThreadsViewModel(service);
    var contacts = new StaffSupportContactsViewModel(service);
    var contact = new StaffSupportContactViewModel(service);

    threads.ReportUnexpectedError(error);
    contacts.ReportUnexpectedError(error);
    contact.ReportUnexpectedError(error);

    Assert.Equal(expected, threads.ErrorMessage);
    Assert.Equal(expected, contacts.ErrorMessage);
    Assert.Equal(expected, contact.ErrorMessage);
  }

  [Fact]
  public async Task ThreadListTrimsFiltersAppendsDistinctPagesAndRendersFailures()
  {
    var service = new StaffSupportTestService
    {
      ThreadResults = [StaffSupportTestService.ThreadFor("one")],
      ThreadPageInfo = new("one", true, "next"),
    };
    var viewModel = new StaffSupportThreadsViewModel(service)
    { Query = " account ", Status = StaffSupportThreadStatusFilter.Assigned };

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    service.ThreadResults = [StaffSupportTestService.ThreadFor("one"), StaffSupportTestService.ThreadFor("two")];
    service.ThreadPageInfo = new("two", false, null);
    await viewModel.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["one", "two"], viewModel.Threads.Select(thread => thread.Id));
    Assert.Equal([null, "one"], service.ThreadAfters);
    Assert.Contains("threads:account:Assigned:30", service.Calls);
    Assert.False(viewModel.HasMore);

    service.Failure = new HttpRequestException("offline");
    await viewModel.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal("offline", viewModel.ErrorMessage);
    Assert.False(viewModel.IsLoading);
  }

  [Fact]
  public async Task ContactListAndDetailUseOpaqueCursorsAndAvoidDuplicateRows()
  {
    var service = new StaffSupportTestService
    {
      ContactResults = [StaffSupportTestService.ContactFor("one")],
      ContactPageInfo = new("one", true, "next"),
      ContactThreads = [StaffSupportTestService.ThreadFor("thread-one")],
      ContactThreadPageInfo = new("thread-one", true, "older"),
    };
    var contacts = new StaffSupportContactsViewModel(service) { Query = "  traveler " };

    await contacts.LoadAsync(TestContext.Current.CancellationToken);
    service.ContactResults = [StaffSupportTestService.ContactFor("one"), StaffSupportTestService.ContactFor("two")];
    service.ContactPageInfo = new("two", false, null);
    await contacts.LoadMoreAsync(TestContext.Current.CancellationToken);

    var contact = new StaffSupportContactViewModel(service);
    await contact.LoadAsync("contact", TestContext.Current.CancellationToken);
    service.ContactThreads = [StaffSupportTestService.ThreadFor("thread-one"), StaffSupportTestService.ThreadFor("thread-two")];
    service.ContactThreadPageInfo = new("thread-two", false, null);
    await contact.LoadMoreAsync(TestContext.Current.CancellationToken);

    Assert.Equal(["one", "two"], contacts.Contacts.Select(item => item.Id));
    Assert.Equal([null, "one"], service.ContactAfters);
    Assert.Contains("contacts:traveler:30", service.Calls);

    service.Failure = new HttpRequestException("offline");
    await contacts.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal("offline", contacts.ErrorMessage);
    Assert.False(contacts.IsLoading);

    service.Failure = null;
    Assert.Equal(["thread-one", "thread-two"], contact.Threads.Select(item => item.Id));
    Assert.False(contact.HasMore);

    service.Failure = new HttpRequestException("offline");
    await contact.LoadAsync("contact", TestContext.Current.CancellationToken);
    Assert.Equal("offline", contact.ErrorMessage);
    Assert.False(contact.IsLoading);
  }
}
