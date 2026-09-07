using Voucha.Client.Core.Api;
using Voucha.Client.Core.Search;
using Xunit;

namespace Voucha.Client.Core.Tests.Search;

public sealed partial class OmnisearchViewModelTests
{
  [Fact]
  public void MapGroupsUsesLanguageOnlyForNormalizedAuthoredTitles()
  {
    var groups = OmnisearchViewModel.MapGroups(new CombinedSearchResponse(
        Topics: null,
        Posts:
        [
          new("post-1", "discussion", "System title", "  عنوان  ", "ar"),
          new("post-2", "discussion", "System fallback", "  ", "ar"),
        ],
        News: null,
        Domains: null,
        Communities: null));

    var rows = Assert.Single(groups).Rows;
    Assert.Equal("عنوان", rows[0].Title);
    Assert.Equal("RightToLeft", rows[0].TitleFlowDirection);
    Assert.Equal("System fallback", rows[1].Title);
    Assert.Null(rows[1].TitleFlowDirection);
  }
}
