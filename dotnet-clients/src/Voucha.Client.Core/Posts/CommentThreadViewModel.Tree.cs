using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Posts;

public sealed partial class CommentThreadViewModel
{
  private static CommentThreadNodeViewModel[] BuildThreadNodes(
      PostThreadResponse response,
      string sort) =>
      BuildSortedNodes(
          response.Results
              .Select(result => EntityId(result) is { } id && response.Posts.TryGetValue(id, out var post) ? post : null)
              .Where(post => post is not null)
              .Select(post => post!)
              .ToArray(),
          response.PostElections,
          sort);

  private static CommentThreadNodeViewModel[] BuildSortedNodes(
      IReadOnlyList<Post> posts,
      IReadOnlyDictionary<string, PostElection>? elections,
      string sort)
  {
    var nodes = new Dictionary<string, CommentThreadNodeViewModel>(StringComparer.Ordinal);
    var children = new Dictionary<string, List<Post>>(StringComparer.Ordinal);
    var roots = new List<Post>();
    var postIds = posts.Select(post => post.Id).ToHashSet(StringComparer.Ordinal);

    foreach (var post in posts)
    {
      if (post.ParentId is not null && postIds.Contains(post.ParentId))
      {
        if (!children.TryGetValue(post.ParentId, out var list))
        {
          list = [];
          children[post.ParentId] = list;
        }

        list.Add(post);
      }
      else
      {
        roots.Add(post);
      }
    }

    CommentThreadNodeViewModel BuildNode(Post post)
    {
      if (nodes.TryGetValue(post.Id, out var existing))
      {
        return existing;
      }

      var childPosts = children.TryGetValue(post.Id, out var childList)
          ? SortPosts(childList, elections, sort)
          : [];
      var node = new CommentThreadNodeViewModel(
          post,
          childPosts.Select(BuildNode).ToArray());
      nodes[post.Id] = node;
      return node;
    }

    return SortPosts(roots, elections, sort).Select(BuildNode).ToArray();
  }

  private static Post[] SortPosts(
      IReadOnlyList<Post> source,
      IReadOnlyDictionary<string, PostElection>? elections,
      string sort) =>
      source
          .OrderByDescending(post => SortValue(post, elections, sort))
          .ThenByDescending(post => post.CreatedAt ?? DateTimeOffset.MinValue)
          .ThenBy(post => post.Id, StringComparer.Ordinal)
          .ToArray();

  private static double SortValue(
      Post post,
      IReadOnlyDictionary<string, PostElection>? elections,
      string sort)
  {
    if (string.Equals(sort, CommentThreadSorts.New, StringComparison.Ordinal))
    {
      return post.CreatedAt?.ToUnixTimeMilliseconds() ?? long.MinValue;
    }

    return elections is not null && elections.TryGetValue(post.Id, out var election)
        ? election.VotesScoreNet
        : 0d;
  }
}
