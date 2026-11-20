using Xunit;
using static Voucha.Client.Core.Tests.Notifications.NotificationRenderSupport;

namespace Voucha.Client.Core.Tests.Notifications;

public sealed class NotificationPostRenderTests
{
  private const string Actors = "\"actors\":{\"u1\":{\"id\":\"u1\",\"username\":\"alice\"},\"u2\":{\"id\":\"u2\",\"username\":\"bob\"}}";

  private static string Posts(string type, string? actor = "u1") =>
      $"\"posts\":{{\"p1\":{{\"id\":\"p1\",\"post_type\":\"{type}\",\"snippet\":\"Hello world\",\"actor_user_id\":{(actor is null ? "null" : $"\"{actor}\"")}}}}},{Actors}";

  [Theory]
  [InlineData("review", "New review from @alice")]
  [InlineData("discussion", "New discussion from @alice")]
  [InlineData("data_point", "New data point from @alice")]
  [InlineData("link", "New link from @alice")]
  [InlineData("poll", "New post from @alice")]
  public void PostNewTitlesFollowPostType(string type, string title)
  {
    Assert.Equal((title, "Hello world"), Render("post_new", "\"post_id\":\"p1\"", Posts(type)));
  }

  [Fact]
  public void PostNewIsAnonymousWhenActorIsHiddenAndTitleOnlyWhenPostIsGone()
  {
    Assert.Equal(("New discussion", "Hello world"), Render("post_new", "\"post_id\":\"p1\"", Posts("discussion", null)));
    Assert.Equal(("New post", ""), Render("post_new", "\"post_id\":\"gone\""));
    Assert.Equal(("New post", ""), Render("post_new"));
  }

  [Theory]
  [InlineData("review", "@bob sent you a review")]
  [InlineData("discussion", "@bob sent you a discussion")]
  [InlineData("data_point", "@bob sent you a data point")]
  [InlineData("link", "@bob sent you a link")]
  [InlineData("other", "@bob sent you a post")]
  public void PostManualSendNamesTheSender(string type, string title)
  {
    Assert.Equal(
        (title, "Hello world"),
        Render("post_manual_send", "\"post_id\":\"p1\",\"sent_by_id\":\"u2\"", Posts(type)));
  }

  [Fact]
  public void PostManualSendIsAnonymousWithoutSender()
  {
    Assert.Equal(
        ("Someone sent you a review", "Hello world"),
        Render("post_manual_send", "\"post_id\":\"p1\"", Posts("review")));
  }

  [Fact]
  public void PostReplyNamesTheReplierOrIsAnonymous()
  {
    Assert.Equal(("New reply from @alice", "Hello world"), Render("post_reply", "\"post_id\":\"p1\"", Posts("discussion")));
    Assert.Equal(("New reply", "Hello world"), Render("post_reply", "\"post_id\":\"p1\"", Posts("discussion", null)));
    Assert.Equal(("New reply", ""), Render("post_reply"));
  }

  [Fact]
  public void PostRemovedIsTitleOnly()
  {
    Assert.Equal(("Your post has been removed", ""), Render("post_removed"));
  }

  [Fact]
  public void RssItemNewUsesFeedNameWithFallback()
  {
    const string Feeds = "\"rss_feed_items\":{\"r1\":{\"id\":\"r1\",\"feed_name\":\"Daily Feed\",\"snippet\":\"Item text\"}}";
    Assert.Equal(("New item from Daily Feed", "Item text"), Render("rss_item_new", "\"rss_feed_item_id\":\"r1\"", Feeds));
    Assert.Equal(("New item from RSS Feed", ""), Render("rss_item_new", "\"rss_feed_item_id\":\"gone\""));
    Assert.Equal(("New item from RSS Feed", ""), Render("rss_item_new"));
  }

  [Fact]
  public void RssItemManualSendNamesTheSenderOrIsAnonymous()
  {
    const string Feeds = "\"rss_feed_items\":{\"r1\":{\"id\":\"r1\",\"feed_name\":\"Daily Feed\",\"snippet\":\"Item text\"}}," + Actors;
    Assert.Equal(
        ("@bob sent you an article", "Item text"),
        Render("rss_item_manual_send", "\"rss_feed_item_id\":\"r1\",\"sent_by_id\":\"u2\"", Feeds));
    Assert.Equal(("Someone sent you an article", ""), Render("rss_item_manual_send"));
  }

  [Fact]
  public void LocalizedTitlesFollowTheLocale()
  {
    Assert.NotEqual(
        Render("post_removed").Title,
        Render("post_removed", locale: "es").Title);
  }
}
