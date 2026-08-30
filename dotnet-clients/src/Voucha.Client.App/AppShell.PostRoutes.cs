using Voucha.Client.App.Pages;
using Voucha.Client.Core.Auth;
using Voucha.Client.Core.Navigation;
using Voucha.Client.Core.Posts;
using System.Text.Json;

namespace Voucha.Client.App;

public sealed partial class AppShell
{
  private Task? TryOpenPostRouteAsync(NativeDeepLinkResolution resolution)
  {
    if (resolution.DestinationId == NativeRouteDestinationId.PostCompose &&
        resolution.Match is { } composeMatch &&
        TryGetComposePostType(composeMatch, out var postType))
    {
      var page = serviceProvider.GetRequiredService<PostComposePage>();
      if (page.BindingContext is PostComposeViewModel viewModel)
      {
        viewModel.ResetDraft();
        viewModel.PostType = postType;
        viewModel.CommunitySlug = composeMatch.Param("slug") ?? composeMatch.QueryValue("community") ?? "";
        viewModel.RelatedLinkIdentifier = string.Join(",", QueryList(composeMatch.QueryValue("related_url_ids")));
        var topicId = composeMatch.QueryValue("topic_id") ?? "";
        switch (postType)
        {
          case PostComposeTypes.Review:
            viewModel.ReviewTopicId = topicId;
            break;
          case PostComposeTypes.DataPoint when !string.IsNullOrWhiteSpace(topicId):
            viewModel.StructuredDataJson = DataPointTopicSeedJson(topicId);
            break;
          case PostComposeTypes.Discussion:
            viewModel.DiscussionCategoryTopicId = topicId;
            viewModel.AddDiscussionCategoryTopic();
            break;
        }
      }

      return Navigation.PushAsync(page);
    }

    if (resolution.DestinationId != NativeRouteDestinationId.PostDetail ||
        resolution.Match?.Param("id", "idOrSlug") is not { } postId)
    {
      return null;
    }

    return OpenPostDetailAsync(postId, resolution.Match.Param("commentId"));
  }

  private Task OpenPostDetailAsync(string postId, string? commentId) =>
      Navigation.PushAsync(new PostDetailPage(
        serviceProvider.GetRequiredService<ICommentThreadService>(),
        serviceProvider.GetRequiredService<IPostsService>(),
        serviceProvider.GetRequiredService<ISessionStore>(),
        serviceProvider,
        postId,
        commentId));

  private static bool TryGetComposePostType(NativeRouteMatch match, out string postType)
  {
    postType = match.Path switch
    {
      "/discussions/create" => PostComposeTypes.Discussion,
      "/reviews/create" => PostComposeTypes.Review,
      "/data-points/create" => PostComposeTypes.DataPoint,
      "/links/create" => PostComposeTypes.Link,
      "/articles/create" => PostComposeTypes.Article,
      "/blog/create" => PostComposeTypes.Blog,
      _ when match.Path.StartsWith("/communities/", StringComparison.Ordinal) &&
          match.Path.EndsWith("/posts/create", StringComparison.Ordinal) => PostComposeTypes.Discussion,
      _ => "",
    };
    return postType.Length > 0;
  }

  private static IEnumerable<string> QueryList(string? value) =>
      (value ?? "")
          .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
          .Take(20);

  private static string DataPointTopicSeedJson(string topicId) =>
      JsonSerializer.Serialize(new { topic_ids = new[] { topicId } });
}
