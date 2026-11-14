using System.Reflection;
using System.Text.Json;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.Core;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Tests.Api;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class TopicManagementPageMergeTests
{
  [Theory]
  [InlineData("queued", nameof(UiMessageKey.SettingsTopicsMergeQueued), true)]
  [InlineData("running", nameof(UiMessageKey.SettingsTopicsMergeQueued), true)]
  [InlineData("completed", nameof(UiMessageKey.SettingsTopicsMergeCompleted), false)]
  [InlineData("failed", nameof(UiMessageKey.SettingsTopicsMergeFailed), false)]
  public async Task CheckStatusMapsEachFixtureState(string state, string keyName, bool canCheckAgain)
  {
    var service = ServiceProxy.Create(Fixture("native.topics.merge.accepted"), Fixture($"native.topics.merge.status.{state}"));
    var page = CreatePage(service.Proxy);
    page.FindByName<Entry>("MergeDestinationEntry").Text = "destination";

    await page.MergeAsync();

    var status = page.FindByName<Label>("StatusLabel");
    var check = page.FindByName<Button>("CheckMergeStatusButton");
    Assert.Equal(UiCopy.Localize(UiMessageKey.SettingsTopicsMergeQueued), status.Text);
    Assert.True(check.IsVisible);
    Assert.False(page.FindByName<Button>("MergeButton").IsEnabled);
    Assert.Equal(0, service.StatusReads);

    await page.CheckMergeStatusAsync();

    Assert.Equal(UiCopy.Localize(Key(keyName)), status.Text);
    Assert.Equal(canCheckAgain, check.IsVisible);
    var operation = Fixture($"native.topics.merge.status.{state}").TopicMerge;
    Assert.Equal(state, page.FindByName<Label>("MergeStatusLabel").Text);
    Assert.Equal(operation.Stage, page.FindByName<Label>("MergeStageLabel").Text);
    Assert.Equal(operation.ErrorCode ?? string.Empty, page.FindByName<Label>("MergeErrorLabel").Text);
    Assert.Equal(operation.CompletedAt is { } completed ? UiCopy.FormatDateTime(completed) : string.Empty,
        page.FindByName<Label>("MergeCompletedLabel").Text);
    Assert.Equal(1, service.StatusReads);
    Assert.Equal(("70000000-0000-7000-8000-000000005170", "70000000-0000-7000-8000-000000005172"), service.LastStatusRequest);
  }

  [Fact]
  public async Task StatusReadFailureShowsStatusFailedAndKeepsOperation()
  {
    var service = ServiceProxy.Create(Fixture("native.topics.merge.accepted"), statusFailure: new HttpRequestException("offline"));
    var page = CreatePage(service.Proxy);
    page.FindByName<Entry>("MergeDestinationEntry").Text = "destination";
    await page.MergeAsync();

    await page.CheckMergeStatusAsync();

    Assert.Equal(UiCopy.Localize(UiMessageKey.SettingsTopicsMergeStatusFailed), page.FindByName<Label>("StatusLabel").Text);
    Assert.True(page.FindByName<Button>("CheckMergeStatusButton").IsVisible);
    Assert.True(page.FindByName<Button>("CheckMergeStatusButton").IsEnabled);
  }

  [Fact]
  public async Task MergeRequestFailureShowsErrorWithoutOperation()
  {
    var service = ServiceProxy.Create(mergeFailure: new InvalidOperationException("rejected"));
    var page = CreatePage(service.Proxy);
    page.FindByName<Entry>("MergeDestinationEntry").Text = "destination";

    await page.MergeAsync();
    await page.CheckMergeStatusAsync();

    Assert.Equal("rejected", page.FindByName<Label>("StatusLabel").Text);
    Assert.False(page.FindByName<Button>("CheckMergeStatusButton").IsVisible);
    Assert.Equal(0, service.StatusReads);
  }

  [Fact]
  public async Task BlankDestinationDoesNotRequestMerge()
  {
    var service = ServiceProxy.Create(Fixture("native.topics.merge.accepted"));
    var page = CreatePage(service.Proxy);

    await page.MergeAsync();

    Assert.Equal(0, service.MergeRequests);
  }

  private static TopicMergeResponse Fixture(string id) =>
      JsonSerializer.Deserialize<TopicMergeResponse>(ApiFixtureLoader.LoadResponse(id), VouchaApiJson.Options)!;

  private static UiMessageKey Key(string name) =>
      (UiMessageKey)typeof(UiMessageKey).GetField(name)!.GetValue(null)!;

  private static TopicManagementPage CreatePage(ITopicsService topics)
  {
    DispatcherProvider.SetCurrent(new ImmediateDispatcherProvider());
    _ = new Application
    {
      Resources =
      {
        ["UiLocaleVersion"] = new Voucha.Client.App.UiLocaleVersion(
            new Voucha.Client.Core.Localization.UiLocaleController(new StubDeviceLanguageProvider("en"))),
        ["UiLocalizedValue"] = new Voucha.Client.App.UiLocalizedValueConverter(
            Voucha.Client.Core.Localization.UiLocalization.English),
        ["Metadata"] = new Style(typeof(Label)),
        ["Body"] = new Style(typeof(Label)),
      },
    };
    return new TopicManagementPage(
        topics,
        System.Reflection.DispatchProxy.Create<IImageUploadService, TopicImagePlacementPageTests.UnusedService>(),
        new VouchaApiClient(new HttpClient { BaseAddress = new Uri("https://api.test") }),
        "70000000-0000-7000-8000-000000005170");
  }

  public class ServiceProxy : DispatchProxy
  {
    private TopicMergeResponse? accepted;
    private TopicMergeResponse? status;
    private Exception? statusFailure;
    private Exception? mergeFailure;

    public ITopicsService Proxy { get; private set; } = null!;
    public int MergeRequests { get; private set; }
    public int StatusReads { get; private set; }
    public (string Source, string Operation)? LastStatusRequest { get; private set; }

    public static ServiceProxy Create(
        TopicMergeResponse? accepted = null,
        TopicMergeResponse? status = null,
        Exception? statusFailure = null,
        Exception? mergeFailure = null)
    {
      var proxy = DispatchProxy.Create<ITopicsService, ServiceProxy>();
      var self = (ServiceProxy)(object)proxy;
      self.Proxy = proxy;
      self.accepted = accepted;
      self.status = status;
      self.statusFailure = statusFailure;
      self.mergeFailure = mergeFailure;
      return self;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
      switch (targetMethod?.Name)
      {
        case "MergeTopicAliasesAsync":
          MergeRequests++;
          return mergeFailure is null
              ? Task.FromResult(accepted!)
              : Task.FromException<TopicMergeResponse>(mergeFailure);
        case "FetchTopicMergeStatusAsync":
          StatusReads++;
          LastStatusRequest = ((string)args![0]!, (string)args[1]!);
          return statusFailure is null
              ? Task.FromResult(status!)
              : Task.FromException<TopicMergeResponse>(statusFailure);
        default:
          throw new InvalidOperationException($"Unexpected call: {targetMethod?.Name}");
      }
    }
  }
}
