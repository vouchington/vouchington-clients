using System.Reflection;
using System.Text.Json;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Copyright;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Tests.Api;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class CopyrightNoticesDetailPageTests
{
  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task DetailLinksOnlyTheCurrentPublicClaimant(bool hasClaimant)
  {
    PrepareMaui();
    var suffix = hasClaimant ? "populated" : "null-claimant";
    var detail = Fixture<CopyrightNoticeResponse>($"web.copyright.notice.detail.{suffix}");
    var originalParticipant = Fixture<CopyrightParticipantNoticeResponse>($"web.copyright.notice.participant.{suffix}");
    var participant = originalParticipant with
    {
      CopyrightNotice = originalParticipant.CopyrightNotice with
      {
        Timeline = [.. originalParticipant.CopyrightNotice.Timeline,
            originalParticipant.CopyrightNotice.Timeline[0] with { EventType = "future_event_type" }],
      },
    };
    var service = new CaseService
    {
      Detail = (_, _) => Task.FromResult(detail),
      Participant = (_, _) => Task.FromResult(participant),
    };
    var model = Model(service);
    using var locales = new UiLocaleController(new EnglishLanguages());
    NativeRoutePath? navigated = null;
    Uri? opened = null;
    using var page = new CopyrightNoticesPage(model, locales,
        path => { navigated = path; return Task.CompletedTask; }, detail.CopyrightNotice.Id,
        uri => { opened = uri; return Task.CompletedTask; });

    Appearing(page);
    await WaitUntilAsync(() => model.SelectedCase is not null);

    Assert.Equal(["participant", "detail"], service.Calls);
    Assert.Contains(Labels(page), label => label.Text == $"Case {detail.CopyrightNotice.Id}");
    Assert.Contains(Labels(page), label => label.Text == "Status: active");
    Assert.Contains(Labels(page), label => label.Text == "future event type");
    Assert.Contains(Labels(page), label => label.Text ==
        UiLocalization.English.Localize(UiMessageKey.NativeCopyrightNoticesCaseTimeline));
    Assert.Equal(hasClaimant, Labels(page).Any(label => label.Text ==
        UiLocalization.English.Localize(UiMessageKey.NativeCopyrightNoticesStatementsAndDecisions)));
    var claimantButtons = Buttons(page).Where(button =>
        button.AutomationId == $"copyright-claimant-{detail.CopyrightNotice.Id}").ToArray();
    if (hasClaimant)
    {
      var claimant = Assert.Single(claimantButtons);
      Assert.Equal("Current claimant", claimant.Text);
      claimant.SendClicked();
      Assert.Equal(NativeRoutePath.Entity("user", "00000000-0000-7000-8000-000000000806"), navigated);
      Assert.Contains(Labels(page), label => label.Text?.Contains("A person confirmed the image restriction.", StringComparison.Ordinal) == true);
    }
    else
    {
      Assert.Empty(claimantButtons);
      Assert.DoesNotContain(Labels(page), label => label.Text ==
          UiLocalization.English.Localize(UiMessageKey.NativeCopyrightNoticesClaimant));
    }
    Find<Button>(page, "copyright-target-00000000-0000-7000-8000-000000000805").SendClicked();
    Assert.Equal(new Uri("https://voucha.ai/posts/fixture"), opened);
  }

  [Fact]
  public async Task EuDetailKeepsClaimantAndTimelinePrivateWhileShowingStatementsAndRedress()
  {
    PrepareMaui();
    var fixture = Fixture<CopyrightParticipantNoticeResponse>("web.copyright.eu.participant.no-action-complaint");
    var originalEu = fixture.CopyrightNotice.Eu!;
    var decidedAt = DateTimeOffset.Parse("2026-02-03T04:05:00Z", System.Globalization.CultureInfo.InvariantCulture);
    var outcomeSettlement = new CopyrightEuDisputeSettlement(
        "settlement-with-outcome",
        "Outcome settlement body",
        decidedAt,
        new CopyrightEuDisputeOutcome("fully_implemented", decidedAt, null));
    var response = fixture with
    {
      CopyrightNotice = fixture.CopyrightNotice with
      {
        Claimant = new CopyrightPublicClaimant("private-user", "Must not appear"),
        Eu = originalEu with { DisputeSettlements = [.. originalEu.DisputeSettlements, outcomeSettlement] },
      },
    };
    var service = new CaseService
    {
      Participant = (_, _) => Task.FromResult(response),
      Detail = (_, _) => throw new InvalidOperationException("EU participant detail must not fetch public detail"),
    };
    var model = Model(service);
    using var locales = new UiLocaleController(new EnglishLanguages());
    using var page = new CopyrightNoticesPage(model, locales, _ => Task.CompletedTask, response.CopyrightNotice.Id);

    Appearing(page);
    await WaitUntilAsync(() => model.SelectedCase is not null);

    Assert.Equal(["participant"], service.Calls);
    Assert.DoesNotContain(Buttons(page), button => button.AutomationId?.StartsWith("copyright-claimant-", StringComparison.Ordinal) == true);
    Assert.DoesNotContain(Labels(page), label => label.Text == "Must not appear" || label.Text ==
        UiLocalization.English.Localize(UiMessageKey.NativeCopyrightNoticesCaseTimeline));
    Assert.Contains(Labels(page), label => label.Text ==
        UiLocalization.English.Localize(UiMessageKey.NativeCopyrightNoticesOtherRedressRoutes));
    Assert.Contains(Labels(page), label => label.Text ==
        UiLocalization.English.Localize(UiMessageKey.NativeCopyrightNoticesStatementsAndDecisions));
    Assert.Contains(Labels(page), label => label.Text?.Contains("We decided not to restrict the material", StringComparison.Ordinal) == true);
    Assert.Contains(Labels(page), label => label.Text == "Example certified dispute settlement body");
    Assert.Contains(Labels(page), label => label.Text?.StartsWith("Outcome: fully implemented.", StringComparison.Ordinal) == true);
    Assert.DoesNotContain(Labels(page), label => label.Text?.Contains("fully_implemented", StringComparison.Ordinal) == true);
  }

  [Fact]
  public async Task EuSettlementRetryRetainsRowsAndForwardsTheOpaqueCursor()
  {
    PrepareMaui();
    var fixture = Fixture<CopyrightParticipantNoticeResponse>("web.copyright.eu.participant.no-action-complaint");
    var eu = fixture.CopyrightNotice.Eu!;
    var response = fixture with
    {
      CopyrightNotice = fixture.CopyrightNotice with
      {
        Eu = eu with { DisputeSettlementsPageInfo = new PageInfo("opaque/+?=", true, null) },
      },
    };
    var second = eu.DisputeSettlements[0] with { Id = "settlement-2", BodyName = "Second certified body" };
    var attempts = 0;
    var service = new CaseService
    {
      Participant = (_, _) => Task.FromResult(response),
      Settlements = (_, _, _, _) => ++attempts == 1
          ? Task.FromException<CopyrightEuDisputeSettlementsResponse>(new HttpRequestException("offline"))
          : Task.FromResult(new CopyrightEuDisputeSettlementsResponse([second], new PageInfo(null, false, null))),
    };
    var model = Model(service);
    using var locales = new UiLocaleController(new EnglishLanguages());
    using var page = new CopyrightNoticesPage(model, locales, _ => Task.CompletedTask, response.CopyrightNotice.Id);

    Appearing(page);
    await WaitUntilAsync(() => Buttons(page).Any(button => button.AutomationId == "copyright-settlements-more"));
    Find<Button>(page, "copyright-settlements-more").SendClicked();
    await WaitUntilAsync(() => model.HasSettlementError && !model.IsLoadingSettlements);

    Assert.Contains(Labels(page), label => label.Text == "Example certified dispute settlement body");
    Assert.Equal(UiLocalization.English.Localize(UiMessageKey.NativeCommonRetry),
        Find<Button>(page, "copyright-settlements-more").Text);
    Find<Button>(page, "copyright-settlements-more").SendClicked();
    await WaitUntilAsync(() => Labels(page).Any(label => label.Text == "Second certified body"));

    Assert.Equal(["opaque/+?=", "opaque/+?="], service.Cursors);
    Assert.Contains(Labels(page), label => label.Text == "Example certified dispute settlement body");
    Assert.DoesNotContain(Buttons(page), button => button.AutomationId == "copyright-settlements-more");
    Assert.False(model.HasSettlementError);
  }

  [Fact]
  public async Task DisappearingCancelsHeldDetailAndReappearingLoadsItFresh()
  {
    PrepareMaui();
    var response = Fixture<CopyrightParticipantNoticeResponse>("web.copyright.notice.participant.populated");
    var detail = Fixture<CopyrightNoticeResponse>("web.copyright.notice.detail.populated");
    var held = new TaskCompletionSource<CopyrightParticipantNoticeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
    var participantCalls = 0;
    var service = new CaseService
    {
      Participant = (_, _) => ++participantCalls == 1 ? held.Task : Task.FromResult(response),
      Detail = (_, _) => Task.FromResult(detail),
    };
    var model = Model(service);
    using var locales = new UiLocaleController(new EnglishLanguages());
    using var page = new CopyrightNoticesPage(model, locales, _ => Task.CompletedTask, detail.CopyrightNotice.Id);

    var visible = true;
    try
    {
      Appearing(page);
      await WaitUntilAsync(() => participantCalls == 1 && model.IsLoading);
      Disappearing(page);
      visible = false;
      held.TrySetResult(response);
      await WaitUntilAsync(() => !model.IsLoading);
      Assert.Null(model.SelectedCase);
      Assert.DoesNotContain(Buttons(page), button => button.AutomationId?.StartsWith("copyright-claimant-", StringComparison.Ordinal) == true);

      Appearing(page);
      visible = true;
      await WaitUntilAsync(() => participantCalls == 2 &&
          Buttons(page).Any(button => button.AutomationId == $"copyright-claimant-{detail.CopyrightNotice.Id}"));
      Assert.Equal(["participant", "participant", "detail"], service.Calls);
      Assert.Equal("Current claimant", Find<Button>(page, $"copyright-claimant-{detail.CopyrightNotice.Id}").Text);
    }
    finally
    {
      if (visible) Disappearing(page);
      held.TrySetResult(response);
      await WaitUntilAsync(() => !model.IsLoading);
    }
  }

  private static CopyrightNoticesViewModel Model(CaseService service) => new(service, new NavigationViewer(true, []));

  private static T Fixture<T>(string id) => JsonSerializer.Deserialize<T>(
      File.ReadAllText(FilamentsContractPaths.ApiFixture($"responses/{id}.json")), VouchaApiJson.Options)!;

  private sealed class CaseService : ICopyrightNoticesService
  {
    public List<string> Calls { get; } = [];
    public List<string> Cursors { get; } = [];
    public Func<string, CancellationToken, Task<CopyrightNoticeResponse>> Detail { get; init; } =
        (_, _) => throw new NotSupportedException();
    public Func<string, CancellationToken, Task<CopyrightParticipantNoticeResponse>> Participant { get; init; } =
        (_, _) => throw new NotSupportedException();
    public Func<string, string, int, CancellationToken, Task<CopyrightEuDisputeSettlementsResponse>> Settlements { get; init; } =
        (_, _, _, _) => throw new NotSupportedException();
    public Task<CopyrightNoticesResponse> FetchPageAsync(string? after, int limit, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    public Task<CopyrightNoticeResponse> FetchDetailAsync(string id, CancellationToken cancellationToken)
    {
      Calls.Add("detail");
      return Detail(id, cancellationToken);
    }
    public Task<CopyrightParticipantNoticeResponse> FetchParticipantAsync(string id, CancellationToken cancellationToken)
    {
      Calls.Add("participant");
      return Participant(id, cancellationToken);
    }
    public Task<CopyrightEuDisputeSettlementsResponse> FetchSettlementsAsync(string id, string after, int limit, CancellationToken cancellationToken)
    {
      Cursors.Add(after);
      return Settlements(id, after, limit, cancellationToken);
    }
  }

  private static void PrepareMaui()
  {
    UiCopy.UseLocalization(UiLocalization.English);
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    var app = new Application();
    foreach (var key in UiMessageKey.All) app.Resources[key.Value] = UiLocalization.English.Localize(key);
  }
  private static void Appearing(Page page) =>
      typeof(CopyrightNoticesPage).GetMethod("OnAppearing",
          BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
          null, Type.EmptyTypes, null)!.Invoke(page, null);
  private static void Disappearing(Page page) =>
      typeof(CopyrightNoticesPage).GetMethod("OnDisappearing",
          BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
          null, Type.EmptyTypes, null)!.Invoke(page, null);
  private static T Find<T>(Element root, string id) where T : Button =>
      Assert.Single(Descendants<T>(root), element => element.AutomationId == id);
  private static IEnumerable<Button> Buttons(Element root) => Descendants<Button>(root);
  private static IEnumerable<Label> Labels(Element root) => Descendants<Label>(root);
  private static IEnumerable<T> Descendants<T>(Element root) where T : Element
  {
    foreach (var child in ((IVisualTreeElement)root).GetVisualChildren().OfType<Element>())
    {
      if (child is T match) yield return match;
      foreach (var descendant in Descendants<T>(child)) yield return descendant;
    }
  }
  private static async Task WaitUntilAsync(Func<bool> condition)
  {
    for (var attempt = 0; attempt < 100 && !condition(); attempt++)
      await Task.Delay(10, TestContext.Current.CancellationToken);
    Assert.True(condition());
  }
  private sealed class EnglishLanguages : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages => ["en"];
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
}
