using Voucha.Client.Core.Tags;

namespace Voucha.Client.Core.Navigation;

public static partial class NativeRouteCatalog
{
  private static readonly string[] TopicSubpaths =
  [
    "/posts",
    "/discussions",
    "/reviews",
    "/data-points",
    "/referral-links",
    "/latest",
    "/news",
    "/articles",
    "/blog-posts",
    "/followers",
  ];

  private static readonly string[] PostTypes =
      TagManagementRoutes.PostTypes;

  private static readonly string[] TopicTypes =
      TagManagementRoutes.TopicTypes;

  private static readonly string[] PostDetailSubpaths =
  [
    "",
    "/comment/:commentId",
  ];

  private static readonly string[] ProfileSubpages =
  [
    "/posts",
    "/discussions",
    "/reviews",
    "/comments",
    "/topics/following",
    "/users/following",
    "/users/followers",
    "/rss-feeds/following",
    "/communities/member",
  ];

  private static readonly string[] PostCreatePaths =
  [
    "/reviews/create",
    "/discussions/create",
    "/links/create",
    "/data-points/create",
    "/articles/create",
    "/blog/create",
  ];

  private static readonly string[] SourceDetailSubpaths =
  [
    "",
    "/latest",
    "/news",
    "/posts",
    "/discussions",
    "/reviews",
    "/data-points",
    "/referral-links",
    "/articles",
    "/blog-posts",
    "/followers",
    "/crawls",
    "/crawls/:crawlId",
  ];

  private static readonly string[] TopicSettingsSubpaths =
  [
    "/settings",
    "/settings/**",
  ];

  private static readonly string[] TopicManagementSubpaths =
  [
    "/settings/about",
    "/settings/behavior",
    "/settings/domains",
    "/settings/source",
    "/settings/aliases",
    "/settings/merge",
  ];

  private static readonly string[] TopicCreatePaths = ["/topics/create"];

  private static readonly string[] UserProfileRootPattern = ["/user/:idOrUsername"];

  private static IEnumerable<string> TopicDetailPatterns() =>
      TopicTypes.SelectMany(topicType =>
          new[] { $"/{topicType}/:idOrSlug" }
              .Concat(TopicSubpaths.Select(subpath => $"/{topicType}/:idOrSlug{subpath}")));

  private static IEnumerable<string> PostDetailPatterns() =>
      PostTypes.SelectMany(postType =>
          PostDetailSubpaths.Select(subpath => $"/{postType}/:id{subpath}"));

  private static IEnumerable<string> PostEditPatterns() =>
      PostTypes.Select(postType => $"/{postType}/:id/edit");

  private static IEnumerable<string> SourceDetailPatterns() =>
      SourceDetailSubpaths.Select(subpath => $"/source/:idOrSlug{subpath}");

  private static IEnumerable<string> UserProfilePatterns() =>
      UserProfileRootPattern.Concat(ProfileSubpages.Select(subpath => $"/user/:idOrUsername{subpath}"));

  private static IEnumerable<string> ExcludedTopicSettingsPatterns() =>
      TopicTypes.SelectMany(topicType => TopicSettingsSubpaths.Select(subpath => $"/{topicType}/:idOrSlug{subpath}"));

  private static IEnumerable<string> TopicManagementPatterns() =>
      TopicCreatePaths.Concat(
          TopicTypes.SelectMany(topicType =>
              TopicManagementSubpaths.Select(subpath => $"/{topicType}/:idOrSlug{subpath}")));

  private static IEnumerable<string> TagManagementPatterns() =>
      PostTypes.SelectMany(postType => new[] { $"/{postType}/:id/tags/:objectType" })
          .Concat(TopicTypes.SelectMany(topicType => new[] { $"/{topicType}/:idOrSlug/tags/:objectType" }))
          .Concat(["/rss-feed-items/:id/tags/topic"]);
}
