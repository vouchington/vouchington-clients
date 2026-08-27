using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Controls;

public sealed partial class NativeMarkdownEditorView : ContentView
{
  public static readonly BindableProperty MarkdownProperty = BindableProperty.Create(
      nameof(Markdown),
      typeof(string),
      typeof(NativeMarkdownEditorView),
      "",
      BindingMode.TwoWay,
      propertyChanged: OnMarkdownChanged);

  public static readonly BindableProperty IsReadOnlyProperty = BindableProperty.Create(
      nameof(IsReadOnly),
      typeof(bool),
      typeof(NativeMarkdownEditorView),
      false,
      propertyChanged: OnIsReadOnlyChanged);

  private readonly Editor editor;
  private readonly NativeHtmlContentView preview;
  private readonly Button writeButton;
  private readonly Button previewButton;
  private readonly VerticalStackLayout suggestionsLayout;
  private CancellationTokenSource? autocompleteCancellation;
  private int previewRequest;
  private AutocompleteToken? autocompleteToken;

  public NativeMarkdownEditorView()
  {
    writeButton = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpEditorWrite);
    previewButton = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpEditorPreview);
    editor = UiCopy.Bind(new Editor
    {
      MinimumHeightRequest = 160,
      AutoSize = EditorAutoSizeOption.TextChanges,
    }, Editor.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpEditorBody);
    preview = new NativeHtmlContentView
    {
      MinimumHeightRequest = 160,
      IsVisible = false,
    };
    suggestionsLayout = new VerticalStackLayout
    {
      Spacing = 0,
      IsVisible = false,
    };

    writeButton.Clicked += (_, _) => SetPreviewMode(false);
    previewButton.Clicked += async (_, _) => await SetPreviewModeAsync();
    editor.TextChanged += OnEditorTextChanged;

    var tabs = new Grid
    {
      ColumnDefinitions =
      {
        new ColumnDefinition(GridLength.Star),
        new ColumnDefinition(GridLength.Star),
      },
      ColumnSpacing = 8,
    };
    tabs.Add(writeButton);
    tabs.Add(previewButton, 1);

    Content = new VerticalStackLayout
    {
      Spacing = 8,
      Children =
      {
        tabs,
        editor,
        preview,
        suggestionsLayout,
      },
    };
  }

  public string Markdown
  {
    get => (string)GetValue(MarkdownProperty);
    set => SetValue(MarkdownProperty, value);
  }

  public VouchaApiClient? ApiClient { get; set; }

  public bool IsReadOnly
  {
    get => (bool)GetValue(IsReadOnlyProperty);
    set => SetValue(IsReadOnlyProperty, value);
  }

  private static void OnMarkdownChanged(BindableObject bindable, object oldValue, object newValue)
  {
    var view = (NativeMarkdownEditorView)bindable;
    var text = newValue as string ?? "";
    if (!string.Equals(view.editor.Text, text, StringComparison.Ordinal))
    {
      view.editor.Text = text;
    }
  }

  private static void OnIsReadOnlyChanged(BindableObject bindable, object oldValue, object newValue)
  {
    ((NativeMarkdownEditorView)bindable).editor.IsReadOnly = newValue is true;
  }

  private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
  {
    var text = e.NewTextValue ?? "";
    if (!string.Equals(Markdown, text, StringComparison.Ordinal))
    {
      Markdown = text;
    }

    ScheduleAutocomplete(text);
  }

  private void SetPreviewMode(bool enabled)
  {
    editor.IsVisible = !enabled;
    suggestionsLayout.IsVisible = !enabled && suggestionsLayout.Children.Count > 0;
    preview.IsVisible = enabled;
  }

  private async Task SetPreviewModeAsync()
  {
    SetPreviewMode(true);
    var text = Markdown?.Trim() ?? "";
    var request = Interlocked.Increment(ref previewRequest);
    if (text.Length == 0)
    {
      preview.Html = "";
      preview.Fallback = "";
      return;
    }

    if (ApiClient is null)
    {
      preview.Html = "";
      preview.Fallback = Markdown;
      return;
    }

    try
    {
      var response = await ApiClient.PreviewMarkdownAsync(text).ConfigureAwait(true);
      if (request != previewRequest) return;
      preview.Html = response.Html;
      preview.Fallback = Markdown;
    }
    catch (Exception)
    {
      if (request != previewRequest) return;
      preview.Html = "";
      preview.Fallback = Markdown;
    }
  }

}
