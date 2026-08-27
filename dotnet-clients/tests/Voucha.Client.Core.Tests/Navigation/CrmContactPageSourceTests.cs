using Xunit;

namespace Voucha.Client.Core.Tests.Navigation;

public sealed class CrmContactPageSourceTests
{
  [Fact]
  public void ContactPageSourceCoversComposerPopulationAndResetFlows()
  {
    var contactPage = File.ReadAllText(RepoPath(
        "dotnet-clients",
        "src",
        "Voucha.Client.App",
        "Pages",
        "CrmContactPage.cs"));
    var contactLayout = File.ReadAllText(RepoPath(
        "dotnet-clients",
        "src",
        "Voucha.Client.App",
        "Pages",
        "CrmContactPage.Layout.cs"));
    var contactsPage = string.Concat(
        File.ReadAllText(RepoPath(
            "dotnet-clients",
            "src",
            "Voucha.Client.App",
            "Pages",
            "CrmContactsPage.cs")),
        File.ReadAllText(RepoPath(
            "dotnet-clients",
            "src",
            "Voucha.Client.App",
            "Pages",
            "CrmContactsPage.Operations.cs")));

    Assert.Contains("emailSubjectEntry.Text = viewModel.EmailSubject;", contactPage, StringComparison.Ordinal);
    Assert.Contains("emailHtmlEditor.Text = viewModel.EmailBodyHtml;", contactPage, StringComparison.Ordinal);
    Assert.Contains("emailTextEditor.Text = viewModel.EmailBodyText;", contactPage, StringComparison.Ordinal);
    Assert.Contains("emailProviderPicker.SelectedIndex = EmailProviderToPickerIndex(viewModel.EmailProvider);", contactPage, StringComparison.Ordinal);
    Assert.Contains("viewModel.EmailProvider = PickerIndexToEmailProvider(emailProviderPicker.SelectedIndex);", contactPage, StringComparison.Ordinal);
    Assert.Contains("viewModel.EmailSubject = string.Empty;", contactPage, StringComparison.Ordinal);
    Assert.Contains("viewModel.EmailBodyHtml = string.Empty;", contactPage, StringComparison.Ordinal);
    Assert.Contains("viewModel.EmailBodyText = string.Empty;", contactPage, StringComparison.Ordinal);
    Assert.Contains("viewModel.DraftPrompt = string.Empty;", contactPage, StringComparison.Ordinal);
    Assert.Contains("viewModel.DraftTone = string.Empty;", contactPage, StringComparison.Ordinal);
    Assert.Contains("Shell.Current.GoToAsync(\"//crm\")", contactPage, StringComparison.Ordinal);

    Assert.Contains("UiMessageKey.NativeDotnetCsharpCrmEmailProvider", contactLayout, StringComparison.Ordinal);
    Assert.Contains("emailProviderPicker", contactLayout, StringComparison.Ordinal);

    Assert.Contains("if (viewModel.ErrorMessage is null)", contactsPage, StringComparison.Ordinal);
    Assert.Contains("ClearCreateForm();", contactsPage, StringComparison.Ordinal);
    Assert.Contains("ClearImportCsv();", contactsPage, StringComparison.Ordinal);
    Assert.Contains("nameEntry.Text = string.Empty;", contactsPage, StringComparison.Ordinal);
    Assert.Contains("emailEntry.Text = string.Empty;", contactsPage, StringComparison.Ordinal);
    Assert.Contains("phoneEntry.Text = string.Empty;", contactsPage, StringComparison.Ordinal);
    Assert.Contains("createVerticalPicker.SelectedIndex = 0;", contactsPage, StringComparison.Ordinal);
    Assert.Contains("createTypePicker.SelectedIndex = 0;", contactsPage, StringComparison.Ordinal);
    Assert.Contains("viewModel.ImportCsv = string.Empty;", contactsPage, StringComparison.Ordinal);
    Assert.Contains("importCsvEditor.Text = string.Empty;", contactsPage, StringComparison.Ordinal);
  }

  private static string RepoPath(params string[] parts)
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
      var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
      if (File.Exists(candidate))
      {
        return candidate;
      }

      directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
  }
}
