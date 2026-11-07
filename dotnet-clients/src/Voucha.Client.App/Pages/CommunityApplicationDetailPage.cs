using System.ComponentModel;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.App.Pages;

public sealed class CommunityApplicationDetailPage : ContentPage
{
  private readonly CommunityApplicationDetailViewModel viewModel;
  private CancellationTokenSource? lifetime;
  private readonly VerticalStackLayout body = new() { Padding = 16, Spacing = 8 };
  public CommunityApplicationDetailPage(CommunityApplicationDetailViewModel viewModel)
  {
    this.viewModel = viewModel;
    SetDynamicResource(TitleProperty, UiMessageKey.NativeDotnetCsharpCommunitiesApplications.Value);
    Content = new ScrollView { Content = body };
    viewModel.PropertyChanged += OnDetailChanged;
    Render();
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    viewModel.PropertyChanged -= OnDetailChanged;
    viewModel.PropertyChanged += OnDetailChanged;
    lifetime = new CancellationTokenSource();
    await viewModel.LoadAsync(lifetime.Token).ConfigureAwait(true);
  }

  protected override void OnDisappearing()
  {
    lifetime?.Cancel();
    lifetime?.Dispose();
    lifetime = null;
    viewModel.PropertyChanged -= OnDetailChanged;
    base.OnDisappearing();
  }

  private void OnDetailChanged(object? sender, PropertyChangedEventArgs args) => Render();

  private void Render()
  {
    body.Children.Clear();
    var close = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpClose);
    close.Clicked += async (_, _) => await Navigation.PopModalAsync().ConfigureAwait(true);
    body.Children.Add(close);
    if (viewModel.State == LoadState.Loading)
      body.Children.Add(UiCopy.Bind(new Label { AutomationId = "application-detail-loading" }, Label.TextProperty, UiMessageKey.NativeDotnetResidualLoading));
    if (viewModel.Error is { } error)
      body.Children.Add(new Label { Text = UiCopy.Resolve(error), AutomationId = "application-detail-error" });
    if (viewModel.Application is not { } application) return;
    body.Children.Add(UiCopy.Bind(new Label(), Label.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesApplicationId));
    body.Children.Add(new Label { Text = UiCopy.Resolve(UiText.ProtocolValue(application.Id)), AutomationId = "application-detail-id" });
    body.Children.Add(UiCopy.Bind(new Label(), Label.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesMessage));
    body.Children.Add(new Label { Text = UiCopy.Resolve(UiText.UserContent(application.Message)), AutomationId = "application-detail-message" });
    var answerNumber = 0;
    foreach (var answer in application.Answers.Values)
    {
      body.Children.Add(new HorizontalStackLayout
      {
        Spacing = 4,
        Children =
        {
          new Label { Text = "💬" },
          new Label { Text = UiCopy.FormatNumber(++answerNumber), FontAttributes = FontAttributes.Bold },
        },
      });
      var answerText = UiText.UserContent(answer.ToString());
      body.Children.Add(new Label { Text = UiCopy.Resolve(answerText), AutomationId = "application-detail-answer" });
    }
  }
}
