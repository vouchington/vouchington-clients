using Voucha.Client.Core.Api;
using Voucha.Client.Core.Crm;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed partial class CrmContactPage
{
  private VerticalStackLayout BuildLayout() =>
      new()
      {
        Padding = 16,
        Spacing = 12,
        Children =
        {
          UiCopy.Bind(new Label { FontSize = 24, FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpCrmCrmContact),
          errorLabel,
          nameEntry,
          emailEntry,
          phoneEntry,
          verticalEntry,
          contactTypeEntry,
          followerCountEntry,
          notesEditor,
          new HorizontalStackLayout { Spacing = 8, Children = { BuildButton(UiMessageKey.CommonSave, OnSaveClicked), BuildButton(UiMessageKey.NativeDotnetCsharpArchive, OnArchiveClicked), BuildButton(UiMessageKey.NativeDotnetCsharpCrmLink, OnLinkClicked), BuildButton(UiMessageKey.NativeDotnetCsharpCrmUnlink, OnUnlinkClicked) } },
          linkUserEntry,
          UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpCrmAiDraft),
          draftPromptEntry,
          draftToneEntry,
          new HorizontalStackLayout { Spacing = 8, Children = { BuildButton(UiMessageKey.NativeDotnetCsharpCrmDraft, OnDraftClicked), BuildButton(UiMessageKey.NativeDotnetCsharpCrmSendEmail, OnSendEmailClicked) } },
          UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpCrmEmailProvider),
          emailProviderPicker,
          emailSubjectEntry,
          emailHtmlEditor,
          emailTextEditor,
          UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpCrmNotes),
          noteBodyEditor,
          BuildButton(UiMessageKey.NativeDotnetCsharpCrmAddNote, OnAddNoteClicked),
          UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpCrmSocialAccounts),
          socialAccountsView,
          UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpCrmEmails),
          emailsView,
          UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpCrmNotesHistory),
          notesView,
        },
      };

  private View BuildSocialAccountTemplate()
  {
    var title = new Label { FontAttributes = FontAttributes.Bold };
    title.SetBinding(Label.TextProperty, new Binding(nameof(CrmContactSocialAccount.Platform), stringFormat: "{0}"));
    var body = new Label();
    body.SetBinding(Label.TextProperty, new Binding(nameof(CrmContactSocialAccount.Handle), stringFormat: "{0}"));
    return new VerticalStackLayout { Spacing = 2, Children = { title, body } };
  }

  private View BuildEmailTemplate()
  {
    var subject = new Label { FontAttributes = FontAttributes.Bold };
    subject.SetBinding(Label.TextProperty, nameof(CrmEmailRow.Subject));
    var body = new Label();
    body.SetBinding(Label.TextProperty, nameof(CrmEmailRow.BodyText));
    return new Border { Stroke = Colors.LightGray, StrokeThickness = 1, Padding = 10, Content = new VerticalStackLayout { Spacing = 4, Children = { subject, body } } };
  }

  private View BuildNoteTemplate()
  {
    var body = new Label();
    body.SetBinding(Label.TextProperty, nameof(CrmNoteRow.Body));
    var delete = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpDelete);
    delete.Clicked += OnDeleteNoteClicked;
    delete.SetBinding(Button.CommandParameterProperty, new Binding("."));
    return new Border { Stroke = Colors.LightGray, StrokeThickness = 1, Padding = 10, Content = new VerticalStackLayout { Spacing = 4, Children = { body, delete } } };
  }

  private static Button BuildButton(UiMessageKey key, EventHandler handler)
  {
    var button = UiCopy.Bind(new Button(), Button.TextProperty, key);
    button.Clicked += handler;
    return button;
  }
}
