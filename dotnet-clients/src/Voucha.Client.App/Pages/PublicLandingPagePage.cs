using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.LandingPages;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.App.Pages;

public sealed class PublicLandingPagePage : ContentPage, IDisposable
{
  private readonly PublicLandingPageViewModel model;
  private readonly Func<PublicLandingPageTarget, Task> open;
  private readonly VerticalStackLayout content = new() { Padding = 20, Spacing = 12 };
  private readonly Label error = new();

  public PublicLandingPagePage(PublicLandingPageViewModel model, Func<PublicLandingPageTarget, Task> open)
  {
    this.model = model ?? throw new ArgumentNullException(nameof(model));
    this.open = open ?? throw new ArgumentNullException(nameof(open));
    Content = new ScrollView { Content = content };
    model.PropertyChanged += OnStateChanged;
    Unloaded += (_, _) => Dispose();
    Render();
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    await PageLoadGuard.RunAsync(() => model.ActivateAsync()).ConfigureAwait(true);
  }

  protected override void OnDisappearing()
  {
    model.Deactivate();
    base.OnDisappearing();
  }

  private void OnStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => Dispatcher.Dispatch(Render);

  private void Render()
  {
    content.Clear();
    Title = UiCopy.Resolve(UiText.UserContent(model.Page?.Title));
    if (model.Page is { } page)
    {
      content.Add(new Label { Text = UiCopy.Resolve(UiText.UserContent(page.Title)), FontSize = 24 });
      content.Add(new Label { Text = UiCopy.Resolve(UiText.UserContent(page.Subtitle)) });
      content.Add(new Label { Text = UiCopy.Resolve(UiText.UserContent(model.Owner?.DisplayName)) });
      foreach (var item in page.Items ?? []) AddItem(item, null);
    }
    content.Add(new ActivityIndicator { IsRunning = model.IsLoading, IsVisible = model.IsLoading });
    error.Text = model.Error is { } message ? UiCopy.Resolve(message) : string.Empty;
    content.Add(error);
    var retry = UiCopy.Bind(new Button { AutomationId = "public-landing-reload" }, Button.TextProperty, model.Error is null ? UiMessageKey.NativeDotnetCommonRefresh : UiMessageKey.NativeCommonRetry);
    retry.Clicked += async (_, _) => await model.ReloadAsync().ConfigureAwait(true);
    content.Add(retry);
  }

  private void AddItem(LandingPageItem item, string? parent)
  {
    if (item is LandingPageTopicGroupItem group)
    {
      content.Add(new Label { Text = UiCopy.Resolve(PublicLandingPageTarget.Label(item)), FontAttributes = FontAttributes.Bold });
      foreach (var entry in group.Entries) AddItem(entry, group.Id);
      return;
    }
    if (PublicLandingPageTarget.For(item) is null) return;
    var button = new Button
    {
      Text = UiCopy.Resolve(PublicLandingPageTarget.Label(item)),
      AutomationId = $"public-landing-item-{item.Id}",
    };
    var activation = model.ActivationVersion;
    button.Clicked += async (_, _) => await ActivateItemAsync(parent ?? item.Id,
        parent is null ? null : PublicLandingPageTarget.MemberId(item), activation).ConfigureAwait(true);
    content.Add(button);
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "An external target failure must not crash the public viewer.")]
  internal async Task ActivateItemAsync(string itemId, string? memberId = null, int? activationVersion = null)
  {
    if (model.ActivateItem(itemId, memberId, activationVersion) is not { } target) return;
    try { await open(target).ConfigureAwait(true); }
    catch (Exception ex) { error.Text = UiCopy.Resolve(UiText.Verbatim(ex.Message)); }
  }

  public void Dispose()
  {
    model.PropertyChanged -= OnStateChanged;
    model.Dispose();
  }
}
