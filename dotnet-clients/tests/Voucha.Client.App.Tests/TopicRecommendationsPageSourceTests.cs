using System.Runtime.CompilerServices;
using Xunit;

namespace Voucha.Client.App.Tests;

public sealed class TopicRecommendationsPageSourceTests
{
  [Fact]
  public void RootSurfaceIncludesSignedInHashtagFiltersAndAdminActions()
  {
    var source = PageSource();

    Assert.Contains("TopHashtagsViewModel", source, StringComparison.Ordinal);
    Assert.Contains("sessionStore.Current.IsAuthenticated", source, StringComparison.Ordinal);
    Assert.Contains("TopHashtagMapping.Linked", source, StringComparison.Ordinal);
    Assert.Contains("LinkAsync", source, StringComparison.Ordinal);
    Assert.Contains("CreateAsync", source, StringComparison.Ordinal);
    Assert.Contains("UnlinkAsync", source, StringComparison.Ordinal);
    Assert.Contains("ShowHashtagMutationError", source, StringComparison.Ordinal);
    Assert.Contains("hashtags.ErrorMessage ?? string.Empty", source, StringComparison.Ordinal);
    Assert.DoesNotContain("await ShowHashtagsAsync().ConfigureAwait(true);", source[source.IndexOf("private void ShowHashtagMutationError", StringComparison.Ordinal)..], StringComparison.Ordinal);
  }

  [Fact]
  public void HashtagLabelDoesNotDuplicateAnAuthoredLeadingHash()
  {
    var source = PageSource();

    Assert.Contains("hashtag.StartsWith('#') ? hashtag : $\"#{hashtag}\"", source, StringComparison.Ordinal);
    Assert.Contains("AuthoredHashtagToken(hashtag.Hashtag)", source, StringComparison.Ordinal);
  }

  [Fact]
  public void HashtagMutationsRebindRowsAndPagination()
  {
    var source = PageSource();

    Assert.Equal(3, Count(source, "await RunHashtagMutationAsync("));
    Assert.Contains("RebindHashtagRows();\n      ShowHashtagMutationError();", source, StringComparison.Ordinal);
    Assert.Contains("rows.ItemsSource = hashtags.Items;", source, StringComparison.Ordinal);
    Assert.Contains("more.IsVisible = hashtags.HasMore;", source, StringComparison.Ordinal);
  }

  [Fact]
  public void LoadMoreSurfacesTheActiveViewModelError()
  {
    var source = PageSource();

    Assert.Contains("if (loadingHashtags) { error.Text = hashtags.ErrorMessage; RebindHashtagRows(); }", source, StringComparison.Ordinal);
    Assert.Contains("else { error.Text = recommendations.ErrorMessage; rows.ItemsSource = recommendations.Items;", source, StringComparison.Ordinal);
  }

  [Fact]
  public void HashtagActionsBindTheirAvailabilityToTheLoadingState()
  {
    var source = PageSource();

    Assert.Contains("nameof(TopHashtagsViewModel.CanMutate)", source, StringComparison.Ordinal);
    Assert.Contains("source: hashtags", source, StringComparison.Ordinal);
  }

  [Fact]
  public void CurrentTopicSlugDoesNotOfferAnUnlinkAction()
  {
    var source = PageSource();

    Assert.Contains("unlink.IsVisible = hashtag is not null && hashtags.CanUnlink(hashtag);", source, StringComparison.Ordinal);
  }

  [Fact]
  public void RecycledRowsHideHashtagActionsForPostsAndClearInputs()
  {
    var source = PageSource();

    Assert.Contains("actions.IsVisible = hashtag is not null && sessionStore.Current.CanManageTopics();", source, StringComparison.Ordinal);
    Assert.Contains("topicId.Text = string.Empty;", source, StringComparison.Ordinal);
    Assert.Contains("name.Text = string.Empty;", source, StringComparison.Ordinal);
  }

  [Fact]
  public void TabLoadsDoNotRebindResultsAfterTheSelectedTabChanges()
  {
    var source = PageSource();

    Assert.Contains("private int tabLoadGeneration;", source, StringComparison.Ordinal);
    Assert.Equal(2, Count(source, "Interlocked.Increment(ref tabLoadGeneration)"));
    Assert.Equal(6, Count(source, "IsCurrentTab(requestGeneration"));
    Assert.Contains("if (!IsCurrentTab(requestGeneration, false)) return;", source, StringComparison.Ordinal);
    Assert.Contains("if (!IsCurrentTab(requestGeneration, true)) return;", source, StringComparison.Ordinal);
    Assert.Contains("if (!IsCurrentTab(requestGeneration, loadingHashtags)) return;", source, StringComparison.Ordinal);
    Assert.Contains("var requestGeneration = Volatile.Read(ref tabLoadGeneration);\n    try", source, StringComparison.Ordinal);
    Assert.Contains("if (!IsCurrentTab(requestGeneration, true)) return;\n      RebindHashtagRows();", source, StringComparison.Ordinal);
    Assert.Contains("if (IsCurrentTab(requestGeneration, true)) error.Text = exception.Message;", source, StringComparison.Ordinal);
    Assert.Contains("requestGeneration == Volatile.Read(ref tabLoadGeneration) && showingHashtags == shouldShowHashtags", source, StringComparison.Ordinal);
  }

  private static int Count(string source, string value) => source.Split(value, StringSplitOptions.None).Length - 1;

  private static string PageSource() =>
      File.ReadAllText(AppSource("TopicRecommendationsPage.cs"))
      + Environment.NewLine
      + File.ReadAllText(AppSource("TopicRecommendationsPage.HashtagMutations.cs"));

  private static string AppSource(string file, [CallerFilePath] string sourceFile = "")
  {
    var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
    while (directory is not null)
    {
      var root = Path.Combine(directory.FullName, "src", "Voucha.Client.App", "Pages", file);
      if (File.Exists(root)) return root;
      directory = directory.Parent;
    }
    throw new FileNotFoundException("Could not find the .NET topic recommendations page source.", file);
  }
}
