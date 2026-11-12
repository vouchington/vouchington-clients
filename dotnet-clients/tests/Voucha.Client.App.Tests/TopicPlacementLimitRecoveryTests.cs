using System.Net;
using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using Voucha.Client.App.Pages;
using Voucha.Client.App.Controls;
using Voucha.Client.App.Support;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Images;
using Voucha.Client.Core.Tests.Api;
using Voucha.Client.Core.Topics;
using Xunit;

namespace Voucha.Client.App.Tests;

[Collection(MauiPageTestCollection.Name)]
public sealed class TopicPlacementLimitRecoveryTests
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task FailedImageSaveKeepsTopicDraftAndAllowsRetry(bool replaceImage)
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
    var service = DispatchProxy.Create<ITopicsService, PlacementTopicService>();
    var proxy = (PlacementTopicService)service;
    using var http = new HttpClient { BaseAddress = new Uri("https://api.test") };
    var page = new TopicManagementPage(service,
        DispatchProxy.Create<IImageUploadService, TopicImagePlacementPageTests.UnusedService>(),
        new VouchaApiClient(http), "topic-1");
    page.FindByName<Entry>("NameEntry").Text = "Draft name";
    page.FindByName<Entry>("SlugEntry").Text = "draft";
    page.FindByName<Entry>("TypeEntry").Text = "topic";
    page.FindByName<Entry>("LogoImageEntry").Text = "limited-logo";
    page.FindByName<Entry>("HeroImageEntry").Text = "limited-hero";
    page.FindByName<NativeMarkdownEditorView>("MarkdownEditor").Markdown = "Draft body";

    await page.SaveTopicAsync();
    Assert.Equal("This image has reached the placement limit. Choose another image or remove an existing use and retry.",
        page.FindByName<Label>("StatusLabel").Text);
    Assert.Equal("Draft name", page.FindByName<Entry>("NameEntry").Text);
    Assert.Equal("Draft body", page.FindByName<NativeMarkdownEditorView>("MarkdownEditor").Markdown);
    Assert.Equal("limited-logo", page.FindByName<Entry>("LogoImageEntry").Text);
    Assert.Equal("limited-hero", page.FindByName<Entry>("HeroImageEntry").Text);
    Assert.True(page.FindByName<Button>("SaveButton").IsEnabled);

    proxy.Fail = false;
    if (replaceImage)
    {
      page.FindByName<Entry>("LogoImageEntry").Text = "replacement-logo";
      page.FindByName<Entry>("HeroImageEntry").Text = "replacement-hero";
    }
    await page.SaveTopicAsync();
    Assert.Equal(2, proxy.Updates);
    Assert.Equal(replaceImage ? "replacement-logo" : "limited-logo", proxy.LastBody!.LogoImageId?.Value);
    Assert.Equal(replaceImage ? "replacement-hero" : "limited-hero", proxy.LastBody.HeroImageId?.Value);
    Assert.Equal("Saved topic-1", page.FindByName<Label>("StatusLabel").Text);
  }

  public class PlacementTopicService : DispatchProxy
  {
    public bool Fail { get; set; } = true;
    public int Updates { get; private set; }
    public UpdateTopicBody? LastBody { get; private set; }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
      switch (targetMethod?.Name)
      {
        case nameof(ITopicsService.UpdateTopicAsync):
          Updates++;
          LastBody = (UpdateTopicBody)args![1]!;
          if (Fail) return Task.FromException<TopicMutationResponse>(new VouchaApiException(
              HttpStatusCode.Conflict, File.ReadAllText(ContractPaths.ApiFixture(
                  "responses/native.posts.images.placement-limit.conflict.json"))));
          return Task.FromResult(new TopicMutationResponse(new Topic("topic-1", LastBody.Name!, LastBody.Slug!,
              LastBody.TopicType!, Markdown: LastBody.Markdown, LogoImageId: LastBody.LogoImageId?.Value,
              HeroImageId: LastBody.HeroImageId?.Value)));
        case nameof(ITopicsService.FetchTopicAliasesAsync):
          return Task.FromResult(new ListResponse<TopicAlias>([], new PageInfo(null, false, null)));
        case nameof(ITopicsService.FetchTopicAdditionalHostnamesAsync):
          return Task.FromResult(new ListResponse<TopicAdditionalHostname>([], new PageInfo(null, false, null)));
        case nameof(ITopicsService.FetchRssFeedsForTopicAsync):
          return Task.FromResult(new RssFeedsResponse([], new PageInfo(null, false, null)));
        default:
          throw new InvalidOperationException($"Unexpected call: {targetMethod?.Name}");
      }
    }
  }
}
