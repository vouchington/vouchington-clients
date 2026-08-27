using System.Runtime.CompilerServices;
using Voucha.Client.App.Pages;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class StaffSupportPageSourceTests
{
  [Fact]
  public void StaffSupportListsExposeAnAllFilterAndVisibleErrorRetryControls()
  {
    var threads = AppSource("Pages", "StaffSupportThreadsPage.cs");
    var contacts = AppSource("Pages", "StaffSupportContactsPage.cs");
    var contact = AppSource("Pages", "StaffSupportContactPage.cs");

    Assert.Contains("new(null, UiCopy.Localize(UiMessageKey.NativeSwiftCommonAll))", threads, StringComparison.Ordinal);
    Assert.Contains("(status.SelectedItem as StaffSupportStatusOption)?.Status", threads, StringComparison.Ordinal);
    Assert.Contains("status.SelectedItem is StaffSupportStatusOption option ? option.Status : viewModel.Status", threads, StringComparison.Ordinal);
    Assert.DoesNotContain("(status.SelectedItem as StaffSupportStatusOption)?.Status ?? viewModel.Status", threads, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeTaxonomySupportOpen", threads, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftPresentationValuesAssigned", threads, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeSwiftIntegrityResolved", threads, StringComparison.Ordinal);
    Assert.Contains("IUiLocaleChangeListener", threads, StringComparison.Ordinal);
    Assert.Contains("OnUiLocaleChanged() => ConfigureStatusPicker()", threads, StringComparison.Ordinal);
    Assert.Contains("staff-support-threads-error", threads, StringComparison.Ordinal);
    Assert.Contains("staff-support-threads-retry", threads, StringComparison.Ordinal);
    Assert.Contains("staff-support-contacts-error", contacts, StringComparison.Ordinal);
    Assert.Contains("staff-support-contacts-retry", contacts, StringComparison.Ordinal);
    Assert.Contains("staff-support-contact-error", contact, StringComparison.Ordinal);
    Assert.Contains("staff-support-contact-retry", contact, StringComparison.Ordinal);
    Assert.Contains("StaffSupportPageOperation.RunAsync", threads, StringComparison.Ordinal);
    Assert.Contains("StaffSupportPageOperation.RunAsync", contacts, StringComparison.Ordinal);
    Assert.Contains("StaffSupportPageOperation.RunAsync", contact, StringComparison.Ordinal);
    Assert.Contains("more.IsVisible = viewModel.HasMore", threads, StringComparison.Ordinal);
    Assert.Contains("more.IsEnabled = viewModel.HasMore && !viewModel.IsLoading", threads, StringComparison.Ordinal);
    Assert.Contains("more.IsVisible = viewModel.HasMore", contacts, StringComparison.Ordinal);
    Assert.Contains("more.IsEnabled = viewModel.HasMore && !viewModel.IsLoading", contacts, StringComparison.Ordinal);
    Assert.Contains("more.IsVisible = viewModel.HasMore", contact, StringComparison.Ordinal);
    Assert.Contains("more.IsEnabled = viewModel.HasMore && !viewModel.IsLoading", contact, StringComparison.Ordinal);
    var listPages = threads + contacts + contact;
    Assert.Equal(3, Count("more.IsVisible = false", listPages));
    Assert.Equal(3, Count("more.IsEnabled = false", listPages));
    Assert.Contains("staff-support-threads-load-more", threads, StringComparison.Ordinal);
    Assert.Contains("staff-support-contacts-load-more", contacts, StringComparison.Ordinal);
    Assert.Contains("staff-support-contact-load-more", contact, StringComparison.Ordinal);
    Assert.Equal(3, Count("viewModel.PropertyChanged += ViewModelPropertyChanged", listPages));
    Assert.Equal(3, Count("viewModel.PropertyChanged -= ViewModelPropertyChanged", listPages));
    Assert.DoesNotContain("System.Diagnostics.Debug.WriteLine", threads + contacts + contact, StringComparison.Ordinal);
  }

  [Fact]
  public void ThreadPageRefreshesPollingStatePreservesFailedDraftEditsAndLabelsDirections()
  {
    var thread = AppSource("Pages", "StaffSupportThreadPage.cs");

    Assert.Contains("viewModel.PropertyChanged += ViewModelPropertyChanged", thread, StringComparison.Ordinal);
    Assert.Contains("IUiLocaleChangeListener", thread, StringComparison.Ordinal);
    Assert.Contains("OnUiLocaleChanged() => Refresh()", thread, StringComparison.Ordinal);
    Assert.Contains("staff-support-thread-retry", thread, StringComparison.Ordinal);
    Assert.Contains("UiMessageKey.NativeCommonRetry", thread, StringComparison.Ordinal);
    Assert.Contains("retry.IsVisible = false", thread, StringComparison.Ordinal);
    Assert.Contains("retry.IsEnabled = !viewModel.IsBusy", thread, StringComparison.Ordinal);
    Assert.Contains("StaffSupportPageOperation.RunAsync", thread, StringComparison.Ordinal);
    Assert.Contains("await RunAsync(() => viewModel.LoadAsync(id))", thread, StringComparison.Ordinal);
    Assert.Contains("await RunAsync(() => viewModel.LoadAsync(threadId))", thread, StringComparison.Ordinal);
    Assert.Contains("retry.IsVisible = !string.IsNullOrWhiteSpace(viewModel.ErrorMessage)", thread, StringComparison.Ordinal);
    Assert.Contains("unsavedDraftBodies[message.Id] = body", thread, StringComparison.Ordinal);
    Assert.Contains("editor.TextChanged += (_, _) =>", thread, StringComparison.Ordinal);
    Assert.Contains("unsavedDraftBodies[message.Id] = editor.Text ?? string.Empty;", thread, StringComparison.Ordinal);
    Assert.Contains("if (viewModel.ErrorMessage is null) unsavedDraftBodies.Remove(message.Id)", thread, StringComparison.Ordinal);
    Assert.Contains("UiTaxonomy.MessageDirection(message.Direction)", thread, StringComparison.Ordinal);
    Assert.Contains("viewModel.CanGenerateDraft", thread, StringComparison.Ordinal);
    Assert.Contains("string.Equals(editor.Text, message.BodyText, StringComparison.Ordinal)", thread, StringComparison.Ordinal);
    Assert.Contains("viewModel.CanReply && StaffSupportThreadViewModel.CanEditDraft(message)", thread, StringComparison.Ordinal);
    Assert.Contains("viewModel.CanReply && StaffSupportThreadViewModel.CanSendDraft(message)", thread, StringComparison.Ordinal);
  }

  [Fact]
  public void ContactThreadRowsNavigateToTheStaffThreadWorkflow()
  {
    var contact = AppSource("Pages", "StaffSupportContactPage.cs");

    Assert.Contains("new DataTemplate(BuildThreadRow)", contact, StringComparison.Ordinal);
    Assert.Contains("services.GetRequiredService<StaffSupportThreadPage>()", contact, StringComparison.Ordinal);
    Assert.Contains("page.SetContext(thread.Id)", contact, StringComparison.Ordinal);
  }

  [Fact]
  public async Task PageOperationRendersUnexpectedFailuresWithoutEscapingAnAsyncHandler()
  {
    Exception? reported = null;
    var refreshed = false;

    await StaffSupportPageOperation.RunAsync(
        () => Task.FromException(new InvalidOperationException("unexpected")),
        exception => reported = exception,
        () => refreshed = true);

    Assert.IsType<InvalidOperationException>(reported);
    Assert.True(refreshed);
  }

  [Fact]
  public void StaffSupportRegistrationDoesNotThrowForAnAnonymousSession()
  {
    var registration = AppSource("", "MauiProgram.StaffSupport.cs");

    Assert.Contains("Current.Identity?.Id,", registration, StringComparison.Ordinal);
    Assert.DoesNotContain("Staff support requires an authenticated administrator", registration, StringComparison.Ordinal);
  }

  private static string AppSource(string directory, string file, [CallerFilePath] string sourceFile = "")
  {
    var root = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
    while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "src"))) root = root.Parent;
    return File.ReadAllText(Path.Combine(root?.FullName ?? throw new DirectoryNotFoundException(), "src", "Voucha.Client.App", directory, file));
  }

  private static int Count(string value, string source) => source.Split(value, StringSplitOptions.None).Length - 1;
}
