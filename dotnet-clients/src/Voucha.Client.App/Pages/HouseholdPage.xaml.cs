using System.Diagnostics.CodeAnalysis;
using Voucha.Client.Core.Households;
using Voucha.Client.Core.Localization;
using Voucha.Client.App.Controls;

namespace Voucha.Client.App.Pages;

public partial class HouseholdPage : ContentPage
{
  private readonly HouseholdViewModel viewModel;
  private readonly ViewportPaginationTrigger<HybridPaginationControl> paginationVisibility = new();

  public HouseholdPage(HouseholdViewModel viewModel)
  {
    InitializeComponent();
    this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    BindingContext = viewModel;
  }

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "MAUI lifecycle errors are represented by the view model.")]
  protected override async void OnAppearing()
  {
    base.OnAppearing();
    try { await viewModel.LoadAsync(); }
    catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
  }

  private async void OnRetryHouseholdClicked(object? sender, EventArgs e) => await viewModel.LoadAsync();

  private async void OnLoadMoreMembersRequested(object? sender, EventArgs eventArgs)
  {
    if (sender is HybridPaginationControl { BindingContext: HouseholdSection section })
    {
      await viewModel.LoadMembershipsAsync(section.Id);
      paginationVisibility.Rearm((HybridPaginationControl)sender);
      TryLoadVisiblePagination(HouseholdScroll, HouseholdScroll.ScrollY);
    }
  }

  private async void OnLoadMoreHouseholdsRequested(object? sender, EventArgs eventArgs)
  {
    await viewModel.LoadMoreMemberHouseholdsAsync();
    if (sender is HybridPaginationControl control) paginationVisibility.Rearm(control);
    TryLoadVisiblePagination(HouseholdScroll, HouseholdScroll.ScrollY);
  }

  private void OnHouseholdScrolled(object? sender, ScrolledEventArgs eventArgs)
  {
    if (sender is ScrollView scroll) TryLoadVisiblePagination(scroll, eventArgs.ScrollY);
  }

  private void OnHouseholdViewportChanged(object? sender, EventArgs eventArgs) =>
      TryLoadVisiblePagination(HouseholdScroll, HouseholdScroll.ScrollY);

  private void TryLoadVisiblePagination(ScrollView scroll, double scrollY)
  {
    if (scroll.Height <= 0 || scroll.Content is not VisualElement content) return;
    var candidates = scroll.GetVisualTreeDescendants()
        .OfType<HybridPaginationControl>()
        .Where(control => control.HasMore)
        .Select(control => (
            Control: control,
            Top: VerticalOffset(control, content),
            Height: control.Height > 0 ? control.Height : control.DesiredSize.Height))
        .Where(candidate => double.IsFinite(candidate.Top) && candidate.Height > 0)
        .ToArray();
    foreach (var control in paginationVisibility.EnteredViewport(candidates, scrollY, scroll.Height))
    {
      control.TryLoadAutomatically();
    }
  }

  private static double VerticalOffset(VisualElement control, VisualElement content)
  {
    var offset = 0d;
    Element? current = control;
    while (current is VisualElement visual && !ReferenceEquals(current, content))
    {
      offset += visual.Y;
      current = visual.Parent;
    }
    return ReferenceEquals(current, content) ? offset : double.PositiveInfinity;
  }

  private async void OnCreateClicked(object? sender, EventArgs e) => await viewModel.CreateHouseholdAsync();

  private async void OnRemoveClicked(object? sender, EventArgs e)
  {
    if (sender is not Button { CommandParameter: HouseholdMemberRow member }) return;
    var confirmed = await DisplayAlertAsync(
        UiCopy.Localize(UiMessageKey.NativeSwiftHouseholdsBookmarksRemoveMemberConfirmation),
        member.LocalizedDisplayName,
        UiCopy.Localize(UiMessageKey.NativeSwiftHouseholdsBookmarksRemoveMember),
        UiCopy.Localize(UiMessageKey.CommonCancel));
    if (!confirmed) return;
    var section = viewModel.Sections.FirstOrDefault(item => item.Id == member.Membership.HouseholdId);
    if (section is not null) await viewModel.RemoveMembershipAsync(section, member);
  }
}
