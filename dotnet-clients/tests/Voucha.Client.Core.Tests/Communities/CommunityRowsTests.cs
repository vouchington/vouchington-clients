using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Localization;
using Xunit;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityRowsTests
{
  [Fact]
  public void CommunityMemberRowUsesNameUsernameAndUserIdFallbacks()
  {
    var member = new CommunityMember("member-1", "community-1", "user-1", "moderator");

    var named = CommunityMemberRow.FromMember(member, new PublicUser("user-1", "tester", Name: "Test User"));
    var username = CommunityMemberRow.FromMember(member, new PublicUser("user-1", "tester"));
    var fallback = CommunityMemberRow.FromMember(member, null);

    Assert.Equal("Test User", named.DisplayName);
    Assert.Equal("tester", username.DisplayName);
    Assert.Equal("user-1", fallback.DisplayName);
  }

  [Fact]
  public void CommunityPostRowUsesTitleSlugAndIdFallbacks()
  {
    var titled = CommunityPostRow.FromPost(
        new Post("post-1", "discussion", "Title", "Body", "user-1"),
        new PostMetrics(null, "post-1", new PostMetricCounts(1)));
    var slugged = CommunityPostRow.FromPost(
        new Post("post-2", null, null, "Body", "user-1", Slug: "slug-title"),
        null);
    var fallback = CommunityPostRow.FromPost(
        new Post("post-3", null, null, "Body", "user-1"),
        null);

    Assert.Equal("Title", titled.Title);
    Assert.Equal("discussion", titled.ProtocolPostType);
    Assert.Equal("Discussion", titled.PostType);
    Assert.Equal(1, titled.ReplyCount);
    Assert.Equal("slug-title", slugged.Title);
    Assert.Equal("post", slugged.ProtocolPostType);
    Assert.Equal("Post", slugged.PostType);
    Assert.Equal("post-3", fallback.Title);
  }

  [Fact]
  public void CommunityPostRowUsesTheServerProvidedLinkEmbed()
  {
    var row = CommunityPostRow.FromPost(
        new Post("post-1", "link", "Title", null, "user-1"),
        null,
        new UrlEmbed(Title: "Provider title", SourceUrl: "https://example.com/source"));

    Assert.Equal("Provider title", row.EmbedPreview?.Title);
    Assert.Equal(new Uri("https://example.com/source"), row.EmbedPreview?.SourceUrl);
  }

  [Fact]
  public void LocalizedManagementRowsResolveAgainAfterLocaleChanges()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    var invite = new CommunityInvite(
        Id: "invite-1",
        CommunityId: "community-1",
        Code: "code-1",
        InvitedUserId: null,
        InvitedEmail: null,
        InvitedById: "moderator-1",
        AcceptedAt: null,
        AcceptedByUserId: null,
        DeclinedAt: null,
        RevokedAt: null,
        CreatedAt: DateTimeOffset.UnixEpoch);
    var inviteRow = CommunityInviteRow.FromInvite(invite, localization);
    var summaryRow = new CommunitySummaryRow(
        "summary-1",
        UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesPinnedPost),
        UiText.Localized(UiMessageKey.NativeDotnetCsharpCommunitiesOrder, ("value", 2)),
        null,
        localization);

    Assert.Equal("Anyone with code", inviteRow.Recipient);
    Assert.Equal("Pinned post", summaryRow.Title);
    Assert.Equal("Order 2", summaryRow.Subtitle);

    controller.ApplySavedLocale("es");

    Assert.Equal("Cualquiera con el código", inviteRow.Recipient);
    Assert.Equal("Publicación fijada", summaryRow.Title);
    Assert.Equal("Orden 2", summaryRow.Subtitle);
  }

  [Fact]
  public void MemberRolesAndPostTypesResolveAgainAfterLocaleChanges()
  {
    var controller = new UiLocaleController(new StubDeviceLanguageProvider("en"));
    var localization = new UiLocalization(controller);
    var member = CommunityMemberRow.FromMember(
        new CommunityMember("member-1", "community-1", "user-1", "owner"),
        new PublicUser("user-1", "owner"),
        localization);
    var post = CommunityPostRow.FromPost(
        new Post("post-1", "discussion", "Title", null, "user-1"),
        null,
        localization);

    Assert.Equal("owner", member.ProtocolRole);
    Assert.Equal("Owner", member.Role);
    Assert.Equal("discussion", post.ProtocolPostType);
    Assert.Equal("Discussion", post.PostType);

    controller.ApplySavedLocale("fr");

    Assert.Equal("Propriétaire", member.Role);
    Assert.Equal("Discussion", post.PostType);
  }

  private sealed class StubDeviceLanguageProvider(string language) : IDeviceLanguageProvider
  {
    public IReadOnlyList<string> PreferredLanguages { get; } = [language];
  }
}
