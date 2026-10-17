using Voucha.Client.Core.Api;
using Voucha.Client.Core.Posts;
using Xunit;

namespace Voucha.Client.Core.Tests.Posts;

public sealed partial class CommentThreadViewModelTests
{
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task AuthorBioBelongsOnlyToPublicRootAuthor(bool anonymous)
  {
    var root = NewPost("root", postType: "discussion") with { IsAnonymous = anonymous };
    var aside = new PostDetailAuthorAside("<p>Biography</p>", false, [], "ar");
    var service = new RecordingPostsService
    {
      RootResponse = new PostResponse(root, AuthorAside: aside),
      DescendantsResponse = MakeThreadResponse(),
    };
    var model = new CommentThreadViewModel(service, root.Id);
    Assert.Null(model.AuthorAside);
    await model.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Equal(anonymous ? null : aside, model.AuthorAside);
    var withoutBio = new CommentThreadViewModel(new RecordingPostsService
    {
      RootResponse = new PostResponse(root),
      DescendantsResponse = MakeThreadResponse(),
    }, root.Id);
    await withoutBio.LoadAsync(TestContext.Current.CancellationToken);
    Assert.Null(withoutBio.AuthorAside);
  }
}
