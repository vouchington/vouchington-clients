using Voucha.Client.Core.Settings;
using Xunit;

namespace Voucha.Client.Core.Tests.Settings;

public sealed partial class SettingsViewModelTests
{
  [Fact]
  public void OptionListsExposeCompiledBindingChoices()
  {
    var viewModel = new SettingsViewModel(new FakeSettingsService());

    Assert.Contains(viewModel.DisplayNameSourceOptions, option => option.ProtocolValue == "github");
    Assert.Contains(viewModel.ProfileLinkTypeOptions, option => option.ProtocolValue == "twitter");
    Assert.Equal(["rss", "mcp"], viewModel.ApiKeyTypeOptions.Select(option => option.ProtocolValue));
  }

  [Fact]
  public async Task LoadAsyncPopulatesTheSettingsSurface()
  {
    var service = new FakeSettingsService();
    var viewModel = new SettingsViewModel(service);

    await viewModel.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal("alice", viewModel.Username);
    Assert.Equal("github", viewModel.DisplayNameSource);
    Assert.Equal("image-1", viewModel.ProfileImageId);
    Assert.Equal("alice · alice@example.com", viewModel.IdentitySummary);
    Assert.Equal("14 characters", viewModel.ProfileSummary);
    Assert.Equal("Current plan: Pro · Active", viewModel.MembershipSummary);
    Assert.Equal(SettingsViewModelTestSupport.DataRequestStatusText("Ready"), viewModel.LocalizedDataRequestStatus);
    Assert.Single(viewModel.ApiKeys);
    Assert.Single(viewModel.Sessions);
    Assert.Single(viewModel.ProfileLinks);
    Assert.Equal(3, viewModel.MembershipPlanOptions.Count);
    Assert.Single(viewModel.PushSubscriptions);
    Assert.Equal(13, viewModel.PrivacySelections.Count);
    Assert.Equal(3, viewModel.PrivacyToggles.Count);
    Assert.Equal("en", viewModel.PrivacySelections.Single(item => item.Key == "ui_locale").Value);
    Assert.False(viewModel.IsLoading);
    Assert.Null(viewModel.ErrorMessage);
    Assert.Equal("user-1", service.LastFetchedUserIdOrSlug);

    var free = viewModel.MembershipPlanOptions.Single(item => item.Slug == "free");
    Assert.Equal("USD\u00A00.00", free.PriceOptions.Single().Label);
    Assert.Equal("free tier", free.PriceOptions.Single().Interval);
    Assert.Equal("Available", free.StateLabel);
    Assert.Equal("Native billing pending", free.PurchaseButtonLabel);
    Assert.Contains("Publish public contributions: After the new-member wait", free.FeatureBullets);

    var plus = viewModel.MembershipPlanOptions.Single(item => item.Slug == "plus");
    Assert.Contains("Contribution capacity: More", plus.FeatureBullets);
    Assert.Contains("Automatic post topics: More", plus.FeatureBullets);
    Assert.Contains("Post downvote counts: Included", plus.FeatureBullets);
    Assert.Contains("Support service level: Priority", plus.FeatureBullets);

    var pro = viewModel.MembershipPlanOptions.Single(item => item.Slug == "pro");
    Assert.True(pro.IsCurrentPlan);
    Assert.Equal(
        ["USD\u00A015.00 monthly", "USD\u00A0150.00 yearly"],
        pro.PriceOptions.Select(item => item.Label));
    Assert.Equal("Current plan", pro.StateLabel);
    Assert.Contains("AI moderation rules: 10", pro.FeatureBullets);
    Assert.Contains("Support service level: Highest priority", pro.FeatureBullets);
    Assert.DoesNotContain(
        viewModel.MembershipPlanOptions.SelectMany(option => option.FeatureBullets),
        feature => feature.Contains("vote weight", StringComparison.OrdinalIgnoreCase));
    Assert.Equal(
        "Native purchase and billing management are not available yet.",
        viewModel.MembershipActionNotice);
  }

}
