using Voucha.Client.Core.Communities;
using Xunit;

namespace Voucha.Client.Core.Tests.Communities;

public sealed class CommunityRouteSectionsTests
{
  [Theory]
  [InlineData("/communities/voucha", CommunityDetailSurfaceSection.Overview)]
  [InlineData("/communities/voucha/posts", CommunityDetailSurfaceSection.Posts)]
  [InlineData("/communities/voucha/news/topics", CommunityDetailSurfaceSection.News)]
  [InlineData("/communities/voucha/lists/topics", CommunityDetailSurfaceSection.Lists)]
  [InlineData("/communities/voucha/playlists", CommunityDetailSurfaceSection.Overview)]
  [InlineData("/communities/voucha/members", CommunityDetailSurfaceSection.Members)]
  [InlineData("/communities/voucha/settings", CommunityDetailSurfaceSection.Settings)]
  [InlineData("/communities/voucha/settings/pinned-posts", CommunityDetailSurfaceSection.PinnedPosts)]
  [InlineData("/communities/voucha/settings/applications", CommunityDetailSurfaceSection.Applications)]
  [InlineData("/communities/voucha/settings/invites", CommunityDetailSurfaceSection.Invites)]
  [InlineData("/communities/voucha/settings/moderation", CommunityDetailSurfaceSection.Moderation)]
  [InlineData("/communities/voucha/settings/moderation/analytics", CommunityDetailSurfaceSection.ModerationAnalytics)]
  [InlineData("/communities/voucha/settings/moderation/modmail", CommunityDetailSurfaceSection.Modmail)]
  [InlineData("/communities/voucha/settings/moderation/modmail/thread-1", CommunityDetailSurfaceSection.Modmail)]
  [InlineData("/communities/voucha/settings/modlog", CommunityDetailSurfaceSection.Modlog)]
  [InlineData("/communities/voucha/pinned-posts", CommunityDetailSurfaceSection.PinnedPosts)]
  [InlineData("/communities/voucha/applications", CommunityDetailSurfaceSection.Applications)]
  [InlineData("/communities/voucha/invites", CommunityDetailSurfaceSection.Invites)]
  [InlineData("/communities/voucha/bans", CommunityDetailSurfaceSection.Bans)]
  [InlineData("/communities/voucha/restrictions", CommunityDetailSurfaceSection.Restrictions)]
  [InlineData("/communities/voucha/moderator-vacation", CommunityDetailSurfaceSection.ModeratorVacation)]
  [InlineData("/communities/voucha/ai-agents", CommunityDetailSurfaceSection.AiAgents)]
  [InlineData("/communities/voucha/agent-prompts", CommunityDetailSurfaceSection.AgentPrompts)]
  [InlineData("/communities/voucha/moderation-queue", CommunityDetailSurfaceSection.Moderation)]
  [InlineData("/communities/voucha/moderator-stats", CommunityDetailSurfaceSection.Moderation)]
  [InlineData("/communities/voucha/automod/simulate", CommunityDetailSurfaceSection.Moderation)]
  [InlineData("/communities/posts", CommunityDetailSurfaceSection.Overview)]
  [InlineData("/communities/news", CommunityDetailSurfaceSection.Overview)]
  [InlineData("/communities/voucha/modlog", CommunityDetailSurfaceSection.Modlog)]
  [InlineData("/communities/voucha/modmail", CommunityDetailSurfaceSection.Modmail)]
  [InlineData("/communities/voucha/modmail/thread-1", CommunityDetailSurfaceSection.Modmail)]
  public void ForPathMapsCommunityRoutesToNativeSections(string path, CommunityDetailSurfaceSection expected)
  {
    Assert.Equal(expected, CommunityRouteSections.ForPath(path));
  }
}
