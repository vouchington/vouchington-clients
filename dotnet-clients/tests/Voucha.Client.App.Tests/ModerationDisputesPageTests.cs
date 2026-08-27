using System.Text.Json;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Moderation;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Support;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class ModerationDisputesPageTests
{
  [Fact]
  public async Task StaffPageRendersImmutableClaimSeparateDraftsAndLifecycleActions()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var viewModel = new ModerationDisputesViewModel(
        new Service(),
        new NavigationViewer(true, ["moderator"]));
    var page = new ModerationDisputesPage(viewModel);

    await page.ReloadAsync();

    var list = Find<CollectionView>(page, "review-disputes-list");
    var card = Assert.IsAssignableFrom<VerticalStackLayout>(list.ItemTemplate.CreateContent());
    card.BindingContext = viewModel.Disputes.Single();
    Assert.Contains(
        card.Children.OfType<Label>(),
        label => label.Text?.Contains("The review is inaccurate", StringComparison.Ordinal) == true);
    Assert.Contains(
        card.Children.OfType<Label>(),
        label => label.Text?.Contains("4", StringComparison.Ordinal) == true);
    Assert.Equal(2, card.Children.OfType<Editor>().Count());
    Assert.Equal(
        ModerationDisputesViewModel.AnnotationBodyMaximumLength,
        card.Children.OfType<Editor>().Last().MaxLength);
    Assert.True(card.GetVisualTreeDescendants().OfType<Button>().Count() >= 7);
    Assert.DoesNotContain(card.Children.OfType<Editor>(), editor =>
        editor.Text?.Contains("The review is inaccurate", StringComparison.Ordinal) == true);
  }

  [Theory]
  [InlineData("no_action", "No action")]
  [InlineData("remove", "Remove")]
  [InlineData("annotate", "Annotate")]
  [InlineData("dismiss", "Dismiss")]
  public void ProtocolActionsUseLocalizedLabels(string action, string expected)
  {
    var dispute = new Service().Dispute with
    {
      RecommendedAction = action,
      ResolutionAction = action == "no_action" ? null : action,
      ResolvedAt = DateTimeOffset.UtcNow,
    };
    Assert.Contains(expected, ModerationDisputeCardPresentation.Recommendation(dispute));
    if (action == "no_action") return;
    Assert.Contains(expected, ModerationDisputeCardPresentation.Lifecycle(dispute));
    Assert.DoesNotContain(
        $": {action}",
        ModerationDisputeCardPresentation.Lifecycle(dispute),
        StringComparison.Ordinal);
  }

  [Fact]
  public async Task DisappearingPageCancelsInFlightCardMutation()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var service = new Service { BlockUpdates = true };
    var viewModel = new ModerationDisputesViewModel(
        service,
        new NavigationViewer(true, ["moderator"]));
    var page = new ModerationDisputesPage(viewModel);
    await page.ReloadAsync();
    var list = Find<CollectionView>(page, "review-disputes-list");
    var card = Assert.IsAssignableFrom<VerticalStackLayout>(list.ItemTemplate.CreateContent());
    card.BindingContext = viewModel.Disputes.Single();
    card.Children.OfType<Editor>().First().Text = "Updated response";

    Find<Button>(card, "review-dispute-approve").SendClicked();
    await service.UpdateStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    page.CancelLifecycleOperations();

    await service.CancellationObserved.Task.WaitAsync(TestContext.Current.CancellationToken);
  }

  [Fact]
  public void MemberRoleGetsDedicatedDenialWithoutStaffControls()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var page = new ModerationDisputesPage(new ModerationDisputesViewModel(
        new Service(),
        new NavigationViewer(true, ["member"])));

    Assert.NotNull(Find<Label>(page, "review-disputes-role-denied"));
    Assert.Empty(page.GetVisualTreeDescendants().OfType<Editor>());
    Assert.Empty(page.GetVisualTreeDescendants().OfType<Button>());
  }

  [Fact]
  public async Task ReloadWaitsForCanceledLoadBeforeStartingItsReplacement()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var service = new Service { BlockFirstLoad = true };
    var page = new ModerationDisputesPage(new ModerationDisputesViewModel(
        service,
        new NavigationViewer(true, ["moderator"])));

    var first = page.ReloadAsync();
    await service.LoadStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
    var replacement = page.ReloadAsync();

    await Task.WhenAll(first, replacement);
    Assert.Equal(2, service.FetchCount);
    Assert.Equal(
        LoadState.Loaded,
        Assert.IsType<ModerationDisputesViewModel>(page.BindingContext).State);
  }

  [Fact]
  public async Task LocaleChangeRefreshesStatusOptionsAndEmptyCopy()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    using var controller = new UiLocaleController(new Languages());
    using var localization = UiCopy.PushLocalization(new UiLocalization(controller));
    using var viewModel = new ModerationDisputesViewModel(
        new Service { Empty = true },
        new NavigationViewer(true, ["moderator"]),
        controller);
    var page = new ModerationDisputesPage(viewModel);
    await page.ReloadAsync();
    var picker = Find<Picker>(page, "review-disputes-status");
    var empty = Find<Label>(page, "review-disputes-empty");
    var englishOptions = picker.ItemsSource.Cast<string>().ToArray();
    var englishEmpty = empty.Text;

    controller.ApplySavedLocale("es");

    Assert.NotEqual(englishOptions, picker.ItemsSource.Cast<string>().ToArray());
    Assert.NotEqual(englishEmpty, empty.Text);
    Assert.Equal(
        new UiLocalization(controller).Localize(
            UiMessageKey.NativeSwiftReviewDisputesNoPending),
        empty.Text);
  }

  [Fact]
  public void ReplacingThePageDisposesItsViewModelAndSubscriptions()
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    using var controller = new UiLocaleController(new Languages());
    using var localization = UiCopy.PushLocalization(new UiLocalization(controller));
    var page = new ModerationDisputesPage(new ModerationDisputesViewModel(
        new Service(),
        new NavigationViewer(true, ["moderator"]),
        controller));
    var picker = Find<Picker>(page, "review-disputes-status");
    var englishOptions = picker.ItemsSource.Cast<string>().ToArray();
    var content = new ShellContent { Content = page };

    ModerationDisputesPageLifetime.Replace(content, page);
    controller.ApplySavedLocale("es");
    var spanishOptions = picker.ItemsSource.Cast<string>().ToArray();
    ModerationDisputesPageLifetime.Replace(content, new ContentPage());
    page.Dispose();
    controller.ApplySavedLocale("fr");

    Assert.NotEqual(englishOptions, spanishOptions);
    Assert.Equal(spanishOptions, picker.ItemsSource.Cast<string>().ToArray());
  }

  private static T Find<T>(Element root, string id) where T : Element =>
      Assert.Single(
          root.GetVisualTreeDescendants().OfType<T>(),
          item => item.AutomationId == id);

  private sealed class Service : IModerationDisputesService
  {
    public bool BlockUpdates { get; init; }
    public bool BlockFirstLoad { get; init; }
    public bool Empty { get; init; }
    public int FetchCount { get; private set; }
    public TaskCompletionSource LoadStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource UpdateStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource CancellationObserved { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ModerationDispute Dispute => Current;

    private ModerationDispute Current { get; set; } = JsonSerializer.Deserialize<ModerationDispute>(
        """
        {
          "id":"dispute-1",
          "post_id":"post-1",
          "status":"pending",
          "reason":"incorrect",
          "claim_text":"The review is inaccurate",
          "topic_id":"topic-1",
          "disputant_user_id":"user-1",
          "recommended_action":"annotate",
          "created_at":"2026-07-01T00:00:00Z",
          "updated_at":"2026-07-02T00:00:00Z",
          "ai_public_response":"Draft response",
          "ai_internal_response":"Internal analysis",
          "internal_notes":"Staff note",
          "staff_context":{
            "disputant":{"id":"user-1","username":"member","verified_display_name":null,"profile_image_id":null},
            "review":{
              "post":{"id":"post-1","title":"Card review","slug":"card-review","markdown_preview":"Preview","created_by_id":"user-1","created_at":"2026-06-01T00:00:00Z"},
              "topic":{"id":"topic-1","name":"Travel","slug":"travel","topic_type":"category"},
              "rating":4
            }
          }
        }
        """,
        VouchaApiJson.Options)!;

    public async Task<ModerationDisputeListResponse> FetchDisputesAsync(
        ModerationDisputeStatus status,
        string? after = null,
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
      FetchCount++;
      if (BlockFirstLoad && FetchCount == 1)
      {
        LoadStarted.SetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
      }
      return new ModerationDisputeListResponse(
          Empty ? [] : [Current with { Status = status }],
          new PageInfo(null, false, null));
    }

    public Task<ModerationDisputeResponse> FetchDisputeAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ModerationDisputeResponse(Current));

    public async Task<ModerationDisputeResponse> UpdatePublicResponseAsync(
        string id,
        string publicResponse,
        CancellationToken cancellationToken = default) =>
        BlockUpdates
            ? await BlockUpdateAsync(cancellationToken)
            : new ModerationDisputeResponse(
                Current = Current with { PublicResponse = publicResponse });

    public Task<ModerationDisputeResponse> ApproveAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ModerationDisputeResponse(
            Current = Current with { ApprovedAt = DateTimeOffset.UtcNow }));

    public Task<ModerationDisputeResponse> DeliverAsync(
        string id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ModerationDisputeResponse(
            Current = Current with { SentAt = DateTimeOffset.UtcNow }));

    public Task<ModerationDisputeResponse> ResolveAsync(
        string id,
        ModerationDisputeResolutionAction action,
        string? annotation,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ModerationDisputeResponse(
            Current = Current with { Status = ModerationDisputeStatus.Resolved }));

    public Task<ModerationDisputeQueueResponse> RerunResolutionDraftAsync(
        string id,
        CancellationToken cancellationToken = default) =>
      Task.FromResult(new ModerationDisputeQueueResponse(true, "staff-1"));

    private async Task<ModerationDisputeResponse> BlockUpdateAsync(
        CancellationToken cancellationToken)
    {
      using var registration = cancellationToken.Register(
          CancellationObserved.SetResult);
      UpdateStarted.SetResult();
      await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
      return new ModerationDisputeResponse(Current);
    }
  }

  private sealed class ImmediateDispatcherProvider : IDispatcherProvider
  {
    public IDispatcher GetForCurrentThread() => ImmediateDispatcher.Instance;
  }

  private sealed class ImmediateDispatcher : IDispatcher
  {
    public static ImmediateDispatcher Instance { get; } = new();
    public bool IsDispatchRequired => false;
    public bool Dispatch(Action action) { action(); return true; }
    public bool DispatchDelayed(TimeSpan delay, Action action) { action(); return true; }
    public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
  }

  private sealed class Languages : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages => ["en"];
  }
}
