using Voucha.Client.Core.Content;

namespace Voucha.Client.App.Controls;

public sealed class HackerNewsReferencesView : VerticalStackLayout
{
  private readonly ILauncher launcher;
  public HackerNewsReferencesView() : this(Launcher.Default) { }
  public HackerNewsReferencesView(ILauncher launcher) => this.launcher = launcher;
  public static readonly BindableProperty PresentationProperty = BindableProperty.Create(
      nameof(Presentation), typeof(HackerNewsPresentation), typeof(HackerNewsReferencesView),
      propertyChanged: (view, _, _) => ((HackerNewsReferencesView)view).Refresh());
  public HackerNewsPresentation? Presentation
  {
    get => (HackerNewsPresentation?)GetValue(PresentationProperty);
    set => SetValue(PresentationProperty, value);
  }
  public static readonly BindableProperty ShowDetailsProperty = BindableProperty.Create(
      nameof(ShowDetails), typeof(bool), typeof(HackerNewsReferencesView), false,
      propertyChanged: (view, _, _) => ((HackerNewsReferencesView)view).Refresh());
  public bool ShowDetails
  {
    get => (bool)GetValue(ShowDetailsProperty);
    set => SetValue(ShowDetailsProperty, value);
  }
  private void Refresh()
  {
    Children.Clear();
    IsVisible = Presentation?.HasReferences == true;
    if (Presentation is not { HasReferences: true } presentation) return;
    Children.Add(new Label { Text = presentation.Summary });
    if (!ShowDetails) return;
    AddLinks(presentation.SeenHeading, presentation.Stories);
    AddLinks(presentation.MentionedHeading, presentation.Comments);
  }
  private void AddLinks(string heading, IReadOnlyList<HackerNewsLink> links)
  {
    if (links.Count == 0) return;
    Children.Add(new Label { Text = heading, FontAttributes = FontAttributes.Bold });
    foreach (var link in links)
    {
      var button = new Button { Text = link.Label };
      button.Clicked += async (_, _) => await launcher.OpenAsync(link.Url);
      Children.Add(button);
    }
  }
}
