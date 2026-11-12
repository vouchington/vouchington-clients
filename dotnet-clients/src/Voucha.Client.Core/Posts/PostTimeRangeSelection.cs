using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Posts;

public sealed record PostTimeRangeOption(string Value, UiText LabelText, IUiLocalization Localization)
{
  public string Label => Localization.Resolve(LabelText);
}

/// <summary>Input-driven ranges with an explicit API time window.</summary>
public sealed class PostTimeRangeSelection : ObservableObject
{
  public static IReadOnlyList<string> BrowseOptions { get; } =
      [PostTimeRanges.LastWeek, PostTimeRanges.Last30Days, PostTimeRanges.LastYear, PostTimeRanges.AllTime];
  private readonly IUiLocalization localization;
  private IReadOnlyList<PostTimeRangeOption> options;
  private PostTimeRangeOption selectedOption;

  public PostTimeRangeSelection(
      IUiLocalization localization,
      IReadOnlyList<string>? ranges = null,
      string defaultRange = PostTimeRanges.Last30Days)
  {
    this.localization = localization;
    options = (ranges ?? BrowseOptions).Select(Option).ToArray();
    selectedOption = options.FirstOrDefault(option => option.Value == defaultRange)
        ?? throw new ArgumentException("Default range must be offered.", nameof(defaultRange));
  }

  public UiText LabelText => UiText.Localized(UiMessageKey.ExtractedPostsPostTimeRangeSelectLabel);
  public IReadOnlyList<PostTimeRangeOption> Options => options;
  public PostTimeRangeOption SelectedOption => selectedOption;
  public string RequestValue => selectedOption.Value;

  public bool Select(string value)
  {
    var option = options.FirstOrDefault(option => option.Value == value)
        ?? throw new ArgumentException("Range must be offered.", nameof(value));
    if (selectedOption.Value == value) return false;
    selectedOption = option;
    OnPropertyChanged(nameof(SelectedOption));
    OnPropertyChanged(nameof(RequestValue));
    return true;
  }

  public void RefreshLabels()
  {
    var value = selectedOption.Value;
    options = options.Select(option => Option(option.Value)).ToArray();
    selectedOption = options.Single(option => option.Value == value);
    OnPropertyChanged(nameof(Options));
    OnPropertyChanged(nameof(SelectedOption));
  }

  private PostTimeRangeOption Option(string value) => new(value, UiText.Localized(value switch
  {
    PostTimeRanges.LastWeek => UiMessageKey.ExtractedPostsPostTimeRangeSelectLast7Days,
    PostTimeRanges.Last30Days => UiMessageKey.ExtractedPostsPostTimeRangeSelectLast30Days,
    PostTimeRanges.LastYear => UiMessageKey.ExtractedPostsPostTimeRangeSelectLastYear,
    PostTimeRanges.AllTime => UiMessageKey.ExtractedPostsPostTimeRangeSelectAllTime,
    _ => throw new ArgumentException("Unsupported post range.", nameof(value)),
  }), localization);
}
