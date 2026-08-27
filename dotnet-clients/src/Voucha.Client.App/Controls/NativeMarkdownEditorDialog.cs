using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Controls;

public sealed class NativeMarkdownEditorDialog : ContentPage
{
  private readonly TaskCompletionSource<string?> completion = new();
  private readonly NativeMarkdownEditorView editor;

  private NativeMarkdownEditorDialog(string title, string accept, string initialMarkdown, VouchaApiClient apiClient)
  {
    Title = title;
    editor = new NativeMarkdownEditorView
    {
      ApiClient = apiClient,
      Markdown = initialMarkdown,
    };

    var cancelButton = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpCancel);
    var acceptButton = new Button { Text = accept };
    cancelButton.Clicked += async (_, _) => await CompleteAsync(null).ConfigureAwait(true);
    acceptButton.Clicked += async (_, _) => await CompleteAsync(editor.Markdown).ConfigureAwait(true);
    var actions = new HorizontalStackLayout
    {
      Spacing = 8,
      HorizontalOptions = LayoutOptions.End,
      Children =
      {
        cancelButton,
        acceptButton,
      },
    };
    actions.SetValue(Grid.RowProperty, 1);

    Content = new Grid
    {
      RowDefinitions =
      {
        new RowDefinition(GridLength.Star),
        new RowDefinition(GridLength.Auto),
      },
      Padding = 20,
      RowSpacing = 16,
      Children =
      {
        editor,
        actions,
      },
    };
  }

  public static async Task<string?> ShowAsync(
      INavigation navigation,
      string title,
      string accept,
      string initialMarkdown,
      VouchaApiClient apiClient)
  {
    var dialog = new NativeMarkdownEditorDialog(title, accept, initialMarkdown, apiClient);
    await navigation.PushModalAsync(new NavigationPage(dialog)).ConfigureAwait(true);
    return await dialog.completion.Task.ConfigureAwait(true);
  }

  private async Task CompleteAsync(string? markdown)
  {
    if (completion.Task.IsCompleted) return;
    completion.TrySetResult(markdown);
    await Navigation.PopModalAsync().ConfigureAwait(true);
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();
    completion.TrySetResult(null);
  }
}
