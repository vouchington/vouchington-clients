using System.ComponentModel;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

internal sealed class ModerationDetailPage<T> : ContentPage
{
  private readonly Func<CancellationToken, Task<T>> load;
  private readonly Func<T, CancellationToken, View> render;
  private readonly VerticalStackLayout body = new() { Padding = 16, Spacing = 8 };
  private CancellationTokenSource? cancellation;
  private readonly INotifyPropertyChanged? updates;
  private T detail = default!;
  private bool hasDetail;

  public ModerationDetailPage(Func<CancellationToken, Task<T>> load, Func<T, View> render, INotifyPropertyChanged? updates = null)
      : this(load, (detail, _) => render(detail), updates) { }

  public ModerationDetailPage(Func<CancellationToken, Task<T>> load, Func<T, CancellationToken, View> render, INotifyPropertyChanged? updates = null)
  {
    this.load = load;
    this.render = render;
    this.updates = updates;
    var close = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpClose);
    close.Clicked += async (_, _) => await Navigation.PopModalAsync().ConfigureAwait(true);
    Content = new ScrollView { Content = new VerticalStackLayout { Children = { close, body } } };
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    cancellation?.Cancel();
    cancellation?.Dispose();
    cancellation = new CancellationTokenSource();
    var token = cancellation.Token;
    hasDetail = false;
    if (updates is not null)
    {
      updates.PropertyChanged -= Updated;
      updates.PropertyChanged += Updated;
    }
    body.Children.Clear();
    body.Children.Add(new ActivityIndicator { IsRunning = true, AutomationId = "moderation-detail-loading" });
    try
    {
      var loadedDetail = await load(token).ConfigureAwait(true);
      if (token.IsCancellationRequested) return;
      detail = loadedDetail;
      body.Children.Clear();
      hasDetail = true;
      body.Children.Add(render(detail, token));
    }
    catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    catch (Exception exception) when (exception is not OperationCanceledException || !token.IsCancellationRequested)
    {
      if (token.IsCancellationRequested) return;
      body.Children.Clear();
      body.Children.Add(UiCopy.Bind(new Label { AutomationId = "moderation-detail-error" },
          Label.TextProperty, UiMessageKey.NativeSwiftModerationReportsActionFailed));
    }
  }

  protected override void OnDisappearing()
  {
    if (updates is not null) updates.PropertyChanged -= Updated;
    hasDetail = false;
    detail = default!;
    cancellation?.Cancel();
    cancellation?.Dispose();
    cancellation = null;
    base.OnDisappearing();
  }
  private void Updated(object? sender, PropertyChangedEventArgs args)
  {
    if (!hasDetail || cancellation is not { IsCancellationRequested: false } active) return;
    body.Children.Clear();
    body.Children.Add(render(detail, active.Token));
  }

}
