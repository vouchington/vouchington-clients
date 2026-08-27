using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Voucha.Client.App.Controls;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

public sealed partial class CommunityActionPage : ContentPage
{
  private readonly CommunityActionViewModel viewModel;
  private readonly ITurnstileTokenProvider turnstileTokenProvider;
  private readonly Label titleLabel = new() { FontAttributes = FontAttributes.Bold, FontSize = 20 };
  private readonly Entry communitySlugEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCommunitiesCommunitySlug);
  private readonly Entry nameEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCommunitiesName);
  private readonly Entry slugEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCommunitiesSlug);
  private readonly NativeMarkdownEditorView markdownEditor = new();
  private readonly Entry emailEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCommunitiesEmail);
  private readonly Entry usernameEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCommunitiesUsername);
  private readonly Entry codeEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCommunitiesInviteCode);
  private readonly Entry messageEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCommunitiesMessage);
  private readonly VerticalStackLayout questionFields = new() { Spacing = 8 };
  private readonly Dictionary<string, Entry> answerEntries = new(StringComparer.Ordinal);
  private readonly Label errorLabel = new() { TextColor = Colors.Red };
  private readonly Button submitButton = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpSubmit);
  private string renderedQuestionKey = "";
  private bool isSubmitInProgress;

  public CommunityActionPage(
      CommunityActionViewModel viewModel,
      ITurnstileTokenProvider turnstileTokenProvider,
      VouchaApiClient apiClient)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.turnstileTokenProvider = turnstileTokenProvider ?? throw new ArgumentNullException(nameof(turnstileTokenProvider));
    markdownEditor.ApiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    BindingContext = viewModel;
    SetDynamicResource(TitleProperty, UiMessageKey.NativeDotnetCsharpCommunitiesCommunities.Value);
    viewModel.PropertyChanged += OnPropertyChanged;
    submitButton.Clicked += async (_, _) => await SubmitAsync().ConfigureAwait(true);

    Content = new ScrollView
    {
      Content = new VerticalStackLayout
      {
        Padding = 16,
        Spacing = 12,
        Children =
        {
          titleLabel,
          communitySlugEntry,
          nameEntry,
          slugEntry,
          markdownEditor,
          emailEntry,
          usernameEntry,
          codeEntry,
          questionFields,
          messageEntry,
          submitButton,
          errorLabel,
        },
      },
    };
    Render();
  }

  [SuppressMessage(
      "Design",
      "CA1031:Do not catch general exception types",
      Justification = "MAUI async event handlers must render submit failures instead of crashing the app.")]
  private async Task SubmitAsync()
  {
    if (isSubmitInProgress) return;
    isSubmitInProgress = true;
    try
    {
      viewModel.CommunitySlug = communitySlugEntry.Text ?? string.Empty;
      viewModel.Name = nameEntry.Text ?? string.Empty;
      viewModel.Slug = slugEntry.Text ?? string.Empty;
      viewModel.Markdown = markdownEditor.Markdown;
      viewModel.Email = emailEntry.Text;
      viewModel.Username = usernameEntry.Text;
      viewModel.Code = codeEntry.Text;
      viewModel.Message = messageEntry.Text;
      ApplyAnswers();
      if (!await EnsureTurnstileTokenAsync().ConfigureAwait(true))
      {
        return;
      }

      await viewModel.SubmitAsync().ConfigureAwait(true);
      Render();
    }
    catch (Exception ex)
    {
      System.Diagnostics.Debug.WriteLine(ex);
      errorLabel.Text = ex.Message;
      errorLabel.IsVisible = true;
    }
    finally
    {
      isSubmitInProgress = false;
    }
  }

  private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e) => Render();

  private void Render()
  {
    titleLabel.Text = viewModel.Kind switch
    {
      CommunityActionKind.Create => UiCopy.Localize(UiMessageKey.NativeSwiftCommunitiesCreateCommunity),
      CommunityActionKind.Apply => UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCommunitiesApply),
      CommunityActionKind.Invite => UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCommunitiesInviteMember),
      CommunityActionKind.RedeemInvite => UiCopy.Localize(UiMessageKey.NativeSwiftCommunitiesRedeemInvite),
      _ => UiCopy.Localize(UiMessageKey.NativeDotnetCsharpCommunitiesAction),
    };

    nameEntry.IsVisible = viewModel.Kind == CommunityActionKind.Create;
    slugEntry.IsVisible = viewModel.Kind == CommunityActionKind.Create;
    markdownEditor.IsVisible = viewModel.Kind == CommunityActionKind.Create;
    emailEntry.IsVisible = viewModel.Kind == CommunityActionKind.Invite;
    usernameEntry.IsVisible = viewModel.Kind == CommunityActionKind.Invite;
    codeEntry.IsVisible = viewModel.Kind == CommunityActionKind.RedeemInvite;
    messageEntry.IsVisible = viewModel.Kind is CommunityActionKind.Apply or CommunityActionKind.Create;
    questionFields.IsVisible = viewModel.Kind == CommunityActionKind.Apply && viewModel.ApplicationQuestions.Count > 0;
    communitySlugEntry.IsVisible = viewModel.Kind is not CommunityActionKind.Create;
    submitButton.SetDynamicResource(Button.TextProperty, UiMessageKey.NativeDotnetCsharpSubmit.Value);
    errorLabel.Text = viewModel.ErrorMessage ?? string.Empty;
    errorLabel.IsVisible = viewModel.HasError;
    RenderApplicationQuestions();
  }

  private void RenderApplicationQuestions()
  {
    var key = string.Join("|", viewModel.ApplicationQuestions.Select(question => question.Id));
    if (key == renderedQuestionKey)
    {
      return;
    }

    renderedQuestionKey = key;
    questionFields.Children.Clear();
    answerEntries.Clear();
    foreach (var question in viewModel.ApplicationQuestions.OrderBy(question => question.OrderIndex))
    {
      var label = new Label
      {
        Text = question.Required
            ? UiInvariantText.RequiredMarker.AppendTo(question.Question)
            : question.Question,
      };
      var entry = new Entry { Placeholder = question.FieldType };
      if (viewModel.ApplicationAnswers.TryGetValue(question.Id, out var answer))
      {
        entry.Text = UiUserInputText.FromValue(answer).Value;
      }

      answerEntries[question.Id] = entry;
      questionFields.Children.Add(label);
      questionFields.Children.Add(entry);
    }
  }

  private void ApplyAnswers()
  {
    foreach (var (questionId, entry) in answerEntries)
    {
      viewModel.SetApplicationAnswer(questionId, entry.Text);
    }
  }

}
