using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Navigation;

public static class PostBrowseRoute
{
  public static FetchPostsRequest? FromMatch(NativeRouteMatch? match) => match?.Path switch
  {
    "/posts" => new(),
    "/reviews" => new(PostTypes: "review"),
    "/discussions" => new(PostTypes: "discussion"),
    "/data-points" => new(PostTypes: "data_point"),
    "/articles" => new(PostTypes: "article"),
    "/blog" => new(PostTypes: "blog_post"),
    "/links" => new(PostTypes: "link"),
    _ => null,
  };
}
