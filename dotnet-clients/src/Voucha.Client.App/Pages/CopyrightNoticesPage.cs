using System.ComponentModel;
using Voucha.Client.Core.Copyright;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;

namespace Voucha.Client.App.Pages;

public sealed partial class CopyrightNoticesPage : ContentPage, IUiLocaleChangeListener, IDisposable
{
  private readonly CopyrightNoticesViewModel viewModel;
  private readonly IUiLocaleController locales;
  private readonly Func<NativeRoutePath, Task> navigate;
  private readonly Func<Uri, Task> openExternal;
  private readonly string? caseId;
  private readonly VerticalStackLayout body = new() { Spacing = 12, Padding = 16 };
  private CancellationTokenSource lifetime = new();
  private IDisposable? localeSubscription;
  private bool disposed;
  private Task activeRequest = Task.CompletedTask;
  private int presentationGeneration;

  public CopyrightNoticesPage(CopyrightNoticesViewModel viewModel, IUiLocaleController locales,
      Func<NativeRoutePath, Task> navigate, string? caseId = null, Func<Uri, Task>? openExternal = null)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    this.locales = locales ?? throw new ArgumentNullException(nameof(locales));
    this.navigate = navigate ?? throw new ArgumentNullException(nameof(navigate));
    this.openExternal = openExternal ?? (uri => Microsoft.Maui.ApplicationModel.Browser.Default.OpenAsync(uri));
    this.caseId = caseId;
    AutomationId = "copyright-notices-page";
    Content = new ScrollView { Content = body };
    Render();
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();
    if (disposed) return;
    lifetime.Cancel();
    lifetime.Dispose();
    lifetime = new();
    viewModel.PropertyChanged += Changed;
    localeSubscription = locales.SubscribeLocaleChanges(this);
    _ = ResumeAsync(activeRequest, ++presentationGeneration, lifetime.Token);
  }

  protected override void OnDisappearing()
  {
    lifetime.Cancel();
    presentationGeneration++;
    viewModel.PropertyChanged -= Changed;
    localeSubscription?.Dispose();
    localeSubscription = null;
    base.OnDisappearing();
  }

  public Task LoadAsync(CancellationToken cancellationToken = default) => activeRequest = caseId is null
      ? viewModel.LoadAsync(cancellationToken) : viewModel.LoadCaseAsync(caseId, cancellationToken);

  private async Task ResumeAsync(Task previous, int generation, CancellationToken cancellationToken)
  {
    await previous.ConfigureAwait(true);
    if (generation == presentationGeneration && !cancellationToken.IsCancellationRequested)
      await LoadAsync(cancellationToken).ConfigureAwait(true);
  }

  private Task LoadMoreAsync() => activeRequest = viewModel.LoadMoreAsync(lifetime.Token);

  public void OnUiLocaleChanged() => Render();

  private void Changed(object? sender, PropertyChangedEventArgs args) => Render();

  private void Render()
  {
    body.Children.Clear();
    SetDynamicResource(TitleProperty, (caseId is null
        ? UiMessageKey.NativeCopyrightNoticesTitle : UiMessageKey.NativeCopyrightNoticesDetailTitle).Value);
    if (!viewModel.IsSignedIn)
    {
      body.Children.Add(Link(UiCopy.Localize(UiMessageKey.ExtractedVotesSemanticVoteSignIn),
          NativeRoutePath.Segments("login"), "copyright-sign-in"));
      return;
    }
    if (viewModel.IsLoading)
      body.Children.Add(new ActivityIndicator { IsRunning = true, AutomationId = "copyright-loading" });
    if (viewModel.HasError)
    {
      var retry = UiCopy.Bind(new Button { AutomationId = "copyright-retry" }, Button.TextProperty, UiMessageKey.NativeCommonRetry);
      retry.Clicked += (_, _) => _ = caseId is null && viewModel.Notices.Count > 0 && viewModel.HasMore
          ? LoadMoreAsync() : LoadAsync(lifetime.Token);
      body.Children.Add(retry);
    }
    if (caseId is null) RenderList();
    else if (viewModel.SelectedCase is { } selected) RenderDetail(selected);
  }

  private Button Link(string text, NativeRoutePath path, string id)
  {
    var button = new Button { Text = text, AutomationId = id };
    button.Clicked += (_, _) => _ = navigate(path);
    return button;
  }

  private static Label Message(UiMessageKey key) => UiCopy.Bind(new Label(), Label.TextProperty, key);
  private static Label Value(string text) => new() { Text = UiCopy.Resolve(UiText.Verbatim(text)) };
  private static Label Format(UiMessageKey key, params (string Name, object? Value)[] arguments) =>
      Value(UiCopy.Format(key, arguments));

  public void Dispose()
  {
    if (disposed) return;
    disposed = true;
    presentationGeneration++;
    lifetime.Cancel();
    lifetime.Dispose();
    viewModel.PropertyChanged -= Changed;
    localeSubscription?.Dispose();
    GC.SuppressFinalize(this);
  }
}
