using System.ComponentModel;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.App.Pages;

[QueryProperty(nameof(Slug), "slug")]
public sealed partial class CommunityDetailPage : ContentPage
{
  private readonly CommunityDetailViewModel viewModel;
  private readonly Entry slugEntry = UiCopy.Bind(new Entry(), Entry.PlaceholderProperty, UiMessageKey.NativeDotnetCsharpCommunitiesCommunitySlug);
  private readonly Label titleLabel = new() { FontSize = 24, FontAttributes = FontAttributes.Bold };
  private readonly Label statusLabel = new();
  private readonly Label provenanceLabel = new();
  private readonly Label metricsLabel = new();
  private readonly Label errorLabel = new() { TextColor = Colors.Red };
  private readonly Button loadButton = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpLoad);
  private readonly Button joinButton = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesJoin);
  private readonly Button leaveButton = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesLeave);
  private readonly Button archiveButton = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesArchive);
  private readonly Button unarchiveButton = UiCopy.Bind(new Button(), Button.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesUnarchive);
  private readonly VerticalStackLayout membersLayout = new() { Spacing = 6 };
  private readonly VerticalStackLayout postsLayout = new() { Spacing = 6 };
  private string? slug;

  public CommunityDetailPage(CommunityDetailViewModel viewModel)
  {
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
    SetDynamicResource(TitleProperty, UiMessageKey.NativeDotnetCsharpCommunitiesCommunities.Value);
    viewModel.PropertyChanged += OnViewModelPropertyChanged;

    loadButton.Clicked += async (_, _) => await LoadCurrentSlugAsync().ConfigureAwait(true);
    joinButton.Clicked += async (_, _) => await MutateAsync(viewModel.JoinAsync).ConfigureAwait(true);
    leaveButton.Clicked += async (_, _) => await MutateAsync(viewModel.LeaveAsync).ConfigureAwait(true);
    archiveButton.Clicked += async (_, _) => await MutateAsync(viewModel.ArchiveAsync).ConfigureAwait(true);
    unarchiveButton.Clicked += async (_, _) => await MutateAsync(viewModel.UnarchiveAsync).ConfigureAwait(true);

    Content = new ScrollView
    {
      Content = new VerticalStackLayout
      {
        Padding = 16,
        Spacing = 12,
        Children =
        {
          slugEntry,
          loadButton,
          titleLabel,
          provenanceLabel,
          statusLabel,
          metricsLabel,
          errorLabel,
          new HorizontalStackLayout
          {
            Spacing = 8,
            Children = { joinButton, leaveButton, archiveButton, unarchiveButton },
          },
          UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesMembers),
          membersLayout,
          UiCopy.Bind(new Label { FontAttributes = FontAttributes.Bold }, Label.TextProperty, UiMessageKey.NativeDotnetCsharpCommunitiesPosts),
          postsLayout,
        },
      },
    };
    Render();
  }

  public string? Slug
  {
    get => slug;
    set
    {
      slug = value;
      slugEntry.Text = value ?? string.Empty;
      _ = LoadCurrentSlugAsync();
    }
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    if (!string.IsNullOrWhiteSpace(Slug))
    {
      await LoadCurrentSlugAsync().ConfigureAwait(true);
    }
  }

  private async Task LoadCurrentSlugAsync()
  {
    var nextSlug = (slugEntry.Text ?? Slug ?? string.Empty).Trim();
    if (nextSlug.Length == 0)
    {
      return;
    }

    await viewModel.LoadAsync(nextSlug).ConfigureAwait(true);
    Render();
  }

  private async Task MutateAsync(Func<CancellationToken, Task<bool>> action)
  {
    await action(CancellationToken.None).ConfigureAwait(true);
    Render();
  }

  private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args) => Render();

  private void Render()
  {
    titleLabel.Text = viewModel.Community?.Name ?? UiCopy.Localize(UiMessageKey.NativeDotnetResidualCommunity);
    provenanceLabel.Text = PublicProvenanceLabels.Resolve(viewModel.Community?.Provenance, UiCopy.CurrentLocalization);
    provenanceLabel.IsVisible = provenanceLabel.Text is not null;
    statusLabel.Text = viewModel.IsLoading
        ? UiCopy.Localize(UiMessageKey.NativeDotnetResidualLoading)
        : viewModel.IsArchived
            ? UiCopy.Localize(UiMessageKey.NativeDotnetResidualArchived)
            : viewModel.HasPendingApplication
                ? UiCopy.Localize(UiMessageKey.NativeDotnetResidualApplicationPending)
                : viewModel.Community is { } community
                    ? UiCopy.Resolve(UiTaxonomy.ListVisibility(community.Visibility))
                    : UiCopy.Localize(UiMessageKey.NativeDotnetResidualEnterCommunitySlug);
    metricsLabel.Text = viewModel.CommunityMetrics is { } metrics
        ? UiCopy.Format(UiMessageKey.NativeDotnetResidualCommunityMetrics, ("members", metrics.MemberCount), ("posts", metrics.PostCount), ("items", ListItemCountTotal(metrics)))
        : string.Empty;
    errorLabel.Text = viewModel.ErrorMessage ?? string.Empty;
    errorLabel.IsVisible = viewModel.HasError;
    joinButton.IsVisible = viewModel.CanJoin;
    leaveButton.IsVisible = viewModel.CanLeave;
    archiveButton.IsVisible = viewModel.CanArchive;
    unarchiveButton.IsVisible = viewModel.CanUnarchive;
    RenderRows(membersLayout, viewModel.Members.Select(member => $"{member.DisplayName} · {member.Role}"));
    RenderPostRows(postsLayout, viewModel.Posts);
  }

  private int ListItemCountTotal(CommunityMetrics metrics) =>
      viewModel.ListItemCounts is { } counts
          ? counts.Topic + counts.RssFeed + counts.Post + counts.UrlHostname + counts.Url
          : metrics.ListItemCount;

  private static void RenderRows(VerticalStackLayout layout, IEnumerable<string> rows)
  {
    layout.Children.Clear();
    foreach (var row in rows)
    {
      layout.Add(new Label { Text = row });
    }
  }
}
